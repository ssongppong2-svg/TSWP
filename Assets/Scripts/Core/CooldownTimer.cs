// 근거: 직업 시스템.md — 모든 스킬은 쿨타임을 반드시 가진다.
// SkillCaster·아이템 효과 등에서 공용으로 쓰는 쿨타임 유틸.
using UnityEngine;

namespace TSWP.Core
{
    public sealed class CooldownTimer
    {
        public float Duration { get; private set; }
        public float Remaining { get; private set; }
        public bool IsReady => Remaining <= 0f;
        /// <summary>0(사용 직후)~1(사용 가능). UI 쿨타임 표시용.</summary>
        public float NormalizedProgress => Duration <= 0f ? 1f : 1f - Mathf.Clamp01(Remaining / Duration);

        public CooldownTimer(float duration)
        {
            Duration = Mathf.Max(0f, duration);
        }

        /// <summary>쿨타임 감소 효과(CooldownReduction 스탯) 적용 시 호출.</summary>
        public void SetDuration(float duration) => Duration = Mathf.Max(0f, duration);

        public void Tick(float deltaTime)
        {
            if (Remaining > 0f)
                Remaining = Mathf.Max(0f, Remaining - deltaTime);
        }

        /// <summary>사용 가능하면 소모하고 true. 쿨타임 중이면 false.</summary>
        public bool TryUse()
        {
            if (!IsReady) return false;
            Remaining = Duration;
            return true;
        }

        public void Reset() => Remaining = 0f;
    }
}
