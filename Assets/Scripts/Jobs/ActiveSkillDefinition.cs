// 근거: 직업 시스템.md — 모든 직업은 하나의 고유 스킬(Q)을 가지며, 스킬은 강력한 효과와 함께 반드시 쿨타임이 존재한다.
// 효과 로직은 직업별 파생 SO가 Execute를 오버라이드하는 전략 패턴으로 분리한다 (스펙 unityNotes ②).
// 아군 피해 오버라이드(예: 폭탄마 거대 폭탄 = 현재 체력의 20%)·폭발 판정은 전투 시스템.md 규칙과 연동된다.
using UnityEngine;
using TSWP.Combat;

namespace TSWP.Jobs
{
    /// <summary>직업당 정확히 1개의 액티브 스킬(Q) 정의. 데이터는 여기, 로직은 파생 SO의 Execute.</summary>
    [CreateAssetMenu(menuName = "TSWP/Jobs/Active Skill", fileName = "Skill_")]
    public class ActiveSkillDefinition : ScriptableObject
    {
        /// <summary>액티브 스킬 입력 키 — Q 고정 (문서: 직업 구성).
        /// TODO: Input System 도입(키 리바인딩) 시 Player.IPlayerInput 추상화로 대체.</summary>
        public const KeyCode InputKey = KeyCode.Q;

        [Header("식별")]
        [SerializeField] private string skillId;
        [SerializeField] private string displayName;
        [TextArea]
        [Tooltip("강력한 효과 설명 — 실제 로직은 직업별 파생 SO의 Execute가 구현.")]
        [SerializeField] private string effectDescription;

        [Header("쿨타임 — 반드시 존재 (0 불가, 문서 규칙)")]
        [SerializeField] private float cooldown = 1f; // TODO(밸런스): 문서 미정 — 개별 직업 데이터에서 정의

        [Header("무적 — 일부 스킬 사용 중 일시 무적 (상시 무적 금지 — CombatEntity 타이머 전용)")]
        [SerializeField] private bool grantsInvincibility;
        [SerializeField] private float invincibilityDuration; // TODO(밸런스): 문서 미정

        [Header("아군 피해 규칙 — 미사용 시 기본 50% 규칙 (GameRules.FriendlyFireDamageRatio)")]
        [Tooltip("체크하면 이 스킬의 아군 피해에 별도 규칙을 적용한다 (예: 폭탄마 거대 폭탄 = CurrentHpPercent 0.2).")]
        [SerializeField] private bool useFriendlyFireOverride;
        [SerializeField] private FriendlyFireRule friendlyFireOverride;

        [Header("폭발 판정 — 구조물은 폭발 공격만 파괴 가능 (전투 시스템 연계)")]
        [SerializeField] private bool isExplosive;

        public string SkillId => skillId;
        public string DisplayName => displayName;
        public string EffectDescription => effectDescription;
        public float Cooldown => cooldown;
        public bool GrantsInvincibility => grantsInvincibility;
        public float InvincibilityDuration => invincibilityDuration;
        public bool IsExplosive => isExplosive;

        /// <summary>DamageInfo.FriendlyFireOverride에 그대로 넣는 값. 미사용이면 null → 기본 50% 규칙.</summary>
        public FriendlyFireRule? FriendlyFireOverride =>
            useFriendlyFireOverride ? friendlyFireOverride : (FriendlyFireRule?)null;

        /// <summary>
        /// 스킬 발동 — 직업별 효과는 파생 SO가 오버라이드한다 (전략 패턴).
        /// 게이트(침묵/쿨타임)는 SkillCaster.TryCastSkill이 이미 통과시킨 뒤 호출하므로 여기서 재검사하지 않는다.
        /// </summary>
        public virtual void Execute(SkillCaster caster)
        {
            // TODO: 직업별 파생 SO(예: 폭탄마/의사/용사 스킬)가 실제 효과를 구현한다.
            //       피해형 스킬은 caster.Entity를 Source로 한 DamageInfo를 구성하고
            //       SkillBonus·IsExplosive·FriendlyFireOverride를 채워 대상 IDamageable.TakeDamage로 전달한다.
            // TODO(연출): 시전 애니메이션/이펙트/사운드 — Art 연동.
            Debug.Log($"[ActiveSkillDefinition] {skillId} 발동 — 효과 미구현 (직업별 파생 SO에서 오버라이드).", this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 문서 규칙: 액티브 스킬은 반드시 쿨타임이 존재한다 — 0 이하 값을 강제 보정.
            if (cooldown <= 0f)
            {
                Debug.LogWarning($"[ActiveSkillDefinition] {name}: 쿨타임은 반드시 0보다 커야 한다 (직업 시스템.md). 0.1초로 보정.", this);
                cooldown = 0.1f; // TODO(밸런스): 문서 미정 — 최소 쿨타임 값
            }

            if (invincibilityDuration < 0f)
            {
                invincibilityDuration = 0f;
            }

            if (grantsInvincibility && invincibilityDuration <= 0f)
            {
                Debug.LogWarning($"[ActiveSkillDefinition] {name}: 무적 부여 스킬인데 지속시간이 0 — CombatEntity.SetInvincibleFor가 무시한다 (상시 무적 금지).", this);
            }
        }
#endif
    }
}
