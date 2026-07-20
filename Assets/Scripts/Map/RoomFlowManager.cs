// 근거: 맵 시스템.md / 방 시스템.md — 방 그래프(논리)를 씬 오브젝트(물리)에 연결하고 방 전환을 수행한다.
//   RoomManager(논리 상태 머신)는 수정하지 않는다. 이 컴포넌트는 GameEvents.RoomEntered/RoomCleared를
//   구독해 '물리 계층'만 담당한다 — 방 활성화/비활성화, 문 배선, 플레이어 재배치, 방 번호 노출.
// 방 번호는 UI가 조회할 수 있게 프로퍼티로 노출한다 (UI는 게임 로직을 직접 참조하지 않는다 — ARCHITECTURE.md §3-5).
// SYNC: 호스트 권위 — 이동 요청은 RoomManager.TryMoveToRoom 단일 지점을 거친다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;
using TSWP.Player;

namespace TSWP.Map
{
    /// <summary>
    /// 현재 방 추적 / 방 전환 / 방 번호 관리.
    /// 맵 그래프의 출처는 2가지 — 씬에 직접 배치한 방들(SceneAuthored) 또는 절차 생성(Procedural).
    /// 두 경우 모두 결과는 MapGraph 하나이며 RoomManager가 논리를 소유한다 (분기 없는 단일 흐름).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomFlowManager : MonoBehaviour
    {
        public static RoomFlowManager Instance { get; private set; }

        /// <summary>맵 그래프 출처.</summary>
        public enum MapSourceMode
        {
            SceneAuthored, // 씬에 배치된 RoomInstance 순서대로 선형 연결 (프로토타입 Forest 1맵)
            Procedural,    // MapGenerator의 Branch&Merge DAG (최종 — 맵 다수)
        }

        [Header("그래프 출처")]
        [SerializeField] private MapSourceMode mapSource = MapSourceMode.SceneAuthored;

        [Tooltip("비우면 RoomManager.Instance 또는 씬에서 자동 탐색한다.")]
        [SerializeField] private RoomManager roomManager;

        [Tooltip("Start()에서 자동으로 스테이지를 시작한다. 끄면 외부(부트스트랩)가 StartStage를 호출해야 한다.")]
        [SerializeField] private bool autoStartOnStart = true;

        [Tooltip("RunManager가 없을 때 사용할 시드 (단독 테스트용).")]
        [SerializeField] private int fallbackSeed = 20260719;

        [Header("SceneAuthored 모드")]
        [Tooltip("진행 순서대로 배치한 방들. 첫 번째 방은 RoomType.Start여야 한다.")]
        [SerializeField] private List<RoomInstance> authoredRooms = new List<RoomInstance>();

        [Header("Procedural 모드")]
        [SerializeField] private StagePlan stagePlan;
        [SerializeField] private RoomCatalog roomCatalog;
        [SerializeField] private BiomeType fallbackBiome = BiomeType.Forest;

        [Tooltip("런타임 생성한 방 프리팹의 부모. 비우면 이 오브젝트 밑에 붙인다.")]
        [SerializeField] private Transform roomRoot;

        [Header("플레이어 배치")]
        [Tooltip("방 진입 시 플레이어를 그 방의 시작 위치로 옮긴다.")]
        [SerializeField] private bool movePlayersToSpawnPoint = true;

        [Tooltip("여러 명이 겹치지 않도록 좌우로 벌리는 간격(월드 단위).")]
        [SerializeField, Min(0f)] private float playerSpreadSpacing = 1.2f; // TODO(밸런스): 문서 미정

        // ── 런타임 상태 ───────────────────────────────────────────
        private readonly Dictionary<int, RoomInstance> _instances = new Dictionary<int, RoomInstance>();
        private readonly Dictionary<int, RoomDefinition> _definitions = new Dictionary<int, RoomDefinition>();
        private readonly List<int> _doorTargets = new List<int>();
        private bool _started;

        /// <summary>현재 방 id (-1 = 없음). UI/미니맵 조회용.</summary>
        public int CurrentRoomId { get; private set; } = -1;

        /// <summary>현재 방 번호 (1부터 — UI 표시용). 방이 없으면 0.</summary>
        public int CurrentRoomNumber => CurrentRoomId >= 0 ? CurrentRoomId + 1 : 0;

        /// <summary>이 스테이지의 총 방 수 (UI "3 / 11" 표기용).</summary>
        public int TotalRoomCount => Graph != null ? Graph.Rooms.Count : 0;

        /// <summary>현재 활성 방의 씬 오브젝트 (물리 방이 없으면 null).</summary>
        public RoomInstance CurrentRoom { get; private set; }

        /// <summary>현재 방 종류 (방이 없으면 Start).</summary>
        public RoomType CurrentRoomType =>
            Graph != null && CurrentRoomId >= 0 && Graph.GetRoom(CurrentRoomId) != null
                ? Graph.GetRoom(CurrentRoomId).RoomType
                : RoomType.Start;

        /// <summary>현재 스테이지 그래프 (RoomManager 소유 그래프의 참조).</summary>
        public MapGraph Graph => roomManager != null ? roomManager.Graph : null;

        /// <summary>방 활성화 통지 (연출/카메라 훅).</summary>
        public event Action<RoomInstance> RoomActivated;

        /// <summary>방 클리어 통지 (연출/보상 훅).</summary>
        public event Action<RoomInstance> RoomClearedNotified;

        // ── 수명 주기 ─────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (roomManager == null) roomManager = RoomManager.Instance;
            if (roomManager == null) roomManager = FindFirstObjectByType<RoomManager>();
            if (roomRoot == null) roomRoot = transform;
        }

