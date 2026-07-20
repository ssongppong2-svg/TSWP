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

        // ── 예비 동작 (telegraph) ─────────────────────────────────
        // 근거: 전투 시스템.md — "공격은 명확해야 한다 / 피격이 공정해야 한다".
        //   예고 없는 공격은 반응할 수 없어 불공정하다. 예비 동작 동안 적은 이동을 멈추고
        //   색/이펙트로 공격을 예고하며, 시간이 지나면 반드시 발동한다(회피 가능한 확정 공격).
        [Header("예비 동작 (공격 예고)")]
        [Tooltip("공격 발동 전 예고 시간(초). 0이면 예고 없이 즉시 발동한다. 난이도 패턴 속도 배율로 나뉘어 짧아진다.")]
        [Min(0f)] public float telegraphDuration = 0.35f; // TODO(밸런스): 문서 미정

        [Tooltip("예비 동작 중 본체 스프라이트 색. 흰색이면 색을 바꾸지 않는다.")]
        public Color telegraphColor = new Color(1f, 0.55f, 0.2f); // 주황 — 위험 예고 (팔레트 시스템.md 의미 색상)

        [Tooltip("예비 동작 시작 시 재생할 이펙트 id (Art.VfxId). 비우면 없음.")]
        public string telegraphVfxId;
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
        // 근거: 적 시스템.md — 적 1종 = SO 에셋 1개. 몸통(리지드바디/콜라이더/컴포넌트 구성)은 적마다 같으므로
        //   프리팹을 종마다 만들면 '에셋 1개 = 적 1종' 원칙이 깨진다. 비워 두면 SpawnManager의
        //   공용 몸통 프리팹(defaultEnemyPrefab)이 사용되고, 외형 차이는 아래 '외형' 항목이 담당한다.
        [Tooltip("전용 프리팹이 필요한 적만 지정. 비우면 SpawnManager의 공용 적 프리팹을 사용한다.")]
        public GameObject enemyPrefab;

        [Header("외형 (공용 프리팹 + 에셋별 차별화)")]
        // 근거: 도트 시스템.md — 캐릭터 32px/PPU 16. 프로토타입 단계에서는 스프라이트 대신 색·크기로 종을 구분한다.
        [Tooltip("본체 스프라이트. 비우면 프리팹의 스프라이트를 그대로 쓴다.")]
        public Sprite bodySprite;

        [Tooltip("본체 색조. 프로토타입에서는 이 색으로 적 종류를 구분한다(팔레트 시스템.md 의미 색상).")]
        public Color bodyColor = Color.white;

        [Tooltip("프리팹 크기 대비 배율. (1,1)이면 프리팹 그대로. 콜라이더도 함께 커진다.")]
        public Vector2 bodyScale = Vector2.one;

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

        [Header("AI 성격 (감지/판단/거리 유지)")]
        // 근거: 적 시스템.md — 난이도는 체력이 아니라 '행동'으로 만든다. 행동 성향은 적의 정체성이므로
        //   프리팹이 아니라 이 에셋이 소유한다. → 프리팹 1개 + EnemyData N개 = 적 N종.
        [Tooltip("EnemyAI가 사용하는 감지 거리·후퇴 임계치·사거리 유지 등. 비워 둘 수 없다(항상 인스턴스 보유).")]
        public EnemyAIProfile aiProfile = new EnemyAIProfile();

        [Header("이동/지형")]
        // 근거: 적 시스템.md — 적도 환경(낙사)의 영향을 받는다. 다만 '스스로 걸어서' 떨어지면
        //   전투가 성립하지 않는다. 플레이어가 유도해 떨어뜨리는 것은 여전히 가능해야 하므로
        //   이 옵션은 '자발적 이동'만 막고 넉백/밀림에는 관여하지 않는다.
        [Tooltip("발밑에 땅이 없으면 그 방향으로 스스로 걸어가지 않는다. 넉백으로 밀려 떨어지는 것은 막지 않는다.")]
        public bool avoidLedges = true;

        [Tooltip("낭떠러지 회피를 무시하고 추격하는 적 (비행/도약형). avoidLedges보다 우선한다.")]
        public bool canTraverseGaps = false;

        [Header("연출")]
        [Tooltip("사망 시 재생할 이펙트 id. 비우면 Art.VfxId.Death를 사용한다.")]
        public string deathVfxId = "";

        [Tooltip("사망 후 오브젝트가 제거되기까지의 연출 시간(초). 0이면 즉시 제거.")]
        [Min(0f)] public float deathLingerDuration = 0.4f; // TODO(밸런스): 문서 미정

        [Tooltip("사망 연출 동안 스프라이트를 서서히 투명하게 만든다.")]
        public bool fadeOutOnDeath = true;

        [Tooltip("머리 위 체력바 표시 (EnemyHealthBar). 프리팹에 이미 있으면 그것을 사용한다.")]
        public bool showHealthBar = true;

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

        /// <summary>
        /// AI 성격 조회. 직렬화 누락(구버전 에셋)으로 null이어도 공용 기본값을 돌려주어
        /// AI가 NullReference로 멈추지 않는다 — 데이터 결함이 게임 로직을 깨뜨리지 않게 한다.
        /// </summary>
        public EnemyAIProfile ResolveAIProfile() => aiProfile ?? EnemyAIProfile.Default;

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

            // 프리팹이 비어 있는 것은 정상(공용 몸통 사용). 경고 대상은 '크기 0' 같은 실수뿐이다.
            if (bodyScale.x <= 0f || bodyScale.y <= 0f)
            {
                Debug.LogWarning($"[EnemyData] '{name}': bodyScale에 0 이하 값이 있습니다 — (1,1)로 되돌립니다.", this);
                bodyScale = Vector2.one;
            }

            // 원거리 공격인데 투사체가 없으면 EnemyAI가 근접 판정으로 폴백해 사거리 밖에서 헛치게 된다.
            if (basicAttack != null && basicAttack.isRanged && basicAttack.projectilePrefab == null)
                Debug.LogWarning($"[EnemyData] '{name}': isRanged인데 projectilePrefab이 없습니다 — 원거리 공격이 근접 판정으로 폴백합니다.", this);

            // 원거리 적이 사거리를 유지하지 않으면 플레이어에게 붙어 버려 원거리 정체성이 사라진다.
            if (basicAttack != null && basicAttack.isRanged && aiProfile != null && !aiProfile.KeepsDistance)
                Debug.LogWarning($"[EnemyData] '{name}': 원거리 적은 aiProfile.holdDistanceRatio > 0 권장 (사거리 유지).", this);

            // 보스 등급은 Bosses 시스템 소관
            if (grade == EnemyGrade.Boss)
                Debug.LogWarning($"[EnemyData] '{name}': Boss 등급의 실제 로직은 TSWP.Bosses(BossData)가 담당합니다 — 분류 확인.", this);
        }
#endif
    }
}
