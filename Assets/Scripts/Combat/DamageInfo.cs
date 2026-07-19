// 근거: 전투 시스템.md — 피해 = 기본 공격력 + 아이템 효과 + 스킬 효과 + 기타 효과 (합산).
// 피격 추가 효과(넉백·상태이상·디버프)는 공격마다 다르게 정의된다.
using System.Collections.Generic;
using TSWP.StatusEffects;

namespace TSWP.Combat
{
    public struct DamageInfo
    {
        // ── 피해 성분 (합산 공식) ──────────────────────────────────
        public float BaseDamage;   // 기본 공격력
        public float ItemBonus;    // 아이템 효과
        public float SkillBonus;   // 스킬 효과
        public float MiscBonus;    // 기타 효과

        public bool IsCritical;    // 치명타 (기본 확률 0% — 아이템/버프로만)
        public bool IsExplosive;   // 폭발 판정 — 구조물은 폭발 공격만 파괴 가능

        /// <summary>넉백 정보. null이면 넉백 없음.</summary>
        public KnockbackInfo? Knockback;

        /// <summary>부여할 상태이상 목록. null 허용 (없음).</summary>
        public List<StatusEffectData> StatusEffects;

        /// <summary>공격자. 아군 판정(TeamType 비교)·통계 귀속에 사용. 환경 피해는 null.</summary>
        public CombatEntity Source;

        /// <summary>일부 스킬의 아군 피해 별도 규칙. null이면 기본 50% 규칙 적용.</summary>
        public FriendlyFireRule? FriendlyFireOverride;

        /// <summary>합산 총 피해.</summary>
        public float TotalDamage => BaseDamage + ItemBonus + SkillBonus + MiscBonus;
    }
}
