// 근거: 퍼즐 시스템.md — 레버 퍼즐: 정해진 순서 또는 동시에 조작해야 한다.
// 근거: 보스 시스템.md — Hatch Queen Phase 2에서 레버 퍼즐을 사용한다.
// 순서형/동시형을 데이터로 고른다 — 퍼즐이 늘어나도 이 코드는 그대로다.
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Puzzles
{
    /// <summary>레버 퍼즐 판정 방식.</summary>
    public enum LeverPuzzleMode
    {
        /// <summary>모든 레버를 동시에 정방향으로 — 8인 협동에서 인원이 흩어져야 한다.</summary>
        Simultaneous,
        /// <summary>orderIndex 순서대로 당겨야 한다. 틀리면 초기화된다.</summary>
        Sequence,
    }

    /// <summary>
    /// 레버 퍼즐. 동시형과 순서형을 한 컨트롤러가 데이터로 처리한다.
    /// </summary>
    public class LeverPuzzleController : PuzzleController
    {
        [Header("구성 요소")]
        [Tooltip("이 퍼즐에 속한 레버들. 순서형이면 각 레버의 orderIndex를 0부터 지정한다.")]
        [SerializeField] private List<PuzzleLever> levers = new List<PuzzleLever>();

        [Header("판정")]
        [SerializeField] private LeverPuzzleMode mode = LeverPuzzleMode.Simultaneous;

        [Tooltip("순서형에서 틀렸을 때 전부 초기화할지. false면 틀린 레버만 되돌린다.")]
        [SerializeField] private bool resetAllOnWrongOrder = true;

        [Tooltip("목록이 비어 있으면 자식에서 레버를 자동 수집한다 (씬에 놓기만 해도 동작하도록).")]
        [SerializeField] private bool autoCollectChildren = true;

        protected override void Awake()
        {
            base.Awake();

            if (!autoCollectChildren || levers.Count > 0) return;

            GetComponentsInChildren(true, levers);
            PuzzleLog.Record(this, $"{name}: 자식에서 레버 {levers.Count}개 자동 수집");
        }

        /// <summary>순서형 진행도 — 다음에 당겨야 할 orderIndex.</summary>
        private int _expectedIndex;

        /// <summary>진행도 0~1. 보스 협동 퍼즐 어댑터가 게이지로 표시할 수 있다.</summary>
        public float Progress
        {
            get
            {
                if (levers.Count == 0) return 0f;

                if (mode == LeverPuzzleMode.Sequence)
                    return Mathf.Clamp01((float)_expectedIndex / levers.Count);

                int pulled = 0;
                for (int i = 0; i < levers.Count; i++)
                    if (levers[i] != null && levers[i].Direction == LeverDirection.Forward) pulled++;

                return Mathf.Clamp01((float)pulled / levers.Count);
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < levers.Count; i++)
            {
                if (levers[i] == null) continue;
                levers[i].StateChanged += OnLeverChanged;
                levers[i].WrongAction += OnLeverWrongAction;
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < levers.Count; i++)
            {
                if (levers[i] == null) continue;
                levers[i].StateChanged -= OnLeverChanged;
                levers[i].WrongAction -= OnLeverWrongAction;
            }
        }

        private void OnLeverChanged(PuzzleElement element)
        {
            if (mode == LeverPuzzleMode.Sequence)
                EvaluateSequenceStep(element as PuzzleLever);

            CheckSolved();
        }

        private void OnLeverWrongAction(PuzzleElement element, TrollOutcome outcome)
        {
            if (outcome == null) return;

            // 오조작 결과 적용 — 퍼즐 전체 실패 여부는 데이터가 정한다.
            ApplyPenalty(outcome.consequence);

            if (outcome.causesPuzzleFailure) Fail();
        }

        /// <summary>순서형: 방금 당긴 레버가 기대 순서와 맞는지 확인한다.</summary>
        private void EvaluateSequenceStep(PuzzleLever lever)
        {
            if (lever == null) return;
            if (lever.Direction != LeverDirection.Forward) return; // 되돌린 것은 판정하지 않는다

            if (lever.OrderIndex == _expectedIndex)
            {
                _expectedIndex++;
                return;
            }

            // 순서가 틀렸다 — 되돌린다. 게임 오버는 없다(퍼즐 시스템.md).
            if (resetAllOnWrongOrder) ResetPuzzle();
            else lever.ResetElement();

            _expectedIndex = 0;
        }

        protected override bool EvaluateSolved()
        {
            if (levers.Count == 0) return false;

            if (mode == LeverPuzzleMode.Sequence)
                return _expectedIndex >= levers.Count;

            // 동시형 — 전부 정방향이어야 한다
            for (int i = 0; i < levers.Count; i++)
            {
                if (levers[i] == null) continue;
                if (levers[i].Direction != LeverDirection.Forward) return false;
            }
            return true;
        }

        public override void ResetPuzzle()
        {
            _expectedIndex = 0;
            for (int i = 0; i < levers.Count; i++)
                levers[i]?.ResetElement();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (mode != LeverPuzzleMode.Sequence) return;

            // 순서형인데 orderIndex가 안 매겨져 있으면 영원히 풀리지 않는다.
            for (int i = 0; i < levers.Count; i++)
            {
                if (levers[i] == null) continue;
                if (levers[i].OrderIndex < 0)
                {
                    Debug.LogWarning(
                        $"[LeverPuzzleController] '{name}': 순서형인데 '{levers[i].name}'의 orderIndex가 -1입니다. " +
                        "0부터 순서를 지정하세요.", this);
                }
            }
        }
#endif
    }
}
