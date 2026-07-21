// 근거: 퍼즐 시스템.md — 운반 퍼즐: 오브젝트를 목적지까지 옮긴다. 운반 중에는 공격받을 수 있다(피격 시 낙하).
//       운반자는 무방비가 되므로 팀원의 보호가 필요하다 — 협동 강제 장치.
// 프로토타입: E키로 들 수 있어야 하고, 들린 상태가 색으로 보여야 하며, 들고 있는 동안에도 다시 E로 내려놓을 수 있어야 한다.
//   → 들었을 때 콜라이더를 '끄지 않고' 트리거로만 바꾼다. 끄면 PlayerInteraction 탐색에서 사라져 내려놓기가 불가능해진다.
using UnityEngine;
using TSWP.Combat;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 운반 가능한 오브젝트. 들고 있는 동안 운반자를 따라다니며, 피격 시 떨어진다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CarryObject : PuzzleElement, IInteractable
    {
        [Header("운반")]
        [Tooltip("운반자 머리 위 오프셋.")]
        [SerializeField] private Vector2 carryOffset = new Vector2(0f, 1.2f);

        [Tooltip("피격 시 떨어뜨리는가 (문서: 운반 중 공격받을 수 있다).")]
        [SerializeField] private bool dropOnDamage = true;

        [Header("목표")]
        [SerializeField] private Transform destination;
        [SerializeField, Min(0.05f)] private float destinationTolerance = 0.6f;

        [Header("연출")]
        [Tooltip("들린 상태 색.")]
        [SerializeField] private Color carriedColor = new Color(0.35f, 0.70f, 1f, 1f);

        [Tooltip("배달 완료 색.")]
        [SerializeField] private Color deliveredColor = new Color(1f, 0.85f, 0.25f, 1f);

        private PlayerController _carrier;
        private CombatEntity _carrierEntity;
        private Rigidbody2D _body;
        private RigidbodyType2D _bodyTypeDefault;
        private Vector2 _initialPosition;

        public bool IsCarried => _carrier != null;
        public bool IsDelivered { get; private set; }

        public string PromptDescription => IsCarried ? "내려놓기" : "들기";

        public override string DebugStatus =>
            IsDelivered ? "배달 완료"
            : IsCarried ? $"'{_carrier.name}'이(가) 운반 중"
            : (destination == null ? "대기 (목적지 미지정)" : "대기");

        protected override void Awake()
        {
            base.Awake();

            _initialPosition = transform.position;
            _body = GetComponent<Rigidbody2D>();
            if (_body != null) _bodyTypeDefault = _body.bodyType;
        }

        public bool CanInteract(PlayerController user)
        {
            if (owner != null && owner.State != PuzzleState.Active) return false;
            // 이미 다른 사람이 들고 있으면 뺏을 수 없다.
            return _carrier == null || _carrier == user;
        }

        public void Interact(PlayerController user)
        {
            if (!CanInteract(user)) return;

            RegisterParticipant(user);

            if (IsCarried) Drop(false);
            else PickUp(user);
        }

        private void PickUp(PlayerController user)
        {
            if (user == null) return;

            _carrier = user;
            _carrierEntity = user.GetComponent<CombatEntity>();

            if (dropOnDamage && _carrierEntity != null)
                _carrierEntity.Damaged += OnCarrierDamaged;

            // 들려 있는 동안은 물리를 멈추고 운반자를 밀지 않게 한다(탐지는 유지).
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
                _body.bodyType = RigidbodyType2D.Kinematic;
            }
            SetCollidersPassable(true);

            visual.SetActive(true);
            visual.SetOverrideColor(carriedColor);
            Log($"'{user.name}'이(가) 들었다");

            NotifyStateChanged();
        }

        /// <summary>내려놓기. wasForced가 true면 피격으로 인한 낙하(트롤 결과).</summary>
        public void Drop(bool wasForced)
        {
            if (_carrierEntity != null)
                _carrierEntity.Damaged -= OnCarrierDamaged;

            // 머리 위가 아니라 운반자 발밑 근처에 놓는다 — 공중에 떠 있는 것처럼 보이지 않게.
            if (_carrier != null)
                transform.position = (Vector2)_carrier.transform.position + new Vector2(_carrier.FacingSign * 0.6f, 0f);

            _carrier = null;
            _carrierEntity = null;

            if (_body != null)
            {
                _body.bodyType = _bodyTypeDefault;
                _body.linearVelocity = Vector2.zero;
            }
            SetCollidersPassable(false);

            visual.SetActive(false);
            visual.ClearOverrideColor();

            EvaluateDestination();

            if (wasForced)
            {
                Log("피격으로 운반물을 떨어뜨렸다");
                NotifyWrongAction(); // 운반물 낙하 — 진행도 손실
            }
            else
            {
                Log("내려놓았다");
            }

            NotifyStateChanged();
        }

        private void OnCarrierDamaged(DamageInfo info) => Drop(true);

        private void LateUpdate()
        {
            if (_carrier == null) return;

            transform.position = (Vector2)_carrier.transform.position + carryOffset;

            // 목적지까지 '들고 간' 순간에도 즉시 판정된다 — 내려놓아야만 완료되는 답답함을 없앤다.
            EvaluateDestination();
        }

        private void EvaluateDestination()
        {
            bool delivered = destination != null
                             && Vector2.Distance(transform.position, destination.position) <= destinationTolerance;

            if (delivered == IsDelivered) return;
            IsDelivered = delivered;

            if (delivered)
            {
                visual.SetOverrideColor(deliveredColor);
                Log("목적지 도착!");
            }
            else if (!IsCarried)
            {
                visual.ClearOverrideColor();
            }

            NotifyStateChanged();
        }

        public void ResetElement()
        {
            if (_carrierEntity != null)
                _carrierEntity.Damaged -= OnCarrierDamaged;

            _carrier = null;
            _carrierEntity = null;
            IsDelivered = false;

            if (_body != null)
            {
                _body.bodyType = _bodyTypeDefault;
                _body.linearVelocity = Vector2.zero;
            }
            SetCollidersPassable(false);

            transform.position = _initialPosition;
            visual.ResetVisual();
        }

        private void OnDestroy()
        {
            if (_carrierEntity != null)
                _carrierEntity.Damaged -= OnCarrierDamaged;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (destination == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(destination.position, destinationTolerance);
            Gizmos.DrawLine(transform.position, destination.position);
        }
#endif
    }
}
