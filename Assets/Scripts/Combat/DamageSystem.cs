// 근거: 전투 시스템.md — 피해 = 기본 공격력 + 아이템 효과 + 스킬 효과 + 기타 효과 (합산).
// 단일 진입점 파이프라인: 무적 체크 → 아군 판정(오버라이드 우선/기본 50%) → 구조물 폭발 판정
//   → 즉시 HP 차감 → 넉백(면역 체크) → 상태이상 위임 → GameEvents 통지. (스펙 unityNotes ②)
// 모든 피해는 반드시 이 클래스를 통과한다 — 개별 시스템이 HP를 직접 깎는 경로를 만들지 않는다.
using UnityEngine;
using TSWP.Core;

namespace TSWP.Combat
{
    public static class DamageSystem
    {
        /// <summary>
        /// 튜닝 설정 (치명타 배율·넉백 기본값 등). 부트스트랩(GameFlowManager 초기화 등)에서 주입.
        /// null이면 각 지점의 폴백 기본값 사용. TODO: 주입 지점 확정 (씬 로더 or Resources).
        /// </summary>
        public static CombatConfig Config { get; set; }

        /// <summary>치명타 배율. TODO(밸런스): 문서 미정 — 배율 미기재, CombatConfig에서 튜닝.</summary>
        private static float CritMultiplier => Config != null ? Config.critDamageMultiplier : 2f;

        /// <summary>
        /// IDamageable 일반 진입점. CombatEntity면 통합 파이프라인을 태우고,
        /// 그 외 커스텀 구현체는 자체 TakeDamage에 위임한다.
        /// </summary>
        public static void Apply(IDamageable target, in DamageInfo info)
        {
            if (target == null) return;
            if (target is CombatEntity entity)
            {
                Apply(entity, in info);
                return;
            }
            target.TakeDamage(in info);
        }

        /// <summary>피해 파이프라인 단일 진입점. // SYNC: 호스트 권위 — 피해 판정은 호스트에서만 실행, 추후 NGO 결과 동기화.</summary>
        public static void Apply(CombatEntity target, in DamageInfo info)
        {
            if (target == null || target.IsDead) return;

            // 1) 무적 체크 — 일부 스킬 사용 중 일시 무적. 상시 무적은 존재하지 않는다.
            if (target.IsInvincible) return;

            // 2) 아군 판정 — 레이어가 아닌 TeamType 필드 비교 (ARCHITECTURE.md §3-6).
            //    NOTE: 자기 자신에게 준 피해(자폭 등)는 아군 감쇠를 적용하지 않는다(문서 미정 — 원 피해 100%).
            bool friendly = info.Source != null
                            && info.Source != target
                            && info.Source.Team == target.Team;
            float finalDamage = ResolveFinalDamage(target, in info, friendly);

            // 3) 구조물 폭발 판정 — 구조물은 폭발 공격만 파괴 (건축가 설치 일부 예외).
            if (target is Structure structure && !structure.CanBeDamagedBy(in info))
                return;

            // 4) 즉시 HP 차감 — 피해 적용에 지연 없음 (전투 시스템.md '피격').
            target.ApplyDamageDirect(finalDamage, in info);

            // 4-1) 피격 훅 — 빙결 즉시 해제 등 (상태이상.md 소관, 컨트롤러에 위임).
            var status = target.StatusController;
            if (status != null && finalDamage > 0f)
                status.OnDamageTaken();

            // 5) 넉백 — 적·아군 모두 적용, 대상이 면역(일부 보스)이면 무시.
            if (info.Knockback.HasValue && !target.IsKnockbackImmune)
                target.ApplyKnockback(info.Knockback.Value);

            // 6) 상태이상 위임 — 부여 규칙(면역/갱신)은 StatusEffectController가 소유.
            if (info.StatusEffects != null && status != null)
            {
                GameObject sourceGo = info.Source != null ? info.Source.gameObject : null;
                for (int i = 0; i < info.StatusEffects.Count; i++)
                {
                    if (info.StatusEffects[i] == null) continue;
                    status.ApplyEffect(info.StatusEffects[i], sourceGo);
                }
            }

            // 7) 통계/UI 통지 — 공격자가 플레이어일 때만 (환경·적 피해는 플레이어 통계 비대상).
            //    wasFriendly는 트롤 통계(결과 화면 '가장 많은 트롤') 집계에 쓰인다.
            if (info.Source != null && info.Source.OwnerPlayerId >= 0)
                GameEvents.RaiseDamageDealt(info.Source.OwnerPlayerId, finalDamage, friendly);
        }

        /// <summary>
        /// 최종 피해량 산출.
        /// 비아군: 합산 피해(치명타 시 배율 적용).
        /// 아군: FriendlyFireOverride 우선 —
        ///   DefaultPercent/null → 원 피해의 GameRules.FriendlyFireDamageRatio(50%),
        ///   CurrentHpPercent   → 대상 '현재 체력'의 value 비율 (예: 폭탄마 거대 폭탄 0.2 = 20%),
        ///   Custom             → value를 절대 피해량으로 사용.
        /// </summary>
        private static float ResolveFinalDamage(CombatEntity target, in DamageInfo info, bool friendly)
        {
            float total = info.TotalDamage;
            if (info.IsCritical)
                total *= CritMultiplier; // 기본 치명타 확률 0% — 발동 판정은 공격 측(스탯 시스템)에서 완료된 상태

            if (!friendly)
                return Mathf.Max(0f, total);

            FriendlyFireRule rule = info.FriendlyFireOverride
                                    ?? new FriendlyFireRule(FriendlyFireMode.DefaultPercent, GameRules.FriendlyFireDamageRatio);
            switch (rule.Mode)
            {
                case FriendlyFireMode.CurrentHpPercent:
                    return Mathf.Max(0f, target.CurrentHp * rule.Value); // 대상 현재 체력 기준
                case FriendlyFireMode.Custom:
                    return Mathf.Max(0f, rule.Value);                    // 절대 피해량
                case FriendlyFireMode.DefaultPercent:
                default:
                    return Mathf.Max(0f, total * GameRules.FriendlyFireDamageRatio);
            }
        }
    }
}
