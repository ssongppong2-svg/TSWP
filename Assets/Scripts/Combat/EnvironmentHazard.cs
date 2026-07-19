// 근거: 전투 시스템.md — 환경 피해(용암·독·가시·낙석·폭발·얼음)는 플레이어와 적 모두에게 적용 (진영 무관).
//   낙사(FallDeath)는 즉시 사망 처리 → 공유 부활 횟수 1회 소모 (Core.SharedReviveSystem 경로).
// HazardType enum은 DamageTypes.cs 한 곳에만 정의 (ARCHITECTURE.md §5) — 여기서는 참조만.
using System.Collections.Generic;
using UnityEngine;
using TSWP.StatusEffects;

namespace TSWP.Combat
{
    /// <summary>
    /// 환경 피해 오브젝트. 트리거 2D 콜라이더 진입/체류로 피해를 가한다.
    /// 진영 판정을 하지 않는다 — 환경은 플레이어·적 모두에게 피해 (DamageInfo.Source = null).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EnvironmentHazard : MonoBehaviour
    {
        [Header("해저드 종류")]
        [SerializeField] private HazardType hazardType = HazardType.Spike;

        [Header("피해량")]
        [SerializeField] private float damagePerHit = 5f;   // TODO(밸런스): 문서 미정 — 해저드별 피해 수치 미기재
        [Tooltip("체류 중 반복 피해 간격(초). 0 이하면 진입 시 1회만 피해.")]
        [SerializeField] private float tickInterval = 1f;   // TODO(밸런스): 문서 미정
        [SerializeField] private bool applyOnEnter = true;

        [Header("부가 효과")]
        [Tooltip("0이면 넉백 없음. 방향은 해저드 중심 → 대상.")]
        [SerializeField] private float knockbackForce = 0f; // TODO(밸런스): 문서 미정
        [Tooltip("부여 상태이상 (예: 용암→화상, 독→중독, 얼음→빙결/슬로우) — 상태이상.md의 환경 발생원.")]
        [SerializeField] private List<StatusEffectData> statusEffects = new List<StatusEffectData>();

        /// <summary>체류 중인 유닛별 다음 틱 시각.</summary>
        private readonly Dictionary<CombatEntity, float> _nextTickAt = new Dictionary<CombatEntity, float>();

        public HazardType Type => hazardType;

        private void OnDisable() => _nextTickAt.Clear();

        private void OnTriggerEnter2D(Collider2D other)
        {
            var entity = other.GetComponentInParent<CombatEntity>();
            if (entity == null || entity.IsDead) return;

            if (applyOnEnter)
                ApplyTo(entity);

            if (tickInterval > 0f)
                _nextTickAt[entity] = Time.time + tickInterval;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (tickInterval <= 0f) return;
            var entity = other.GetComponentInParent<CombatEntity>();
            if (entity == null || entity.IsDead) return;
            if (!_nextTickAt.TryGetValue(entity, out float next) || Time.time < next) return;

            ApplyTo(entity);
            _nextTickAt[entity] = Time.time + tickInterval;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var entity = other.GetComponentInParent<CombatEntity>();
            if (entity != null)
                _nextTickAt.Remove(entity);
        }

        /// <summary>피해 적용 — 진영 무관, 아군 감쇠 없음 (Source = null이므로 파이프라인이 자동으로 100% 적용).</summary>
        private void ApplyTo(CombatEntity entity)
        {
            // 낙사: 즉시 사망 → CombatEntity.Kill → Die → Core.SharedReviveSystem.TryConsume(부활 1회 소모) 경로.
            if (hazardType == HazardType.FallDeath)
            {
                entity.Kill(null);
                return;
            }

            var info = new DamageInfo
            {
                BaseDamage = damagePerHit,
                // 폭발형 해저드는 폭발 판정 — 구조물 파괴 가능 (전투 시스템.md '구조물 피해').
                IsExplosive = hazardType == HazardType.Explosion,
                Source = null, // 환경 피해 — 공격자 없음, 아군 판정·플레이어 통계 비대상
                Knockback = knockbackForce > 0f
                    ? new KnockbackInfo
                    {
                        Direction = ((Vector2)(entity.transform.position - transform.position)).normalized,
                        Force = knockbackForce,
                    }
                    : (KnockbackInfo?)null,
                StatusEffects = statusEffects.Count > 0 ? statusEffects : null,
            };
            DamageSystem.Apply(entity, in info);
        }
    }
}
