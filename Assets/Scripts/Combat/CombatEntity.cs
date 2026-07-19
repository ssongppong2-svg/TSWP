// 근거: 전투 시스템.md — 플레이어·적·보스·구조물은 모두 동일한 전투 규칙을 따른다 (통합 전투 유닛 베이스).
// 무적은 일부 스킬 사용 중 일시적으로만 존재한다 — 상시 무적 경로를 만들지 않는다 (타이머 전용).
// 사망 시 즉시 부활 + 공유 부활 1회 소모 — 부활 판정은 Core.SharedReviveSystem 단일 경로 (ARCHITECTURE.md §5).
// SYNC: 호스트 권위, 추후 NGO NetworkVariable — CurrentHp / 무적 타이머 / 사망·부활 상태.
using System;
using UnityEngine;
using TSWP.Core;
using TSWP.StatusEffects;

namespace TSWP.Combat
{
    /// <summary>
    /// 공통 전투 유닛. 플레이어/적/보스/구조물이 이 베이스(또는 파생)를 사용해
    /// 동일한 피해 파이프라인(DamageSystem.Apply)을 탄다.
    /// </summary>
    public class CombatEntity : MonoBehaviour, IDamageable
    {
        [Header("체력")]
        [SerializeField] private float maxHp = 100f; // TODO(밸런스): 문서 미정 — 직업/적 데이터 SO가 SetMaxHp로 주입

        [Header("진영/소속")]
        [Tooltip("아군 판정은 레이어가 아닌 이 값 비교로 한다 (아군사격 상시 존재 — ARCHITECTURE.md §3-6).")]
        [SerializeField] private TeamType team = TeamType.Neutral;

        [Tooltip("플레이어 유닛이면 0 이상(플레이어 ID), 적/구조물/환경 유닛은 -1.")]
        [SerializeField] private int ownerPlayerId = -1;

        [Header("면역/부활")]
        [SerializeField] private bool isKnockbackImmune; // 일부 보스 true (전투 시스템.md — 넉백 면역)
        [Tooltip("문서: '사망 시 즉시 부활'(공유 부활 1회 소모). NOTE(기획 확인 필요): E키 팀원 구조부활 안이 채택되면 이 값을 끄고 구조 상호작용이 Revive()를 호출한다.")]
        [SerializeField] private bool autoReviveOnDeath = true;

        [Header("사망/부활 연출")]
        [Tooltip("사망 시 재생할 이펙트 id (Art.VfxId). 비우면 재생하지 않는다.")]
        [SerializeField] private string deathVfxId = Art.VfxId.Death;

        [Tooltip("부활 시 재생할 이펙트 id (Art.VfxId). 비우면 재생하지 않는다.")]
        [SerializeField] private string reviveVfxId = Art.VfxId.Spawn;

        private float _currentHp;
        private float _invincibleTimer; // 상시 무적 금지 — 반드시 유한 타이머로만 무적 부여
        private bool _isDead;
        private StatusEffectController _statusController;
        private Rigidbody2D _body;

        // ── 상태 조회 ─────────────────────────────────────────────
        public float CurrentHp => _currentHp;              // 폭탄마 거대 폭탄 '현재 체력의 20%' 계산에 사용
        public float MaxHp => maxHp;
        public TeamType Team => team;
        public int OwnerPlayerId => ownerPlayerId;
        public bool IsKnockbackImmune { get => isKnockbackImmune; protected set => isKnockbackImmune = value; }
        public bool IsInvincible => _invincibleTimer > 0f; // 일부 스킬 사용 중 일시 무적 — 상시 무적 없음
        public bool IsDead => _isDead;

        /// <summary>같은 오브젝트의 상태이상 컨트롤러 (없으면 null — 상태이상 미적용 유닛).</summary>
        public StatusEffectController StatusController => _statusController;

        // ── 이벤트 (UI 직접 참조 금지 — 게임플레이 내부 구독용, UI는 GameEvents 경유) ──
        public event Action<DamageInfo> Damaged;
        public event Action<CombatEntity> Died;

        protected virtual void Awake()
        {
            _currentHp = maxHp;
            _statusController = GetComponent<StatusEffectController>();
            _body = GetComponent<Rigidbody2D>();
        }

        protected virtual void Update()
        {
            // 무적 타이머 감소 — 이 경로 외에 무적을 유지하는 수단을 두지 않는다.
            if (_invincibleTimer > 0f)
                _invincibleTimer -= Time.deltaTime;
        }

        // ── IDamageable ───────────────────────────────────────────
        /// <summary>피격 진입점. 실제 규칙 처리는 단일 파이프라인 DamageSystem.Apply가 담당한다.</summary>
        public void TakeDamage(in DamageInfo info) => DamageSystem.Apply(this, in info);

