// 근거: 퍼즐 시스템.md — 폭탄 퍼즐: 폭탄으로 벽이나 구조물을 파괴한다. 폭탄 전달(릴레이) 퍼즐 존재.
//       트롤: 폭탄을 잘못 던지면 다리가 무너진다 — 스트리머 포인트("야 너 뭐했냐ㅋㅋㅋ").
// 근거: 전투 시스템.md — 구조물은 폭발 공격으로만 파괴된다 (DamageInfo.IsExplosive).
using UnityEngine;
using TSWP.Combat;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 퍼즐용 폭탄. 들고 던질 수 있으며 일정 시간 후 폭발해 구조물을 파괴한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class BombObject : PuzzleElement, IInteractable
    {
        [Header("폭탄")]
        [Tooltip("점화 후 폭발까지의 시간(초). 릴레이 퍼즐의 긴장 요소.")]
        [SerializeField, Min(0.1f)] private float fuseSeconds = 3f;   // TODO(밸런스): 문서 미정

        [SerializeField, Min(0.1f)] private float explosionRadius = 2f; // TODO(밸런스): 문서 미정
        [SerializeField, Min(0f)] private float explosionDamage = 30f;  // TODO(밸런스): 문서 미정

        [Header("투척")]
        [SerializeField] private Vector2 throwForce = new Vector2(6f, 4f); // TODO(밸런스): 문서 미정
        [SerializeField] private Vector2 carryOffset = new Vector2(0f, 1.2f);

        [Header("판정")]
        [Tooltip("이 레이어의 오브젝트가 폭발에 닿으면 오조작(다리 붕괴 등).")]
        [SerializeField] private LayerMask fragileMask;

        private Rigidbody2D _body;
        private PlayerController _carrier;
        private Vector2 _initialPosition;
        private float _fuseTimer;
        private bool _lit;

        public bool IsCarried => _carrier != null;
        public bool IsLit => _lit;

        public string PromptDescription => IsCarried ? "폭탄 던지기" : "폭탄 들기";

        protected override void Awake()
        {
            base.Awake();
            _body = GetComponent<Rigidbody2D>();
            _initialPosition = transform.position;
        }

        public bool CanInteract(PlayerController user)
        {
            if (owner != null && owner.State != PuzzleState.Active) return false;
            return _carrier == null || _carrier == user;
        }

        public void Interact(PlayerController user)
        {
            if (!CanInteract(user)) return;

            owner?.AddParticipant(user != null ? user.PlayerId : -1);

            if (IsCarried) Throw(user);
            else PickUp(user);
        }

        private void PickUp(PlayerController user)
        {
            _carrier = user;
            _body.simulated = false;
            Light();
            NotifyStateChanged();
        }

        private void Throw(PlayerController user)
        {
            _carrier = null;
            _body.simulated = true;

            int sign = user != null ? user.FacingSign : 1;
            _body.linearVelocity = new Vector2(throwForce.x * sign, throwForce.y);

            NotifyStateChanged();
        }

        /// <summary>도화선 점화 — 들거나 외부 트리거로 시작한다.</summary>
        public void Light()
        {
            if (_lit) return;
            _lit = true;
            _fuseTimer = fuseSeconds;
        }

        private void Update()
        {
            if (_carrier != null)
                transform.position = (Vector2)_carrier.transform.position + carryOffset;

            if (!_lit) return;

            _fuseTimer -= Time.deltaTime;
            if (_fuseTimer <= 0f)
                Explode();
        }

        private void Explode()
        {
            _lit = false;

            Vector2 center = transform.position;

            // 폭발 판정 — 구조물은 폭발 공격으로만 파괴된다.
            var hits = Physics2D.OverlapCircleAll(center, explosionRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                var target = hits[i].GetComponent<CombatEntity>();
                if (target == null) continue;

                var info = new DamageInfo
                {
                    BaseDamage = explosionDamage,
                    IsExplosive = true,
                    Source = null, // 퍼즐 폭탄은 환경 취급 — 아군 감쇠 없음
                };
                DamageSystem.Apply(target, in info);
            }

            // 부서지면 안 되는 것(다리 등)이 범위에 있었다면 오조작 — 다리 붕괴
            var fragile = Physics2D.OverlapCircle(center, explosionRadius, fragileMask);
            if (fragile != null)
                NotifyWrongAction();

            NotifyStateChanged();
            gameObject.SetActive(false);
        }

        public void ResetElement()
        {
            _carrier = null;
            _lit = false;
            _fuseTimer = 0f;
            transform.position = _initialPosition;
            if (_body != null)
            {
                _body.simulated = true;
                _body.linearVelocity = Vector2.zero;
            }
            gameObject.SetActive(true);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
