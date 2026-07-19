// 근거: 적 시스템.md — 적 1종 = SO 에셋 1개 (데이터-로직 분리). 역할 최소 1개, 일반 적은 기본 공격만,
//       엘리트 이상 패턴 조합·일부 상태이상 면역·희귀 드롭 확률 증가, 적도 환경의 영향을 받는다.
// 난이도 배율은 체력/공격력/패턴 속도 3종만 변경한다 — 핵심 기믹·행동은 불변 (스펙 DifficultyScaling).
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;
using TSWP.StatusEffects;

namespace TSWP.Enemies
{
    /// <summary>
    /// 적 공격 1건 (기본 공격/특수 공격 공용). 피해는 DamageInfo.BaseDamage 성분으로 들어간다.
    /// </summary>
    [Serializable]
    public class EnemyAttack
    {
        public string attackName = "기본 공격";

        [Min(0f)] public float damage = 5f;        // TODO(밸런스): 문서 미정
        [Min(0f)] public float range = 1.5f;       // TODO(밸런스): 문서 미정
        [Min(0.05f)] public float cooldown = 1.5f; // TODO(밸런스): 문서 미정

        [Tooltip("폭발 판정 — 구조물은 폭발 공격만 파괴 가능 (자폭 등).")]
        public bool isExplosive;

        [Tooltip("피격 시 넉백 적용 여부. Direction은 실행 시점에 대상 방향으로 갱신된다.")]
        public bool applyKnockback;
        public KnockbackInfo knockback;

        [Tooltip("피격 시 부여할 상태이상 (CC/디버프 역할군의 수단).")]
        public List<StatusEffectData> statusEffects = new List<StatusEffectData>();

        [Header("원거리")]
        [Tooltip("투사체를 발사하는 공격인가 (저격수 역할군).")]
        public bool isRanged;

        [Tooltip("발사할 투사체 프리팹. isRanged일 때만 사용한다.")]
        public Projectile projectilePrefab;

        [Tooltip("투사체 속도.")]
        [Min(0.1f)] public float projectileSpeed = 8f; // TODO(밸런스): 문서 미정

        [Tooltip("총구 오프셋 — 자기 콜라이더 안에서 생성되지 않도록 앞으로 띄운다.")]
        public float muzzleForward = 0.7f;

        [Tooltip("공격 시 재생할 이펙트 id (Art.VfxId). 비우면 없음.")]
        public string attackVfxId;
    }

    /// <summary>공격 조합 패턴 — 엘리트 이상만 사용한다 ("엘리트 이상은 패턴을 조합하여 사용한다").</summary>
    [Serializable]
    public class EnemyPattern
    {
        public string patternName;

        [Tooltip("순서대로 실행할 공격 조합.")]
        public List<EnemyAttack> sequence = new List<EnemyAttack>();
    }

    /// <summary>
    /// 난이도 배율 — 체력/공격력/패턴 속도 3종만 (이 외 배율 필드 추가 금지 — 기믹·행동 불변 원칙).
    /// </summary>
    [Serializable]
    public class EnemyDifficultyScaling
    {
        [Min(0.1f)] public float hpMultiplier = 1f;           // TODO(밸런스): 문서 미정
        [Min(0.1f)] public float attackMultiplier = 1f;       // TODO(밸런스): 문서 미정
        [Min(0.1f)] public float patternSpeedMultiplier = 1f; // TODO(밸런스): 문서 미정 (>1 = 빨라짐)
    }

