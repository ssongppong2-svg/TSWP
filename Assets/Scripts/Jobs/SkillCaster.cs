// 근거: 직업 시스템.md — 액티브 스킬(Q)은 반드시 쿨타임이 존재한다.
// 근거: 상태이상 시스템.md — 침묵/기절/빙결 중 스킬 불가 (StatusEffectController.CanUseSkill 질의만 사용).
// Q 입력 처리 진입점: Player의 입력 계층(IPlayerInput/PlayerController)이 Q 입력 시 TryCastSkill()을 호출한다.
// 쿨타임은 이 컴포넌트가 일괄 관리한다 (스펙 unityNotes ④).
using UnityEngine;
using TSWP.Core;
using TSWP.Combat;
using TSWP.StatusEffects;

namespace TSWP.Jobs
{
    /// <summary>
    /// 플레이어(또는 스킬 사용 유닛)의 액티브 스킬 시전 컴포넌트.
    /// 흐름: CanUseSkill 게이트 → CooldownTimer.TryUse → 무적 부여 → Execute(전략) → GameEvents.RaiseSkillUsed.
    /// </summary>
    public class SkillCaster : MonoBehaviour
    {
        [Tooltip("직업 조립 시 JobSelectionManager.ApplyJobTo가 SetSkill로 주입한다. 테스트용 직접 지정 가능.")]
        [SerializeField] private ActiveSkillDefinition skill;

        // 쿨타임 상태. // SYNC: 호스트 권위, 추후 NGO NetworkVariable (잔여 쿨타임 — 발동 자체는 서버 권위 이벤트로 동기화 예정)
        private CooldownTimer _cooldown;
        private CombatEntity _entity;
        private StatusEffectController _statusController;
        private Player.PlayerController _controller;
        private Player.PlayerStats _stats;

        /// <summary>현재 장착 스킬 (없으면 null).</summary>
        public ActiveSkillDefinition Skill => skill;

        /// <summary>시전 주체 전투 유닛 — 스킬 구현체(Execute)가 DamageInfo.Source로 사용한다.</summary>
        public CombatEntity Entity => _entity;

        /// <summary>UI(SkillCooldownInfo) 표시용 쿨타임 접근. 스킬 미장착 시 null.</summary>
        public CooldownTimer Cooldown => _cooldown;

        // ── 쿨타임 조회 API (UI.SkillCooldownHudBridge / 디버그 표시용) ─────
        // 브리지는 Skill·Cooldown 객체를 직접 읽지만, 스킬 미장착 시 null 참조를 매번 방어해야 한다.
        // 아래 프로퍼티들은 null-safe 폴백을 제공해 호출측 분기를 없앤다.

        /// <summary>남은 쿨타임(초). 스킬이 없으면 0.</summary>
        public float CooldownRemaining => _cooldown != null ? _cooldown.Remaining : 0f;

        /// <summary>전체 쿨타임(초). 스킬이 없으면 0.</summary>
        public float CooldownDuration => _cooldown != null ? _cooldown.Duration : 0f;

        /// <summary>0(막 사용)~1(사용 가능) 게이지 비율. 스킬이 없으면 1.</summary>
        public float CooldownNormalized => _cooldown != null ? _cooldown.NormalizedProgress : 1f;

        /// <summary>지금 Q를 눌러 실제로 발동되는지 (스킬 존재 + 생존 + 침묵 아님 + 쿨타임 완료).</summary>
        public bool IsSkillReady =>
            skill != null
            && _cooldown != null
            && _cooldown.IsReady
            && (_entity == null || !_entity.IsDead)
            && (_statusController == null || _statusController.CanUseSkill);

        /// <summary>
        /// 스킬 시전 방향. PlayerController가 있으면 마우스 조준을, 없으면(적/더미 등) 오른쪽을 쓴다.
        /// 스킬 구현체는 방향 계산을 중복하지 말고 이 값을 쓴다.
        /// </summary>
        public Vector2 AimDirection
        {
            get
            {
                if (_controller != null)
                {
                    Vector2 aim = _controller.GetAimDirection();
                    if (aim.sqrMagnitude > 0.0001f) return aim.normalized;
                }
                return Vector2.right;
            }
        }

        /// <summary>시전 원점 — 발밑이 아닌 몸통 높이에서 나가야 범위가 자연스럽다.</summary>
        public Vector2 CastOrigin => (Vector2)transform.position;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _statusController = GetComponent<StatusEffectController>();
            _controller = GetComponent<Player.PlayerController>();
            _stats = GetComponent<Player.PlayerStats>();
            if (skill != null)
            {
                _cooldown = new CooldownTimer(EffectiveCooldown(skill));
            }
        }

