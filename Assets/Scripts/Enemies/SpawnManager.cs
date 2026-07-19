// 근거: 적 시스템.md — 적은 지정된 생성 지점 또는 화면 밖에서 등장한다. 플레이어 근처에서 갑자기 생성되지 않는다.
// 스폰 판정은 호스트 권위로 처리하고 시드 난수를 사용해 전 클라이언트가 동일 결과를 얻는다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;

namespace TSWP.Enemies
{
    /// <summary>월드에 배치하는 적 생성 지점.</summary>
    public class SpawnPoint : MonoBehaviour
    {
        [Tooltip("화면 밖에서만 사용하는 지점인지 여부.")]
        public bool offscreenOnly;
    }

    /// <summary>
    /// 적 스폰 매니저. 지정 지점/화면 밖 규칙과 "플레이어 근처 갑툭튀 금지" 최소 거리 검사를 강제한다.
    /// </summary>
    public class SpawnManager : MonoBehaviour
    {
        public static SpawnManager Instance { get; private set; }

        [Header("스폰 규칙")]
        [Tooltip("플레이어로부터 이 거리 안에는 절대 스폰하지 않는다 (갑툭튀 금지).")]
        [SerializeField, Min(0f)] private float minDistanceFromPlayer = 6f; // TODO(밸런스): 문서 미정

        [Tooltip("화면 밖 판정에 사용할 카메라. 비우면 Camera.main.")]
        [SerializeField] private Camera viewCamera;

        [Header("생성 지점")]
        [Tooltip("씬에 배치된 생성 지점. 비어 있으면 자식 SpawnPoint를 자동 수집한다.")]
        [SerializeField] private List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

        private System.Random _rng;
        private readonly List<CombatEntity> _players = new List<CombatEntity>();
        private readonly List<SpawnPoint> _candidates = new List<SpawnPoint>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (spawnPoints.Count == 0)
                GetComponentsInChildren(true, spawnPoints);
        }

        /// <summary>RunManager 시드에서 파생한 난수를 주입한다 (멀티 동기화).</summary>
        public void InitializeRng(int seed) => _rng = new System.Random(seed);

        /// <summary>거리 판정을 위해 플레이어를 등록한다.</summary>
        public void RegisterPlayer(CombatEntity player)
        {
            if (player != null && !_players.Contains(player))
                _players.Add(player);
        }

        public void UnregisterPlayer(CombatEntity player) => _players.Remove(player);

        /// <summary>
        /// 조합(EncounterComposition) 단위 스폰 — 방 진입 시 호출한다.
        /// 역할군 조합은 데이터가 결정하고, 여기서는 배치 규칙만 강제한다.
        /// </summary>
        public void SpawnEncounter(EncounterComposition composition, Difficulty difficulty)
        {
            if (composition == null) return;

            var entries = composition.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.enemy == null) continue;

                for (int n = 0; n < entry.count; n++)
                    Spawn(entry.enemy, difficulty);
            }
        }

        /// <summary>적 1기 스폰. 유효한 지점이 없으면 스폰하지 않는다(규칙 위반 방지).</summary>
        public EnemyController Spawn(EnemyData data, Difficulty difficulty)
        {
            // SYNC: 호스트 권위 — 스폰 추첨·위치 결정은 호스트 전용, 클라이언트는 복제 수신.
            if (data == null || data.enemyPrefab == null)
            {
                Debug.LogWarning("[SpawnManager] EnemyData 또는 프리팹 미할당 — 스폰 생략", this);
                return null;
            }

            if (!TryPickSpawnPosition(out Vector2 position))
            {
                Debug.LogWarning("[SpawnManager] 규칙을 만족하는 생성 지점이 없어 스폰을 생략했습니다.", this);
                return null;
            }

            GameObject instance = Instantiate(data.enemyPrefab, position, Quaternion.identity);
            var controller = instance.GetComponent<EnemyController>();
            if (controller == null)
            {
                Debug.LogError($"[SpawnManager] '{data.name}' 프리팹에 EnemyController가 없습니다.", instance);
                return null;
            }

            controller.Initialize(data, difficulty, _rng ?? new System.Random());
            return controller;
        }

        /// <summary>지정 지점 중 "플레이어 최소 거리" 규칙을 만족하는 곳을 고른다.</summary>
        private bool TryPickSpawnPosition(out Vector2 position)
        {
            position = default;

            _candidates.Clear();
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                var point = spawnPoints[i];
                if (point == null) continue;

                Vector2 candidate = point.transform.position;

                // 플레이어 근처 갑툭튀 금지
                if (IsTooCloseToAnyPlayer(candidate)) continue;

                // 화면 밖 전용 지점은 실제로 화면 밖일 때만 사용
                if (point.offscreenOnly && IsVisible(candidate)) continue;

                _candidates.Add(point);
            }

            if (_candidates.Count == 0) return false;

            var rng = _rng ?? new System.Random();
            position = _candidates[rng.Next(_candidates.Count)].transform.position;
            return true;
        }

        private bool IsTooCloseToAnyPlayer(Vector2 candidate)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                var player = _players[i];
                if (player == null || player.IsDead) continue;
                if (Vector2.Distance(candidate, player.transform.position) < minDistanceFromPlayer)
                    return true;
            }
            return false;
        }

        private bool IsVisible(Vector2 worldPosition)
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
