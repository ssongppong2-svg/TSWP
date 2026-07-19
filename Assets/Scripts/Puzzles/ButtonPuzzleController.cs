// 근거: 퍼즐 시스템.md — 버튼 퍼즐: 여러 버튼을 동시에 눌러야 한다 (협동 강제).
// 유형별 컨트롤러의 표준 구현 예시. 다른 유형(레버 순서/발판 유지/운반/폭탄 릴레이)도 같은 패턴으로 파생한다:
//   ① 요소 목록을 [SerializeField]로 받고 ② OnEnable에서 StateChanged/WrongAction 구독
//   ③ EvaluateSolved()에서 요소 상태를 집계 판정 ④ ResetPuzzle()에서 요소별 ResetElement 호출
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 여러 버튼을 동시에 눌러야 열리는 퍼즐. 버튼의 유지 시간(holdSeconds) 안에 전원이 눌러야 한다.
    /// </summary>
    public class ButtonPuzzleController : PuzzleController
    {
        [Header("구성 요소")]
        [Tooltip("정답 조합에 포함되는 버튼들. 전부 동시에 눌려야 해결된다.")]
        [SerializeField] private List<PuzzleButton> buttons = new List<PuzzleButton>();

        private void OnEnable()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == null) continue;
                buttons[i].StateChanged += OnElementStateChanged;
                buttons[i].WrongAction += OnElementWrongAction;
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == null) continue;
                buttons[i].StateChanged -= OnElementStateChanged;
                buttons[i].WrongAction -= OnElementWrongAction;
            }
        }

        private void OnElementStateChanged(PuzzleElement element) => CheckSolved();

        private void OnElementWrongAction(PuzzleElement element, TrollOutcome outcome)
        {
            if (outcome == null) return;

            // 오조작 결과 적용 — 퍼즐 전체 실패로 이어질지는 데이터가 결정한다.
            ApplyPenalty(outcome.consequence);

            if (outcome.causesPuzzleFailure)
                Fail();
        }

        /// <summary>모든 정답 버튼이 동시에 눌려 있는가.</summary>
        protected override bool EvaluateSolved()
        {
            if (buttons.Count == 0) return false;

            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i] == null) continue;
                if (!buttons[i].IsPressed) return false;
            }
            return true;
        }

        public override void ResetPuzzle()
        {
            for (int i = 0; i < buttons.Count; i++)
                buttons[i]?.ResetElement();
        }
    }
}