        // ── 초기화/주입 (PlayerStats·EnemyData 등 소유 시스템이 호출) ──
        public void SetTeam(TeamType newTeam) => team = newTeam;
        public void SetOwnerPlayerId(int playerId) => ownerPlayerId = playerId;

        /// <summary>최대 체력 변경 (아이템 modifier 등). keepRatio면 현재 체력 비율 유지.</summary>
        public void SetMaxHp(float newMax, bool keepRatio = false)
        {
            newMax = Mathf.Max(1f, newMax);
            float ratio = maxHp > 0f ? _currentHp / maxHp : 1f;
            maxHp = newMax;
            _currentHp = keepRatio ? maxHp * ratio : Mathf.Min(_currentHp, maxHp);
            NotifyHealthChanged();
        }

        // ── 무적 ─────────────────────────────────────────────────
        /// <summary>
        /// 유한 시간 무적 부여 (일부 스킬 사용 중 등). duration이 0 이하면 무시 —
        /// 상시 무적을 만들 수 있는 API를 의도적으로 제공하지 않는다 (전투 시스템.md — 무적).
        /// </summary>
        public void SetInvincibleFor(float duration)
        {
            if (duration <= 0f) return;
            _invincibleTimer = Mathf.Max(_invincibleTimer, duration);
        }

        // ── 회복 ─────────────────────────────────────────────────
        /// <summary>회복. HealBlock 상태이상이면 차단 (StatusEffectController.CanBeHealed 질의만 사용).</summary>
        public void Heal(float amount, int healerPlayerId = -1)
        {
            if (_isDead || amount <= 0f) return;
            if (_statusController != null && !_statusController.CanBeHealed) return; // 회복 차단
            float before = _currentHp;
            _currentHp = Mathf.Min(maxHp, _currentHp + amount);
            float healed = _currentHp - before;
            if (healed <= 0f) return;
            NotifyHealthChanged();

            // 회복량 표시(초록) — 프로토타입 밸런스 확인용. 표시 창구가 없으면 조용히 생략된다.
            UI.DamageNumberSpawner.Instance?.ShowHeal(transform.position, healed);
            Art.VfxSpawner.Instance?.Play(Art.VfxId.Heal, transform.position);

            if (healerPlayerId >= 0)
                GameEvents.RaiseHealingDone(healerPlayerId, healed); // 결과 화면 '가장 많은 회복' 집계
        }

        // ── 즉시 사망 (낙사 등) ───────────────────────────────────
        /// <summary>
        /// 즉시 사망 처리. 낙사(HazardType.FallDeath)가 대표 경로 — 무적/방어 무관하게 사망한다.
        /// 부활 소모는 Die() 내부에서 Core.SharedReviveSystem.TryConsume 단일 경로를 탄다.
        /// </summary>
        public void Kill(CombatEntity source)
        {
            if (_isDead) return;
            _currentHp = 0f;
            NotifyHealthChanged();
            Die(source);
        }

        // ── 부활 ─────────────────────────────────────────────────
        /// <summary>
        /// 부활 실행 (부활 횟수 소모 판정은 호출측 책임 — Die()의 즉시부활 또는 추후 E키 구조부활 흐름).
        /// </summary>
        public void Revive()
        {
            if (!_isDead) return;
            _isDead = false;

            var cfg = DamageSystem.Config;
            float hpRatio = cfg != null ? cfg.reviveHpRatio : 1f;                    // TODO(밸런스): 문서 미정 — 부활 체력 비율
            float invDuration = cfg != null ? cfg.reviveInvincibleDuration : 1.5f;   // TODO(밸런스): 문서 미정 — 부활 직후 짧은 무적(상시 아님)
            _currentHp = Mathf.Max(1f, maxHp * hpRatio);

            // 부활 직후 짧은 무적 — 되살아나자마자 같은 장판에 다시 죽는 것을 막는다.
            // 유한 타이머 전용 API만 사용한다 (상시 무적 경로 없음 — 전투 시스템.md '무적').
            SetInvincibleFor(invDuration);
            NotifyHealthChanged();

            // 부활 연출 — VfxSpawner가 없으면 조용히 생략된다.
            if (!string.IsNullOrEmpty(reviveVfxId))
                Art.VfxSpawner.Instance?.Play(reviveVfxId, transform.position);

            if (ownerPlayerId >= 0)
                GameEvents.RaisePlayerRevived(ownerPlayerId);
            // TODO(연출): 무적 동안 깜빡임 — Art.CharacterVisual 연동.
        }

