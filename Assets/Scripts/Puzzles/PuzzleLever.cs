// 근거: 퍼즐 시스템.md — 레버 퍼즐: 정해진 순서 또는 동시에 조작해야 한다.
//       트롤: 레버를 반대로 당기면 문 대신 함정이 열린다.
using UnityEngine;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>레버 방향.</summary>
    public enum LeverDirection
    {
        Neutral, // 중립(초기)
        Forward, // 정방향 — 정답
        Reverse, // 역방향 — 트롤 결과 유발
    }

    /// <summary>
    /// 퍼즐 레버. 순서 퍼즐에서는 orderIndex로 정답 순서를 판정한다.
    /// </summary>
    public class PuzzleLever : PuzzleElement, IInteractable
    {
        [Header("레버")]
        [Tooltip("순서 퍼즐에서의 정답 순서(0부터). 동시 조작 퍼즐이면 -1.")]
        [SerializeField] private int orderIndex = -1;

        [Tooltip("역방향으로도 당길 수 있는가. true면 잘못 당겼을 때 함정이 열린다(트롤).")]
        [SerializeField] private bool allowReverse = true;

        public LeverDirection Direction { get; private set; } = LeverDirection.Neutral;
        public int OrderIndex => orderIndex;

        public string PromptDescription => Direction == LeverDirection.Forward ? "레버 되돌리기" : "레버 당기기";

        public bool CanInteract(PlayerController user)
        {
            return owner == null || owner.State == PuzzleState.Active;
        }

        public void Interact(PlayerController user)
        {
            if (!CanInteract(user)) return;

            owner?.AddParticipant(user != null ? user.PlayerId : -1);

            // 플레이어가 바라보는 방향으로 당긴다 — 반대로 당기면 트롤 결과.
            bool pullForward = user == null || user.FacingSign >= 0;

            if (!pullForward && allowReverse)
            {
                Direction = LeverDirection.Reverse;
                NotifyStateChanged();
                NotifyWrongAction(); // 문 대신 함정이 열린다
                return;
            }

            Direction = Direction == LeverDirection.Forward ? LeverDirection.Neutral : LeverDirection.Forward;
            NotifyStateChanged();
        }

        public void ResetElement()
        {
            Direction = LeverDirection.Neutral;
        }
    }
}
