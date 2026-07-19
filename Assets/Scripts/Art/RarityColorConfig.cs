// 근거: 팔레트 시스템.md — 희귀도 색상: 일반=회색, 고급=초록, 희귀=파랑, 영웅=보라, 전설=주황/금색, 개발자=무지개(또는 보라+금색).
// ItemRarity는 TSWP.Items가 소유한다 (ARCHITECTURE.md §5) — 여기서는 참조만 한다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Items;

namespace TSWP.Art
{
    /// <summary>희귀도 → 색상 매핑. 아이템 테두리/이름/드롭 이펙트가 이 값을 사용한다.</summary>
    [CreateAssetMenu(menuName = "TSWP/Art/Rarity Colors", fileName = "RarityColorConfig")]
    public class RarityColorConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public ItemRarity rarity;
            public Color color = Color.white;

            [Tooltip("개발자 등급의 무지개 연출처럼 시간에 따라 색이 변하는지.")]
            public bool animated;

            [Tooltip("animated일 때 순환할 색 목록.")]
            public List<Color> gradient = new List<Color>();
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        public Color Get(ItemRarity rarity)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].rarity == rarity) return entries[i].color;

            return Color.white;
        }

        /// <summary>애니메이션 색상(개발자 등급 무지개). t는 0~1 순환값.</summary>
        public Color GetAnimated(ItemRarity rarity, float t)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.rarity != rarity) continue;

                if (!entry.animated || entry.gradient.Count == 0) return entry.color;

                float scaled = Mathf.Repeat(t, 1f) * entry.gradient.Count;
                int index = Mathf.FloorToInt(scaled) % entry.gradient.Count;
                int next = (index + 1) % entry.gradient.Count;
                return Color.Lerp(entry.gradient[index], entry.gradient[next], scaled - Mathf.Floor(scaled));
            }
            return Color.white;
        }
    }
}
