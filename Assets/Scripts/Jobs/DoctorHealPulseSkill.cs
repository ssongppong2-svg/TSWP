// 근거: 직업 시스템.md — 의사(doctor). 팀 기여 능력 '회복'. 기본 공격은 주사(대상 무관 회복 = 트롤 요소).
// 근거: 게임 성경.md — "모든 강력한 능력에는 반드시 위험이 따른다."
//   → 광역 치유는 공짜가 아니다: 시전자가 자신의 체력을 대가로 지불한다(수혈).
// 근거: 상태이상 시스템.md — 회복 차단(HealBlock)은 CombatEntity.Heal이 CanBeHealed로 이미 검사한다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;

namespace TSWP.Jobs
{
    /// <summary>
    /// 의사 — 수혈 파동. 주변 아군을 한 번에 회복시킨다.
    /// 위험: 회복시킨 아군 수에 비례해 시전자의 체력이 깎인다. 팀이 뭉쳐 있을수록 의사가 위험해진다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Jobs/Skills/Doctor Heal Pulse", fileName = "Skill_DoctorHealPulse")]
    public class DoctorHealPulseSkill : ActiveSkillDefinition
    {
        [Header("치유 범위")]
        [SerializeField, Min(0.1f)] private float radius = 4.5f;    // TODO(밸런스): 문서 미정
        [SerializeField, Min(0f)] private float healAmount = 35f;   // TODO(밸런스): 문서 미정

        [Tooltip("시전자 자신도 회복 대상에 포함할지. 끄면 '남만 살리는 의사'가 되어 위험이 커진다.")]
        [SerializeField] private bool healsSelf;

        [Header("위험 요소 — 수혈 대가")]
        [Tooltip("아군 1명을 회복시킬 때마다 시전자가 잃는 체력. 많이 살릴수록 자신이 위험해진다.")]
        [SerializeField, Min(0f)] private float hpCostPerTarget = 8f; // TODO(밸런스): 문서 미정

        [Tooltip("대가로 죽지는 않도록 남겨 둘 최소 체력.")]
        [SerializeField, Min(1f)] private float minHpAfterCost = 1f;

        [Header("연출")]
        [SerializeField] private string healVfxId = Art.VfxId.Heal;
        [SerializeField] private string casterVfxId = Art.VfxId.Buff;

        private readonly List<CombatEntity> _targets = new List<CombatEntity>(16);

        public override void Execute(SkillCaster caster)
        {
            if (caster == null) return;

            CombatEntity self = caster.Entity;
            Vector2 center = caster.CastOrigin;

            if (!string.IsNullOrEmpty(casterVfxId))
                Art.VfxSpawner.Instance?.Play(casterVfxId, center);

            int healerId = self != null ? self.OwnerPlayerId : -1;
            int healedCount = 0;

            SkillTargeting.OverlapEntities(center, radius, self, _targets);
            for (int i = 0; i < _targets.Count; i++)
            {
                CombatEntity target = _targets[i];
                if (target == null) continue;

                // 아군만 회복한다 — 팀 판정은 레이어가 아닌 TeamType 비교 (ARCHITECTURE.md §3-6).
                // 적 회복 트롤은 기본 공격(주사)이 담당하므로 스킬까지 무차별로 두지 않는다.
                if (!SkillTargeting.IsAlly(self, target)) continue;

                // Heal 내부에서 HealBlock(회복 차단)·사망 여부를 검사한다.
                target.Heal(healAmount, healerId);
                healedCount++;

                if (!string.IsNullOrEmpty(healVfxId))
                    Art.VfxSpawner.Instance?.Play(healVfxId, target.transform.position);
            }

            if (healsSelf && self != null)
            {
                self.Heal(healAmount, healerId);
                if (!string.IsNullOrEmpty(healVfxId))
                    Art.VfxSpawner.Instance?.Play(healVfxId, center);
            }

            ApplyBloodCost(self, healedCount);
        }

        /// <summary>
        /// 수혈 대가 — 회복시킨 인원수에 비례해 자기 체력을 지불한다.
        /// DamageSystem을 태우지 않는다: 무적 중이면 대가가 면제되는 구멍이 생기기 때문이다.
        /// </summary>
        private void ApplyBloodCost(CombatEntity self, int healedCount)
        {
            if (self == null || healedCount <= 0 || hpCostPerTarget <= 0f) return;

            float cost = hpCostPerTarget * healedCount;
            cost = Mathf.Min(cost, Mathf.Max(0f, self.CurrentHp - minHpAfterCost));
            if (cost <= 0f) return;

            var info = new DamageInfo { MiscBonus = cost, Source = null };
            self.ApplyDamageDirect(cost, in info);
        }
    }
}
