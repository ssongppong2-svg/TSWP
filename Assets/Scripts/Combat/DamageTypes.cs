// 근거: 전투 시스템.md — 아군 피해 규칙(기본 50%, 스킬별 오버라이드), 넉백, 환경 피해 6종 + 낙사.
using UnityEngine;

namespace TSWP.Combat
{
    public enum FriendlyFireMode
    {
        DefaultPercent,   // 원 피해의 GameRules.FriendlyFireDamageRatio(50%)
        CurrentHpPercent, // 대상 현재 체력의 value 비율 (예: 폭탄마 거대 폭탄 = 0.2)
        Custom,           // value를 절대 피해량으로 사용
    }

    /// <summary>일부 스킬의 아군 피해 별도 규칙. DamageInfo.friendlyFireOverride가 null이면 기본 50% 규칙.</summary>
    [System.Serializable]
    public struct FriendlyFireRule
    {
        public FriendlyFireMode Mode;
        public float Value;

        public FriendlyFireRule(FriendlyFireMode mode, float value)
        {
            Mode = mode; Value = value;
        }
    }

    /// <summary>넉백. 적·아군 모두 적용, 대상이 넉백 면역이면 무시. 절벽/함정 낙하 연계 가능.</summary>
    [System.Serializable]
    public struct KnockbackInfo
    {
        public Vector2 Direction;
        public float Force;
        /// <summary>공중 띄우기(Launch) 등 짧은 행동 불가 시간. TODO(밸런스): 문서 미정.</summary>
        public float StunDuration;
    }

    /// <summary>환경 해저드 종류 — 이 enum은 여기 한 곳에만 정의한다 (ARCHITECTURE.md §5).
    /// 환경 피해는 진영 무관(플레이어·적 모두)이다.</summary>
    public enum HazardType
    {
        Lava,        // 용암
        Poison,      // 독
        Spike,       // 가시
        FallingRock, // 낙석
        Explosion,   // 폭발
        Ice,         // 얼음
        FallDeath,   // 낙사 — 즉시 사망, 공유 부활 1회 소모
    }
}
