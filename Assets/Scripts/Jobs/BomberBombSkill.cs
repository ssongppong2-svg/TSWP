// 근거: 직업 시스템.md — 폭탄마(bomber). 장점: 강한 범위 공격 / 위험 요소: 아군도 피해를 입을 수 있다.
// 근거: 전투 시스템.md — 아군 피해 기본 50%, 일부 스킬은 FriendlyFireRule로 오버라이드
//   (거대 폭탄 = 대상 '현재 체력'의 20% → FriendlyFireMode.CurrentHpPercent 0.2).
// 근거: 게임 성경.md — "모든 강력한 능력에는 반드시 위험이 따른다." → 자기 자신도 폭발 범위에 들어가면 맞는다.
using UnityEngine;
using TSWP.Combat;

namespace TSWP.Jobs
{
    /// <summary>
    /// 폭탄마 — 거대 폭탄 투척. 포물선으로 날아가 도화선이 끝나면 광역 폭발.
    /// 폭발은 진영을 가리지 않는다: 적·아군·구조물·시전자 본인 모두 범위 안이면 맞는다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Jobs/Skills/Bomber Bomb", fileName = "Skill_BomberBomb")]
    public class BomberBombSkill : ActiveSkillDefinition
    {
        [Header("투척")]
        [Tooltip("던지는 속도(월드 유닛/초). 조준 방향으로 날아간다.")]
        [SerializeField, Min(0.1f)] private float throwSpeed = 9f;     // TODO(밸런스): 문서 미정

        [Tooltip("포물선 중력 가속도. 클수록 빨리 떨어진다.")]
        [SerializeField, Min(0f)] private float gravity = 18f;         // TODO(밸런스): 문서 미정

        [Tooltip("도화선(초). 이 시간이 지나거나 지형에 닿으면 폭발한다 — 팀원이 피할 시간을 준다.")]
        [SerializeField, Min(0.05f)] private float fuseSeconds = 1.2f; // TODO(밸런스): 문서 미정

        [Tooltip("던진 위치를 조준 방향으로 얼마나 띄울지 — 발밑에서 터지는 사고를 줄인다.")]
        [SerializeField, Min(0f)] private float spawnForwardOffset = 0.6f;

        [Header("폭발")]
        [SerializeField, Min(0.1f)] private float explosionRadius = 3f; // TODO(밸런스): 문서 미정
        [SerializeField, Min(0f)] private float explosionDamage = 70f;  // TODO(밸런스): 문서 미정
        [SerializeField, Min(0f)] private float knockbackForce = 18f;   // TODO(밸런스): 문서 미정
        [SerializeField, Range(0f, 1f)] private float knockbackUpward = 0.7f;

        [Header("위험 요소 — 자폭")]
        [Tooltip("시전자 본인이 폭심에 있을 때 받는 피해 비율. 아군 감쇠가 걸리지 않으므로 별도로 낮춘다.")]
        [SerializeField, Range(0f, 1f)] private float selfDamageRatio = 0.5f; // TODO(밸런스): 문서 미정

        [Header("지형")]
        [Tooltip("지형 레이어. 비워 두면(Nothing) 도화선으로만 폭발한다 — 씬 설정이 없어도 동작한다.")]
        [SerializeField] private LayerMask groundMask;

        [Header("표시")]
        [Tooltip("폭탄 사각형 색 — 도트 에셋 도입 전 프로토타입 표시용.")]
        [SerializeField] private Color bombColor = new Color(0.15f, 0.15f, 0.18f, 1f);

        [SerializeField, Min(0.1f)] private float bombSize = 0.5f;

        [Tooltip("폭발 이펙트 id (Art.VfxId). VfxSpawner가 없으면 조용히 생략된다.")]
        [SerializeField] private string explosionVfxId = Art.VfxId.Explosion;

        public override void Execute(SkillCaster caster)
        {
            if (caster == null) return;

            Vector2 forward = caster.AimDirection;
            Vector2 spawnPosition = caster.CastOrigin + forward * spawnForwardOffset;

            ThrownBomb bomb = ThrownBomb.Spawn(spawnPosition, bombColor, bombSize);
            if (bomb == null) return;

            // 폭발 시점의 아군 규칙은 스킬 정의(ActiveSkillDefinition)의 오버라이드를 그대로 넘긴다.
            // 미설정이면 null → DamageSystem이 기본 50% 규칙을 적용한다.
            bomb.Launch(
                owner: caster.Entity,
                velocity: forward * throwSpeed,
                gravity: gravity,
                fuseSeconds: fuseSeconds,
                damage: explosionDamage + caster.BonusAttackPower, // 아이템/패시브 공격력 반영
                radius: explosionRadius,
                knockbackForce: knockbackForce,
                knockbackUpward: knockbackUpward,
                selfDamageRatio: selfDamageRatio,
                friendlyFireOverride: FriendlyFireOverride,
                explosionVfxId: explosionVfxId,
                groundMask: groundMask,
                useGroundCheck: groundMask.value != 0);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate(); // 쿨타임 >0 강제 등 공통 검증을 반드시 먼저 태운다

            // 자폭 비율이 0이면 폭탄마의 위험 요소가 사라진다 (게임 성경.md — 강력한 능력에는 위험이 따른다).
            if (selfDamageRatio <= 0f)
            {
                Debug.LogWarning($"[BomberBombSkill] {name}: 자폭 비율이 0 — 위험 요소가 없는 광역기가 된다. " +
                                 "직업 시스템.md의 '위험 요소' 원칙을 확인할 것.", this);
            }
        }
#endif
    }
}
