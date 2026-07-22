// 근거: 적 시스템.md — 적은 지정된 생성 지점 또는 화면 밖에서 등장한다. 플레이어 근처에서 갑자기 생성되지 않는다.
// 스폰 판정은 호스트 권위로 처리하고 시드 난수를 사용해 전 클라이언트가 동일 결과를 얻는다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;

namespace TSWP.Enemies
{
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

        [Header("공용 몸통 프리팹")]
        // 근거: 적 시스템.md — 적 1종 = SO 에셋 1개. 적마다 프리팹을 만들면 그 원칙이 깨지므로
        //   몸통은 하나를 공유하고 외형/수치/행동은 EnemyData가 결정한다.
        [Tooltip("EnemyData.enemyPrefab이 비어 있을 때 사용할 공용 적 프리팹 (EnemyController+CombatEntity 보유).")]
        [SerializeField] private GameObject defaultEnemyPrefab;

        [Header("생성 지점")]
        [Tooltip("씬에 배치된 생성 지점. 비어 있으면 자식 SpawnPoint를 자동 수집한다. " +
                 "방 프리팹의 SpawnPoint는 스스로 등록하므로 여기에 넣을 필요가 없다.")]
        [SerializeField] private List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

        [Header("연동")]
        [Tooltip("스폰한 적을 RoomManager의 전멸형 클리어 카운트에 등록한다. 끄면 전투 방이 클리어되지 않는다.")]
        [SerializeField] private bool registerToRoomManager = true;

        [Tooltip("RegisterPlayer가 한 번도 호출되지 않았을 때 씬에서 플레이어를 자동 탐색한다(최소 거리 규칙 보호).")]
        [SerializeField] private bool autoDiscoverPlayers = true;

        [Tooltip("자동 탐색 재시도 간격(초). 매 스폰마다 씬을 훑지 않도록 캐시한다.")]
        [SerializeField, Min(0.1f)] private float playerDiscoveryInterval = 2f;

        private System.Random _rng;
        private readonly List<CombatEntity> _players = new List<CombatEntity>();
        private readonly List<SpawnPoint> _candidates = new List<SpawnPoint>();
        private float _nextPlayerDiscoveryAt;

        /// <summary>
        /// 런타임에 등장한 생성 지점(방 프리팹 인스턴스 등). SpawnPoint가 스스로 등록/해제하므로
        /// 방을 활성화하는 쪽(Map)이 SpawnManager를 알 필요가 없다.
        /// 매니저 인스턴스가 아니라 정적 목록인 이유: 방 프리팹이 매니저보다 먼저 켜질 수 있기 때문.
        /// </summary>
        private static readonly List<SpawnPoint> _runtimePoints = new List<SpawnPoint>();

        /// <summary>SpawnPoint가 활성화될 때 스스로 호출한다.</summary>
        public static void RegisterSpawnPoint(SpawnPoint point)
        {
            if (point != null && !_runtimePoints.Contains(point)) _runtimePoints.Add(point);
        }

        /// <summary>SpawnPoint가 비활성화/파괴될 때 스스로 호출한다.</summary>
        public static void UnregisterSpawnPoint(SpawnPoint point) => _runtimePoints.Remove(point);

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

        private void OnDestroy()
        {
            // 파괴된 싱글턴을 Instance에 남기면 `Instance?.` 호출이 C# null 검사를 통과해
            // MissingReferenceException을 던진다 (Unity의 == 오버로드를 ?. 가 우회하기 때문).
            if (Instance == this) Instance = null;
        }

        /// <summary>RunManager 시드에서 파생한 난수를 주입한다 (멀티 동기화).</summary>
        public void InitializeRng(int seed) => _rng = new System.Random(seed);

        /// <summary>
        /// 시드 난수 확보. 주입되지 않았으면 RunManager 시드에서 파생한다 —
        /// 전 클라이언트가 같은 시드를 공유하므로 스폰 위치 추첨 결과가 일치한다.
        /// RunManager조차 없으면(단독 테스트) 시간 기반 난수로 폴백한다.
        /// </summary>
        private System.Random GetRng()
        {
            if (_rng != null) return _rng;

            var run = RunManager.Instance;
            if (run != null)
            {
                // 드롭 추첨 등 다른 소비자와 수열이 겹치지 않도록 고정 상수로 파생시킨다.
                _rng = new System.Random(run.Seed ^ 0x5A317);
                return _rng;
            }

            // SYNC: 시드 미주입 상태 — 멀티에서는 결정성이 깨진다. 런 시작 시 InitializeRng를 호출할 것.
            _rng = new System.Random();
            return _rng;
        }

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