        private void OnDestroy()
        {
            // 파괴된 싱글턴이 Instance에 남으면 `Instance?.`가 C# null 검사를 통과해 예외가 난다.
            if (Instance == this) Instance = null;
        }

        private void OnEnable()
        {
            // SetGraph가 즉시 RoomEntered를 발행하므로 구독이 반드시 먼저여야 한다.
            GameEvents.RoomEntered += OnRoomEntered;
            GameEvents.RoomCleared += OnRoomCleared;
        }

        private void OnDisable()
        {
            GameEvents.RoomEntered -= OnRoomEntered;
            GameEvents.RoomCleared -= OnRoomCleared;
        }

        private void Start()
        {
            if (autoStartOnStart) StartStage();
        }

        // ── 스테이지 시작 ─────────────────────────────────────────
        /// <summary>맵 그래프를 구성해 RoomManager에 주입한다 (= 시작 방 진입).</summary>
        public void StartStage()
        {
            if (_started) return;
            if (roomManager == null)
            {
                Debug.LogError("[RoomFlowManager] RoomManager가 없습니다 — 방 흐름을 시작할 수 없습니다.", this);
                return;
            }

            MapGraph graph = mapSource == MapSourceMode.SceneAuthored
                ? BuildSceneAuthoredGraph()
                : BuildProceduralGraph();

            if (graph == null) return;
            if (graph.StartRoom == null)
            {
                Debug.LogError("[RoomFlowManager] 시작 방(RoomType.Start)이 없어 진입할 수 없습니다.", this);
                return;
            }

            _started = true;

            // 시작 전 모든 방을 꺼 둔다 (활성 방 1개 규칙).
            foreach (var pair in _instances)
                if (pair.Value != null) pair.Value.Deactivate();

            roomManager.SetGraph(graph); // → EnterRoom → GameEvents.RoomEntered → OnRoomEntered
        }

        /// <summary>씬에 배치된 방들을 진행 순서대로 선형 연결한 그래프를 만든다.</summary>
        private MapGraph BuildSceneAuthoredGraph()
        {
            if (authoredRooms.Count == 0)
            {
                Debug.LogError("[RoomFlowManager] SceneAuthored 모드인데 authoredRooms가 비어 있습니다.", this);
                return null;
            }

            int seed = RunManager.Instance != null ? RunManager.Instance.Seed : fallbackSeed;
            int stage = RunManager.Instance != null ? RunManager.Instance.CurrentStage : 1;
            BiomeType biome = ResolveBiome(stage);

            var graph = new MapGraph(seed, stage, biome);
            var rng = new System.Random(seed);

            _instances.Clear();
            _definitions.Clear();

            RoomNode previous = null;
            for (int i = 0; i < authoredRooms.Count; i++)
            {
                var instance = authoredRooms[i];
                if (instance == null)
                {
                    Debug.LogWarning($"[RoomFlowManager] authoredRooms[{i}]가 비어 있습니다 — 건너뜁니다.", this);
                    continue;
                }

                var node = graph.AddRoom(instance.RoomType, i, 0, rng.Next());
                ApplyContentIds(node, instance.Definition);

                _instances[node.RoomId] = instance;
                if (instance.Definition != null) _definitions[node.RoomId] = instance.Definition;

                if (previous != null) graph.Connect(previous, node);
                previous = node;
            }

            return graph;
        }

