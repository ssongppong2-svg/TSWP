// 근거: 상태이상 시스템.md — 상태이상 16종, CC 우선순위(기절>빙결>속박>둔화>기타), 면역 부여 주체 4종.
// enum 순서는 ARCHITECTURE.md §4 계약에 고정되어 있다 — 순서 변경 금지.
namespace TSWP.StatusEffects
{
    /// <summary>
    /// 상태이상 16종.
    /// 주의: Knockback / Launch는 지속형이 아닌 즉발(물리)형이다 —
    /// StatusEffectController의 지속형 리스트에 넣지 않고,
    /// TSWP.Combat.KnockbackInfo(방향/힘/짧은 행동불가 시간)가 물리 이동·낙하 연계를 처리한다.
    /// </summary>
    public enum StatusEffectType
    {
        Burn,       // 화상 — 지속 피해, 시간 경과 자동 해제
        Poison,     // 중독 — 화상보다 약한 피해, 더 긴 지속시간
        Freeze,     // 빙결 — 행동 불가, 피격 시 즉시 해제 가능
        Shock,      // 감전 — 이동/공격속도 감소, 다른 적에게 전이 가능
        Bleed,      // 출혈 — 이동량 비례 추가 피해 (정지 시 무피해)
        Fear,       // 공포 — 일정 시간 반대 방향 강제 이동
        Confusion,  // 혼란 — 좌우 이동 입력 반전 (공격은 정상)
        Silence,    // 침묵 — 직업 스킬(Q) 봉인 (기본 공격 가능)
        Slow,       // 둔화 — 이동속도 감소
        Root,       // 속박 — 이동 불가 (공격/스킬 가능)
        Stun,       // 기절 — 완전 행동 불가 (최상위 CC)
        Weak,       // 약화 — 공격력 감소
        Vulnerable, // 취약 — 받는 피해 증가
        HealBlock,  // 회복 불가 — 회복 효과 차단
        Knockback,  // 넉백 — 즉발형. Combat.KnockbackInfo가 처리 (여기서는 식별용)
        Launch,     // 공중 띄우기 — 즉발형 + 짧은 행동 불가. Combat.KnockbackInfo.StunDuration이 처리
    }

    /// <summary>면역 부여 주체 4종 (문서: 보스/엘리트/아이템/패시브).</summary>
    [System.Flags]
    public enum ImmunitySource
    {
        None    = 0,
        Boss    = 1 << 0, // 보스 고유 면역
        Elite   = 1 << 1, // 엘리트 면역
        Item    = 1 << 2, // 아이템에 의한 면역
        Passive = 1 << 3, // 직업 패시브에 의한 면역
    }

    /// <summary>
    /// CC 우선순위 기본값 헬퍼. 문서에 서열만 명시됨: 기절 > 빙결 > 속박 > 둔화 > 기타.
    /// 실제 판정은 StatusEffectData.CcPriority(에셋 값)를 사용하고, 이 값은 에셋 기본값 제안용이다.
    /// </summary>
    public static class StatusEffectCcPriority
    {
        public const int Stun = 100;
        public const int Freeze = 80;
        public const int Root = 60;
        public const int Slow = 40;
        public const int Other = 0;

        /// <summary>문서 서열에 따른 기본 우선순위 값을 돌려준다.</summary>
        public static int GetDefault(StatusEffectType type)
        {
            switch (type)
            {
                case StatusEffectType.Stun: return Stun;
                case StatusEffectType.Freeze: return Freeze;
                case StatusEffectType.Root: return Root;
                case StatusEffectType.Slow: return Slow;
                default: return Other;
            }
        }
    }
}
