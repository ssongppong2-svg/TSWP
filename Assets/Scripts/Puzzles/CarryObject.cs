// 근거: 퍼즐 시스템.md — 운반 퍼즐: 오브젝트를 목적지까지 옮긴다. 운반 중에는 공격받을 수 있다(피격 시 낙하).
//       운반자는 무방비가 되므로 팀원의 보호가 필요하다 — 협동 강제 장치.
using UnityEngine;
using TSWP.Combat;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 운반 가능한 오브젝트. 들고 있는 동안 운반자를 따라다니며, 피격 시 떨어진다.
    /// </summary>
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

        private PlayerController _carrier;
        private CombatEntity _carrierEntity;
        private Vector2 _initialPosition;

        public bool IsCarried => _carrier != null;
        public bool IsDelivered { get; private set; }

        public string PromptDescription => IsCarried ? "내려놓기" : "들기";

        protected override void Awake()
        {
            base.Awake();
            _initialPosition = transform.position;
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

            owner?.AddParticipant(user != null ? user.PlayerId : -1);

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

            NotifyStateChanged();
        }

        /// <summary>내려놓기. wasForced가 true면 피격으로 인한 낙하(트롤 결과).</summary>
        public void Drop(bool wasForced)
        {
            if (_carrierEntity != null)
                _carrierEntity.Damaged -= OnCarrierDamaged;

            _carrier = null;
            _carrierEntity = null;

            EvaluateDestination();

            if (wasForced)
                NotifyWrongAction(); // 운반물 낙하 — 진행도 손실

            NotifyStateChanged();
        }

        private void OnCarrierDamaged(DamageInfo info) => Drop(true);

        private void LateUpdate()
        {
            if (_carrier == null) return;
            transform.position = (Vector2)_carrier.transform.position + carryOffset;
        }

        private void EvaluateDestination()
        {
            if (destination == null) { IsDelivered = false; return; }
            IsDelivered = Vector2.Distance(transform.position, destination.position) <= destinationTolerance;
        }

        public void ResetElement()
        {
            if (_carrierEntity != null)
                _carrierEntity.Damaged -= OnCarrierDamaged;

            _carrier = null;
            _carrierEntity = null;
            IsDelivered = false;
            transform.position = _initialPosition;
        }

        private void OnDestroy()
        {
            if (_carrierEntity != null)
                _carrierEntity.Damaged -= OnCarrierDamaged;
        }
    }
}
