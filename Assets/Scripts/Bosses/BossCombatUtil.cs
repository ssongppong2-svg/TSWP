// 근거: 전투 시스템.md — 모든 피해는 DamageSystem.Apply 단일 파이프라인을 통과한다.
//       아군 판정은 레이어가 아닌 TeamType 비교 (ARCHITECTURE.md §3-6).
// 보스 패턴/기믹이 공통으로 쓰는 히트 판정 유틸. 레이어 마스크에 의존하지 않고
// CombatEntity.Team으로 걸러내므로 프리팹 레이어 설정이 어긋나도 판정이 깨지지 않는다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;
using TSWP.StatusEffects;

namespace TSWP.Bosses
{
    /// <summary>보스 패턴 공용 히트 판정/피해 적용 헬퍼.</summary>
    public static class BossCombatUtil
    {
        // 프레임마다 새 List를 만들지 않도록 공용 버퍼를 재사용한다 (8인 전투 GC 압박 방지).
        private static readonly List<Collider2D> ColliderBuffer = new List<Collider2D>(32);

        /// <summary>모든 레이어를 훑고 트리거도 포함한다 — 실제 선별은 TeamType 비교로 한다.</summary>
        private static ContactFilter2D BuildFilter()
        {
            var filter = ContactFilter2D.noFilter;
            filter.useTriggers = true;
            return filter;
        }

        /// <summary>
        /// 원형 범위 안의 '살아있는 플레이어 진영' 유닛을 모은다.
        /// Unity 6: OverlapCircleNonAlloc은 제거됨 — ContactFilter2D + List 오버로드를 쓴다.
        /// </summary>
        public static void CollectPlayers(Vector2 center, float radius, List<CombatEntity> results)
        {
            if (results == null) return;
            results.Clear();
            if (radius <= 0f) return;

            ColliderBuffer.Clear();
            Physics2D.OverlapCircle(center, radius, BuildFilter(), ColliderBuffer);

            for (int i = 0; i < ColliderBuffer.Count; i++)
            {
                var col = ColliderBuffer[i];
                if (col == null) continue;

                // 자식 콜라이더(무기/발판 등)에 붙어 있어도 본체를 찾아낸다.
                var entity = col.GetComponentInParent<CombatEntity>();
                if (entity == null || entity.IsDead) continue;
                if (entity.Team != TeamType.Players) continue;
                if (results.Contains(entity)) continue; // 콜라이더 여러 개짜리 유닛의 중복 타격 방지

                results.Add(entity);
            }
        }

        /// <summary>
        /// 피해 1회 적용. 반드시 DamageSystem 단일 파이프라인을 탄다
        /// (무적/아군감쇠/넉백면역/상태이상 규칙이 전부 그 안에 있다).
        /// </summary>
        public static void ApplyHit(
            CombatEntity source,
            CombatEntity target,
            float damage,
            Vector2 knockbackOrigin,
            float knockbackForce = 0f,
            float stunDuration = 0f,
            List<StatusEffectData> statusEffects = null,
            bool isExplosive = false)
        {
            if (target == null || target.IsDead) return;

            var info = new DamageInfo
            {
                BaseDamage = Mathf.Max(0f, damage),
                Source = source,
                IsExplosive = isExplosive,
                StatusEffects = statusEffects,
            };

            if (knockbackForce > 0f)
            {
                Vector2 dir = (Vector2)target.transform.position - knockbackOrigin;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector2.up; // 정확히 겹친 경우 위로 밀어낸다
                info.Knockback = new KnockbackInfo
                {
                    Direction = dir.normalized,
                    Force = knockbackForce,
                    StunDuration = stunDuration,
                };
            }

            DamageSystem.Apply(target, in info);
        }

        /// <summary>원형 범위 전체 타격 (범위 공격/폭발 패턴 공용). 타격한 대상 수를 반환한다.</summary>
        public static int ApplyAreaHit(
            CombatEntity source,
            Vector2 center,
            float radius,
            float damage,
            float knockbackForce = 0f,
            float stunDuration = 0f,
            List<StatusEffectData> statusEffects = null,
            bool isExplosive = false)
        {
            var targets = new List<CombatEntity>(8);
            CollectPlayers(center, radius, targets);

            for (int i = 0; i < targets.Count; i++)
                ApplyHit(source, targets[i], damage, center, knockbackForce, stunDuration, statusEffects, isExplosive);

            return targets.Count;
        }
    }
}
