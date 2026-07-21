// 근거: 퍼즐 시스템.md — 폭탄 퍼즐: 폭탄으로 벽이나 구조물을 파괴한다. 폭탄 전달(릴레이) 퍼즐 존재.
//       트롤: 폭탄을 잘못 던지면 다리가 무너진다 — 스트리머 포인트("야 너 뭐했냐ㅋㅋㅋ").
// 근거: 전투 시스템.md — 구조물은 폭발 공격으로만 파괴된다 (DamageInfo.IsExplosive).
// 프로토타입: 점화되면 색이 깜빡이고, 폭발하면 눈에 보이며, 일정 시간 뒤 되살아나 반복 검증이 가능해야 한다.
//   들었을 때 콜라이더를 끄지 않는다(끄면 E키 탐색에서 사라져 던질 수 없다) — 트리거로만 바꾼다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 퍼즐용 폭탄. 들고 던질 수 있으며 일정 시간 후 폭발해 구조물을 파괴한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class BombObject : PuzzleElement, IInteractable
    {
        [Header("폭탄")]
        [Tooltip("점화 후 폭발까지의 시간(초). 릴레이 퍼즐의 긴장 요소.")]
        [SerializeField, Min(0.1f)] private float fuseSeconds = 3f;   // TODO(밸런스): 문서 미정

        [SerializeField, Min(0.1f)] private float explosionRadius = 2f; // TODO(밸런스): 문서 미정
        [SerializeField, Min(0f)] private float explosionDamage = 30f;  // TODO(밸런스): 문서 미정

        [Tooltip("집어 드는 순간 점화할지. 끄면 Light()를 외부에서 호출할 때만 도화선이 붙는다.")]
        [SerializeField] private bool lightOnPickup = true;

        [Header("투척")]
        [SerializeField] private Vector2 throwForce = new Vector2(6f, 4f); // TODO(밸런스): 문서 미정
        [SerializeField] private Vector2 carryOffset = new Vector2(0f, 1.2f);

        [Header("판정")]
        [Tooltip("이 레이어의 오브젝트가 폭발에 닿으면 오조작(다리 붕괴 등).")]
        [SerializeField] private LayerMask fragileMask;

        [Header("프로토타입 편의")]
        [Tooltip("폭발 후 되살아나기까지의 시간(초). 0 이하면 폭발과 함께 비활성화된다(원래 동작).")]
        [SerializeField, Min(0f)] private float respawnSeconds = 4f;

        [Header("연출")]
        [Tooltip("점화 중 깜빡이는 색.")]
        [SerializeField] private Color fuseColor = new Color(1f, 0.35f, 0.2f, 1f);

        [Tooltip("깜빡임 기본 주기(초). 도화선이 짧아질수록 빨라진다.")]
        [SerializeField, Min(0.05f)] private float blinkInterval = 0.4f;

        private Rigidbody2D _body;
        private PlayerController _carrier;
        private RigidbodyType2D _bodyTypeDefault;
        private Vector2 _initialPosition;
        private float _fuseTimer;
        private bool _lit;
        private float _blinkTimer;
        private bool _blinkOn;
        private float _respawnTimer;
        private bool _exploded;

        // 폭발 판정 버퍼 — Unity 6에서 제거/비권장인 NonAlloc 대신 ContactFilter2D 오버로드를 쓴다.
        private static readonly List<Collider2D> ExplosionHits = new List<Collider2D>(32);
        private ContactFilter2D _explosionFilter;

        public bool IsCarried => _carrier != null;
        public bool IsLit => _lit;

        public string PromptDescription => IsCarried ? "폭탄 던지기" : "폭탄 들기";

        public override string DebugStatus =>
            _exploded ? (respawnSeconds > 0f ? $"폭발함 (재생성까지 {_respawnTimer:0.0}s)" : "폭발함")
            : _lit ? $"점화! 폭발까지 {_fuseTimer:0.0}s{(IsCarried ? " (운반 중)" : "")}"
            : (IsCarried ? "운반 중" : "대기");

        protected override void Awake()
        {
            base.Awake();

            _body = GetComponent<Rigidbody2D>();
            if (_body != null) _bodyTypeDefault = _body.bodyType;

            _initialPosition = transform.position;
            _explosionFilter = new ContactFilter2D { useTriggers = true };
        }

        public bool CanInteract(PlayerController user)
        {
            if (_exploded) return false;
            if (owner != null && owner.State != PuzzleState.Active) return false;
            return _carrier == null || _carrier == user;
        }

        public void Interact(PlayerController user)
        {
            if (!CanInteract(user)) return;

            RegisterParticipant(user);

            if (IsCarried) Throw(user);
            else PickUp(user);
        }

        private void PickUp(PlayerController user)
        {
            _carrier = user;

            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
                _body.bodyType = RigidbodyType2D.Kinematic;
            }
            SetCollidersPassable(true);

            Log($"'{(user != null ? user.name : "?")}'이(가) 폭탄을 들었다");

            if (lightOnPickup) Light();

            NotifyStateChanged();
        }

        private void Throw(PlayerController user)
        {
            _carrier = null;

            if (_body != null)
            {
                _body.bodyType = _bodyTypeDefault;
                if (_body.bodyType == RigidbodyType2D.Static) _body.bodyType = RigidbodyType2D.Dynamic;
            }
            SetCollidersPassable(false);

            int sign = user != null ? user.FacingSign : 1;
            if (_body != null)
                _body.linearVelocity = new Vector2(throwForce.x * sign, throwForce.y);

            Log($"폭탄을 {(sign > 0 ? "오른쪽" : "왼쪽")}으로 던졌다");
            NotifyStateChanged();
        }

        /// <summary>도화선 점화 — 들거나 외부 트리거로 시작한다.</summary>
        public void Light()
        {
            if (_lit || _exploded) return;

            _lit = true;
            _fuseTimer = fuseSeconds;
            _blinkTimer = 0f;

            Log($"도화선 점화 — {fuseSeconds:0.#}초 후 폭발");
            NotifyStateChanged();
        }

        protected override void Update()
        {
            base.Update(); // 시각 보간

            if (_exploded)
            {
                if (respawnSeconds <= 0f) return;

                _respawnTimer -= Time.deltaTime;
                if (_respawnTimer <= 0f) Respawn();
                return;
            }

            if (_carrier != null)
                transform.position = (Vector2)_carrier.transform.position + carryOffset;

            if (!_lit) return;

            _fuseTimer -= Time.deltaTime;
            UpdateFuseBlink();

            if (_fuseTimer <= 0f)
                Explode();
        }

        /// <summary>도화선이 짧아질수록 빨리 깜빡인다 — 남은 시간이 눈으로 읽혀야 한다.</summary>
        private void UpdateFuseBlink()
        {
            float ratio = fuseSeconds > 0f ? Mathf.Clamp01(_fuseTimer / fuseSeconds) : 0f;
            float interval = Mathf.Lerp(0.06f, blinkInterval, ratio);

            _blinkTimer -= Time.deltaTime;
            if (_blinkTimer > 0f) return;

            _blinkTimer = interval;
            _blinkOn = !_blinkOn;

            if (_blinkOn) visual.SetOverrideColor(fuseColor);
            else visual.ClearOverrideColor();
        }

        private void Explode()
        {
            _lit = false;
            _exploded = true;
            _carrier = null;
            SetCollidersPassable(false); // 들린 채 터졌을 수 있으므로 트리거 설정을 원복한다

            Vector2 center = transform.position;

            // 폭발 판정 — 구조물은 폭발 공격으로만 파괴된다.
            _explosionFilter.useTriggers = true;
            _explosionFilter.useLayerMask = false;

            ExplosionHits.Clear();
            Physics2D.OverlapCircle(center, explosionRadius, _explosionFilter, ExplosionHits);

            int damaged = 0;
            for (int i = 0; i < ExplosionHits.Count; i++)
            {
                var target = ExplosionHits[i] != null ? ExplosionHits[i].GetComponentInParent<CombatEntity>() : null;
                if (target == null) continue;

                var info = new DamageInfo
                {
                    BaseDamage = explosionDamage,
                    IsExplosive = true,
                    Source = null, // 퍼즐 폭탄은 환경 취급 — 아군 감쇠 없음
                };
                DamageSystem.Apply(target, in info);
                damaged++;
            }

            Log($"폭발! 반경 {explosionRadius:0.#} — {damaged}개 대상 피해");

            // 폭발 연출(프로토타입): 흰 섬광 후 숨김
            visual.ClearOverrideColor();
            visual.Flash(Color.white);

            // 부서지면 안 되는 것(다리 등)이 범위에 있었다면 오조작 — 다리 붕괴
            var fragileFilter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
            fragileFilter.SetLayerMask(fragileMask);

            ExplosionHits.Clear();
            Physics2D.OverlapCircle(center, explosionRadius, fragileFilter, ExplosionHits);
            if (ExplosionHits.Count > 0)
                NotifyWrongAction();

            NotifyStateChanged();

            if (respawnSeconds > 0f)
            {
                // 오브젝트를 살려 둔 채 숨긴다 — 프로토타입에서 반복 검증할 수 있게.
                _respawnTimer = respawnSeconds;
                visual.SetVisible(false);
                SetPhysicsEnabled(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void Respawn()
        {
            _exploded = false;
            _fuseTimer = 0f;
            _blinkOn = false;

            transform.position = _initialPosition;
            SetPhysicsEnabled(true);
            visual.SetVisible(true);
            visual.ResetVisual();

            Log("폭탄 재생성");
            NotifyStateChanged();
        }

        private void SetPhysicsEnabled(bool enabled)
        {
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
                _body.simulated = enabled;
                if (enabled) _body.bodyType = _bodyTypeDefault;
            }
        }

        public void ResetElement()
        {
            _carrier = null;
            _lit = false;
            _exploded = false;
            _fuseTimer = 0f;
            _respawnTimer = 0f;
            _blinkOn = false;

            transform.position = _initialPosition;
            SetPhysicsEnabled(true);
            SetCollidersPassable(false);

            visual.SetVisible(true);
            visual.ResetVisual();

            gameObject.SetActive(true);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}
