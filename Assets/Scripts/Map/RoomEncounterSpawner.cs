// 근거: 적 시스템.md — "적은 지정된 생성 지점 또는 화면 밖에서 등장한다. 플레이어 근처에서 갑자기 생성되지 않는다."
//   Enemies.SpawnManager는 씬 싱글턴이라 자기 자식 SpawnPoint만 Awake에 1회 수집한다 →
//   런타임에 인스턴스화되는 '방 프리팹'의 생성 지점은 볼 수 없다.
//   그래서 방 범위(room-scoped) 스폰만 담당하는 이 유틸을 둔다. 규칙(최소 거리/화면 밖/등록)은 동일하게 지킨다.
// 이 클래스는 '방 안에 조우를 배치하는 것' 한 가지만 한다 (ARCHITECTURE.md §3 — 한 클래스 한 책임).
// SYNC: 호스트 권위 — 위치 추첨은 시드 난수(System.Random)로 전 클라이언트 동일 결과.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;
using TSWP.Enemies;

namespace TSWP.Map
{
    /// <summary>방 하나의 조우를 그 방의 생성 지점에만 배치하는 스폰 유틸 (정적, 상태 없음).</summary>
    public static class RoomEncounterSpawner
    {
        // 후보 필터링용 재사용 버퍼 — 메인 스레드 단일 호출이라 정적 재사용이 안전하다.
        private static readonly List<SpawnPoint> Candidates = new List<SpawnPoint>();
        private static readonly List<CombatEntity> Players = new List<CombatEntity>();

        /// <summary>
        /// 조우 1건을 배치한다. 스폰된 적은 RoomManager의 전멸형 카운트에 등록되며,
        /// 마지막에 FinishEnemyRegistration으로 등록 종료를 통지한다(조기 클리어/소프트락 방지).
        /// </summary>
        /// <param name="spawned">스폰된 적을 담을 목록(선택). null이면 수집하지 않는다.</param>
        /// <returns>실제로 스폰된 적 수.</returns>
        public static int SpawnEncounter(
            EncounterComposition composition,
            IReadOnlyList<SpawnPoint> points,
            Difficulty difficulty,
            System.Random rng,
            float minDistanceFromPlayer,
            Camera viewCamera,
            List<EnemyController> spawned = null)
        {
            var room = RoomManager.Instance;
            int count = 0;

            if (composition != null && (points == null || points.Count == 0))
            {
                // 조우는 있는데 생성 지점이 없으면 적이 0기 스폰되고 전투 방이 즉시 열린다 — 조용히 넘기면 안 되는 배선 실수.
                Debug.LogWarning($"[RoomEncounterSpawner] 조우 '{composition.name}'에 사용할 SpawnPoint가 없습니다 — 적이 생성되지 않습니다.");
            }

            if (composition != null && points != null && points.Count > 0)
            {
                RefreshPlayers();
                var entries = composition.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (entry == null || entry.enemy == null) continue;

                    for (int n = 0; n < entry.count; n++)
                    {
                        var enemy = SpawnOne(entry.enemy, points, difficulty, rng, minDistanceFromPlayer, viewCamera, room);
                        if (enemy == null) continue;
                        spawned?.Add(enemy);
                        count++;
                    }
                }
            }

            // 등록 종료 통지 — 이게 빠지면 SpawnFinished가 영영 false로 남아 전투 방이 클리어되지 않는다.
            // 스폰이 0기여도 반드시 호출해야 빈 전투 방이 즉시 열린다.
            if (room != null) room.FinishEnemyRegistration();
            return count;
        }

