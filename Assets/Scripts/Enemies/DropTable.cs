// 근거: 적 시스템.md — 적 처치 시 골드/회복 아이템/소비 아이템/장비를 드롭할 수 있다.
//       엘리트와 미니 보스는 희귀 아이템 확률이 더 높다. (구체 확률 수치는 문서에 없음 — 밸런스 필드로 개방)
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Items;

namespace TSWP.Enemies
{
    /// <summary>
    /// 적 드롭 테이블 SO. 골드는 범위 굴림, 아이템은 카테고리(회복/소비/장비)별 독립 굴림 후
    /// 희귀도 가중치로 후보를 추첨한다. 엘리트/미니 보스는 EnemyData.rareDropMultiplier(>1)로
    /// Rare 이상 가중치가 상향된다.
    /// // SYNC: 호스트 권위 — 굴림은 호스트 전용 (RunManager.Rng 시드 결정성 유지).
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Enemies/Drop Table", fileName = "DropTable_")]
    public class DropTable : ScriptableObject
    {
        [Serializable]
        public class RarityWeight
        {
            public ItemRarity rarity;
            [Min(0f)] public float weight = 1f; // TODO(밸런스): 문서 미정
        }

        [Header("골드 (범위 굴림)")]
        [Min(0)] public int goldMin = 1;  // TODO(밸런스): 문서 미정
        [Min(0)] public int goldMax = 5;  // TODO(밸런스): 문서 미정

        [Header("카테고리별 드롭 확률 (독립 굴림)")]
        [Range(0f, 1f)] public float healingDropChance = 0.10f;    // TODO(밸런스): 문서 미정
        [Range(0f, 1f)] public float consumableDropChance = 0.10f; // TODO(밸런스): 문서 미정
        [Range(0f, 1f)] public float equipmentDropChance = 0.05f;  // TODO(밸런스): 문서 미정

        [Header("드롭 후보 (카테고리별)")]
        public List<ItemDefinition> healingItems = new List<ItemDefinition>();
        public List<ItemDefinition> consumables = new List<ItemDefinition>();
        public List<ItemDefinition> equipment = new List<ItemDefinition>();

        [Header("희귀도 가중치 (미지정 희귀도는 1 취급)")]
        [Tooltip("Rare 이상은 rareDropMultiplier가 추가로 곱해진다 — 엘리트/미니 보스 상향 경로.")]
        public List<RarityWeight> rarityWeights = new List<RarityWeight>();

        // 추첨 후보 버퍼 (호출당 할당 최소화)
        private readonly List<ItemDefinition> _candidates = new List<ItemDefinition>();

        /// <summary>골드 굴림. 범위가 0이면 0.</summary>
        public int RollGold(System.Random rng)
        {
            if (rng == null || goldMax <= 0) return 0;
            int min = Mathf.Min(goldMin, goldMax);
            int max = Mathf.Max(goldMin, goldMax);
            return rng.Next(min, max + 1);
        }

        /// <summary>
        /// 아이템 드롭 굴림 — 카테고리별 확률 굴림 후 당첨 카테고리에서 희귀도 가중치 추첨.
        /// 결과는 results에 추가한다 (호출측 버퍼 재사용).
        /// </summary>
        /// <param name="rareDropMultiplier">Rare 이상 가중치 배율 — 엘리트/미니 보스는 1보다 크게.</param>
        public void RollDrops(System.Random rng, float rareDropMultiplier, List<ItemDefinition> results)
        {
            if (rng == null || results == null) return;

            TryRollCategory(healingItems, healingDropChance, rareDropMultiplier, rng, results);
            TryRollCategory(consumables, consumableDropChance, rareDropMultiplier, rng, results);
            TryRollCategory(equipment, equipmentDropChance, rareDropMultiplier, rng, results);
        }

        private void TryRollCategory(List<ItemDefinition> pool, float chance, float rareMultiplier,
                                     System.Random rng, List<ItemDefinition> results)
        {
            if (pool == null || pool.Count == 0 || chance <= 0f) return;
            if (rng.NextDouble() >= chance) return;

            ItemDefinition picked = PickWeighted(pool, rareMultiplier, rng);
            if (picked != null)
                results.Add(picked);
        }

        /// <summary>희귀도 가중치 추첨. Developer 등급은 정식 드롭 풀에서 제외 (Items.LootTable과 동일 규칙).</summary>
        private ItemDefinition PickWeighted(List<ItemDefinition> pool, float rareMultiplier, System.Random rng)
        {
            _candidates.Clear();
            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                ItemDefinition item = pool[i];
                if (item == null) continue;
                if (item.rarity == ItemRarity.Developer) continue;
                _candidates.Add(item);
                totalWeight += GetWeight(item.rarity, rareMultiplier);
            }
            if (_candidates.Count == 0 || totalWeight <= 0f) return null;

            float roll = (float)(rng.NextDouble() * totalWeight);
            float accumulated = 0f;
            for (int i = 0; i < _candidates.Count; i++)
            {
                accumulated += GetWeight(_candidates[i].rarity, rareMultiplier);
                if (roll < accumulated)
                    return _candidates[i];
            }
            return _candidates[_candidates.Count - 1]; // 부동소수점 잔여 보정
        }

        private float GetWeight(ItemRarity rarity, float rareMultiplier)
        {
            float weight = 1f;
            if (rarityWeights != null)
            {
                for (int i = 0; i < rarityWeights.Count; i++)
                {
                    if (rarityWeights[i].rarity == rarity)
                    {
                        weight = Mathf.Max(0f, rarityWeights[i].weight);
                        break;
                    }
                }
            }

            // 엘리트/미니 보스 희귀 확률 상향 — Rare 이상에만 배율 적용
            if (rarity >= ItemRarity.Rare)
                weight *= Mathf.Max(0f, rareMultiplier);
            return weight;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (goldMax < goldMin)
            {
                Debug.LogWarning($"[DropTable] '{name}': goldMax({goldMax}) < goldMin({goldMin}) — 범위를 확인하세요.", this);
            }

            WarnNullEntries(healingItems, nameof(healingItems));
            WarnNullEntries(consumables, nameof(consumables));
            WarnNullEntries(equipment, nameof(equipment));
        }

        private void WarnNullEntries(List<ItemDefinition> pool, string label)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == null)
                {
                    Debug.LogWarning($"[DropTable] '{name}': {label}[{i}]가 비어 있습니다.", this);
                    break;
                }
            }
        }
#endif
    }
}
