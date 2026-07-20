// 근거: 보스 시스템.md — 권장 패턴 구성 5종 중 '일반 공격'과 '범위 공격'.
//       예고 후 발동(공정성) / 난이도는 속도만 바꾸고 판정 자체는 그대로.
// 보스별 전용 클래스가 아니다 — 반경·오프셋·피해만 다른 애셋을 여러 개 만들어
// '일반 공격'과 '범위 공격' 둘 다로 쓴다 (보스 15기 공용).
using System.Collections.Generic;
using UnityEngine;
using TSWP.StatusEffects;

namespace TSWP.Bosses
{
    /// <summary>보스를 중심(또는 가장 가까운 플레이어 위치)으로 원형 범위를 한 번 타격하는 패턴.</summary>
    [CreateAssetMenu(menuName = "TSWP/Bosses/Patterns/Area Attack", fileName = "PatternBehaviour_AreaAttack_")]
    public sealed class AreaAttackPatternBehaviour : BossPatternBehaviour
    {
        [Header("판정 범위")]
        [Tooltip("타격 반경(월드 유닛).")]
        [SerializeField, Min(0.1f)] private float radius = 3f; // TODO(밸런스): 문서 미정

        [Tooltip("true면 예고 시점의 '가장 가까운 플레이어' 위치에 판정을 깔고, false면 보스 자신을 중심으로 한다.")]
        [SerializeField] private bool centerOnNearestPlayer;

        [Tooltip("보스 기준 판정 중심 오프셋 (centerOnNearestPlayer가 false일 때).")]
        [SerializeField] private Vector2 centerOffset = Vector2.zero;

        [Header("피해/추가 효과")]
        [Tooltip("넉백 세기. 0이면 넉백 없음.")]
        [SerializeField, Min(0f)] private float knockbackForce = 4f; // TODO(밸런스): 문서 미정

        [Tooltip("넉백에 동반되는 경직 시간(초).")]
        [SerializeField, Min(0f)] private float stunDuration = 0.15f; // TODO(밸런스): 문서 미정

        [Tooltip("폭발 판정 여부 — 구조물은 폭발 공격만 파괴할 수 있다.")]
        [SerializeField] private bool isExplosive;

        [Tooltip("타격 시 부여할 상태이상. 비워도 된다.")]
        [SerializeField] private List<StatusEffectData> statusEffects = new List<StatusEffectData>();

        [Header("연출")]
        [SerializeField] private string impactVfxId;

        [Tooltip("판정 후 경직(다음 행동까지의 여운) 시간(초).")]
        [SerializeField, Min(0f)] private float recoverySeconds = 0.3f; // TODO(밸런스): 문서 미정

        public float Radius => radius;
        public bool CenterOnNearestPlayer => centerOnNearestPlayer;
        public Vector2 CenterOffset => centerOffset;
        public float KnockbackForce => knockbackForce;
        public float StunDuration => stunDuration;
        public bool IsExplosive => isExplosive;
        public List<StatusEffectData> StatusEffects => statusEffects;
        public string ImpactVfxId => impactVfxId;
        public float RecoverySeconds => recoverySeconds;

        public override BossPatternRunner CreateRunner() => new AreaAttackRunner(this);
    }

    /// <summary>AreaAttackPatternBehaviour의 실행 상태.</summary>
    public sealed class AreaAttackRunner : BossPatternRunner
    {
        private readonly AreaAttackPatternBehaviour _data;
        private Vector2 _center;
        private bool _hitApplied;

        public AreaAttackRunner(AreaAttackPatternBehaviour data) : base(data)
        {
            _data = data;
        }

        /// <summary>예고 시점에 판정 위치를 확정한다 — 예고를 보고 피할 수 있어야 공정하다.</summary>
        protected override void OnTelegraphStart(BossPatternContext ctx)
        {
            _center = ResolveCenter(ctx);
        }

        protected override void OnActiveStart(BossPatternContext ctx)
        {
            // 예고 시간이 0인 애셋이면 OnTelegraphStart 직후 곧바로 여기로 오므로 중심이 이미 확정돼 있다.
            BossCombatUtil.ApplyAreaHit(
                ctx.Boss, _center, _data.Radius, ctx.Damage,
                _data.KnockbackForce, _data.StunDuration,
                _data.StatusEffects.Count > 0 ? _data.StatusEffects : null,
                _data.IsExplosive);

            ctx.PlayVfx(_data.ImpactVfxId, _center);
            _hitApplied = true;
        }

        protected override bool OnActiveTick(BossPatternContext ctx, float deltaTime)
        {
            // 타격은 발동 순간 1회뿐 — 이후는 여운 시간만 흘려보낸다.
            return _hitApplied && StageElapsed >= ctx.Scale(_data.RecoverySeconds);
        }

        private Vector2 ResolveCenter(BossPatternContext ctx)
        {
            if (_data.CenterOnNearestPlayer && ctx.TryGetNearestPlayerPosition(out Vector2 target))
                return target;
            return ctx.BossPosition + _data.CenterOffset;
        }
    }
}
