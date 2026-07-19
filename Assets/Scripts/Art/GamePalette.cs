// 근거: 팔레트 시스템.md — 게임 전체가 하나의 공통 팔레트(48~64색)를 사용한다.
//       색은 장식이 아니라 정보다: 플레이어는 색만 보고도 위험/회복/희귀도를 판단할 수 있어야 한다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>색상이 전달하는 의미 8종.</summary>
    public enum ColorMeaning
    {
        Red,    // 공격 / 위험 / 폭발 / 피해 / 적
        Blue,   // 회복 / 보호 / 물 / 얼음
        Green,  // 독 / 자연 / 회복
        Yellow, // 번개 / 속도 / 희귀 아이템
        Purple, // 저주 / 혼란 / 마법
        Orange, // 화염 / 폭발 / 용암
        White,  // 기본 텍스트 / 중립
        Gray,   // 비활성 / 일반 등급 / 배경
    }

    /// <summary>
    /// 공통 팔레트. 코드에 Color를 하드코딩하지 않고 반드시 이 에셋을 경유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Art/Game Palette", fileName = "GamePalette")]
    public class GamePalette : ScriptableObject
    {
        [Serializable]
        public class MeaningEntry
        {
            public ColorMeaning meaning;
            public Color color = Color.white;
        }

        [Header("공통 팔레트 (48~64색)")]
        [Tooltip("게임 전체에서 사용하는 색 목록. 이 목록 밖의 색은 사용하지 않는다.")]
        public List<Color> colors = new List<Color>();

        [Header("의미별 대표색")]
        public List<MeaningEntry> semanticColors = new List<MeaningEntry>();

        /// <summary>의미에 해당하는 대표색을 조회한다. 미설정 시 흰색.</summary>
        public Color Get(ColorMeaning meaning)
        {
            for (int i = 0; i < semanticColors.Count; i++)
            {
                if (semanticColors[i].meaning == meaning)
                    return semanticColors[i].color;
            }
            return Color.white;
        }

#if UNITY_EDITOR
        private const int MinColors = 48;
        private const int MaxColors = 64;

        private void OnValidate()
        {
            if (colors.Count > 0 && (colors.Count < MinColors || colors.Count > MaxColors))
            {
                Debug.LogWarning(
                    $"[GamePalette] '{name}': 공통 팔레트는 {MinColors}~{MaxColors}색을 권장합니다 (현재 {colors.Count}색).", this);
            }
        }
#endif
    }
}
