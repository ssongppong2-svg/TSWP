// 근거: 보스 시스템.md — 기믹 예시 '약점 노출'. 특정 조건에서만 약점이 드러나고, 그때 큰 피해를 준다.
// 전투 시스템.md — 모든 피해는 DamageSystem 단일 파이프라인을 지난다.
// 약점은 별도의 CombatEntity로 두고, 받은 피해를 배율만 곱해 보스 본체로 '중계'한다.
//   → 플레이어의 기존 공격 코드(CombatEntity를 찾아 때리는 방식)를 하나도 고치지 않고 약점을 만들 수 있다.
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;

namespace TSWP.Bosses
{
    /// <summary>
    /// 보스 약점 부위. 자신이 받은 피해를 배율만큼 키워 보스 본체에 전달하고, 자기 체력은 즉시 되돌린다
    /// (약점 자체는 파괴되지 않는다 — 파괴 대상은 보스 본체다).
    /// 기본은 비활성 상태이며 IGimmick(JumpPlatformGimmick 등)이 노출 시점을 결정한다.
    /// </summary>
    public sealed class BossWeakPoint : CombatEntity
    {
        [Header("약점 중계")]
        [Tooltip("피해를 전달할 보스 본체. 비면 부모에서 자동으로 찾는다.")]
        [SerializeField] private CombatEntity bossEntity;

        [Tooltip("약점 적중 시 보스 본체가 받는 피해 배율. 1보다 크면 약점 보너스.")]
        [SerializeField, Min(0f)] private float relayMultiplier = 2f; // TODO(밸런스): 문서 미정

        [Tooltip("약점이 자기 체력으로 파괴되지 않도록 최대 체력을 크게 잡는다.")]
        [SerializeField, Min(1f)] private float shellHp = 999999f;

        [Tooltip("약점 적중 시 재생할 이펙트 id (Art.VfxId). 비우면 재생하지 않는다.")]
        [SerializeField] private string weakPointVfxId;

        private bool _relaying; // 중계로 인한 재진입 방지

        protected override void Awake()
        {
            base.Awake();

            if (bossEntity == null)
                bossEntity = GetComponentInParent<CombatEntity>();

            // 자기 자신을 부모로 잡는 사고 방지 — 그러면 무한 중계가 된다.
            if (bossEntity == this) bossEntity = null;

            SetTeam(TeamType.Enemies);
            SetMaxHp(shellHp);
            SetAutoReviveOnDeath(false);

            if (bossEntity == null)
                Debug.LogWarning($"[BossWeakPoint] '{name}': 중계할 보스 본체(CombatEntity)를 찾지 못했습니다.", this);
        }

        private void OnEnable() => Damaged += OnWeakPointDamaged;
        private void OnDisable() => Damaged -= OnWeakPointDamaged;

        private void OnWeakPointDamaged(DamageInfo info)
        {
            if (_relaying || bossEntity == null || bossEntity.IsDead) return;

            _relaying = true;
            try
            {
                // 원 피해에 약점 배율만 곱해 본체로 넘긴다. 공격자(Source)는 그대로 유지해야
                // 통계 귀속(누가 얼마나 넣었는가)과 아군 판정이 정상 동작한다.
                var relayed = new DamageInfo
                {
                    BaseDamage = info.TotalDamage * relayMultiplier,
                    Source = info.Source,
                    IsCritical = info.IsCritical,
                    IsExplosive = info.IsExplosive,
                    StatusEffects = info.StatusEffects,
                    FriendlyFireOverride = info.FriendlyFireOverride,
                    // 넉백은 중계하지 않는다 — 보스가 자기 약점을 맞고 날아가면 안 된다.
                };

                DamageSystem.Apply(bossEntity, in relayed);

                if (!string.IsNullOrEmpty(weakPointVfxId))
                    Art.VfxSpawner.Instance?.Play(weakPointVfxId, transform.position);
            }
            finally
            {
                _relaying = false;
            }

            // 약점 껍데기는 체력을 즉시 회복해 파괴되지 않는다.
            Heal(MaxHp);
        }
    }
}
