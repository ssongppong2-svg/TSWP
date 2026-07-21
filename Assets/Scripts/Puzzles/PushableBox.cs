// 근거: 퍼즐 시스템.md — 구조물 퍼즐: 상자를 밀어 길을 만들거나 발판을 누른다.
//       트롤: 상자를 잘못 밀면 숨겨진 적이 등장한다.
// 프로토타입: '실제로 밀린다'가 성립해야 한다. 두 가지 밀기를 모두 지원한다.
//   ① E키 한 칸 밀기 — 벽에 막히면 밀리지 않는다 (Rigidbody2D.Cast로 사전 검사)
//   ② 몸으로 밀기 — Dynamic 물리로 자연스럽게 밀린다 (PlayerController가 속도를 직접 유지하므로 접촉만으로 밀린다)
using UnityEngine;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 밀 수 있는 상자. 목표 지점에 도달하면 정답으로 판정된다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class PushableBox : PuzzleElement, IInteractable
    {
        [Header("밀기")]
        [Tooltip("E키로 한 번 상호작용할 때 밀리는 거리(타일 단위 권장).")]
        [SerializeField, Min(0.1f)] private float pushDistance = 1f; // TODO(밸런스): 문서 미정

        [Tooltip("이 레이어에 막히면 E키 밀기가 취소된다. 지형/구조물 레이어를 지정한다.")]
        [SerializeField] private LayerMask blockMask = ~0;

        [Header("물리 (프로토타입 자동 설정)")]
        [Tooltip("켜면 Awake에서 Dynamic·회전잠금·적당한 질량으로 맞춰 몸으로도 밀 수 있게 만든다.")]
        [SerializeField] private bool applyPushablePhysicsDefaults = true;

        [Tooltip("몸으로 밀 때의 무게감. 작을수록 가볍게 밀린다.")]
        [SerializeField, Min(0.1f)] private float mass = 2f; // TODO(밸런스): 문서 미정

        [Tooltip("바닥 마찰 대용 감쇠. 클수록 손을 떼면 금방 멈춘다.")]
        [SerializeField, Min(0f)] private float linearDamping = 6f;

        [Header("목표")]
        [Tooltip("이 지점 반경 안에 들어오면 정답. 비우면 위치 판정을 하지 않는다.")]
        [SerializeField] private Transform targetPoint;
        [SerializeField, Min(0.05f)] private float targetTolerance = 0.5f;

        [Tooltip("도달했을 때 표시할 색.")]
        [SerializeField] private Color onTargetColor = new Color(1f, 0.85f, 0.25f, 1f);

        [Tooltip("이 방향으로 밀면 오조작(숨겨진 적 등장). 0이면 오조작 판정 없음.")]
        [SerializeField] private int wrongPushSign;

        private Rigidbody2D _body;
        private Vector2 _initialPosition;

        // 밀기 사전 검사용 재사용 버퍼 (Unity 6: NonAlloc 계열 대신 ContactFilter2D 오버로드)
        private readonly RaycastHit2D[] _castHits = new RaycastHit2D[8];
        private ContactFilter2D _castFilter;

        public bool IsOnTarget { get; private set; }

        public string PromptDescription => "상자 밀기";

        public override string DebugStatus =>
            targetPoint == null ? "목표 미지정" : (IsOnTarget ? "목표 도달" : "목표 미도달");

        protected override void Awake()
        {
            base.Awake();

            _body = GetComponent<Rigidbody2D>();
            _initialPosition = transform.position;

            if (_body != null && applyPushablePhysicsDefaults)
            {
                // 몸으로 밀리게 하려면 Dynamic이어야 한다. 회전은 잠가야 상자가 굴러가지 않는다.
                _body.bodyType = RigidbodyType2D.Dynamic;
                _body.freezeRotation = true;
                _body.mass = mass;
                _body.linearDamping = linearDamping;
                _body.gravityScale = Mathf.Approximately(_body.gravityScale, 0f) ? 1f : _body.gravityScale;
            }

            _castFilter = new ContactFilter2D { useTriggers = false };

            EvaluateTarget();
        }

        protected override void Update()
        {
            base.Update(); // 시각 보간

            // 몸으로 밀렸을 때도 목표 판정이 갱신되어야 한다.
            EvaluateTarget();
        }

        public bool CanInteract(PlayerController user)
        {
            return owner == null || owner.State == PuzzleState.Active;
        }

        public void Interact(PlayerController user)
        {
            if (!CanInteract(user)) return;

            RegisterParticipant(user);

            int pushSign = user != null ? user.FacingSign : 1;
            Vector2 delta = new Vector2(pushSign * pushDistance, 0f);

            if (IsBlocked(delta, user))
            {
                visual.Flash(Color.gray); // 막혔다는 사실이 보여야 한다
                Log("밀리지 않는다 — 진행 방향이 막혀 있다");
                return;
            }

            // 위치 직접 지정 = 즉시 이동. Dynamic 바디에서도 정상 동작한다(텔레포트).
            Vector2 next = _body.position + delta;
            _body.position = next;
            _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
            transform.position = new Vector3(next.x, next.y, transform.position.z);

            Log($"{(pushSign > 0 ? "오른쪽" : "왼쪽")}으로 {pushDistance:0.##} 밀림");

            if (wrongPushSign != 0 && pushSign == wrongPushSign)
                NotifyWrongAction(); // 숨겨진 적 등장

            EvaluateTarget();
            NotifyStateChanged();
        }

        /// <summary>진행 방향에 벽/구조물이 있는지 사전 검사. 미는 플레이어 자신은 무시한다.</summary>
        private bool IsBlocked(Vector2 delta, PlayerController pusher)
        {
            if (_body == null) return false;

            float distance = delta.magnitude;
            if (distance <= 0.0001f) return true;

            _castFilter.useTriggers = false;
            _castFilter.useLayerMask = true;
            _castFilter.SetLayerMask(blockMask);

            int count = _body.Cast(delta / distance, _castFilter, _castHits, distance);
            for (int i = 0; i < count; i++)
            {
                var col = _castHits[i].collider;
                if (col == null) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                if (pusher != null && (col.transform == pusher.transform || col.transform.IsChildOf(pusher.transform))) continue;
                return true;
            }
            return false;
        }

        private void EvaluateTarget()
        {
            bool onTarget = targetPoint != null
                            && Vector2.Distance(transform.position, targetPoint.position) <= targetTolerance;

            if (onTarget == IsOnTarget) return;
            IsOnTarget = onTarget;

            if (onTarget)
            {
                visual.SetOverrideColor(onTargetColor);
                Log("목표 지점 도달");
            }
            else
            {
                visual.ClearOverrideColor();
            }

            NotifyStateChanged();
        }

        public void ResetElement()
        {
            transform.position = _initialPosition;
            if (_body != null)
            {
                _body.position = _initialPosition;
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
            }

            IsOnTarget = false;
            visual.ResetVisual();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (targetPoint == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPoint.position, targetTolerance);
            Gizmos.DrawLine(transform.position, targetPoint.position);
        }
#endif
    }
}