        /// <summary>
        /// 적 1기 스폰. CombatEntity 하나를 전멸형 카운트에 등록하는 용도로도 쓸 수 있도록
        /// 등록 책임은 RegisterForClear로 분리해 둔다.
        /// </summary>
        private static EnemyController SpawnOne(
            EnemyData data,
            IReadOnlyList<SpawnPoint> points,
            Difficulty difficulty,
            System.Random rng,
            float minDistanceFromPlayer,
            Camera viewCamera,
            RoomManager room)
        {
            if (data == null || data.enemyPrefab == null)
            {
                Debug.LogWarning($"[RoomEncounterSpawner] EnemyData 또는 enemyPrefab 미할당 — 스폰 생략 ({(data != null ? data.name : "null")})");
                return null;
            }

            if (!TryPickPosition(points, minDistanceFromPlayer, viewCamera, rng, out Vector2 position))
            {
                Debug.LogWarning("[RoomEncounterSpawner] 규칙(최소 거리/화면 밖)을 만족하는 생성 지점이 없어 스폰을 생략했습니다.");
                return null;
            }

            GameObject instance = Object.Instantiate(data.enemyPrefab, position, Quaternion.identity);
            var controller = instance.GetComponent<EnemyController>();
            if (controller == null)
            {
                Debug.LogError($"[RoomEncounterSpawner] '{data.name}' 프리팹에 EnemyController가 없습니다.", instance);
                Object.Destroy(instance);
                return null;
            }

            controller.Initialize(data, difficulty, rng);
            RegisterForClear(room, controller.Combat);
            return controller;
        }

        /// <summary>
        /// 전멸형 클리어 카운트에 CombatEntity를 등록한다.
        /// 보스처럼 SpawnManager를 거치지 않는 적도 이 경로로 등록해야 보스 방이 클리어된다.
        /// </summary>
        public static void RegisterForClear(RoomManager room, CombatEntity entity)
        {
            if (room == null || entity == null) return;
            room.RegisterEnemy(entity);
        }

        // ── 배치 규칙 ─────────────────────────────────────────────
        private static bool TryPickPosition(
            IReadOnlyList<SpawnPoint> points,
            float minDistanceFromPlayer,
            Camera viewCamera,
            System.Random rng,
            out Vector2 position)
        {
            position = default;
            Candidates.Clear();

            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point == null) continue;

                Vector2 candidate = point.transform.position;

                // 플레이어 근처 갑툭튀 금지
                if (IsTooCloseToAnyPlayer(candidate, minDistanceFromPlayer)) continue;

                // 화면 밖 전용 지점은 실제로 화면 밖일 때만 사용
                if (point.offscreenOnly && IsVisible(candidate, viewCamera)) continue;

                Candidates.Add(point);
            }

            if (Candidates.Count == 0) return false;

            int index = rng != null ? rng.Next(Candidates.Count) : 0;
            position = Candidates[index].transform.position;
            return true;
        }

        /// <summary>플레이어 진영 CombatEntity 수집 (최소 거리 규칙 보호용).</summary>
        private static void RefreshPlayers()
        {
            Players.Clear();
            // Unity 6: FindObjectOfType는 제거됨 — FindObjectsByType 사용.
            var entities = Object.FindObjectsByType<CombatEntity>();
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == null || entity.IsDead) continue;
                if (entity.Team != TeamType.Players) continue;
                Players.Add(entity);
            }
        }

        private static bool IsTooCloseToAnyPlayer(Vector2 candidate, float minDistance)
        {
            if (minDistance <= 0f) return false;
            for (int i = 0; i < Players.Count; i++)
            {
                var player = Players[i];
                if (player == null || player.IsDead) continue;
                if (Vector2.Distance(candidate, player.transform.position) < minDistance) return true;
            }
            return false;
        }

        private static bool IsVisible(Vector2 worldPosition, Camera viewCamera)
        {
            Camera cam = viewCamera != null ? viewCamera : Camera.main;
            if (cam == null) return false;

            Vector3 viewport = cam.WorldToViewportPoint(worldPosition);
            return viewport.z > 0f
                   && viewport.x >= 0f && viewport.x <= 1f
                   && viewport.y >= 0f && viewport.y <= 1f;
        }
    }
}
