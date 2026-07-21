// 근거: 퍼즐 시스템.md — 레버 퍼즐: 정해진 순서 또는 동시에 조작해야 한다.
//       트롤: 레버를 반대로 당기면 문 대신 함정이 열린다.
// 프로토타입: 컨트롤러 없이 레버 하나만 놓고 당겨도 각도와 색이 바뀌어야 한다.
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
    [RequireComponent(typeof(Collider2D))] // E키 탐색(OverlapCircle) 대상이 되기 위해 필요
    public class PuzzleLever : PuzzleElement, IInteractable
    {
        [Header("레버")]
        [Tooltip("순서 퍼즐에서의 정답 순서(0부터). 동시 조작 퍼즐이면 -1.")]
        [SerializeField] private int orderIndex = -1;

        [Tooltip("역방향으로도 당길 수 있는가. true면 잘못 당겼을 때 함정이 열린다(트롤). 순수 조작 검증만 하려면 끈다.")]
        [SerializeField] private bool allowReverse = true;

        [Header("연출")]
        [Tooltip("역방향으로 넘어갔을 때의 색.")]
        [SerializeField] private Color reverseColor = new Color(0.95f, 0.55f, 0.2f, 1f);

        public LeverDirection Direction { get; private set; } = LeverDirection.Neutral;
        public int OrderIndex => orderIndex;

        public string PromptDescription => Direction == LeverDirection.Forward ? "레버 되돌리기" : "레버 당기기";

        public override string DebugStatus =>
            orderIndex >= 0 ? $"{Direction} (순서 {orderIndex})" : Direction.ToString();

        protected override void Awake()
        {
            // 당기면 손잡이가 넘어간다 — 자동 생성 스프라이트를 45도 기울여 방향이 보이게 한다.
            visual.SuggestMotion(Vector3.zero, -45f);
            base.Awake();
        }

        public bool CanInteract(PlayerController user)
        {
            // owner가 없으면 단독 레버 — 항상 조작 가능(프로토타입 검증).
            return owner == null || owner.State == PuzzleState.Active;
        }

        public void Interact(PlayerController user)
        {
            if (!CanInteract(user)) return;

            RegisterParticipant(user);

            // 플레이어가 바라보는 방향으로 당긴다 — 반대로 당기면 트롤 결과.
            bool pullForward = user == null || user.FacingSign >= 0;

            if (!pullForward && allowReverse)
            {
                Direction = LeverDirection.Reverse;
                visual.SetActive(true);
                visual.SetOverrideColor(reverseColor); // 역방향은 색으로 구분된다
                Log("역방향으로 당김 — 문 대신 함정이 열린다");

                NotifyStateChanged();
                NotifyWrongAction();
                return;
            }

            Direction = Direction == LeverDirection.Forward ? LeverDirection.Neutral : LeverDirection.Forward;

            visual.ClearOverrideColor();
            visual.SetActive(Direction == LeverDirection.Forward);
            Log(Direction == LeverDirection.Forward ? "정방향으로 당김" : "중립으로 되돌림");

            NotifyStateChanged();
        }

        public void ResetElement()
        {
            Direction = LeverDirection.Neutral;
            visual.ResetVisual();
        }
    }
}
