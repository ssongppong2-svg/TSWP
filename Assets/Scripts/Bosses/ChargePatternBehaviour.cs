// 근거: 보스 시스템.md — 권장 패턴 구성 중 '이동기'. 예고 후 발동('공정하지만 방심하면 치명적').
// 보스 01 해치 퀸의 '돌진(Charge)'에 사용하지만 해치 퀸 전용 클래스가 아니다 —
// 속도/거리/판정 반경만 다른 애셋을 만들면 다른 보스도 그대로 쓴다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.StatusEffects;

namespace TSWP.Bosses
{
    /// <summary>예고 시점의 플레이어 방향으로 직선 돌진하며 접촉한 플레이어를 밀쳐내는 패턴.</summary>
    [CreateAssetMenu(menuName = "TSWP/Bosses/Patterns/Charge", fileName = "PatternBehaviour_Charge_")]
    public sealed class ChargePatternBehaviour : BossPatternBehaviour
    {
        [Header("돌진")]
        [Tooltip("돌진 속도(유닛/초).")]
        [SerializeField, Min(0.1f)] private float chargeSpeed = 12f; // TODO(밸런스): 문서 미정

        [Tooltip("최대 돌진 시간(초). 이 시간이 지나면 멈춘다.")]
        [SerializeField, Min(0.05f)] private float chargeSeconds = 1.2f; // TODO(밸런스): 문서 미정

        [Tooltip("true면 수평(좌우)으로만 돌진한다 — 2D 횡스크롤에서 보스가 공중으로 솟구치는 것을 막는다.")]
        [SerializeField] private bool horizontalOnly = true;

        [Header("접촉 판정")]
        [Tooltip("돌진 중 접촉 판정 반경.")]
        [SerializeField, Min(0.1f)] private float hitRadius = 1.2f; // TODO(밸런스): 문서 미정

        [Tooltip("같은 플레이어를 1회만 때린다(true) / 매 프레임 때린다(false). 기본은 1회.")]
        [SerializeField] private bool hitEachTargetOnce = true;

        [SerializeField, Min(0f)] private float knockbackForce = 8f;   // TODO(밸런스): 문서 미정
        [SerializeField, Min(0f)] private float stunDuration = 0.25f;  // TODO(밸런스): 문서 미정

        [Tooltip("돌진 적중 시 부여할 상태이상. 비워도 된다.")]
        [SerializeField] private List<StatusEffectData> statusEffects = new List<StatusEffectData>();

        [Header("마무리")]
        [Tooltip("돌진이 끝난 뒤 경직 시간(초) — 반격 기회(협동 보상)를 만든다.")]
        [SerializeField, Min(0f)] private float recoverySeconds = 0.6f; // TODO(밸런스): 문서 미정

        [SerializeField] private string chargeVfxId;
        [SerializeField] private string impactVfxId;

        public float ChargeSpeed => chargeSpeed;
        public float ChargeSeconds => chargeSeconds;
        public bool HorizontalOnly => horizontalOnly;
        public float HitRadius => hitRadius;
        public bool HitEachTargetOnce => hitEachTargetOnce;
        public float KnockbackForce => knockbackForce;
        public float StunDuration => stunDuration;
        public List<StatusEffectData> StatusEffects => statusEffects;
        public float RecoverySeconds => recoverySeconds;
        public string ChargeVfxId => chargeVfxId;
        public string ImpactVfxId => impactVfxId;

        public override BossPatternRunner CreateRunner() => new ChargeRunner(this);
    }

    /// <summary>ChargePatternBehaviour의 실행 상태 (방향 고정·이미 맞은 대상·경과 시간).</summary>
    public sealed class ChargeRunner : BossPatternRunner
    {
        private readonly ChargePatternBehaviour _data;
        private readonly List<CombatEntity> _hitBuffer = new List<CombatEntity>(8);
        private readonly HashSet<CombatEntity> _alreadyHit = new HashSet<CombatEntity>();

        private Vector2 _direction = Vector2.right;
        private bool _charging;

