// 근거: 방 시스템.md — '분기 후 합류(Branch & Merge)' 구조.
//   예시 흐름: 시작 → (전투|퍼즐 분기) → 이벤트(합류) → (상점|전투 분기) → 엘리트(합류)
//              → 휴식 → 보스 기믹 연습 → 보스 → 다음 스테이지.
// 불변식: ① 모든 경로는 결국 보스로 이어진다 ② 플레이어는 항상 하나 이상의 선택지(분기)를 가진다.
// 순수 C# + System.Random 주입 — 시드 결정론(호스트가 시드만 전송하면 전 클라이언트 동일 맵 재생성).
// SYNC: 호스트 권위 — 시드/스테이지만 동기화, 그래프는 각 클라이언트가 로컬 재생성.
using System;
using System.Collections.Generic;

namespace TSWP.Map
{
    /// <summary>
    /// 맵 생성 튜닝 파라미터. 문서에 수치 미정 — 전부 TODO(밸런스).
    /// [Serializable]이므로 StagePlan/RoomManager 인스펙터에서 노출해 조정 가능.
    /// </summary>
    [Serializable]
    public sealed class MapGenerationConfig
    {
        // TODO(밸런스): 문서 미정 — 분기→합류 구간(세그먼트) 수. 예시 흐름은 2구간.
        public int minBranchSegments = 2;
        public int maxBranchSegments = 4;

        // TODO(밸런스): 문서 미정 — 분기 레이어 폭(동시 선택지 수). 최소 2 = "항상 하나 이상의 선택지".
        public int minBranchWidth = 2;
        public int maxBranchWidth = 3;

        // TODO(밸런스): 문서 미정 — 분기 방 종류 가중치. NormalCombat이 '가장 많이 등장'해야 한다.
        public int normalCombatWeight = 5;
        public int puzzleWeight = 2;
        public int eventWeight = 2;
        public int shopWeight = 1;
        public int restWeight = 1;

        // TODO(밸런스): 문서 미정 — 비밀방 등장 확률/최대 개수 ("일부 맵에는 비밀방이 존재").
        public float secretRoomChance = 0.5f;
        public int maxSecretRooms = 2;
    }

    /// <summary>
    /// Branch &amp; Merge 레이어 DAG 생성기 (순수 C#, 씬 무관).
    /// 구조: 시작(1) → [분기(2~3폭) → 합류(1)] × N → 휴식 → 보스 기믹 연습 → 보스.
    /// 구성상 불변식이 보장되지만, 방어적으로 ValidateInvariants로 재검증한다 (유닛 테스트 권장 지점).
    /// </summary>
    public sealed class MapGenerator
    {
        /// <summary>시드 편의 오버로드. 내부에서 System.Random(seed)을 만들어 주입한다.</summary>
        public MapGraph Generate(int seed, int stageIndex, BiomeType biome, MapGenerationConfig config = null)
            => Generate(new Random(seed), seed, stageIndex, biome, config);

        /// <summary>
        /// 맵 그래프 생성. rng는 주입(결정론) — 같은 시드·같은 설정이면 항상 같은 그래프.
        /// </summary>
        public MapGraph Generate(Random rng, int seed, int stageIndex, BiomeType biome, MapGenerationConfig config = null)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            config ??= new MapGenerationConfig();

            var graph = new MapGraph(seed, stageIndex, biome);
            int layer = 0;

            // ── ① 시작 방 ────────────────────────────────────────
            var start = graph.AddRoom(RoomType.Start, layer++, 0, rng.Next());
            var previousLayer = new List<RoomNode> { start };

            // ── ② 분기 → 합류 반복 구간 ──────────────────────────
            int segments = rng.Next(config.minBranchSegments, config.maxBranchSegments + 1);
            for (int s = 0; s < segments; s++)
            {
                // 분기 레이어: 폭 2~3 — "플레이어는 항상 하나 이상의 선택지를 가진다".
                int width = rng.Next(Math.Max(2, config.minBranchWidth), config.maxBranchWidth + 1);
                var branchRooms = new List<RoomNode>(width);
                for (int i = 0; i < width; i++)
                {
                    // 슬롯 0은 항상 일반 전투 — NormalCombat이 '가장 많이 등장하는 방' 보장
                    // + 모든 분기에서 전투(파밍/성장) 선택지 제공.
                    var type = i == 0 ? RoomType.NormalCombat : RollBranchRoomType(rng, config);
                    var room = graph.AddRoom(type, layer, i, rng.Next());
                    foreach (var prev in previousLayer)
                        graph.Connect(prev, room);
                    branchRooms.Add(room);
                }
                layer++;

                // 합류 레이어: 1개 방으로 수렴. 첫 합류=이벤트, 마지막 합류=엘리트 (예시 흐름 반영).
                var mergeType = s == segments - 1
                    ? RoomType.Elite
                    : (s == 0 ? RoomType.Event : RollMergeRoomType(rng));
                var merge = graph.AddRoom(mergeType, layer, 0, rng.Next());
                foreach (var branch in branchRooms)
                    graph.Connect(branch, merge);
                layer++;

                previousLayer.Clear();
                previousLayer.Add(merge);
            }

            // ── ③ 고정 후미: 휴식 → 보스 기믹 연습 → 보스 ─────────
            var rest = graph.AddRoom(RoomType.Rest, layer++, 0, rng.Next());
            graph.Connect(previousLayer[0], rest);

            var practice = graph.AddRoom(RoomType.BossPractice, layer++, 0, rng.Next());
            graph.Connect(rest, practice); // 보스전 직전 고정 배치 (방 시스템.md)

            var boss = graph.AddRoom(RoomType.Boss, layer, 0, rng.Next());
            graph.Connect(practice, boss);

