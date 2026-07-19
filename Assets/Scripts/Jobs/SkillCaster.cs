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

        /// <summary>현재 장착 스킬 (없으면 null).</summary>
        public ActiveSkillDefinition Skill => skill;

        /// <summary>시전 주체 전투 유닛 — 스킬 구현체(Execute)가 DamageInfo.Source로 사용한다.</summary>
        public CombatEntity Entity => _entity;

        /// <summary>UI(SkillCooldownInfo) 표시용 쿨타임 접근. 스킬 미장착 시 null.</summary>
        public CooldownTimer Cooldown => _cooldown;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _statusController = GetComponent<StatusEffectController>();
            if (skill != null)
            {
                _cooldown = new CooldownTimer(skill.Cooldown);
            }
        }

        private void Update()
        {
            _cooldown?.Tick(Time.deltaTime);
            // TODO: Core.StatType.CooldownReduction 스탯(아이템) 반영 —
            //       Player.PlayerStats 연동 시 스탯 변경 이벤트에서 _cooldown.SetDuration(...) 호출.
        }

        /// <summary>직업 조립 시 스킬 주입. 쿨타임 타이머를 새로 만든다 (잔여 쿨타임 초기화).</summary>
        public void SetSkill(ActiveSkillDefinition newSkill)
        {
            skill = newSkill;
            _cooldown = newSkill != null ? new CooldownTimer(newSkill.Cooldown) : null;
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
