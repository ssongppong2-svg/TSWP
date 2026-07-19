// 근거: 보스 시스템.md — 보스 구성 9필수요소(고유 외형/고유 BGM/일반 공격/특수 패턴/기믹/협동 퍼즐/약점 직업/광폭화/처치 연출),
//       패턴 최소 5개, 기믹 최소 1개, 협동 퍼즐 최소 1개, 유형 1~3개 조합, 기억 요소 최소 1개.
// 총 15종: 보스 1종 = 이 SO 애셋 1개. 각 보스는 런에서 단 한 번만 등장 (GameRules.TotalBossCount = 15).
//   알려진 예시: 숲의 수호자(전투형+환경형), 광기의 광대(심리형+퍼즐형), 용암 거인(전투형+환경형) — 나머지 12종 기획 예정.
// 보상 개수는 GameRules.BossDropCountMin~Max(3~4)를 사용한다 — 여기 중복 필드를 두지 않는다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;
using TSWP.Jobs;

namespace TSWP.Bosses
{
    /// <summary>기믹 데이터 항목 — 런타임 로직은 gimmickPrefab의 IGimmick 구현 컴포넌트가 담당.</summary>
    [System.Serializable]
    public sealed class GimmickEntry
    {
        [SerializeField] private GimmickType gimmickType;
        [Tooltip("IGimmick 구현 컴포넌트를 포함한 프리팹.")]
        [SerializeField] private GameObject gimmickPrefab;
        [SerializeField, TextArea] private string description;

        public GimmickType GimmickType => gimmickType;
        public GameObject GimmickPrefab => gimmickPrefab;
        public string Description => description;
    }

    /// <summary>협동 퍼즐 데이터 항목 — 런타임 로직은 puzzlePrefab의 ICoopPuzzle 구현 컴포넌트가 담당.</summary>
    [System.Serializable]
    public sealed class CoopPuzzleEntry
    {
        [SerializeField] private CoopPuzzleType puzzleType;
        [Tooltip("혼자 해결하기 어렵도록 설계 — 최소 참여 인원 2 이상 권장 (보스 시스템.md).")]
        [SerializeField] private int minPlayers = 2;
        [Tooltip("ICoopPuzzle 구현 컴포넌트를 포함한 프리팹.")]
        [SerializeField] private GameObject puzzlePrefab;
        [SerializeField, TextArea] private string description;

        public CoopPuzzleType PuzzleType => puzzleType;
        public int MinPlayers => minPlayers;
        public GameObject PuzzlePrefab => puzzlePrefab;
        public string Description => description;
    }

    /// <summary>난이도 → 3종 배율 매핑 항목. 미등록 난이도는 배율 1.0으로 취급.</summary>
    [System.Serializable]
    public sealed class DifficultyScalingEntry
    {
        public Difficulty difficulty;
        public DifficultyScaling scaling = new();
    }

    /// <summary>
    /// 보스 1종의 정적 데이터. 제작 체크리스트 10항목을 OnValidate로 코드 강제한다.
    /// 데이터-로직 분리: 실행은 BossController(7단계 FSM)가 담당.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Bosses/Boss Data", fileName = "BossData_")]
    public sealed class BossData : ScriptableObject
    {
        [Header("식별")]
        [SerializeField] private string bossId;
        [SerializeField] private string displayName; // 예: 숲의 수호자, 광기의 광대, 용암 거인

        [Header("유형 (1~3개 조합 — Flags)")]
        [SerializeField] private BossType types = BossType.Combat;

        [Tooltip("심리형(Psychological) 유형 보유 시 사용할 효과 목록.")]
        [SerializeField] private List<PsychologicalEffectType> psychologicalEffects = new();

        [Header("9필수 ① 고유 외형 / ② 고유 BGM")]
        [SerializeField] private GameObject appearancePrefab; // 도트 시스템.md: 보스 스프라이트 64/96/128px
        [SerializeField] private AudioClip bgm;               // TODO(사운드): AudioManager 보스 BGM 전환 연동

        [Header("9필수 ③ 일반 공격 / ④ 특수 패턴 (최소 5개)")]
        [SerializeField] private BasicAttackProfile basicAttack = new();
        [SerializeField] private List<BossPattern> patterns = new();

        [Header("9필수 ⑤ 기믹 (최소 1개) / ⑥ 협동 퍼즐 (최소 1개)")]
        [SerializeField] private List<GimmickEntry> gimmicks = new();
        [SerializeField] private List<CoopPuzzleEntry> coopPuzzles = new();

        [Header("9필수 ⑦ 약점 직업 (jobId 문자열 — 직업 enum 금지, ARCHITECTURE.md §5)")]
        [Tooltip("해당 직업이 없어도 클리어 가능해야 한다 — 보너스는 가산적으로만 설계.")]
        [SerializeField] private List<string> weaknessJobIds = new(); // 예: warrior, bomber, doctor, ...

        [Header("9필수 ⑧ 광폭화 (일부 보스만 — 없으면 패턴 변화가 대체)")]
        [SerializeField] private bool hasEnrage = true;
        [SerializeField] private EnrageConfig enrage = new();

        [Header("9필수 ⑨ 처치 연출 / 등장 연출")]
        [SerializeField] private GameObject introCinematicPrefab; // TODO(연출): 등장 연출 — 압도적 등장은 기억 요소 후보
        [SerializeField] private GameObject deathCinematicPrefab; // TODO(연출): 처치 연출 — 제작 체크리스트 필수 항목

        [Header("기억 요소 (최소 1개 — '아, 그 보스!')")]
        [Tooltip("예: 독특한 대사, 압도적인 등장 연출, 충격적인 패턴, 웃긴 기믹, 독특한 BGM, 특이한 퍼즐, 반전.")]
        [SerializeField, TextArea] private List<string> memorableElements = new();

