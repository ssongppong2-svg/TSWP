// 근거: 이름 시스템.md — 칭호 색상: 기본=흰색, 전설=금색, 개발자=보라색. (색상은 추후 변경 가능)
// TitleColorType은 TSWP.Meta가 소유한다 (ARCHITECTURE.md §5) — 여기서는 참조만 한다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Meta;

namespace TSWP.Art
{
    /// <summary>칭호 색상 매핑. 코드에 Color를 하드코딩하지 않기 위한 분리 지점이다.</summary>
    [CreateAssetMenu(menuName = "TSWP/Art/Title Colors", fileName = "TitleColorConfig")]
    public class TitleColorConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public TitleColorType type;
            public Color color = Color.white;
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>
        {
            new Entry { type = TitleColorType.Default, color = Color.white },
            new Entry { type = TitleColorType.Legendary, color = new Color(1f, 0.84f, 0.2f, 1f) },   // 금색
            new Entry { type = TitleColorType.Developer, color = new Color(0.65f, 0.35f, 0.95f, 1f) }, // 보라색
        };

        public Color Get(TitleColorType type)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].type == type) return entries[i].color;
            return Color.white;
        }
    }
}
