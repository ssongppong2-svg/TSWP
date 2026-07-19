// 근거: 아이템 시스템.md — 아이템 드롭은 무작위(Random), 보스 처치 시 3~4개 드롭(GameRules.RollBossDropCount),
//       시작 아이템(게임 시작과 선택, 직업, 플레이.md: floor(인원×3/5)개 자유 경쟁 — GameRules.GetStartItemCount).
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Items
{
    /// <summary>아이템 드롭·픽업 판정 매니저. 드롭 추첨은 LootTable에 위임하고,
    /// 픽업 승인(선착순 선점)은 여기 단일 지점으로 모은다.</summary>
    public class ItemDropManager : MonoBehaviour
    {
        public static ItemDropManager Instance { get; private set; }

        [SerializeField] private LootTable lootTable;
        [SerializeField] private DroppedItem droppedItemPrefab;

        /// <summary>드롭 산개 반경. // TODO(밸런스): 문서 미정</summary>
        [SerializeField] private float dropScatterRadius = 1.5f;

        /// <summary>playerId → 장비 해석 레지스트리 (PlayerEquipment.Initialize에서 등록).</summary>
        private readonly Dictionary<int, PlayerEquipment> _players = new();

        // SYNC: 호스트 권위, 추후 NGO NetworkVariable — 드롭 추첨·스폰은 호스트만 수행 (시드 결정성 유지).
        private System.Random _rng = new System.Random();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>런 시드로 추첨 RNG 초기화. TODO: RunManager가 런 시작 시 시드를 주입하도록 연결.</summary>
        public void InitializeRng(int seed) => _rng = new System.Random(seed);

        // ── 플레이어 레지스트리 ───────────────────────────────────

        public void RegisterPlayer(PlayerEquipment equipment)
        {
            if (equipment == null) return;
            _players[equipment.PlayerId] = equipment;
        }

        public void UnregisterPlayer(int playerId) => _players.Remove(playerId);

        // ── 드롭 스폰 ─────────────────────────────────────────────

        /// <summary>시작 아이템 드롭 — floor(인원×3/5)개, 자유 경쟁 (GameFlowState.StartItemDrop 단계에서 호출).</summary>
        public void SpawnStartItems(int playerCount, Vector2 origin)
        {
            int count = GameRules.GetStartItemCount(playerCount);
            for (int i = 0; i < count; i++)
                SpawnDrop(AcquisitionMethod.StartingItem, ScatterAround(origin));
        }

        /// <summary>보스 처치 보상 — 3~4개 공용 드롭, 먼저 집는 플레이어가 소유자.</summary>
        public void SpawnBossDrops(Vector2 origin)
        {
            int count = GameRules.RollBossDropCount(_rng);
            for (int i = 0; i < count; i++)
                SpawnDrop(AcquisitionMethod.Boss, ScatterAround(origin));
        }

        /// <summary>획득 경로별 무작위 드롭 1개 스폰 (일반/엘리트 몬스터, 이벤트, 비밀방, 특수 오브젝트 등에서 호출).</summary>
        public DroppedItem SpawnDrop(AcquisitionMethod source, Vector2 position)
        {
            // SYNC: 호스트 권위 — 추첨과 스폰은 호스트 전용, 클라이언트는 스폰 복제만 수신.
            if (lootTable == null || droppedItemPrefab == null)
            {
                Debug.LogWarning("[ItemDropManager] lootTable 또는 droppedItemPrefab 미할당 — 드롭 생략", this);
                return null;
            }

            ItemDefinition definition = lootTable.Roll(source, _rng);
            if (definition == null) return null;

            return SpawnDrop(definition, position);
        }

        /// <summary>지정한 아이템 1개를 월드에 드롭한다 (적의 DropTable처럼 외부에서 이미 추첨을 마친 경우).
        /// // SYNC: 호스트 권위 — 스폰은 호스트 전용, 클라이언트는 복제만 수신.</summary>
        public DroppedItem SpawnDrop(ItemDefinition definition, Vector2 position)
        {
            if (definition == null || droppedItemPrefab == null)
            {
                Debug.LogWarning("[ItemDropManager] definition 또는 droppedItemPrefab 미할당 — 드롭 생략", this);
                return null;
            }

            DroppedItem drop = Instantiate(droppedItemPrefab, position, Quaternion.identity);
            drop.Initialize(definition);
            return drop;
        }

        // ── 픽업 승인 ─────────────────────────────────────────────

        /// <summary>DroppedItem.Pickup에서만 호출되는 픽업 승인 지점.
        /// // SYNC: 호스트 권위 판정 예정 — 선착순 선점의 동시 획득 레이스는 여기서 해소한다.</summary>
        public bool ResolvePickup(DroppedItem drop, int playerId)
        {
            if (drop == null || drop.Item == null) return false;
            if (!_players.TryGetValue(playerId, out PlayerEquipment equipment) || equipment == null)
            {
                Debug.LogWarning($"[ItemDropManager] 미등록 playerId={playerId}의 픽업 요청 — 기각", this);
                return false;
            }
            return equipment.Equip(drop.Item);
        }

        private Vector2 ScatterAround(Vector2 origin)
        {
            // 결정성 유지를 위해 UnityEngine.Random 대신 시드 주입된 System.Random 사용.
            float angle = (float)(_rng.NextDouble() * Mathf.PI * 2.0);
            float radius = (float)(_rng.NextDouble()) * dropScatterRadius;
            return origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }
    }
}
