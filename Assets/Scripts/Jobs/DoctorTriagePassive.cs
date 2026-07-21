// 근거: 직업 시스템.md — 패시브는 플레이 스타일을 바꾸는 효과를 우선한다. 의사는 팀 기여(회복) 직업이다.
// 근거: 게임 성경.md — "모든 강력한 능력에는 반드시 위험이 따른다."
//   → 응급 분류는 위급한 아군이 있을 때만 빨라지고, 그 대가로 자신의 최대 체력이 줄어든다(무리한다).
// 근거: ARCHITECTURE.md §3-6 — 아군 판정은 TeamType 비교.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;
using TSWP.Combat;

namespace TSWP.Jobs
{
    /// <summary>
    /// 의사 패시브 — 응급 분류.
    /// 주변에 체력이 위급한 아군이 있으면 이동 속도가 크게 오르지만, 자신의 최대 체력이 깎인다.
    /// 팀이 위험할수록 의사가 빨라지고, 동시에 의사 자신이 물러진다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Jobs/Passives/Doctor Triage", fileName = "Passive_DoctorTriage")]
    public class DoctorTriagePassive : PassiveDefinition
    {
        [Header("감지")]
        [Tooltip("위급 아군 탐색 반경.")]
        [SerializeField, Min(0.5f)] private float scanRadius = 8f;          // TODO(밸런스): 문서 미정

        [Tooltip("이 체력 비율 이하인 아군을 '위급'으로 본다.")]
        [SerializeField, Range(0.05f, 1f)] private float criticalRatio = 0.4f; // TODO(밸런스): 문서 미정

        [Tooltip("탐색 간격(초). 매 프레임 물리 질의를 돌리면 8인 전투에서 낭비가 크다.")]
        [SerializeField, Min(0.1f)] private float scanInterval = 0.3f;

        [Header("효과 / 대가")]
        [Tooltip("이동 속도 승산 배율 (0.5 = +50%).")]
        [SerializeField] private float moveSpeedModifier = 0.5f;            // TODO(밸런스): 문서 미정

        [Tooltip("최대 체력 승산 배율 — 음수(위험 요소). -0.25 = 최대 체력 25% 감소.")]
        [SerializeField] private float maxHealthModifier = -0.25f;          // TODO(밸런스): 문서 미정

        public override IPassiveBehaviour CreateBehaviour() =>
            new Behaviour(scanRadius, criticalRatio, scanInterval, moveSpeedModifier, maxHealthModifier);

        private sealed class Behaviour : IPassiveBehaviour
        {
            private readonly float _scanRadius;
            private readonly float _criticalRatio;
            private readonly float _scanInterval;
            private readonly float _moveSpeedModifier;
            private readonly float _maxHealthModifier;

            private CombatEntity _entity;
            private StatCollection _stats;
            private Transform _ownerTransform;
            private float _timer;
            private bool _active;

            // 행동 인스턴스마다 자기 버퍼 — 플레이어가 여러 명이어도 서로 간섭하지 않는다.
            private readonly List<CombatEntity> _nearby = new List<CombatEntity>(16);

            public Behaviour(float scanRadius, float criticalRatio, float scanInterval,
                             float moveSpeedModifier, float maxHealthModifier)
            {
                _scanRadius = scanRadius;
                _criticalRatio = criticalRatio;
                _scanInterval = scanInterval;
                _moveSpeedModifier = moveSpeedModifier;
                _maxHealthModifier = maxHealthModifier;
            }

            public void OnAttach(GameObject owner)
            {
                if (owner == null) return;
                _ownerTransform = owner.transform;
                _entity = owner.GetComponent<CombatEntity>();

                var playerStats = owner.GetComponent<Player.PlayerStats>();
                _stats = playerStats != null ? playerStats.Stats : null;
                _timer = 0f;
            }

            public void OnDetach(GameObject owner)
            {
                _stats?.RemoveModifiersFromSource(this);
                _stats = null;
                _entity = null;
                _ownerTransform = null;
                _active = false;
            }

            public void Tick(float deltaTime)
            {
                if (_stats == null || _ownerTransform == null) return;

                _timer -= deltaTime;
                if (_timer > 0f) return;
                _timer = _scanInterval;

                bool shouldBeActive = HasCriticalAlly();
                if (shouldBeActive == _active) return; // 상태가 그대로면 스탯을 건드리지 않는다
                _active = shouldBeActive;

                _stats.RemoveModifiersFromSource(this);
                if (!_active) return;

                _stats.AddModifier(new StatModifier(
                    StatType.MoveSpeed, StatModifierMode.Multiplicative, _moveSpeedModifier, this));

                // 대가 — 최대 체력 감소. PlayerStats가 CombatEntity.SetMaxHp(keepRatio)로 전파해
                // 체력바가 실제로 줄어드는 게 보인다.
                _stats.AddModifier(new StatModifier(
                    StatType.MaxHealth, StatModifierMode.Multiplicative, _maxHealthModifier, this));
            }

            private bool HasCriticalAlly()
            {
                SkillTargeting.OverlapEntities(_ownerTransform.position, _scanRadius, _entity, _nearby);
                for (int i = 0; i < _nearby.Count; i++)
                {
                    CombatEntity other = _nearby[i];
                    if (other == null || other.MaxHp <= 0f) continue;
                    if (!SkillTargeting.IsAlly(_entity, other)) continue;

                    if (other.CurrentHp / other.MaxHp <= _criticalRatio) return true;
                }
                return false;
            }
        }
    }
}
