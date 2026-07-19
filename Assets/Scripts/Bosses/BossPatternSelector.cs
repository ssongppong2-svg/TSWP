// 근거: 보스 시스템.md — 패턴은 무작위가 아닌 상황에 따라 선택된다 / 같은 패턴만 반복하지 않는다.
// 순수 C# — 조건 스코어링(최고점 결정론적 선택) + 최근 패턴 이력 기반 반복 방지.
using System.Collections.Generic;

namespace TSWP.Bosses
{
    /// <summary>
    /// 상황 기반 패턴 선택기. 무작위 선택 금지 — EvaluateScore 최고점을 결정론적으로 고른다.
    /// 직전 패턴은 후보에서 제외하고(대안이 있을 때), 이력 내 등장 횟수만큼 감점해 반복을 막는다.
    /// SYNC: 호스트 권위 — 선택 결과(patternId)만 전파하면 클라이언트 재현 가능.
    /// </summary>
    public sealed class BossPatternSelector
    {
        /// <summary>이력에 1회 등장할 때마다 깎는 감점. TODO(밸런스): 문서 미정 — 반복 억제 강도.</summary>
        public const float RepeatPenaltyPerUse = 0.75f;

        private readonly List<string> _history = new();
        private readonly int _historyLimit;
        private string _lastPatternId;

        /// <summary>반복 방지 판정에 쓰는 최근 이력 (BossAIContext.RecentPatternIds로 공유).</summary>
        public IReadOnlyList<string> RecentPatternIds => _history;

        public BossPatternSelector(int historyLimit = 4) // TODO(밸런스): 이력 창 크기 문서 미정
        {
            _historyLimit = historyLimit < 1 ? 1 : historyLimit;
        }

        /// <summary>
        /// 후보 목록에서 현재 상황에 가장 적합한 패턴을 고른다.
        /// - isEnrageOnly 패턴은 광폭화 중에만 후보.
        /// - 직전 패턴은 다른 유효 후보가 있으면 제외 (같은 패턴 연속 금지).
        /// - 이력 내 등장 횟수 × RepeatPenaltyPerUse 감점.
        /// 후보가 전혀 없으면 null (호출측이 대기/기본 공격 처리).
        /// </summary>
        public BossPattern Select(IReadOnlyList<BossPattern> candidates, BossAIContext context, bool isEnraged)
        {
            if (candidates == null || candidates.Count == 0) return null;

            // 1차: 직전 패턴 제외하고 최고점 탐색.
            BossPattern best = FindBest(candidates, context, isEnraged, excludeLast: true);
            // 2차: 직전 패턴밖에 없다면 허용 (단일 후보 데이터 방어 — OnValidate가 5개 이상을 강제하므로 예외 상황).
            if (best == null)
                best = FindBest(candidates, context, isEnraged, excludeLast: false);

            if (best != null)
                RecordUse(best.PatternId);
            return best;
        }

        /// <summary>이력 초기화 (보스전 시작/페이즈 리셋 시).</summary>
        public void ClearHistory()
        {
            _history.Clear();
            _lastPatternId = null;
        }

        private BossPattern FindBest(IReadOnlyList<BossPattern> candidates, BossAIContext context, bool isEnraged, bool excludeLast)
        {
            BossPattern best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                var pattern = candidates[i];
                if (pattern == null) continue;
                if (pattern.IsEnrageOnly && !isEnraged) continue;               // 광폭화 전용 패턴 차단
                if (excludeLast && pattern.PatternId == _lastPatternId) continue; // 직전 반복 금지

                float score = pattern.EvaluateScore(context) - RepeatPenaltyPerUse * CountInHistory(pattern.PatternId);
                // 동점이면 목록 앞쪽 우선 — 결정론 유지 (무작위 타이브레이크 금지).
                if (score > bestScore)
                {
                    bestScore = score;
                    best = pattern;
                }
            }
            return best;
        }

        private int CountInHistory(string patternId)
        {
            int count = 0;
            for (int i = 0; i < _history.Count; i++)
                if (_history[i] == patternId) count++;
            return count;
        }

        private void RecordUse(string patternId)
        {
            _lastPatternId = patternId;
            _history.Add(patternId);
            while (_history.Count > _historyLimit)
                _history.RemoveAt(0);
        }
    }
}
