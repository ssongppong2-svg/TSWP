// 근거: 직업 시스템.md — 패시브는 단순 능력치 증가보다 "새로운 플레이 방식"을 우선한다.
// 근거: 게임 성경.md — "모든 강력한 능력에는 반드시 위험이 따른다."
//   → 역경의 투지는 위험 그 자체가 발동 조건이다: 건강할 때는 약하고, 죽기 직전에 가장 강해진다.
// 스탯 변경은 Core.StatCollection modifier 스택으로만 한다 (직접 수치 대입 금지 — Core/Stats.cs 계약).
using UnityEngine;
using TSWP.Core;
using TSWP.Combat;

namespace TSWP.Jobs
{
    /// <summary>
    /// 용사 패시브 — 역경의 투지.
    /// 체력이 높으면 공격력에 페널티, 낮으면 큰 보너스. 안전하게 싸울수록 손해가 되는 구조.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Jobs/Passives/Warrior Rage", fileName = "Passive_WarriorRage")]
    public class WarriorRagePassive : PassiveDefinition
    {
        [Header("구간 — 체력 비율(0~1) 기준")]
        [Tooltip("이 비율보다 체력이 높으면 페널티 구간.")]
        [SerializeField, Range(0f, 1f)] private float healthyThreshold = 0.7f;   // TODO(밸런스): 문서 미정

        [Tooltip("이 비율보다 체력이 낮으면 격노 구간.")]
        [SerializeField, Range(0f, 1f)] private float ragingThreshold = 0.35f;   // TODO(밸런스): 문서 미정

        [Header("공격력 승산 배율 (0.4 = +40%)")]
        [SerializeField] private float healthyAttackModifier = -0.2f;            // TODO(밸런스): 문서 미정
        [SerializeField] private float ragingAttackModifier = 0.6f;              // TODO(밸런스): 문서 미정

        [Tooltip("체력 상태를 다시 계산하는 간격(초). 매 프레임 스탯을 갈아끼우면 Changed 이벤트가 폭주한다.")]
        [SerializeField, Min(0.05f)] private float evaluateInterval = 0.2f;

        public override IPassiveBehaviour CreateBehaviour() =>
            new Behaviour(healthyThreshold, ragingThreshold,
                          healthyAttackModifier, ragingAttackModifier, evaluateInterval);

        /// <summary>체력 구간 — 구간이 바뀔 때만 modifier를 교체한다.</summary>
        private enum RageTier { None, Healthy, Raging }

        private sealed class Behaviour : IPassiveBehaviour
        {
            private readonly float _healthyThreshold;
            private readonly float _ragingThreshold;
            private readonly float _healthyModifier;
            private readonly float _ragingModifier;
            private readonly float _interval;

            private CombatEntity _entity;
            private StatCollection _stats;
            private RageTier _tier = RageTier.None;
            private float _timer;

            public Behaviour(float healthyThreshold, float ragingThreshold,
                             float healthyModifier, float ragingModifier, float interval)
            {
                _healthyThreshold = healthyThreshold;
                _ragingThreshold = ragingThreshold;
                _healthyModifier = healthyModifier;
                _ragingModifier = ragingModifier;
                _interval = interval;
            }

            public void OnAttach(GameObject owner)
            {
                if (owner == null) return;
                _entity = owner.GetComponent<CombatEntity>();

                // PlayerStats가 없으면(더미/적 등) 패시브는 조용히 아무것도 하지 않는다.
                var playerStats = owner.GetComponent<Player.PlayerStats>();
                _stats = playerStats != null ? playerStats.Stats : null;

                _timer = 0f;
                Evaluate(); // 부착 즉시 1회 반영
            }

            public void OnDetach(GameObject owner)
            {
                // 이 패시브가 부여한 modifier만 골라 제거한다 (Source 키 = 이 인스턴스).
                _stats?.RemoveModifiersFromSource(this);
                _stats = null;
                _entity = null;
                _tier = RageTier.None;
            }

            public void Tick(float deltaTime)
            {
                if (_stats == null || _entity == null) return;

                _timer -= deltaTime;
                if (_timer > 0f) return;
                _timer = _interval;

                Evaluate();
            }

            private void Evaluate()
            {
                if (_stats == null || _entity == null || _entity.MaxHp <= 0f) return;

                float ratio = _entity.CurrentHp / _entity.MaxHp;
                RageTier next =
                    ratio <= _ragingThreshold ? RageTier.Raging :
                    ratio >= _healthyThreshold ? RageTier.Healthy :
                    RageTier.None;

                if (next == _tier) return; // 구간이 그대로면 손대지 않는다
                _tier = next;

                _stats.RemoveModifiersFromSource(this);

                float value = next switch
                {
                    RageTier.Raging => _ragingModifier,
                    RageTier.Healthy => _healthyModifier,
                    _ => 0f,
                };
                if (Mathf.Approximately(value, 0f)) return;

                _stats.AddModifier(new StatModifier(
                    StatType.AttackPower, StatModifierMode.Multiplicative, value, this));
            }
        }
    }
}
