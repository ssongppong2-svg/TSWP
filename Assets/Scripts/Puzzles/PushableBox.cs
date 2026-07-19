// 근거: 퍼즐 시스템.md — 구조물 퍼즐: 상자를 밀어 길을 만들거나 발판을 누른다.
//       트롤: 상자를 잘못 밀면 숨겨진 적이 등장한다.
using UnityEngine;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 밀 수 있는 상자. 목표 지점에 도달하면 정답으로 판정된다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PushableBox : PuzzleElement, IInteractable
    {
        [Header("밀기")]
        [Tooltip("한 번 상호작용할 때 밀리는 거리(타일 단위 권장).")]
        [SerializeField, Min(0.1f)] private float pushDistance = 1f; // TODO(밸런스): 문서 미정

        [Header("목표")]
        [Tooltip("이 지점 반경 안에 들어오면 정답. 비우면 위치 판정을 하지 않는다.")]
        [SerializeField] private Transform targetPoint;
        [SerializeField, Min(0.05f)] private float targetTolerance = 0.5f;

        [Tooltip("이 방향으로 밀면 오조작(숨겨진 적 등장). 0이면 오조작 판정 없음.")]
        [SerializeField] private int wrongPushSign;

        private Rigidbody2D _body;
        private Vector2 _initialPosition;

        public bool IsOnTarget { get; private set; }

        public string PromptDescription => "상자 밀기";

        protected override void Awake()
        {
            base.Awake();
            _body = GetComponent<Rigidbody2D>();
            _initialPosition = transform.position;
        }

        public bool CanInteract(PlayerController user)
        {
            return owner == null || owner.State == PuzzleState.Active;
        }

        public void Interact(PlayerController user)
        {
            if (!CanInteract(user)) return;

            owner?.AddParticipant(user != null ? user.PlayerId : -1);

            int pushSign = user != null ? user.FacingSign : 1;

            // TODO(물리): 벽/지형 충돌 검사 후 이동 — 현재는 즉시 이동한다.
            _body.MovePosition(_body.position + new Vector2(pushSign * pushDistance, 0f));

            if (wrongPushSign != 0 && pushSign == wrongPushSign)
                NotifyWrongAction(); // 숨겨진 적 등장

            EvaluateTarget();
            NotifyStateChanged();
        }

        private void EvaluateTarget()
        {
            if (targetPoint == null) { IsOnTarget = false; return; }
            IsOnTarget = Vector2.Distance(transform.position, targetPoint.position) <= targetTolerance;
        }

        public void ResetElement()
        {
            transform.position = _initialPosition;
            if (_body != null) _body.linearVelocity = Vector2.zero;
            IsOnTarget = false;
        }
    }
}
