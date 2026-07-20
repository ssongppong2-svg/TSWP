// 근거: UI 시스템.md — 보스전 상단에 기믹 진행 게이지를 표시한다 / 화면 효과(시야 방해 등)는 오버레이 계층.
// ARCHITECTURE.md §3-5는 게임플레이 → UI 통지를 GameEvents 경유로 규정하지만,
// Core.GameEvents는 수정 금지 대상이고 '기믹 게이지'·'화면 오버레이' 채널이 존재하지 않는다.
// 따라서 GameEvents와 완전히 동일한 형태(정적 이벤트 + Raise 헬퍼, 원시타입 페이로드)의
// 보스 전용 보조 허브를 둔다. UI는 이 허브만 구독하고 Bosses의 다른 타입은 참조하지 않는다.
//   → GameEvents에 해당 채널이 추가되는 날 이 파일은 그대로 삭제하고 구독처만 옮기면 된다.
using System;
using UnityEngine;

namespace TSWP.Bosses
{
    /// <summary>보스 → UI 단방향 통지 허브 (게이지/화면 오버레이). UI는 구독만 한다.</summary>
    public static class BossGaugeChannel
    {
        /// <summary>거미줄 화면 오버레이 식별자 — UI가 이 id로 표시할 오버레이를 고른다.</summary>
        public const string OverlayWeb = "boss.overlay.web";

        /// <summary>위험도 게이지 갱신. (bossId, 0~1, 표시 여부)
        /// UI 측 매핑: BossUIModel.SetGimmickGauge(value01, visible).</summary>
        public static event Action<string, float, bool> GaugeChanged;

        /// <summary>화면 오버레이 세기 갱신. (overlayId, 0~1). 0이면 오버레이를 끈다.</summary>
        public static event Action<string, float> ScreenOverlayChanged;

        public static void RaiseGauge(string bossId, float value01, bool visible = true) =>
            GaugeChanged?.Invoke(bossId, Mathf.Clamp01(value01), visible);

        public static void RaiseOverlay(string overlayId, float intensity01) =>
            ScreenOverlayChanged?.Invoke(overlayId, Mathf.Clamp01(intensity01));
    }
}
