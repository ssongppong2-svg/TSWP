// 근거: 퍼즐 시스템.md — 퍼즐 요소(버튼/레버/발판/상자/운반물/폭탄)는 각각 '정답 동작'과 '오답(트롤) 동작'을 가진다.
//       트롤 철학: 실수와 장난은 웃음이 되어야 하며, 고의적 방해를 유도해서는 안 된다.
// 요소는 컨트롤러를 직접 알지 않고 C# event로 상태 변화만 통지한다 (결합도 최소화).
using System;
using UnityEngine;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 퍼즐 구성 요소의 공통 기반. 파생 클래스가 상호작용 방식을 구현한다.
    /// </summary>
    public abstract class PuzzleElement : MonoBehaviour
    {
        [Header("소속")]
        [Tooltip("이 요소가 속한 퍼즐. 비우면 부모에서 자동 탐색한다.")]
        [SerializeField] protected PuzzleController owner;

        [Header("트롤 결과")]
        [Tooltip("오조작 시 발생할 결과. 비워두면 오조작에 결과가 없다.")]
        [SerializeField] protected TrollOutcome trollOutcome;

        /// <summary>요소 상태가 변했을 때 발행 — 컨트롤러가 구독해 정답 판정을 수행한다.</summary>
        public event Action<PuzzleElement> StateChanged;

        /// <summary>오조작(트롤)이 발생했을 때 발행.</summary>
        public event Action<PuzzleElement, TrollOutcome> WrongAction;

        public PuzzleController Owner => owner;

        protected virtual void Awake()
        {
            if (owner == null)
                owner = GetComponentInParent<PuzzleController>();
        }

        /// <summary>상태 변화 통지 — 파생 클래스가 상태를 바꾼 뒤 호출한다.</summary>
        protected void NotifyStateChanged() => StateChanged?.Invoke(this);

        /// <summary>
        /// 오조작 통지. 결과(몬스터 소환/함정 개방/다리 붕괴 등)는 TrollOutcome 데이터가 정의한다.
        /// 웃음을 만드는 것이 목적이므로 진행을 완전히 막는 결과는 넣지 않는다.
        /// </summary>
        protected void NotifyWrongAction()
        {
            WrongAction?.Invoke(this, trollOutcome);
            GameEventsTrollCounter();
        }

        private void GameEventsTrollCounter()
        {
            // 트롤 업적 카운터 — 실수도 기록되어 결과 화면의 '가장 많은 트롤(?)'에 반영된다.
            Core.GameEvents.RaiseStatCounter("puzzle.troll", 1);
        }
    }
}
