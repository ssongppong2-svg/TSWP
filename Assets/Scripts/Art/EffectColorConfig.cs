// 근거: 팔레트 시스템.md — 이펙트 색상: 화염=주황+빨강, 독=초록, 회복=파랑+흰색,
//       감전=노랑, 출혈=빨강, 빙결=하늘색.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>이펙트 종류 → 색상. 파티클/트레일이 이 값을 사용한다.</summary>
    [CreateAssetMenu(menuName = "TSWP/Art/Effect Colors", fileName = "EffectColorConfig")]
    public class EffectColorConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public EffectType effect;

            [Tooltip("주 색상.")]
            public Color primary = Color.white;

            [Tooltip("보조 색상 (예: 화염=주황+빨강, 회복=파랑+흰색). 단색이면 primary와 같게 둔다.")]
            public Color secondary = Color.white;
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>();

        public Color GetPrimary(EffectType effect)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].effect == effect) return entries[i].primary;
            return Color.white;
        }

        public Color GetSecondary(EffectType effect)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].effect == effect) return entries[i].secondary;
            return Color.white;
        }
    }
}