        [Header("기본 능력치 (체력만 많은 보스 금지 — 난이도는 패턴과 기믹으로)")]
        [SerializeField] private float baseMaxHp = 1000f;   // TODO(밸런스): 문서 미정 — HP 절대값 미명시
        [SerializeField] private float baseMoveSpeed = 2f;  // TODO(밸런스): 문서 미정

        [Header("난이도별 배율 (체력/공격력/패턴 속도 3종만 — 기믹 불변)")]
        [SerializeField] private List<DifficultyScalingEntry> difficultyScalings = new();

        private static readonly DifficultyScaling DefaultScaling = new(); // 미등록 난이도용 1.0 배율

        public string BossId => bossId;
        public string DisplayName => displayName;
        public BossType Types => types;
        public IReadOnlyList<PsychologicalEffectType> PsychologicalEffects => psychologicalEffects;
        public GameObject AppearancePrefab => appearancePrefab;
        public AudioClip Bgm => bgm;
        public BasicAttackProfile BasicAttack => basicAttack;
        public IReadOnlyList<BossPattern> Patterns => patterns;
        public IReadOnlyList<GimmickEntry> Gimmicks => gimmicks;
        public IReadOnlyList<CoopPuzzleEntry> CoopPuzzles => coopPuzzles;
        public IReadOnlyList<string> WeaknessJobIds => weaknessJobIds;
        public bool HasEnrage => hasEnrage;
        public EnrageConfig Enrage => enrage;
        public GameObject IntroCinematicPrefab => introCinematicPrefab;
        public GameObject DeathCinematicPrefab => deathCinematicPrefab;
        public IReadOnlyList<string> MemorableElements => memorableElements;
        public float BaseMaxHp => baseMaxHp;
        public float BaseMoveSpeed => baseMoveSpeed;

        /// <summary>유형 플래그 개수 (1~3 유효).</summary>
        public int TypeCount
        {
            get
            {
                int v = (int)types;
                int count = 0;
                while (v != 0) { count += v & 1; v >>= 1; }
                return count;
            }
        }

        /// <summary>난이도별 배율 조회. 미등록 난이도는 1.0 배율 (Human 기준).</summary>
        public DifficultyScaling GetScaling(Difficulty difficulty)
        {
            for (int i = 0; i < difficultyScalings.Count; i++)
            {
                var entry = difficultyScalings[i];
                if (entry != null && entry.difficulty == difficulty && entry.scaling != null)
                    return entry.scaling;
            }
            return DefaultScaling;
        }

        /// <summary>이 보스가 해당 유형을 가지는지.</summary>
        public bool HasType(BossType type) => (types & type) != 0;

#if UNITY_EDITOR
        // 보스 제작 체크리스트(보스 시스템.md)를 에디터에서 코드로 강제한다.
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(bossId))
                Debug.LogWarning($"[BossData] '{name}': bossId 누락 — GameEvents/RunManager 식별에 필수.", this);

            // 유형 1~3개 강제.
            int typeCount = TypeCount;
            if (typeCount < 1 || typeCount > 3)
                Debug.LogWarning($"[BossData] '{name}': 보스 유형은 최소 1개, 최대 3개 ({typeCount}개 설정됨).", this);

            // 심리형 유형과 효과 목록의 정합.
            if (HasType(BossType.Psychological) && psychologicalEffects.Count == 0)
                Debug.LogWarning($"[BossData] '{name}': 심리형 보스인데 심리 효과가 비어 있음.", this);
            if (!HasType(BossType.Psychological) && psychologicalEffects.Count > 0)
                Debug.LogWarning($"[BossData] '{name}': 심리형 유형이 없는데 심리 효과가 설정됨.", this);

            // 패턴 최소 5개 (권장 구성: 일반 공격/범위 공격/이동기/특수 기술/협동 기믹).
            if (patterns.Count < 5)
                Debug.LogWarning($"[BossData] '{name}': 패턴은 최소 5개 필요 (현재 {patterns.Count}개).", this);

            // 기믹 최소 1개 / 협동 퍼즐 최소 1개.
            if (gimmicks.Count < 1)
                Debug.LogWarning($"[BossData] '{name}': 핵심 기믹은 최소 1개 필요.", this);
            if (coopPuzzles.Count < 1)
                Debug.LogWarning($"[BossData] '{name}': 협동 퍼즐은 최소 1개 필요 (모든 보스 필수).", this);

            // 약점 직업 최소 1개 (없어도 클리어 가능해야 하므로 데이터만 검증).
            if (weaknessJobIds.Count < 1)
                Debug.LogWarning($"[BossData] '{name}': 약점 직업(jobId)은 최소 1개 필요.", this);

            // 기억 요소 최소 1개.
            if (memorableElements.Count < 1)
                Debug.LogWarning($"[BossData] '{name}': 기억 요소는 최소 1개 필요 ('아, 그 보스!').", this);

            // 9필수요소 중 참조형 항목 누락 경고.
            if (appearancePrefab == null)
                Debug.LogWarning($"[BossData] '{name}': 고유 외형(appearancePrefab) 누락.", this);
            if (bgm == null)
                Debug.LogWarning($"[BossData] '{name}': 고유 BGM 누락.", this);
            if (deathCinematicPrefab == null)
                Debug.LogWarning($"[BossData] '{name}': 처치 연출 누락 (제작 체크리스트 필수).", this);

            // 광폭화 또는 패턴 변화 — 광폭화가 없어도 FSM의 PatternChange 단계가 있으므로 정보성 로그만.
            if (!hasEnrage)
                Debug.Log($"[BossData] '{name}': 광폭화 없음 — 패턴 변화(PatternChange)가 그 역할을 대체해야 함.", this);
        }
#endif
    }
}
