// 근거: 직업 시스템.md — 패시브는 "새로운 플레이 방식"을 만드는 것을 우선한다 (단순 능력치 증가 지양).
// 근거: 게임 성경.md — "모든 강력한 능력에는 반드시 위험이 따른다."
//   → 폭풍 질주는 '폭발에 맞아야' 발동한다. 빨라지려면 자기 폭탄에 스스로 휘말려야 한다.
// 근거: 전투 시스템.md — 폭발 판정은 DamageInfo.IsExplosive.
using UnityEngine;
using TSWP.Core;
using TSWP.Combat;

namespace TSWP.Jobs
{
    /// <summary>
    /// 폭탄마 패시브 — 폭풍 질주.
    /// 폭발 피해를 입으면 짧게 이동 속도가 크게 오른다. 자기 폭탄에 맞아도 발동한다(의도된 활용).
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Jobs/Passives/Bomber Blast Runner", fileName = "Passive_BomberBlastRunner")]
    public class BomberBlastRunnerPassive : PassiveDefinition
    {
        [Header("가속")]
        [Tooltip("이동 속도 승산 배율 (0.8 = +80%).")]
        [SerializeField] private float moveSpeedModifier = 0.8f;   // TODO(밸런스): 문서 미정

        [Tooltip("지속 시간(초). 폭발에 다시 맞으면 갱신된다(중첩 아님).")]
        [SerializeField, Min(0.1f)] private float duration = 3f;   // TODO(밸런스): 문서 미정

        [Header("연출")]
        [SerializeField] private string activateVfxId = Art.VfxId.Buff;

        public override IPassiveBehaviour CreateBehaviour() =>
            new Behaviour(moveSpeedModifier, duration, activateVfxId);

        private sealed class Behaviour : IPassiveBehaviour
        {
            private readonly float _modifier;
            private readonly float _duration;
            private readonly string _vfxId;

            private CombatEntity _entity;
            private StatCollection _stats;
            private Transform _ownerTransform;
            private float _remaining;
            private bool _active;

            public Behaviour(float modifier, float duration, string vfxId)
            {
                _modifier = modifier;
                _duration = duration;
                _vfxId = vfxId;
            }

            public void OnAttach(GameObject owner)
            {
                if (owner == null) return;
                _ownerTransform = owner.transform;
                _entity = owner.GetComponent<CombatEntity>();

                var playerStats = owner.GetComponent<Player.PlayerStats>();
                _stats = playerStats != null ? playerStats.Stats : null;

                // 피격 훅 — Update 폴링 대신 C# event 구독 (ARCHITECTURE.md §3-8).
                if (_entity != null) _entity.Damaged += OnDamaged;
            }

            public void OnDetach(GameObject owner)
            {
                if (_entity != null) _entity.Damaged -= OnDamaged;
                _stats?.RemoveModifiersFromSource(this);

                _entity = null;
                _stats = null;
                _ownerTransform = null;
                _active = false;
                _remaining = 0f;
            }

            public void Tick(float deltaTime)
            {
                if (!_active) return;

                _remaining -= deltaTime;
                if (_remaining > 0f) return;

                _active = false;
                _stats?.RemoveModifiersFromSource(this);
            }

            private void OnDamaged(DamageInfo info)
            {
                if (!info.IsExplosive) return; // 폭발에만 반응한다

                _remaining = _duration;
                if (_active) return; // 이미 활성 — 지속시간만 갱신 (중첩 금지)

                _active = true;
                _stats?.AddModifier(new StatModifier(
                    StatType.MoveSpeed, StatModifierMode.Multiplicative, _modifier, this));

                if (!string.IsNullOrEmpty(_vfxId) && _ownerTransform != null)
                    Art.VfxSpawner.Instance?.Play(_vfxId, _ownerTransform.position);
            }
        }
    }
}
