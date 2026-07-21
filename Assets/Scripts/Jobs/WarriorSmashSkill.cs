// 근거: 직업 시스템.md — 용사(warrior)의 액티브 스킬(Q). 스킬은 강력한 효과를 가지며 반드시 쿨타임이 존재한다.
// 근거: 게임 성경.md — "모든 강력한 능력에는 반드시 위험이 따른다."
//   → 강타는 자신의 체력을 태워 휘두르고(자해), 전방 부채꼴 전체를 때리므로 아군도 맞는다(기본 50% 규칙).
// 근거: 전투 시스템.md — 모든 피해는 DamageSystem 단일 경로, 팀 판정은 TeamType 비교.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;

namespace TSWP.Jobs
{
    /// <summary>
    /// 용사 — 전방 강타. 넓은 부채꼴에 큰 피해 + 강한 넉백.
    /// 위험: 아군도 범위에 들어오면 그대로 맞고(기본 50%), 시전자는 체력을 대가로 지불한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Jobs/Skills/Warrior Smash", fileName = "Skill_WarriorSmash")]
    public class WarriorSmashSkill : ActiveSkillDefinition
    {
        [Header("강타 범위 — 넓은 범위 큰 피해")]
        [Tooltip("타격 반경(월드 유닛). 기본 공격 사거리보다 넓게 잡는다.")]
        [SerializeField, Min(0.1f)] private float radius = 3.5f;      // TODO(밸런스): 문서 미정

        [Tooltip("전방 부채꼴 반각(도). 90이면 전방 180도 전체.")]
        [SerializeField, Range(10f, 180f)] private float halfAngle = 75f; // TODO(밸런스): 문서 미정

        [Tooltip("스킬 피해량 (DamageInfo.SkillBonus 성분). 기본 공격의 몇 배가 되도록 크게 잡는다.")]
        [SerializeField, Min(0f)] private float skillDamage = 60f;    // TODO(밸런스): 문서 미정

        [Header("넉백 — 날아가는 게 보여야 맞은 맛이 난다")]
        [SerializeField, Min(0f)] private float knockbackForce = 14f;  // TODO(밸런스): 문서 미정
        [SerializeField, Range(0f, 1f)] private float knockbackUpward = 0.5f;

        [Header("위험 요소 — 게임 성경.md: 강력한 능력에는 위험이 따른다")]
        [Tooltip("시전 시 시전자가 잃는 '현재 체력' 비율. 0.1 = 현재 체력의 10%. 체력이 낮으면 자연히 손해가 줄어 자살하지는 않는다.")]
        [SerializeField, Range(0f, 0.5f)] private float selfHpCostRatio = 0.1f; // TODO(밸런스): 문서 미정

        [Tooltip("반동으로 시전자가 뒤로 밀리는 힘. 절벽에서 쓰면 낙사할 수 있다 — 의도된 위험.")]
        [SerializeField, Min(0f)] private float selfRecoilForce = 4f;  // TODO(밸런스): 문서 미정

        [Header("연출")]
        [Tooltip("타격 지점에 재생할 이펙트 id (Art.VfxId). VfxSpawner가 없으면 조용히 생략된다.")]
        [SerializeField] private string impactVfxId = Art.VfxId.Slash;

        // 발동마다 List를 새로 만들면 GC가 튄다. SO는 에셋 1개를 여러 플레이어가 공유하지만
        // 발동 처리는 한 프레임 안에서 순차 실행되므로 재사용 버퍼로 충분하다.
        private readonly List<CombatEntity> _targets = new List<CombatEntity>(16);

        public override void Execute(SkillCaster caster)
        {
            if (caster == null) return;

            CombatEntity self = caster.Entity;
            Vector2 origin = caster.CastOrigin;
            Vector2 forward = caster.AimDirection;

            // 부채꼴 중심을 앞으로 밀어 '전방' 강타처럼 보이게 한다.
            Vector2 center = origin + forward * (radius * 0.5f);

            // 연출 먼저 — 피해로 대상이 사라져도 이펙트는 남는다.
            if (!string.IsNullOrEmpty(impactVfxId))
            {
                float angle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
                Art.VfxSpawner.Instance?.Play(impactVfxId, center, flipX: forward.x < 0f, rotation: angle);
            }

            // 범위 대상 수집 — 피해를 주기 전에 목록을 확정한다.
            SkillTargeting.OverlapEntities(center, radius, self, _targets);
            for (int i = 0; i < _targets.Count; i++)
            {
                CombatEntity target = _targets[i];
                if (target == null) continue;

                // 전방 부채꼴 밖(등 뒤)은 맞지 않는다 — 어디를 때렸는지 보이게 한다.
                if (!SkillTargeting.IsInCone(origin, forward, target.transform.position, halfAngle)) continue;

                var info = new DamageInfo
                {
                    SkillBonus = skillDamage,               // 스킬 성분 (전투 시스템.md 합산 공식)
                    ItemBonus = caster.BonusAttackPower,    // 아이템 modifier + 패시브 배율
                    IsExplosive = IsExplosive,              // 정의에서 켜면 구조물도 부순다
                    Source = self,                          // 아군 판정·통계 귀속
                    FriendlyFireOverride = FriendlyFireOverride, // 미설정이면 기본 50% 규칙
                    Knockback = SkillTargeting.RadialKnockback(
                        origin, target.transform.position, knockbackForce, knockbackUpward),
                };

                DamageSystem.Apply(target, in info); // 아군 감쇠/무적/구조물 판정은 전부 파이프라인 소관
            }

            ApplyRisk(caster, self, forward);
        }

        /// <summary>
        /// 위험 요소 적용 — 체력 소모 + 반동.
        /// 체력 소모는 DamageSystem을 태우지 않는다: 무적 중이면 피해가 무시되어
        /// "무적일 때는 공짜"라는 빠져나갈 구멍이 생기기 때문이다 (대가는 항상 지불한다).
        /// </summary>
        private void ApplyRisk(SkillCaster caster, CombatEntity self, Vector2 forward)
        {
            if (self == null) return;

            if (selfHpCostRatio > 0f)
            {
                float cost = self.CurrentHp * selfHpCostRatio;
                // 자해로 죽지는 않게 최소 1은 남긴다 (자폭 직업은 폭탄마 담당).
                cost = Mathf.Min(cost, Mathf.Max(0f, self.CurrentHp - 1f));
                if (cost > 0f)
                {
                    var selfInfo = new DamageInfo { MiscBonus = cost, Source = null };
                    self.ApplyDamageDirect(cost, in selfInfo); // 무적을 무시하고 확정 지불
                }
            }

            // 반동 — 뒤로 밀린다. 절벽 근처에서의 낙사는 의도된 위험이다.
            if (selfRecoilForce > 0f)
            {
                var body = caster.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.AddForce(-forward.normalized * selfRecoilForce, ForceMode2D.Impulse);
                }
            }
        }
    }
}
