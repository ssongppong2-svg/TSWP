// 근거: 퍼즐 시스템.md — 퍼즐은 '설명 없이도 이해 가능'해야 하며 최소 2명 협동이 기본이다(참여 인원 집계 필요).
// 근거: ARCHITECTURE.md §4 TSWP.Puzzles — 퍼즐 FSM은 PuzzleController가 소유한다. 이 컴포넌트는 '시작 트리거'만 담당한다.
// 문제: PuzzleController가 Idle로 남아 있으면 요소들의 CanInteract가 전부 false라 버튼을 눌러도 아무 일이 없다.
//       방 시스템이 없는 씬(보스 협동 퍼즐/단독 검증)에서는 아무도 Begin()을 불러 주지 않는다.
// 해결: 퍼즐 앞에 이 트리거 구역을 두면 플레이어가 접근하는 순간 Begin()이 호출되고 참여자로 등록된다.
using UnityEngine;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 퍼즐 시작 구역. 플레이어가 트리거에 들어오면 소속 퍼즐을 Active로 만들고 참여자로 등록한다.
    /// 퍼즐이 없거나(미배선) 이미 해결된 퍼즐이면 조용히 아무 일도 하지 않는다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [DisallowMultipleComponent]
    public sealed class PuzzleZoneTrigger : MonoBehaviour
    {
        [Header("대상 퍼즐")]
        [Tooltip("시작시킬 퍼즐. 비우면 부모에서 자동 탐색한다.")]
        [SerializeField] private PuzzleController puzzle;

        [Header("동작")]
        [Tooltip("플레이어가 들어오면 Begin()을 호출한다.")]
        [SerializeField] private bool beginOnEnter = true;

        [Tooltip("구역 안의 플레이어를 퍼즐 참여자로 등록/해제한다 (최소 협동 인원 판정에 쓰인다).")]
        [SerializeField] private bool trackParticipants = true;

        [Tooltip("실패/리커버리로 멈춘 퍼즐도 다시 들어오면 재시작한다 (소프트락 방지). " +
                 "끄면 Idle 상태에서만 시작한다.")]
        [SerializeField] private bool restartWhenNotActive = true;

        [Header("프로토타입 편의")]
        [Tooltip("콜라이더를 자동으로 트리거로 만든다. 끄면 트리거가 아닌 콜라이더에서는 감지되지 않는다.")]
        [SerializeField] private bool forceTriggerCollider = true;

        /// <summary>현재 구역 안에 있는 플레이어 수 (디버그 표시용).</summary>
        public int PlayersInside { get; private set; }

        private void Awake()
        {
            if (puzzle == null) puzzle = GetComponentInParent<PuzzleController>();

            if (!forceTriggerCollider) return;

            // 트리거가 아니면 OnTriggerEnter2D가 오지 않아 '가까이 가도 안 켜진다'가 된다.
            var colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var player = other != null ? other.GetComponentInParent<PlayerController>() : null;
            if (player == null) return;

            PlayersInside++;

            if (puzzle == null) return; // 미배선 — 조용히 생략 (씬 배치가 덜 끝나도 게임이 죽으면 안 된다)

            if (trackParticipants) puzzle.AddParticipant(ResolveParticipantId(player));

            if (!beginOnEnter) return;
            if (puzzle.State == PuzzleState.Solved) return;
            if (puzzle.State == PuzzleState.Active) return;
            if (puzzle.State != PuzzleState.Idle && !restartWhenNotActive) return;

            puzzle.Begin();
            PuzzleLog.Record(this, $"{name}: 플레이어 접근 — '{puzzle.name}' 시작");
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var player = other != null ? other.GetComponentInParent<PlayerController>() : null;
            if (player == null) return;

            if (PlayersInside > 0) PlayersInside--;

            if (puzzle == null || !trackParticipants) return;
            puzzle.RemoveParticipant(ResolveParticipantId(player));
        }

        /// <summary>
        /// 참여자 식별자. CombatEntity가 붙어 있으면 PlayerId를 쓴다.
        /// 폴백이 필요한 이유와 이름 해시를 쓰는 근거는 PuzzleElement.ResolveParticipantId와 동일하다
        /// (GetInstanceID는 Unity 6에서 폐기 — 쓰지 않는다).
        /// </summary>
        private static int ResolveParticipantId(PlayerController user)
        {
            if (user == null) return -1;

            int id = user.PlayerId;
            if (id >= 0) return id;

            return Mathf.Abs(user.name.GetHashCode());
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            var col = GetComponent<Collider2D>();
            if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
#endif
    }
}
