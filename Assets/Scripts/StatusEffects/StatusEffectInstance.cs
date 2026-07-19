// 근거: 상태이상 시스템.md — 공통 규칙: 모든 상태이상은 지속시간을 가지며,
// 같은 상태이상 재적용은 스택 대신 지속시간만 갱신한다 (설계 철학 ③ 무한 중첩 금지).
using UnityEngine;

namespace TSWP.StatusEffects
{
    /// <summary>
    /// 상태이상 런타임 인스턴스. StatusEffectController의 지속형 리스트에 담긴다.
    /// 같은 타입 재적용 = Refresh 호출(지속시간 갱신), 다른 타입 = 리스트 추가(동시 적용 허용).
    /// // SYNC: 호스트 권위, 추후 NGO NetworkVariable (남은 시간·보유 목록 동기화)
    /// </summary>
    public class StatusEffectInstance
    {
        /// <summary>원본 정의 참조 (SO 에셋).</summary>
        public StatusEffectData Data { get; }

        /// <summary>남은 지속시간(초).</summary>
        public float RemainingDuration { get; private set; }

        /// <summary>발생원 (직업/아이템/보스/환경 소유 오브젝트). 통계 귀속·전이 출처 판정용. null 허용(환경).</summary>
        public GameObject Source { get; }

        // 다음 피해 틱까지 누적 시간 (화상/중독)
        private float tickTimer;

        // 출혈: 누적 이동 거리 (OnMoved 훅에서 가산)
        private float accumulatedMoveDistance;

        public bool IsExpired => RemainingDuration <= 0f;

        public StatusEffectInstance(StatusEffectData data, GameObject source)
        {
            Data = data;
            Source = source;
            RemainingDuration = data.Duration;
        }

        /// <summary>
        /// 같은 상태이상 재적용 시 호출 — 스택을 쌓지 않고 지속시간만 갱신한다.
        /// NOTE(기획 확인 필요): 재적용이 남은 시간을 줄이지 않도록 Max로 갱신한다 (짧은 부여로 덮어쓰기 방지).
        /// </summary>
        public void Refresh(float duration)
        {
            RemainingDuration = Mathf.Max(RemainingDuration, duration);
        }

        /// <summary>
        /// 시간을 진행시키고, 이 프레임에 발생한 피해 틱 수(화상/중독)를 돌려준다.
        /// 지속시간 감소와 틱 판정을 인스턴스 한 곳에서 처리해 컨트롤러를 단순하게 유지한다.
        /// </summary>
        public int AdvanceTime(float deltaTime)
        {
            RemainingDuration -= deltaTime;

            if (Data.TickDamage <= 0f || Data.TickInterval <= 0f)
            {
                return 0;
            }

            tickTimer += deltaTime;
            int ticks = 0;
            while (tickTimer >= Data.TickInterval)
            {
                tickTimer -= Data.TickInterval;
                ticks++;
            }
            return ticks;
        }

        /// <summary>
        /// 출혈용 이동 거리 가산 — 이번에 확정된 피해량을 돌려준다 (가만히 있으면 0).
        /// 1유닛 단위로 끊어 피해를 확정해 프레임당 미세 피해 누적을 피한다.
        /// </summary>
        public float AccumulateMoveDamage(float distance)
        {
            if (Data.MoveDamagePerUnit <= 0f || distance <= 0f)
            {
                return 0f;
            }

            accumulatedMoveDistance += distance;
            int wholeUnits = Mathf.FloorToInt(accumulatedMoveDistance);
            if (wholeUnits <= 0)
            {
                return 0f;
            }

            accumulatedMoveDistance -= wholeUnits;
            return wholeUnits * Data.MoveDamagePerUnit;
        }
    }
}
