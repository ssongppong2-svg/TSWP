// 근거: 방 시스템.md — 방 종류별 콘텐츠(적 구성/퍼즐/보상)는 매 플레이 달라진다.
//   맵 다수 / 보스 15기 / 퍼즐 다수로 확장될 때 "코드 수정 없이 에셋만 추가"가 되도록,
//   방 1종의 모든 구성 수치를 이 SO 한 곳에 모은다 (ARCHITECTURE.md §3-2 데이터 주도).
// 적 구성은 Enemies.EncounterComposition을 그대로 재사용한다 (중복 정의 금지 — ARCHITECTURE.md §5).
using UnityEngine;
using TSWP.Enemies;
using TSWP.Items;

namespace TSWP.Map
{
    /// <summary>
    /// 클리어 조건 지정 방식.
    /// 기본은 RoomType이 결정한다 (RoomManager.BuildClearCondition의 매핑 — 전투=전멸형, 퍼즐/연습=목표형, 그 외=없음).
    /// 특수한 방(예: '적을 죽이지 않고 목표만 달성하는 전투 방')만 명시적으로 덮어쓴다.
    /// </summary>
    public enum RoomClearOverride
    {
        UseRoomTypeDefault, // RoomType 기본 규칙 사용 (권장)
        None,               // 조건 없음 — 진입 즉시 클리어
        KillAllEnemies,     // 전멸형
        ObjectiveComplete,  // 목표형
    }

    /// <summary>
    /// 방 1종의 데이터 정의. 씬/프리팹(물리 계층)과 분리된 순수 구성값.
    /// RoomInstance가 이 값을 읽어 적 생성/퍼즐 시작/보스 시작/보상 지급을 수행한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Map/Room Definition", fileName = "Room_")]
    public class RoomDefinition : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("느슨한 식별자. 비우면 에셋 이름을 사용한다.")]
        [SerializeField] private string roomDefinitionId = "";

        [Tooltip("방 종류 — 클리어 조건의 기본값을 결정한다.")]
        [SerializeField] private RoomType roomType = RoomType.NormalCombat;

        [Header("소속 군계 (맵이 늘어나도 이 필드로 필터링만 하면 된다)")]
        [Tooltip("체크하면 모든 군계에서 선택 가능 — 군계 무관 공용 방(시작/휴식 등)에 사용.")]
        [SerializeField] private bool anyBiome = false;
        [SerializeField] private BiomeType biome = BiomeType.Forest;

        [Header("물리 계층")]
        [Tooltip("이 방의 프리팹. 루트에 RoomInstance가 있어야 한다. " +
                 "씬에 방을 직접 배치하는 구성(SceneAuthored)에서는 비워 둔다.")]
        [SerializeField] private GameObject roomPrefab;

        [Header("적 구성 (Enemies.EncounterComposition 재사용)")]
        [Tooltip("전투 방의 적 구성. 비어 있으면 적을 생성하지 않는다.")]
        [SerializeField] private EncounterComposition encounter;

        [Header("클리어 조건")]
        [SerializeField] private RoomClearOverride clearOverride = RoomClearOverride.UseRoomTypeDefault;

        [Tooltip("목표형일 때 달성해야 하는 목표 id (퍼즐 id 등). 비우면 id 대조 없이 아무 목표나 수용한다.")]
        [SerializeField] private string objectiveId = "";

        [Header("콘텐츠 id (느슨한 참조 — 각 시스템이 해석)")]
        [Tooltip("퍼즐 방일 때 퍼즐 id. RoomNode.PuzzleId로 전달된다.")]
        [SerializeField] private string puzzleId = "";

        [Tooltip("보스 방일 때 보스 id. 진단/로그용 — 실제 보스 데이터는 프리팹의 BossController가 들고 있다.")]
        [SerializeField] private string bossId = "";

        [Header("보상 (클리어 시 1회 지급)")]
        [SerializeField] private RoomRewardType rewardType = RoomRewardType.None;

        [Tooltip("ItemDrop일 때 획득 경로 — 드롭 테이블 필터에 사용된다.")]
        [SerializeField] private AcquisitionMethod rewardAcquisition = AcquisitionMethod.NormalMonster;

        [Tooltip("ItemDrop일 때 드롭 개수 / Gold일 때 골드량 / Heal일 때 회복량.")]
        [SerializeField, Min(0)] private int rewardAmount = 1; // TODO(밸런스): 문서 미정 — 방 보상량

        [Header("선택 굴림 가중치 (RoomCatalog가 같은 종류의 방 여러 개 중 하나를 고를 때)")]
        [SerializeField, Min(0)] private int selectionWeight = 1;

        [Tooltip("이 방이 등장 가능한 최소 스테이지 (1~15). 진행도 게이팅.")]
        [SerializeField, Min(1)] private int minStage = 1;

        // ── 조회 프로퍼티 ─────────────────────────────────────────
        public string RoomDefinitionId => string.IsNullOrEmpty(roomDefinitionId) ? name : roomDefinitionId;
        public RoomType RoomType => roomType;
        public bool AnyBiome => anyBiome;
        public BiomeType Biome => biome;
        public GameObject RoomPrefab => roomPrefab;
        public EncounterComposition Encounter => encounter;
        public RoomClearOverride ClearOverride => clearOverride;
        public string ObjectiveId => objectiveId;
        public string PuzzleId => puzzleId;
        public string BossId => bossId;
        public RoomRewardType RewardType => rewardType;
        public AcquisitionMethod RewardAcquisition => rewardAcquisition;
        public int RewardAmount => rewardAmount;
        public int SelectionWeight => selectionWeight;
        public int MinStage => minStage;

        /// <summary>이 방이 해당 군계/스테이지에서 선택 가능한지.</summary>
        public bool Matches(BiomeType targetBiome, int stageIndex)
            => (anyBiome || biome == targetBiome) && stageIndex >= minStage;

        /// <summary>
        /// 클리어 조건 덮어쓰기 값 → RoomClearType. UseRoomTypeDefault면 false를 반환한다
        /// (호출측은 RoomManager가 RoomType으로 구성한 기본 조건을 그대로 둔다).
        /// </summary>
        public bool TryResolveClearType(out RoomClearType clearType)
        {
            switch (clearOverride)
            {
                case RoomClearOverride.None: clearType = RoomClearType.None; return true;
                case RoomClearOverride.KillAllEnemies: clearType = RoomClearType.KillAllEnemies; return true;
                case RoomClearOverride.ObjectiveComplete: clearType = RoomClearType.ObjectiveComplete; return true;
                default: clearType = RoomClearType.None; return false;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 전투 방인데 적 구성이 비어 있으면 전멸형 조건이 즉시 충족되어 방이 그냥 열린다 — 의도 확인용 경고.
            bool combatRoom = roomType == RoomType.NormalCombat || roomType == RoomType.Elite;
            if (combatRoom && encounter == null && clearOverride == RoomClearOverride.UseRoomTypeDefault)
                Debug.LogWarning($"[RoomDefinition] '{name}': 전투 방인데 EncounterComposition이 비어 있습니다 (진입 즉시 클리어됨).", this);

            if (clearOverride == RoomClearOverride.ObjectiveComplete && string.IsNullOrEmpty(objectiveId))
                Debug.LogWarning($"[RoomDefinition] '{name}': 목표형으로 지정했지만 objectiveId가 비어 있습니다 (아무 목표나 수용).", this);
        }
#endif
    }
}