    /// <summary>적 정의 SO. 로직은 EnemyController/EnemyAI가 담당한다 (데이터-로직 분리).</summary>
    [CreateAssetMenu(menuName = "TSWP/Enemies/Enemy Data", fileName = "Enemy_")]
    public class EnemyData : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("적 고유 식별자 — GameEvents.EnemyKilled 페이로드/업적 카운터 키로 사용.")]
        public string enemyId;
        public string displayName;

        [Header("분류 (등급 5단계 / 역할군 9종 — 최소 1개)")]
        public EnemyGrade grade = EnemyGrade.Normal;
        public EnemyRole roles = EnemyRole.Melee;

        [Header("스폰")]
        [Tooltip("EnemyController + CombatEntity가 붙은 프리팹 — SpawnManager가 인스턴스화.")]
        public GameObject enemyPrefab;

        [Header("기본 능력치")]
        [Min(1f)] public float maxHp = 30f;     // TODO(밸런스): 문서 미정 (일반 적은 낮게)
        [Min(0f)] public float moveSpeed = 2f;  // TODO(밸런스): 문서 미정

        [Header("공격 (일반 적은 기본 공격만)")]
        public EnemyAttack basicAttack = new EnemyAttack();

        [Tooltip("특수 공격 — 특수 적 이상.")]
        public List<EnemyAttack> specialAttacks = new List<EnemyAttack>();

        [Tooltip("공격 조합 패턴 — 엘리트 이상 전용.")]
        public List<EnemyPattern> patterns = new List<EnemyPattern>();

        [Tooltip("고유 능력 (특수 적: 힐러/자폭/저격수/소환사/버퍼/디버퍼). 없으면 비움.")]
        public SpecialAbility specialAbility;

        [Header("상태이상/환경")]
        [Tooltip("면역 상태이상 목록 — 일부 적/엘리트. 스폰 시 StatusEffectController에 주입된다.")]
        public List<StatusEffectType> statusImmunities = new List<StatusEffectType>();

        [Tooltip("환경(낙사/용암/독/폭발/얼음) 영향 여부 — 원칙적으로 true (플레이어가 이용해 전투 가능).")]
        public bool affectedByEnvironment = true;

        [Header("드롭/보상")]
        [Tooltip("골드·회복·소비·장비 드롭 테이블.")]
        public DropTable dropTable;

        [Tooltip("희귀 드롭 배율 — 엘리트/미니 보스는 1보다 크게 (희귀 아이템 확률 상향).")]
        [Min(0f)] public float rareDropMultiplier = 1f; // TODO(밸런스): 문서 미정

        [Tooltip("처치 경험치/고정 골드 + EnemyKilled 이벤트 단일 발행 경로 (Combat.KillReward.Grant).")]
        public KillReward killReward = new KillReward();

        [Header("난이도 배율 (체력/공격력/패턴 속도 3종만 — 인간이 기준 1배)")]
        public EnemyDifficultyScaling superCowardScaling = new EnemyDifficultyScaling(); // TODO(밸런스): 1보다 낮게
        public EnemyDifficultyScaling humanScaling = new EnemyDifficultyScaling();       // 기준 난이도 = 1배
        public EnemyDifficultyScaling godScaling = new EnemyDifficultyScaling();         // TODO(밸런스): 1보다 높게
        public EnemyDifficultyScaling memeScaling = new EnemyDifficultyScaling();        // 밈 특수 규칙은 별도 시스템 소관

        /// <summary>난이도별 배율 조회 — SpawnManager가 EnemyController.Initialize에 전달한다.</summary>
        public EnemyDifficultyScaling GetScaling(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.SuperCoward: return superCowardScaling;
                case Difficulty.God: return godScaling;
                case Difficulty.Meme: return memeScaling;
                default: return humanScaling;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 역할 최소 1개 강제 (적 시스템.md 역할군: "모든 적은 최소 하나의 역할을 가진다")
            if (roles == EnemyRole.None)
            {
                roles = EnemyRole.Melee;
                Debug.LogWarning($"[EnemyData] '{name}': 역할은 최소 1개 필수 — Melee로 강제 지정했습니다.", this);
            }

            if (string.IsNullOrEmpty(enemyId))
                Debug.LogWarning($"[EnemyData] '{name}': enemyId가 비어 있습니다 (이벤트/업적 키 필요).", this);

            // 일반 적은 기본 공격만 사용한다
            if (grade == EnemyGrade.Normal && (specialAttacks.Count > 0 || patterns.Count > 0 || specialAbility != null))
                Debug.LogWarning($"[EnemyData] '{name}': 일반 적은 기본 공격만 사용합니다 — 특수 공격/패턴/고유 능력 제거 필요.", this);

            // 패턴 조합은 엘리트 이상 전용
            if (grade < EnemyGrade.Elite && patterns.Count > 0)
                Debug.LogWarning($"[EnemyData] '{name}': 패턴 조합은 엘리트 이상 전용입니다 (grade={grade}).", this);

            // 엘리트/미니 보스는 희귀 아이템 확률이 더 높아야 한다
            if ((grade == EnemyGrade.Elite || grade == EnemyGrade.MiniBoss) && rareDropMultiplier <= 1f)
                Debug.LogWarning($"[EnemyData] '{name}': 엘리트/미니 보스는 rareDropMultiplier > 1 권장 (희귀 드롭 상향).", this);

            // 골드 이중 지급 예방 — DropTable 골드 범위와 KillReward 고정 골드 중 한쪽만 사용
            if (dropTable != null && killReward != null && killReward.Gold > 0)
                Debug.LogWarning($"[EnemyData] '{name}': DropTable 골드 범위와 KillReward.Gold가 동시에 설정됨 — 이중 지급 주의. " +
                                 "// NOTE(기획 확인 필요): 골드 지급 경로 단일화", this);

            // 보스 등급은 Bosses 시스템 소관
            if (grade == EnemyGrade.Boss)
                Debug.LogWarning($"[EnemyData] '{name}': Boss 등급의 실제 로직은 TSWP.Bosses(BossData)가 담당합니다 — 분류 확인.", this);
        }
#endif
    }
}
