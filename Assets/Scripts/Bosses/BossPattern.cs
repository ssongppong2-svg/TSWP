// 근거: 보스 시스템.md — 패턴: 모든 보스는 최소 5개의 행동 패턴을 가진다.
//       패턴은 무작위가 아닌 '상황에 따라' 선택된다 (플레이어 위치/체력/행동/퍼즐 진행 — AI 절).
//       광폭화 시 신규 패턴 추가·기존 패턴 강화 / 난이도에 따라 패턴 속도만 변경.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Bosses
{
    /// <summary>패턴 선택 조건의 종류. BossAIContext의 4요소(플레이어 위치/체력/행동/퍼즐 진행)를 판정한다.</summary>
    public enum PatternConditionType
    {
        AnyPlayerWithinRange,     // 플레이어 위치 — 임의 플레이어가 threshold 거리 이내
        AllPlayersBeyondRange,    // 플레이어 위치 — 전원이 threshold 거리 밖
        PlayersClustered,         // 플레이어 위치 — 산개 반경이 threshold 이하 (뭉침 → 범위 공격 유리)
        PlayersSpreadOut,         // 플레이어 위치 — 산개 반경이 threshold 초과
        BossHealthBelow,          // 체력 — 보스 체력 비율이 threshold 이하
        BossHealthAbove,          // 체력 — 보스 체력 비율이 threshold 초과
        PuzzleInProgress,         // 퍼즐 진행 — 협동 퍼즐이 진행 중
        PuzzleProgressAbove,      // 퍼즐 진행 — 진행도가 threshold 초과 (방해 패턴 트리거 등)
        RecentPlayerActionMatches,// 행동 — 최근 플레이어 행동(스킬 ID 등)이 actionId와 일치
    }

    /// <summary>패턴 선택 조건 1개. 충족 시 weight만큼 스코어를 더한다 (조건 스코어링 — 무작위 금지).</summary>
    [System.Serializable]
    public sealed class PatternCondition
    {
        [SerializeField] private PatternConditionType conditionType;

        [Tooltip("거리(월드 유닛) 또는 비율(0~1) — 조건 종류에 따라 해석.")]
        [SerializeField] private float threshold;

        [Tooltip("RecentPlayerActionMatches 전용 — 비교할 행동 ID (예: 스킬 ID).")]
        [SerializeField] private string actionId;

        [Tooltip("충족 시 더해지는 스코어 가중치.")]
        [SerializeField] private float weight = 1f;

        public float Weight => weight;

        /// <summary>컨텍스트를 보고 조건 충족 여부를 판정한다.</summary>
        public bool Evaluate(BossAIContext context)
        {
            if (context == null) return false;
            switch (conditionType)
            {
                case PatternConditionType.AnyPlayerWithinRange:
                    return context.NearestPlayerDistance() <= threshold;
                case PatternConditionType.AllPlayersBeyondRange:
                    return context.PlayerPositions.Count > 0 && context.NearestPlayerDistance() > threshold;
                case PatternConditionType.PlayersClustered:
                    return context.PlayerSpreadRadius() <= threshold;
                case PatternConditionType.PlayersSpreadOut:
                    return context.PlayerSpreadRadius() > threshold;
                case PatternConditionType.BossHealthBelow:
                    return context.BossHealthRatio <= threshold;
                case PatternConditionType.BossHealthAbove:
                    return context.BossHealthRatio > threshold;
                case PatternConditionType.PuzzleInProgress:
                    return context.PuzzleActive;
                case PatternConditionType.PuzzleProgressAbove:
                    return context.PuzzleProgress > threshold;
                case PatternConditionType.RecentPlayerActionMatches:
                    return context.HasRecentAction(actionId);
                default:
                    return false;
            }
        }
    }

    /// <summary>난이도별 패턴 속도 계수 항목.</summary>
    [System.Serializable]
    public struct DifficultySpeedEntry
    {
        public Difficulty difficulty;
        public float speedMultiplier; // TODO(밸런스): 문서 미정 — 난이도별 구체 계수 없음
    }

    /// <summary>
    /// 보스 행동 패턴 1개 = SO 애셋 1개.
    /// 선택은 조건 스코어링(BossPatternSelector)으로만 — 무작위 선택 금지 (보스 시스템.md '패턴').
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Bosses/Boss Pattern", fileName = "BossPattern_")]
    public sealed class BossPattern : ScriptableObject
    {
        [Header("식별")]
        [SerializeField] private string patternId;
        [SerializeField] private string displayName;
        [SerializeField] private BossPatternCategory category;
        [SerializeField, TextArea] private string description; // 연출/판정 메모 — TODO(연출): 실제 히트박스·이펙트 구현

        [Header("선택 조건 (무작위 금지 — 조건 스코어링)")]
        [Tooltip("비어 있으면 '항상 후보'인 범용 패턴 (기본 스코어만 가진다).")]
        [SerializeField] private List<PatternCondition> selectionConditions = new();

        [Header("광폭화")]
        [Tooltip("광폭화 시에만 추가되는 신규 패턴 여부 (EnrageConfig.newPatterns에 등록).")]
        [SerializeField] private bool isEnrageOnly;

        [Tooltip("광폭화 시 강화되는 기존 패턴 여부 (EnrageConfig의 강화 배율 적용 대상).")]
        [SerializeField] private bool enhancedInEnrage;

        [Header("난이도별 속도 계수")]
        [Tooltip("항목이 없는 난이도는 1.0으로 취급.")]
        [SerializeField] private List<DifficultySpeedEntry> speedMultiplierByDifficulty = new();

        [Header("실행 수치")]
        [SerializeField] private float castTime = 0.5f;    // TODO(밸런스): 문서 미정
        [SerializeField] private float duration = 1.5f;    // TODO(밸런스): 문서 미정
        [SerializeField] private float baseDamage = 10f;   // TODO(밸런스): 문서 미정 — DamageInfo.BaseDamage 성분

        [Header("실행 로직 (전략)")]
        // 이 SO는 '어떤 상황에 고를지'만 안다. '무엇을 하는지'는 이 Behaviour가 안다.
        // 돌진/거미줄/고치 소환 같은 구체 동작을 BossController의 switch문에 넣지 않기 위한 분리다
        //   → 보스 2번의 새 패턴은 Behaviour SO를 하나 더 만들어 꽂으면 끝이고, 컨트롤러는 손대지 않는다.
        [Tooltip("이 패턴이 실제로 무엇을 하는지 정의하는 실행 전략 SO. " +
                 "CoopGimmick 카테고리는 기믹이 대신 동작하므로 비워 둘 수 있다.")]
        [SerializeField] private BossPatternBehaviour behaviour;

        [Tooltip("CoopGimmick 카테고리 전용 — 작동시킬 기믹의 GimmickId. " +
                 "비우면 현재 작동 중이 아닌 첫 번째 기믹을 사용한다.")]
        [SerializeField] private string targetGimmickId;

        public string PatternId => patternId;
        public string DisplayName => displayName;
        public BossPatternCategory Category => category;
        public bool IsEnrageOnly => isEnrageOnly;
        public bool EnhancedInEnrage => enhancedInEnrage;
        public float CastTime => castTime;
        public float Duration => duration;
        public float BaseDamage => baseDamage;
        public BossPatternBehaviour Behaviour => behaviour;
        public string TargetGimmickId => targetGimmickId;

        /// <summary>
        /// 이 패턴 1회 실행분의 Runner를 만든다. Behaviour가 없으면 null —
        /// 호출측(BossController)은 null을 '실행할 동작이 없는 패턴'으로 처리한다.
        /// </summary>
        public BossPatternRunner CreateRunner() => behaviour != null ? behaviour.CreateRunner() : null;

        /// <summary>비어 있는 조건 목록도 후보가 되도록 부여하는 기본 스코어.</summary>
        public const float BaseScore = 1f;

        /// <summary>
        /// 조건 스코어링 — 충족한 조건의 가중치 합 + 기본 스코어.
        /// 최종 선택(최고점·반복 방지)은 BossPatternSelector가 담당한다.
        /// </summary>
        public float EvaluateScore(BossAIContext context)
        {
            float score = BaseScore;
            for (int i = 0; i < selectionConditions.Count; i++)
            {
                var cond = selectionConditions[i];
                if (cond != null && cond.Evaluate(context))
                    score += cond.Weight;
            }
            return score;
        }

        /// <summary>난이도별 패턴 속도 계수 (미등록 난이도는 1.0).</summary>
        public float GetSpeedMultiplier(Difficulty difficulty)
        {
            for (int i = 0; i < speedMultiplierByDifficulty.Count; i++)
            {
                if (speedMultiplierByDifficulty[i].difficulty == difficulty && speedMultiplierByDifficulty[i].speedMultiplier > 0f)
                    return speedMultiplierByDifficulty[i].speedMultiplier;
            }
            return 1f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(patternId))
                Debug.LogWarning($"[BossPattern] '{name}': patternId가 비어 있음 — 이력 반복 방지·동기화 식별에 필수.", this);

            // 협동 기믹 패턴은 기믹이 대신 동작하므로 Behaviour가 없어도 된다.
            // 그 외 패턴에 Behaviour가 없으면 '선택은 되는데 아무 일도 일어나지 않는' 유령 패턴이 된다.
            if (behaviour == null && category != BossPatternCategory.CoopGimmick)
                Debug.LogWarning($"[BossPattern] '{name}': 실행 전략(behaviour)이 비어 있음 — " +
                                 "선택돼도 아무 동작을 하지 않는다.", this);
        }
#endif
    }
}
