// 근거: UI 시스템.md — 접근성 지원 기능 5종
//   (색약 모드 / 자막 / UI 확대 / 화면 흔들림 감소 / 플래시 효과 감소)
// 색약 모드는 팔레트 스왑 방식으로 구현 예정 — 실제 색값은 Art 폴더의 팔레트 SO가 소유한다.
using System;

namespace TSWP.UI
{
    /// <summary>색약 모드 종류. Off 외 3종은 Art 팔레트 SO의 스왑 대상 키로 쓰인다.</summary>
    public enum ColorblindMode
    {
        Off,          // 사용 안 함
        Protanopia,   // 적색맹
        Deuteranopia, // 녹색맹
        Tritanopia,   // 청색맹
    }

    /// <summary>접근성 설정 저장 데이터. SettingsManager가 JSON으로 저장/로드한다.</summary>
    [Serializable]
    public sealed class AccessibilitySettings
    {
        /// <summary>색약 모드. TODO: Art.GamePalette 스왑 연동.</summary>
        public ColorblindMode colorblindMode = ColorblindMode.Off;

        /// <summary>자막 표시. TODO: 사운드 이벤트에 자막 키 부여 후 연동.</summary>
        public bool subtitles = false;

        /// <summary>UI 확대 배율 (UISettings.uiScale와 별도로 접근성 목적으로 추가 적용).</summary>
        public float uiZoom = 1f;   // TODO(밸런스): 허용 범위 문서 미정

        /// <summary>화면 흔들림 감소. TODO: 카메라 셰이크 게인 0 처리 연동.</summary>
        public bool reduceScreenShake = false;

        /// <summary>플래시 효과 감소. TODO: 2D URP Volume 후처리 토글 연동.</summary>
        public bool reduceFlashEffects = false;

        public void Clamp()
        {
            if (uiZoom < MinUiZoom) uiZoom = MinUiZoom;
            if (uiZoom > MaxUiZoom) uiZoom = MaxUiZoom;
        }

        // TODO(밸런스): 문서 미정 — 임시 범위.
        public const float MinUiZoom = 1.0f;
        public const float MaxUiZoom = 2.0f;

        public AccessibilitySettings Clone()
        {
            return new AccessibilitySettings
            {
                colorblindMode = colorblindMode,
                subtitles = subtitles,
                uiZoom = uiZoom,
                reduceScreenShake = reduceScreenShake,
                reduceFlashEffects = reduceFlashEffects,
            };
        }
    }
}