        public ChargeRunner(ChargePatternBehaviour data) : base(data)
        {
            _data = data;
        }

        /// <summary>예고 동안 방향을 계속 갱신하다가, 발동 순간의 방향으로 고정한다.
        /// 예고 중에는 조준이 보이고 발동 후에는 바뀌지 않으므로 회피가 가능하다(공정성).</summary>
        protected override void OnTelegraphTick(BossPatternContext ctx, float deltaTime)
        {
            UpdateAimDirection(ctx);
        }

        protected override void OnTelegraphStart(BossPatternContext ctx)
        {
            UpdateAimDirection(ctx);
        }

        protected override void OnActiveStart(BossPatternContext ctx)
        {
            _charging = true;
            _alreadyHit.Clear();
            ctx.PlayVfx(_data.ChargeVfxId, ctx.BossPosition);
        }

        protected override bool OnActiveTick(BossPatternContext ctx, float deltaTime)
        {
            float chargeTime = ctx.Scale(_data.ChargeSeconds);

            if (_charging)
            {
                MoveBoss(ctx, deltaTime);
                ApplyContactDamage(ctx);

                if (StageElapsed >= chargeTime)
                {
                    StopBoss(ctx);
                    _charging = false;
                }
                return false;
            }

            // 돌진 종료 후 경직 — 이 틈이 플레이어의 반격 타이밍이다.
            return StageElapsed >= chargeTime + ctx.Scale(_data.RecoverySeconds);
        }

        protected override void OnCleanup(BossPatternContext ctx)
        {
            // 중단되든 완료되든 반드시 멈춘다 — 이걸 빼면 보스가 영원히 미끄러진다.
            StopBoss(ctx);
        }

        private void UpdateAimDirection(BossPatternContext ctx)
        {
            if (!ctx.TryGetNearestPlayerPosition(out Vector2 target)) return;

            Vector2 delta = target - ctx.BossPosition;
            if (_data.HorizontalOnly) delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f) return; // 방향이 0이면 직전 방향을 유지한다

            _direction = delta.normalized;
        }

        private void MoveBoss(BossPatternContext ctx, float deltaTime)
        {
            // 속도 배율은 '패턴이 빨라진다'는 뜻이므로 이동 속도에도 반영한다.
            float speed = _data.ChargeSpeed * ctx.SpeedMultiplier;
            var body = ctx.BossBody;

            // Unity 6: Rigidbody2D.velocity는 제거됨 — linearVelocity를 쓴다.
            if (body != null && body.bodyType != RigidbodyType2D.Static)
            {
                Vector2 v = _direction * speed;
                // 중력이 걸린 보스는 수직 속도를 물리에 맡긴다(공중 부양 방지).
                if (_data.HorizontalOnly) v.y = body.linearVelocity.y;
                body.linearVelocity = v;
            }
            else if (ctx.BossTransform != null)
            {
                ctx.BossTransform.position += (Vector3)(_direction * speed * deltaTime);
            }
        }

        private void StopBoss(BossPatternContext ctx)
        {
            var body = ctx.BossBody;
            if (body == null || body.bodyType == RigidbodyType2D.Static) return;

            Vector2 v = body.linearVelocity;
            v.x = 0f;
            if (!_data.HorizontalOnly) v.y = 0f;
            body.linearVelocity = v;
        }

        private void ApplyContactDamage(BossPatternContext ctx)
        {
            BossCombatUtil.CollectPlayers(ctx.BossPosition, _data.HitRadius, _hitBuffer);

            for (int i = 0; i < _hitBuffer.Count; i++)
            {
                var target = _hitBuffer[i];
                if (_data.HitEachTargetOnce && !_alreadyHit.Add(target)) continue;

                BossCombatUtil.ApplyHit(
                    ctx.Boss, target, ctx.Damage, ctx.BossPosition,
                    _data.KnockbackForce, _data.StunDuration,
                    _data.StatusEffects.Count > 0 ? _data.StatusEffects : null);

                ctx.PlayVfx(_data.ImpactVfxId, target.transform.position);
            }
        }
    }
}
