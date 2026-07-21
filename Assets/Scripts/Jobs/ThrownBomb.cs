// 근거: 직업 시스템.md — 폭탄마의 폭탄 투척(포물선 → 착탄/도화선 종료 시 폭발).
// 근거: 전투 시스템.md — 폭발 판정(IsExplosive)만 구조물을 파괴할 수 있다. 아군 피해는 스킬별 오버라이드 가능.
// 물리 컴포넌트를 붙이지 않고 직접 포물선을 적분한다:
//   Rigidbody2D+Collider2D를 붙이면 던진 본인과 충돌해 발밑에서 튕기는 문제가 생기고,
//   프로토타입 단계에서는 지형 레이어 설정도 씬마다 다르기 때문이다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;

namespace TSWP.Jobs
{
    /// <summary>
    /// 투척된 폭탄 1개. BomberBombSkill이 런타임에 생성하며, 도화선이 끝나거나 지형에 닿으면 폭발한다.
    /// 프리팹이 없어도 보이도록 흰 사각형 스프라이트(SkillTargeting.SquareSprite)를 스스로 만든다.
    /// </summary>
    public class ThrownBomb : MonoBehaviour
    {
        private Vector2 _velocity;
        private float _gravity;
        private float _fuseRemaining;
        private bool _exploded;

        // 폭발 제원 — 스킬 정의가 Launch로 주입한다.
        private CombatEntity _owner;
        private float _damage;
        private float _radius;
        private float _knockbackForce;
        private float _knockbackUpward;
        private float _selfDamageRatio;
        private FriendlyFireRule? _friendlyFireOverride;
        private string _explosionVfxId;

        private SpriteRenderer _renderer;
        private Color _baseColor;
        private float _fuseTotal;
        private LayerMask _groundMask;
        private bool _useGroundCheck;

        /// <summary>런타임 생성 진입점 — 오브젝트/표시까지 여기서 만든다.</summary>
        public static ThrownBomb Spawn(Vector2 position, Color color, float size)
        {
            var go = new GameObject("ThrownBomb");
            go.transform.position = position;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SkillTargeting.SquareSprite;
            renderer.color = color;
            renderer.sortingOrder = 50; // 캐릭터보다 앞 — 어디에 떨어지는지 보여야 한다
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, size);

            var bomb = go.AddComponent<ThrownBomb>();
            bomb._renderer = renderer;
            bomb._baseColor = color;
            return bomb;
        }

        /// <summary>투척 제원 주입. 호출 후 폭탄이 스스로 날아가고 터진다.</summary>
        public void Launch(CombatEntity owner, Vector2 velocity, float gravity, float fuseSeconds,
                           float damage, float radius, float knockbackForce, float knockbackUpward,
                           float selfDamageRatio, FriendlyFireRule? friendlyFireOverride,
                           string explosionVfxId, LayerMask groundMask, bool useGroundCheck)
        {
            _owner = owner;
            _velocity = velocity;
            _gravity = gravity;
            _fuseTotal = Mathf.Max(0.05f, fuseSeconds);
            _fuseRemaining = _fuseTotal;
            _damage = damage;
            _radius = radius;
            _knockbackForce = knockbackForce;
            _knockbackUpward = knockbackUpward;
            _selfDamageRatio = selfDamageRatio;
            _friendlyFireOverride = friendlyFireOverride;
            _explosionVfxId = explosionVfxId;
            _groundMask = groundMask;
            _useGroundCheck = useGroundCheck;
        }

        private void Update()
        {
            if (_exploded) return;

            float dt = Time.deltaTime;

            // 포물선 적분 — 물리 엔진 없이 직접 계산한다.
            _velocity.y -= _gravity * dt;
            transform.position += (Vector3)(_velocity * dt);
            transform.Rotate(0f, 0f, 360f * dt); // 굴러가는 느낌 — 날아가는 게 보인다

            // 도화선이 짧아질수록 빨리 깜빡인다 — 언제 터지는지 예고해야 피할 수 있다.
            if (_renderer != null)
            {
                float progress = 1f - Mathf.Clamp01(_fuseRemaining / _fuseTotal);
                float blinkSpeed = Mathf.Lerp(4f, 24f, progress);
                bool bright = Mathf.Repeat(Time.time * blinkSpeed, 1f) < 0.5f;
                _renderer.color = bright ? Color.white : _baseColor;
            }

            // 지형에 닿으면 즉시 폭발 (레이어가 지정된 경우에만 — 미설정 씬에서는 도화선으로만 터진다).
            if (_useGroundCheck && Physics2D.OverlapCircle(transform.position, 0.15f, _groundMask) != null)
            {
                Explode();
                return;
            }

            _fuseRemaining -= dt;
            if (_fuseRemaining <= 0f) Explode();
        }

        /// <summary>폭발 — 범위 내 전원(적·아군·구조물)에게 폭발 피해. 던진 본인도 예외가 아니다.</summary>
        private void Explode()
        {
            if (_exploded) return;
            _exploded = true;

            Vector2 center = transform.position;

            if (!string.IsNullOrEmpty(_explosionVfxId))
                Art.VfxSpawner.Instance?.Play(_explosionVfxId, center);

            // 화면 흔들림 — 폭발이 크다는 게 몸으로 느껴져야 한다. 없으면 조용히 생략.
            Art.CameraShake.Instance?.Shake(0.25f, 0.35f);

            // 폭탄마다 자기 목록을 쓴다 — 피해 처리 중 다른 폭탄이 연쇄로 터져도 순회가 깨지지 않는다.
            var targets = new List<CombatEntity>(16);
            SkillTargeting.OverlapEntities(center, _radius, _owner, targets);
            for (int i = 0; i < targets.Count; i++)
            {
                CombatEntity target = targets[i];
                if (target == null) continue;

                var info = new DamageInfo
                {
                    SkillBonus = _damage,
                    IsExplosive = true,                       // 구조물 파괴 가능 — 폭탄마의 정체성
                    Source = _owner,
                    FriendlyFireOverride = _friendlyFireOverride, // 아군에게는 별도 규칙(현재 체력 비율 등)
                    Knockback = SkillTargeting.RadialKnockback(center, target.transform.position,
                                                               _knockbackForce, _knockbackUpward),
                };
                DamageSystem.Apply(target, in info);
            }

            ApplySelfBlast(center);
            Destroy(gameObject);
        }

        /// <summary>
        /// 자폭 판정 — 폭탄마의 가장 큰 위험 요소.
        /// 시전자에게는 아군 감쇠가 걸리지 않으므로(Source == target), 별도 비율로 낮춰 즉사만 피한다.
        /// </summary>
        private void ApplySelfBlast(Vector2 center)
        {
            if (_owner == null || _owner.IsDead || _selfDamageRatio <= 0f) return;

            float distSqr = ((Vector2)_owner.transform.position - center).sqrMagnitude;
            if (distSqr > _radius * _radius) return; // 제때 도망쳤다

            var info = new DamageInfo
            {
                SkillBonus = _damage * _selfDamageRatio,
                IsExplosive = true,
                Source = _owner, // 통계상 자기 피해로 귀속
                Knockback = SkillTargeting.RadialKnockback(center, _owner.transform.position,
                                                           _knockbackForce, _knockbackUpward),
            };
            DamageSystem.Apply(_owner, in info);
        }
    }
}
