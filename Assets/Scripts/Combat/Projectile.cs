// 근거: 전투 시스템.md — 직업마다 공격 방식이 다르다(궁수=활, 폭탄마=폭탄). 적도 저격수 역할군을 가진다.
//       피해 계산·아군 판정·상태이상 부여는 전부 DamageSystem 단일 경로를 탄다.
// 근거: 적 시스템.md — 특수 적 역할군에 저격수(Sniper)가 있다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;
using TSWP.StatusEffects;

namespace TSWP.Combat
{
    /// <summary>
    /// 직선 비행 투사체. 대상에 닿으면 DamageSystem으로 피해를 전달하고 소멸한다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour
    {
        [Header("비행")]
        [SerializeField, Min(0.1f)] private float speed = 8f;      // TODO(밸런스): 문서 미정
        [SerializeField, Min(0.1f)] private float lifetime = 4f;   // 화면 밖으로 나간 투사체 정리

        [Tooltip("중력 영향 — 켜면 포물선을 그린다(폭탄 등).")]
        [SerializeField] private bool useGravity;

        [Header("충돌")]
        [Tooltip("지형에 닿으면 소멸. 지형 레이어를 지정한다.")]
        [SerializeField] private LayerMask obstacleMask;

        [Header("연출")]
        [Tooltip("착탄 시 재생할 이펙트 id (Art.VfxId).")]
        [SerializeField] private string impactVfxId = Art.VfxId.ProjectileImpact;

        [Tooltip("진행 방향으로 스프라이트를 회전시킨다.")]
        [SerializeField] private bool rotateToDirection = true;

        [Tooltip("비행 중 남기는 꼬리 이펙트 id. 비우면 없음.")]
        [SerializeField] private string trailVfxId = Art.VfxId.ProjectileFly;

        [Tooltip("꼬리 이펙트 생성 간격(초). 짧을수록 촘촘하다.")]
        [SerializeField, Min(0.01f)] private float trailInterval = 0.05f;

        [Tooltip("탄환 자체가 회전하는 속도(도/초). 0이면 회전 없음.")]
        [SerializeField] private float spinSpeed = 360f;

        private Vector2 _direction = Vector2.right;
        private float _damage;
        private CombatEntity _source;
        private TeamType _sourceTeam;
        private List<StatusEffectData> _statusEffects;
        private KnockbackInfo? _knockback;
        private bool _isExplosive;
        private float _lifeTimer;
        private float _trailTimer;
        private Rigidbody2D _body;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();

            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        /// <summary>
        /// 투사체 발사. 피해 정보는 발사 시점에 고정된다.
        /// </summary>
        public void Launch(CombatEntity source, Vector2 direction, float damage,
                           List<StatusEffectData> statusEffects = null,
                           KnockbackInfo? knockback = null, bool isExplosive = false)
        {
            _source = source;
            _sourceTeam = source != null ? source.Team : TeamType.Neutral;
            _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            _damage = damage;
            _statusEffects = statusEffects;
            _knockback = knockback;
            _isExplosive = isExplosive;
            _lifeTimer = lifetime;

            if (_body != null)
            {
                _body.gravityScale = useGravity ? 1f : 0f;
                _body.linearVelocity = _direction * speed;
            }

            if (rotateToDirection)
            {
                float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            _lifeTimer -= dt;
            if (_lifeTimer <= 0f)
            {
                Despawn(playImpact: false);
                return;
            }

            // Rigidbody2D가 없으면 직접 이동한다.
            if (_body == null)
                transform.position += (Vector3)(_direction * (speed * dt));

            // 탄환 회전 — 날아가는 게 보이면 위협적으로 느껴진다.
            // rotateToDirection과 겹치지 않도록 방향 고정이 아닐 때만 돌린다.
            if (!rotateToDirection && !Mathf.Approximately(spinSpeed, 0f))
                transform.Rotate(0f, 0f, spinSpeed * dt);

            // 꼬리 이펙트 — 궤적이 보여야 피할 수 있다 ("피격은 공정해야 한다").
            if (string.IsNullOrEmpty(trailVfxId)) return;

            _trailTimer -= dt;
            if (_trailTimer > 0f) return;

            _trailTimer = trailInterval;
            Art.VfxSpawner.Instance?.Play(trailVfxId, transform.position);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 지형에 맞으면 그대로 소멸
            if (((1 << other.gameObject.layer) & obstacleMask) != 0)
            {
                Despawn(playImpact: true);
                return;
            }

            var target = other.GetComponent<CombatEntity>();
            if (target == null || target.IsDead) return;

            // 발사자 자신은 통과 (총구에서 자폭하지 않도록)
            if (target == _source) return;

            // 같은 진영은 통과시킨다.
            // NOTE(기획 확인 필요): 전투 시스템.md는 아군 공격을 허용하지만(50%),
            //   투사체가 아군을 관통할지 맞을지는 문서에 없다. 우선 통과로 두고
            //   폭탄마처럼 아군을 날리는 재미가 필요한 스킬은 별도 설정으로 연다.
            if (target.Team == _sourceTeam) return;

            var info = new DamageInfo
            {
                BaseDamage = _damage,
                IsExplosive = _isExplosive,
                Source = _source,
                StatusEffects = _statusEffects,
                Knockback = _knockback,
            };

            // 넉백 방향은 비행 방향으로 갱신
            if (info.Knockback.HasValue)
            {
                var kb = info.Knockback.Value;
                kb.Direction = _direction;
                info.Knockback = kb;
            }

            DamageSystem.Apply(target, in info);
            Despawn(playImpact: true);
        }

        private void Despawn(bool playImpact)
        {
            if (playImpact && !string.IsNullOrEmpty(impactVfxId))
                Art.VfxSpawner.Instance?.Play(impactVfxId, transform.position);

            Destroy(gameObject);
        }

        /// <summary>발사 전 속도를 조정한다 (적 데이터에서 주입).</summary>
        public void SetSpeed(float value) => speed = Mathf.Max(0.1f, value);

        /// <summary>지형 레이어 주입.</summary>
        public void SetObstacleMask(LayerMask mask) => obstacleMask = mask;
    }
}