            // ── ④ 비밀방: 중간 방에 막다른 곁가지로 부착 (위치 랜덤) ──
            AttachSecretRooms(graph, rng, config);

            // ── ⑤ 불변식 재검증 (구성상 보장되지만 방어적으로) ─────
            var errors = new List<string>();
            if (!ValidateInvariants(graph, errors))
                throw new InvalidOperationException("맵 불변식 위반: " + string.Join(" / ", errors));

            return graph;
        }

        // ── 방 종류 굴림 ─────────────────────────────────────────
        /// <summary>분기 레이어 방 종류 가중 굴림. NormalCombat 가중치가 가장 높다.</summary>
        private static RoomType RollBranchRoomType(Random rng, MapGenerationConfig c)
        {
            int total = c.normalCombatWeight + c.puzzleWeight + c.eventWeight + c.shopWeight + c.restWeight;
            int roll = rng.Next(total);
            if ((roll -= c.normalCombatWeight) < 0) return RoomType.NormalCombat;
            if ((roll -= c.puzzleWeight) < 0) return RoomType.Puzzle;
            if ((roll -= c.eventWeight) < 0) return RoomType.Event;
            if ((roll -= c.shopWeight) < 0) return RoomType.Shop;
            return RoomType.Rest;
        }

        /// <summary>중간 합류 레이어 방 종류 (첫 합류=Event, 마지막=Elite는 호출측 고정).</summary>
        private static RoomType RollMergeRoomType(Random rng)
            => rng.Next(2) == 0 ? RoomType.Event : RoomType.NormalCombat;

        // ── 비밀방 부착 ──────────────────────────────────────────
        /// <summary>
        /// 비밀방을 중간 방(시작/휴식/연습/보스 제외)에 막다른 곁가지로 부착한다.
        /// 막다른 방이므로 '모든 경로 보스 도달' 불변식 대상에서 제외된다 (검증도 비밀방 제외).
        /// </summary>
        private static void AttachSecretRooms(MapGraph graph, Random rng, MapGenerationConfig config)
        {
            if (rng.NextDouble() >= config.secretRoomChance) return; // "일부 맵에는" 비밀방 존재

            var hosts = new List<RoomNode>();
            foreach (var room in graph.Rooms)
            {
                if (room.RoomType == RoomType.Start || room.RoomType == RoomType.Boss ||
                    room.RoomType == RoomType.BossPractice || room.RoomType == RoomType.Rest ||
                    room.RoomType == RoomType.Secret)
                    continue;
                hosts.Add(room);
            }
            if (hosts.Count == 0) return;

            int count = rng.Next(1, Math.Max(1, config.maxSecretRooms) + 1);
            for (int i = 0; i < count && hosts.Count > 0; i++)
            {
                int hostIndex = rng.Next(hosts.Count);
                var host = hosts[hostIndex];
                hosts.RemoveAt(hostIndex); // 한 방에 비밀방 1개까지

                var secret = graph.AddRoom(RoomType.Secret, host.Layer, host.IndexInLayer + 100, rng.Next());
                graph.Connect(host, secret); // 진행 간선이지만 막다른 곁가지 — 검증에서 제외
            }
        }

        // ── 불변식 검증 ──────────────────────────────────────────
        /// <summary>
        /// 불변식 ①: 모든 (비밀방 제외) 방에서 진행 방향으로 따라가면 반드시 보스에 도달한다.
        /// 보스에서 역방향 BFS로 도달 불가능한 비(非)비밀 방이 있으면 실패 (막다른 방 검출 포함).
        /// </summary>
        public bool ValidateAllPathsReachBoss(MapGraph graph)
        {
            if (graph?.BossRoom == null || graph.StartRoom == null) return false;

            // 보스에서 PrevRooms를 따라 역탐색 — 도달한 방 = 보스로 갈 수 있는 방.
            var canReachBoss = new HashSet<int>();
            var queue = new Queue<RoomNode>();
            queue.Enqueue(graph.BossRoom);
            canReachBoss.Add(graph.BossRoom.RoomId);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var prev in node.PrevRooms)
                {
                    if (canReachBoss.Add(prev.RoomId))
                        queue.Enqueue(prev);
                }
            }

            foreach (var room in graph.Rooms)
            {
                if (room.IsSecret) continue; // 비밀방은 의도된 막다른 곁가지
                if (!canReachBoss.Contains(room.RoomId)) return false;
            }
            return true;
        }

        /// <summary>불변식 ②: 분기(≥2 진행 선택지, 비밀방 제외)가 최소 1곳 존재 — "항상 하나 이상의 선택지".</summary>
        public bool ValidateHasBranch(MapGraph graph)
        {
            if (graph == null) return false;
            foreach (var room in graph.Rooms)
            {
                int forwardChoices = 0;
                foreach (var next in room.NextRooms)
                {
                    if (!next.IsSecret) forwardChoices++;
                }
                if (forwardChoices >= 2) return true;
            }
            return false;
        }

        /// <summary>전체 불변식 검증. 실패 사유를 errors에 누적한다 (유닛 테스트 진입점).</summary>
        public bool ValidateInvariants(MapGraph graph, List<string> errors = null)
        {
            bool ok = true;
            if (graph?.StartRoom == null) { errors?.Add("시작 방 없음"); ok = false; }
            if (graph?.BossRoom == null) { errors?.Add("보스 방 없음"); ok = false; }
            if (ok && !ValidateAllPathsReachBoss(graph)) { errors?.Add("보스에 도달하지 못하는 경로 존재"); ok = false; }
            if (ok && !ValidateHasBranch(graph)) { errors?.Add("분기(선택지) 없음"); ok = false; }
            return ok;
        }
    }
}