        /// <summary>
        /// 공유 부활 1회를 소모해 부활을 시도한다. 부활/게임오버 판정은 Core.SharedReviveSystem
        /// 한 곳을 거친다 (ARCHITECTURE.md §5) — 즉시부활도 E키 구조부활도 이 메서드를 공용 진입점으로 쓴다.
        /// </summary>
        /// <param name="rescuerPlayerId">구조한 플레이어 id (결과 화면 '가장 많은 구조' 집계용). 즉시부활이면 -1.</param>
        /// <returns>부활에 성공했으면 true. 부활 횟수 소진이면 false(죽은 상태 유지).</returns>
        public bool TryReviveShared(int rescuerPlayerId = -1)
        {
            if (!_isDead) return false;

            var revive = RunManager.Instance != null ? RunManager.Instance.ReviveSystem : null;
            if (revive == null || !revive.TryConsume()) return false;

            Revive();

            if (rescuerPlayerId >= 0)
                GameEvents.RaiseStatCounter("rescue.count", 1); // 결과 화면 '가장 많은 구조'
            return true;
        }

        /// <summary>즉시부활/구조부활 정책 전환용 (NOTE(기획 확인 필요) 항목 — 통합 단계에서 주입).</summary>
        public void SetAutoReviveOnDeath(bool value) => autoReviveOnDeath = value;

        // ── 내부 파이프라인 훅 (DamageSystem 전용) ────────────────
        /// <summary>최종 피해를 즉시 HP에서 차감한다 (피해 적용 지연 없음 — 전투 시스템.md '피격').</summary>
        internal void ApplyDamageDirect(float finalDamage, in DamageInfo info)
        {
            if (_isDead) return;
            // 0 피해는 피격으로 취급하지 않는다 (명확한 공격 원칙 — 무의미한 피격 연출 방지).
            // 넉백·상태이상만 있는 공격은 DamageSystem 파이프라인 5·6단계가 별도로 처리한다.
            if (finalDamage <= 0f) return;

            _currentHp = Mathf.Max(0f, _currentHp - finalDamage);
            Damaged?.Invoke(info);
            NotifyHealthChanged();
            // TODO(연출): 피격 플래시/히트스톱 — Art 연동.

            if (_currentHp <= 0f)
                Die(info.Source);
        }

        /// <summary>넉백 적용. 면역 검사는 DamageSystem에서 선행되지만 방어적으로 재확인한다.</summary>
        internal void ApplyKnockback(in KnockbackInfo kb)
        {
            if (isKnockbackImmune) return;
            if (_body != null)
                _body.AddForce(kb.Direction.normalized * kb.Force, ForceMode2D.Impulse);
            // TODO(물리): Rigidbody2D 임펄스 vs 수동 이동 택일 검토 (스펙 unityNotes ⑦) — 절벽/함정 낙하 연계.
            // TODO: kb.StunDuration 경직 — StatusEffects의 Stun/Knockback 상태이상과 연동 예정.
        }

        // ── 사망 처리 ─────────────────────────────────────────────
        private void Die(CombatEntity killer)
        {
            if (_isDead) return;
            _isDead = true;

            // 사망 연출 — 진영 무관하게 재생한다. VfxSpawner가 없으면 조용히 생략된다.
            // Died 구독자가 오브젝트를 즉시 정리할 수 있으므로 통지보다 먼저 재생한다.
            if (!string.IsNullOrEmpty(deathVfxId))
                Art.VfxSpawner.Instance?.Play(deathVfxId, transform.position);

            Died?.Invoke(this); // 적: EnemyController가 구독해 처치 보상(KillReward)·오브젝트 정리 수행

            if (ownerPlayerId < 0)
                return; // 적/구조물은 여기까지 — 드롭/파괴 연출은 소유 시스템(Enemies/Map) 소관

            GameEvents.RaisePlayerDied(ownerPlayerId);

            // 문서: 사망 시 즉시 부활, 공유 부활 횟수 1회 소모. 소진 시 추가 부활 불가.
            // 부활/게임오버 판정은 Core.SharedReviveSystem 한 곳 — 여기서는 호출만 (ARCHITECTURE.md §5).
            if (autoReviveOnDeath)
                TryReviveShared();
            // NOTE(기획 확인 필요): 즉시부활 vs E키 팀원 구조부활 — 구조부활 채택 시 autoReviveOnDeath=false로 두고
            //   PlayerInteraction(E키) 흐름이 TryReviveShared(구조자id)를 호출한다 (같은 진입점).
            // 부활 실패(소진) 시 게임오버(부활 소진 + 전원 사망) 판정은 Core 흐름이 PlayerDied 이벤트 집계로 수행.
        }

        private void NotifyHealthChanged()
        {
            if (ownerPlayerId >= 0)
                GameEvents.RaisePlayerHealthChanged(ownerPlayerId, _currentHp, maxHp); // HUD 체력바 갱신 (GameEvents 경유)
        }
    }
}
