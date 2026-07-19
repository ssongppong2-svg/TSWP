// 근거: 아이템 시스템.md — 아이템은 무작위(Random)로 등장, 획득 방법 8경로.
//       희귀도별 등장 확률 수치는 문서에 없음 → 인스펙터 조정 가능한 가중치 필드로 열어둔다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Items
{
    /// <summary>획득 경로별 무작위 추첨 테이블. 풀 필터(획득 경로 플래그) → 희귀도 가중치 추첨 순으로 굴린다.</summary>
    [CreateAssetMenu(menuName = "TSWP/Items/Loot Table", fileName = "LootTable_")]
    public class LootTable : ScriptableObject
    {
        [Serializable]
        public class RarityWeight
        {
            public ItemRarity rarity;
            /// <summary>등장 가중치. // TODO(밸런스): 문서 미정 — 밸런스 문서 확정 후 조정</summary>
            [Min(0f)] public float weight = 1f;
        }

        /// <summary>추첨 대상 아이템 풀. 각 아이템의 acquisitionMethods 플래그로 경로 필터링한다.</summary>
        [SerializeField] private List<ItemDefinition> itemPool = new();

        /// <summary>희귀도별 가중치. 비어 있거나 항목이 없는 희귀도는 가중치 1로 취급.
        /// // TODO(밸런스): 문서 미정</summary>
        [SerializeField] private List<RarityWeight> rarityWeights = new();

        // 추첨 시 재사용하는 후보 버퍼 (프레임당 할당 최소화)
        private readonly List<ItemDefinition> _candidates = new();

        /// <summary>획득 경로에 맞는 아이템 1개를 무작위 추첨. 후보가 없으면 null.
        /// 결정성 유지를 위해 시드 주입된 System.Random을 호출자(ItemDropManager)가 넘긴다.</summary>
        public ItemDefinition Roll(AcquisitionMethod source, System.Random rng)
        {
            if (rng == null || itemPool == null || itemPool.Count == 0) return null;

            // 1) 획득 경로 필터 — Developer 등급은 정식 드롭 풀에서 제외 (개발자 전용 등급)
            _candidates.Clear();
            float totalWeight = 0f;
            for (int i = 0; i < itemPool.Count; i++)
            {
                ItemDefinition item = itemPool[i];
                if (item == null) continue;
                if (item.rarity == ItemRarity.Developer) continue;
                if ((item.acquisitionMethods & source) == 0) continue;
                _candidates.Add(item);
                totalWeight += GetRarityWeight(item.rarity);
            }
            if (_candidates.Count == 0 || totalWeight <= 0f) return null;

            // 2) 희귀도 가중치 추첨
            float roll = (float)(rng.NextDouble() * totalWeight);
            float accumulated = 0f;
            for (int i = 0; i < _candidates.Count; i++)
            {
                accumulated += GetRarityWeight(_candidates[i].rarity);
                if (roll < accumulated)
                    return _candidates[i];
            }
            return _candidates[_candidates.Count - 1]; // 부동소수점 잔여 보정
        }

        private float GetRarityWeight(ItemRarity rarity)
        {
            if (rarityWeights != null)
            {
                for (int i = 0; i < rarityWeights.Count; i++)
                {
                    if (rarityWeights[i].rarity == rarity)
                        return Mathf.Max(0f, rarityWeights[i].weight);
                }
            }
            return 1f; // 미지정 희귀도 기본 가중치
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 밸런스 원칙 점검 보조: 풀에 null 항목이 섞이면 경고
            if (itemPool == null) return;
            for (int i = 0; i < itemPool.Count; i++)
            {
                if (itemPool[i] == null)
                {
                    Debug.LogWarning($"[LootTable] '{name}': itemPool[{i}]가 비어 있습니다.", this);
                    break;
                }
            }
        }
#endif
    }
}