            // 등록 종료 통지 — 이게 없으면 RoomClearCondition.SpawnFinished가 영영 false로 남아
            // 전멸형 방이 클리어되지 않고 출구가 봉쇄된 채 소프트락이 된다 (감사 §3).
            if (!registerToRoomManager) return;
            var room = Map.RoomManager.Instance;
            if (room != null) room.FinishEnemyRegistration();
        }

        /// <summary>적 1기 스폰. 유효한 지점이 없으면 스폰하지 않는다(규칙 위반 방지).</summary>
        public EnemyController Spawn(EnemyData data, Difficulty difficulty)
        {
            // SYNC: 호스트 권위 — 스폰 추첨·위치 결정은 호스트 전용, 클라이언트는 복제 수신.
            if (data == null) return null;

            if (!TryPickSpawnPosition(out Vector2 position))
            {
                Debug.LogWarning("[SpawnManager] 규칙을 만족하는 생성 지점이 없어 스폰을 생략했습니다.", this);
                return null;
            }

            return SpawnAt(data, difficulty, position);
        }

        /// <summary>
        /// 위치를 지정한 스폰. 배치 규칙(최소 거리/화면 밖)은 호출측이 보장한 것으로 본다 —
        /// 방 진입 시 고정 배치, 보스전 소환 연출 등 '연출상 위치가 정해진' 경우에만 사용한다.
        /// </summary>
        public EnemyController SpawnAt(EnemyData data, Difficulty difficulty, Vector2 position)
        {
            if (data == null) return null;

            // 전용 프리팹이 없으면 공용 몸통을 쓴다 — 적 1종 추가에 프리팹 제작이 필요 없게 한다.
            GameObject prefab = data.enemyPrefab != null ? data.enemyPrefab : defaultEnemyPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[SpawnManager] '{data.name}': 전용 프리팹도 공용 프리팹(defaultEnemyPrefab)도 " +
                                 "없어 스폰을 생략했습니다.", this);
                return null;
            }

            GameObject instance = Instantiate(prefab, position, Quaternion.identity);
            var controller = instance.GetComponent<EnemyController>();
            if (controller == null)
            {
                // 잘못 만든 프리팹이 씬에 유령으로 남지 않게 즉시 정리한다.
                Debug.LogError($"[SpawnManager] '{data.name}' 프리팹에 EnemyController가 없습니다.", instance);
                Destroy(instance);
                return null;
            }

            controller.Initialize(data, difficulty, GetRng());

            // 전멸형 방 클리어 카운트에 등록 (RoomManager가 CombatEntity.Died를 구독해 집계).
            // 소환사(SummonerAbility)가 단발 Spawn을 호출해도 카운트에 포함되어야 방이 조기 클리어되지 않는다.
            if (registerToRoomManager && controller.Combat != null)
            {
                var room = Map.RoomManager.Instance;
                if (room != null) room.RegisterEnemy(controller.Combat);
            }

            return controller;
        }

        /// <summary>지정 지점 중 "플레이어 최소 거리" 규칙을 만족하는 곳을 고른다.</summary>
        private bool TryPickSpawnPosition(out Vector2 position)
        {
            position = default;

            EnsurePlayersKnown();

            // 파괴된 방의 지점이 정적 목록에 쌓이지 않도록 정리한다(씬 전환 누수 방지).
            for (int i = _runtimePoints.Count - 1; i >= 0; i--)
                if (_runtimePoints[i] == null) _runtimePoints.RemoveAt(i);

            _candidates.Clear();
            CollectCandidates(spawnPoints);
            CollectCandidates(_runtimePoints); // 방 프리팹이 들고 온 지점

            if (_candidates.Count == 0) return false;

            var rng = GetRng();
            position = _candidates[rng.Next(_candidates.Count)].transform.position;
            return true;
        }

        /// <summary>
        /// 배치 규칙을 통과한 지점만 후보에 담는다. 인스펙터 목록과 런타임 등록 목록에
        /// 같은 지점이 중복으로 들어와도 한 번만 담는다(추첨 확률 왜곡 방지).
        /// </summary>
        private void CollectCandidates(List<SpawnPoint> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                var point = source[i];
                if (point == null || !point.isActiveAndEnabled) continue;
                if (_candidates.Contains(point)) continue;

                Vector2 candidate = point.transform.position;

                // 플레이어 근처 갑툭튀 금지
                if (IsTooCloseToAnyPlayer(candidate)) continue;

                // 화면 밖 전용 지점은 실제로 화면 밖일 때만 사용
                if (point.offscreenOnly && IsVisible(candidate)) continue;

                _candidates.Add(point);
            }
        }

        /// <summary>
        /// 플레이어 목록 보증. RegisterPlayer가 호출되지 않은 씬에서는 목록이 비어
        /// "플레이어 근처 갑툭튀 금지" 규칙이 통째로 무효화된다 (감사 §12).
        /// 정식 배선은 플레이어 스폰 파이프라인이 RegisterPlayer를 부르는 것이고, 이 탐색은 안전망이다.
        /// </summary>
        private void EnsurePlayersKnown()
        {
            // 파괴된 항목 정리 (씬 전환/사망 파괴)
            for (int i = _players.Count - 1; i >= 0; i--)
                if (_players[i] == null) _players.RemoveAt(i);

            if (_players.Count > 0 || !autoDiscoverPlayers) return;
            if (Time.time < _nextPlayerDiscoveryAt) return;
            _nextPlayerDiscoveryAt = Time.time + playerDiscoveryInterval;

            // Unity 6: FindObjectOfType는 제거됨 — FindObjectsByType 사용.
            var entities = FindObjectsByType<CombatEntity>();
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == null || entity.Team != TeamType.Players) continue;
                if (!_players.Contains(entity)) _players.Add(entity);
            }
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