        private void OnEnable()
        {
            // 쿨타임 감소 스탯(아이템/버프)이 바뀌면 즉시 반영한다 (폴링 대신 이벤트 — ARCHITECTURE.md §3-8).
            if (_stats != null) _stats.Stats.Changed += OnStatChanged;
        }

        private void OnDisable()
        {
            if (_stats != null) _stats.Stats.Changed -= OnStatChanged;
        }

        private void Start()
        {
            // PlayerStats.Awake의 base 값 초기화가 이 컴포넌트의 Awake보다 늦을 수 있어
            // (Changed 구독 전에 발행된 변경을 놓친다) 첫 프레임에 한 번 다시 계산한다.
            if (_cooldown != null && skill != null)
                _cooldown.SetDuration(EffectiveCooldown(skill));
        }

        private void Update()
        {
            _cooldown?.Tick(Time.deltaTime);
        }

        /// <summary>직업 조립 시 스킬 주입. 쿨타임 타이머를 새로 만든다 (잔여 쿨타임 초기화).</summary>
        public void SetSkill(ActiveSkillDefinition newSkill)
        {
            skill = newSkill;
            _cooldown = newSkill != null ? new CooldownTimer(EffectiveCooldown(newSkill)) : null;
        }

        /// <summary>
        /// 스킬 피해에 얹을 추가 공격력 (아이템 modifier + 패시브 배율).
        /// base는 스킬 정의의 피해량이 담당하므로 '최종값 - base'만 보너스로 쓴다 (BasicAttacker와 동일 규약).
        /// </summary>
        public float BonusAttackPower =>
            _stats != null
                ? _stats.Stats.GetValue(StatType.AttackPower) - _stats.Stats.GetBase(StatType.AttackPower)
                : 0f;

        /// <summary>쿨타임 감소 스탯 반영값. 감소율은 0~0.9로 제한한다 (쿨타임 0 = 문서 위반).</summary>
        private float EffectiveCooldown(ActiveSkillDefinition target)
        {
            if (target == null) return 0f;

            float reduction = _stats != null ? _stats.Stats.GetValue(StatType.CooldownReduction) : 0f;
            reduction = Mathf.Clamp(reduction, 0f, 0.9f);
            return Mathf.Max(0.05f, target.Cooldown * (1f - reduction));
        }

        private void OnStatChanged(StatType stat)
        {
            if (stat != StatType.CooldownReduction || _cooldown == null || skill == null) return;
            _cooldown.SetDuration(EffectiveCooldown(skill)); // 진행 중인 잔여 쿨타임은 그대로 둔다
        }

        /// <summary>
        /// Q 입력 처리 진입점. 발동 성공 시 true.
        /// 게이트 순서: 스킬 존재 → 사망 → 상태이상(침묵/기절/빙결) → 쿨타임.
        /// 침묵 중에는 쿨타임을 소모하지 않도록 상태이상 게이트를 쿨타임보다 먼저 검사한다.
        /// </summary>
        public bool TryCastSkill()
        {
            if (skill == null || _cooldown == null)
            {
                return false;
            }

            if (_entity != null && _entity.IsDead)
            {
                return false;
            }

            // 상태이상 게이트 — 스킬 시스템은 이 질의만 사용한다 (상태이상 세부를 알지 않는다).
            if (_statusController != null && !_statusController.CanUseSkill)
            {
                return false;
            }

            // 쿨타임 검사 및 소모 (모든 스킬은 쿨타임 필수 — ActiveSkillDefinition.OnValidate가 >0 보장).
            if (!_cooldown.TryUse())
            {
                return false;
            }

            // 일부 스킬 사용 중 일시 무적 — 유한 타이머 전용 (상시 무적 금지, 전투 시스템 규칙).
            if (skill.GrantsInvincibility && _entity != null)
            {
                _entity.SetInvincibleFor(skill.InvincibilityDuration);
            }

            // 직업별 효과 발동 (전략 패턴 — 파생 SO가 구현).
            skill.Execute(this);
            // TODO(연출): 시전 모션/이펙트 — Art.CharacterVisual 연동.

            // UI/업적 통지는 GameEvents 경유 (직접 참조 금지 — ARCHITECTURE.md §3-5).
            int playerId = _entity != null ? _entity.OwnerPlayerId : -1;
            if (playerId >= 0)
            {
                GameEvents.RaiseSkillUsed(playerId, skill.SkillId);
            }

            return true;
        }
    }
}