        /// <summary>절차 생성 그래프 + 카탈로그로 방 정의를 결정론적으로 배정한다.</summary>
        private MapGraph BuildProceduralGraph()
        {
            int seed = RunManager.Instance != null ? RunManager.Instance.Seed : fallbackSeed;
            int stage = RunManager.Instance != null ? RunManager.Instance.CurrentStage : 1;
            BiomeType biome = ResolveBiome(stage);

            var config = stagePlan != null ? stagePlan.generationConfig : null;
            MapGraph graph;
            try
            {
                graph = new MapGenerator().Generate(seed, stage, biome, config);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RoomFlowManager] 맵 생성 실패: {e.Message}", this);
                return null;
            }

            _instances.Clear();
            _definitions.Clear();

            if (roomCatalog == null)
            {
                Debug.LogWarning("[RoomFlowManager] RoomCatalog 미할당 — 물리 방 없이 논리만 진행합니다.", this);
                return graph;
            }

            for (int i = 0; i < graph.Rooms.Count; i++)
            {
                var node = graph.Rooms[i];
                var definition = roomCatalog.Pick(node.RoomType, biome, stage, node.ContentSeed);
                if (definition == null) continue;

                _definitions[node.RoomId] = definition;
                ApplyContentIds(node, definition);
            }
            return graph;
        }

        /// <summary>RoomDefinition의 느슨한 콘텐츠 id를 논리 노드에 반영한다(퍼즐 목표 대조 등).</summary>
        private static void ApplyContentIds(RoomNode node, RoomDefinition definition)
        {
            if (node == null || definition == null) return;
            if (!string.IsNullOrEmpty(definition.PuzzleId)) node.PuzzleId = definition.PuzzleId;
            if (definition.Encounter != null) node.EncounterId = definition.Encounter.name;
        }

        private BiomeType ResolveBiome(int stageIndex)
            => stagePlan != null ? stagePlan.GetBiome(stageIndex) : fallbackBiome;

        // ── 스테이지 전환 ─────────────────────────────────────────
        /// <summary>
        /// 다음 스테이지로 넘어갈 때 호출한다 (맵 다수 확장 지점).
        /// 런타임 생성한 방을 정리하고 새 그래프로 다시 시작한다 — 코드 수정 없이 스테이지가 늘어난다.
        /// </summary>
        public void StartNextStage()
        {
            if (CurrentRoom != null) CurrentRoom.Deactivate();
            CurrentRoom = null;
            CurrentRoomId = -1;

            // 절차 생성으로 만든 방만 파괴한다(씬에 직접 배치한 방은 그대로 둔다).
            if (mapSource == MapSourceMode.Procedural)
            {
                foreach (var pair in _instances)
                    if (pair.Value != null) Destroy(pair.Value.gameObject);
            }
            _instances.Clear();
            _definitions.Clear();

            _started = false;
            StartStage();
        }

        // ── 이동 요청 ─────────────────────────────────────────────
        /// <summary>방 이동 요청 (RoomDoor가 호출). 판정은 RoomManager가 소유한다.</summary>
        public bool RequestMove(int roomId)
        {
            if (roomManager == null) return false;
            return roomManager.TryMoveToRoom(roomId);
        }

        /// <summary>현재 방에서 진행 방향으로 이어진 첫 번째 방으로 이동한다 (디버그/단순 선형 맵용).</summary>
        public bool RequestMoveToNext()
        {
            var node = Graph != null && CurrentRoomId >= 0 ? Graph.GetRoom(CurrentRoomId) : null;
            if (node == null) return false;
            for (int i = 0; i < node.NextRooms.Count; i++)
            {
                var next = node.NextRooms[i];
                if (next.IsSecret && !next.IsDiscovered) continue;
                if (RequestMove(next.RoomId)) return true;
            }
            return false;
        }

        // ── GameEvents 구독 ───────────────────────────────────────
        private void OnRoomEntered(int roomId)
        {
            var node = Graph != null ? Graph.GetRoom(roomId) : null;

            // 이전 방 정리 (활성 방은 항상 1개)
            if (CurrentRoom != null) CurrentRoom.Deactivate();

            CurrentRoomId = roomId;
            CurrentRoom = ResolveInstance(roomId, node);

            if (CurrentRoom == null)
            {
                // 물리 방이 없어도 논리 진행은 계속되어야 한다 (연출 없이 조용히 생략 — 설계 원칙).
                return;
            }

            CurrentRoom.Bind(roomId, node);
            WireDoors(CurrentRoom, node);

            // 플레이어 배치가 콘텐츠 시작보다 먼저여야 한다 —
            // 스폰의 "플레이어 최소 거리" 규칙이 옛 위치를 기준으로 계산되면 갑툭튀가 발생한다.
            PlacePlayers(CurrentRoom);
            CurrentRoom.Activate();          // 내부에서 적/퍼즐/보스 시작

            RoomActivated?.Invoke(CurrentRoom);
        }

        private void OnRoomCleared(int roomId)
        {
            if (CurrentRoom == null || CurrentRoom.RoomId != roomId) return;
            CurrentRoom.NotifyCleared();     // 문 개방 + 보상
            RoomClearedNotified?.Invoke(CurrentRoom);
        }

        // ── 방 인스턴스 해석 ──────────────────────────────────────
        /// <summary>방 id에 해당하는 씬 오브젝트를 얻는다. 없으면 RoomDefinition의 프리팹으로 1회 생성한다.</summary>
        private RoomInstance ResolveInstance(int roomId, RoomNode node)
        {
            if (_instances.TryGetValue(roomId, out var cached) && cached != null)
                return cached;

            if (!_definitions.TryGetValue(roomId, out var definition) || definition == null) return null;
            if (definition.RoomPrefab == null) return null;

            var go = Instantiate(definition.RoomPrefab, roomRoot);
            go.name = $"Room_{roomId:00}_{definition.RoomType}";
            var instance = go.GetComponent<RoomInstance>();
            if (instance == null)
            {
                Debug.LogError($"[RoomFlowManager] '{definition.name}'의 프리팹 루트에 RoomInstance가 없습니다.", go);
                Destroy(go);
                return null;
            }

            _instances[roomId] = instance;
            return instance;
        }

        // ── 문 배선 ───────────────────────────────────────────────
        /// <summary>
        /// 방의 문에 목적지 방 id를 배정한다. 진행 방향(NextRooms) 우선, 남으면 되돌아가는 문(PrevRooms).
        /// 인스펙터에서 목적지를 수동 지정한 문은 건드리지 않는다.
        /// </summary>
        private void WireDoors(RoomInstance instance, RoomNode node)
        {
            var doors = instance.Doors;
            if (doors == null || doors.Count == 0) return;

            _doorTargets.Clear();
            if (node != null)
            {
                for (int i = 0; i < node.NextRooms.Count; i++)
                {
                    var next = node.NextRooms[i];
                    if (next.IsSecret && !next.IsDiscovered) continue; // 미발견 비밀방은 문을 노출하지 않는다
                    _doorTargets.Add(next.RoomId);
                }
                for (int i = 0; i < node.PrevRooms.Count; i++)
                    _doorTargets.Add(node.PrevRooms[i].RoomId);
            }

            int targetIndex = 0;
            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                if (door == null || door.HasExplicitTarget) continue;

                if (targetIndex < _doorTargets.Count)
                {
                    door.ForceTarget(_doorTargets[targetIndex++]);
                }
                else
                {
                    door.ForceTarget(-1); // 갈 곳 없는 문 (보스 방 등) — 열려도 이동하지 않는다
                }
            }
        }

        // ── 플레이어 재배치 ───────────────────────────────────────
        private void PlacePlayers(RoomInstance instance)
        {
            if (!movePlayersToSpawnPoint) return;
            Transform spawn = instance.PlayerSpawnPoint;
            if (spawn == null) return;

            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player == null) continue;

                // 여러 명이 한 점에 겹치면 물리가 서로 밀어내며 튄다 — 좌우로 벌린다.
                float offset = (i - (players.Length - 1) * 0.5f) * playerSpreadSpacing;
                Vector3 position = spawn.position + new Vector3(offset, 0f, 0f);

                var body = player.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    // Unity 6: Rigidbody2D.velocity는 제거됨 — linearVelocity 사용.
                    body.position = position;
                    body.linearVelocity = Vector2.zero;
                }
                player.transform.position = position;
            }
        }
    }
}
