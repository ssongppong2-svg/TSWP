// 근거: 맵 시스템.md — 랜덤 생성 방 + 통로 그래프, 맵당 생물 군계 1개, 시드 기반 재생성(멀티 동기화).
// 순수 C# — 씬과 무관. 호스트가 시드만 전송하면 모든 클라이언트가 동일 그래프를 재생성한다.
// SYNC: 호스트 권위 — 그래프 자체는 시드로 재생성, 탐험/클리어 상태만 동기화 대상.
using System.Collections.Generic;

namespace TSWP.Map
{
    /// <summary>
    /// 한 스테이지의 방 그래프(Branch &amp; Merge DAG). MapGenerator가 생성하고
    /// RoomManager가 상태(탐험/클리어)를 갱신한다.
    /// </summary>
    public sealed class MapGraph
    {
        /// <summary>절차 생성 시드 — 멀티플레이 동기화용.</summary>
        public readonly int Seed;
        /// <summary>현재 스테이지 (1~GameRules.TotalBossCount).</summary>
        public readonly int StageIndex;
        /// <summary>이 맵의 생물 군계 (맵당 1개).</summary>
        public readonly BiomeType Biome;

        public readonly List<RoomNode> Rooms = new List<RoomNode>();
        public readonly List<RoomConnection> Connections = new List<RoomConnection>();
        /// <summary>비밀방 목록 (위치 랜덤, 발견 전 미니맵 비표시).</summary>
        public readonly List<RoomNode> SecretRooms = new List<RoomNode>();

        public RoomNode StartRoom { get; private set; }
        public RoomNode BossRoom { get; private set; }

        private readonly Dictionary<int, RoomNode> _byId = new Dictionary<int, RoomNode>();

        public MapGraph(int seed, int stageIndex, BiomeType biome)
        {
            Seed = seed;
            StageIndex = stageIndex;
            Biome = biome;
        }

        /// <summary>방 추가. RoomId는 추가 순서로 부여 — 시드가 같으면 전 클라이언트에서 동일 id.</summary>
        public RoomNode AddRoom(RoomType type, int layer, int indexInLayer, int contentSeed)
        {
            var node = new RoomNode
            {
                RoomId = Rooms.Count,
                RoomType = type,
                Layer = layer,
                IndexInLayer = indexInLayer,
                ContentSeed = contentSeed,
                // 비밀방만 미발견 상태로 시작 — 발견 전 미니맵 비표시 (맵 시스템.md).
                IsDiscovered = type != RoomType.Secret,
            };
            Rooms.Add(node);
            _byId[node.RoomId] = node;

            if (type == RoomType.Secret) SecretRooms.Add(node);
            if (type == RoomType.Start) StartRoom = node;
            if (type == RoomType.Boss) BossRoom = node;
            return node;
        }

        /// <summary>통로 연결. from→to는 진행 방향(시작→보스). 이동 판정용 양방향 이웃도 함께 갱신.</summary>
        public void Connect(RoomNode from, RoomNode to)
        {
            if (from == null || to == null || from == to) return;
            if (from.NextRooms.Contains(to)) return; // 중복 간선 방지

            Connections.Add(new RoomConnection(from.RoomId, to.RoomId));
            from.NextRooms.Add(to);
            to.PrevRooms.Add(from);
            from.ConnectedRooms.Add(to);
            to.ConnectedRooms.Add(from);
        }

        public RoomNode GetRoom(int roomId)
            => _byId.TryGetValue(roomId, out var node) ? node : null;

        /// <summary>두 방이 통로로 직접 연결되어 있는지 (방향 무관 — 이동 가능 판정).</summary>
        public bool AreConnected(int roomIdA, int roomIdB)
        {
            var a = GetRoom(roomIdA);
            var b = GetRoom(roomIdB);
            return a != null && b != null && a.ConnectedRooms.Contains(b);
        }
    }
}
