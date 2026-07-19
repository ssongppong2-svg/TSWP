// 근거: UI 시스템.md — 설정 변경 항목 7종
//   (UI 크기 / UI 투명도 / HUD 켜기·끄기 / 데미지 숫자 표시 / 미니맵 표시 / 핑 표시 / 음성 채팅 표시)
//   + 설계 철학 ⑤ "모든 UI는 설정에서 크기와 투명도를 조절할 수 있다".
// JsonUtility 직렬화 대상이므로 public 필드만 사용한다 (프로퍼티는 직렬화되지 않음).
using System;

namespace TSWP.UI
{
    /// <summary>UI 설정 저장 데이터. SettingsManager가 JSON으로 저장/로드한다.</summary>
    [Serializable]
    public sealed class UISettings
    {
        /// <summary>UI 크기 배율. CanvasScaler.scaleFactor에 곱해 적용.</summary>
        public float uiScale = 1f;          // TODO(밸런스): 허용 범위 문서 미정 (기본 1.0 가정)

        /// <summary>UI 투명도 (1 = 불투명). 각 패널 CanvasGroup.alpha에 적용.</summary>
        public float uiOpacity = 1f;        // TODO(밸런스): 최소 투명도 문서 미정

        /// <summary>HUD 전체 켜기/끄기 (스트리머 친화 — 설계 철학 ④).</summary>
        public bool hudEnabled = true;

        /// <summary>데미지 숫자 표시.</summary>
        public bool showDamageNumbers = true;

        /// <summary>미니맵 표시.</summary>
        public bool showMinimap = true;

        /// <summary>핑 표시 (미니맵 + 월드 마커 양쪽).</summary>
        public bool showPings = true;

        /// <summary>음성 채팅 표시 (말하기/음소거 아이콘).</summary>
        public bool showVoiceIndicator = true;

        /// <summary>범위 보정. 인스펙터/로드 직후 호출해 비정상 값을 막는다.</summary>
        public void Clamp()
        {
            if (uiScale < MinUiScale) uiScale = MinUiScale;
            if (uiScale > MaxUiScale) uiScale = MaxUiScale;
            if (uiOpacity < MinUiOpacity) uiOpacity = MinUiOpacity;
            if (uiOpacity > 1f) uiOpacity = 1f;
        }

        // TODO(밸런스): 아래 범위는 문서 미정 — 임시 값. 확정 시 조정.
        public const float MinUiScale = 0.5f;
        public const float MaxUiScale = 2.0f;
        public const float MinUiOpacity = 0.2f;

        public UISettings Clone()
        {
            return new UISettings
            {
                uiScale = uiScale,
                uiOpacity = uiOpacity,
                hudEnabled = hudEnabled,
                showDamageNumbers = showDamageNumbers,
                showMinimap = showMinimap,
                showPings = showPings,
                showVoiceIndicator = showVoiceIndicator,
            };
        }
    }
}
