// 근거: UI 시스템.md — HUD에 상태이상 아이콘과 남은 시간을 표시한다.
// GameEvents에는 상태이상 전용 이벤트가 없고 GameEvents.cs는 수정 금지이므로,
//   StatusEffects.StatusEffectController의 엔티티 단위 이벤트를 StatusEffectHudBridge가 읽어 이 DTO로 밀어 넣는다.
// 상태이상 '판정'은 StatusEffectController가 단독 소유한다 — 여기는 표시용 값만 보관한다(중복 구현 금지).
// 표시 문자열은 신원(종류/이름)이 바뀔 때만 만든다 — IMGUI가 매 프레임 문자열을 만들면 GC로 프레임이 튄다.
using System;
using UnityEngine;
using TSWP.StatusEffects;

namespace TSWP.UI
{
    /// <summary>HUD 상태이상 아이콘 1개의 표시 정보.</summary>
    [Serializable]
    public sealed class StatusEffectHudInfo
    {
        /// <summary>상태이상 종류 (StatusEffects 한 곳에 정의된 16종 — 재정의 금지).</summary>
        public StatusEffectType EffectType { get; private set; }

        /// <summary>한글 표기명 (StatusEffectData.DisplayNameKo).</summary>
        public string DisplayName { get; private set; }

        /// <summary>아이콘이 없을 때 대신 그릴 1~2글자 (미리 만들어 둔다).</summary>
        public string ShortLabel { get; private set; } = string.Empty;

        /// <summary>픽셀아트 아이콘 (StatusEffectData.icon). null이면 뷰가 ShortLabel로 대체한다.</summary>
        public Sprite Icon;

        /// <summary>남은 지속시간(초).</summary>
        public float Remaining;

        /// <summary>전체 지속시간(초) — 남은 시간 게이지 비율 계산용.</summary>
        public float Duration;

        /// <summary>군중 제어 여부 — 뷰가 남은 시간 띠를 경고색으로 그린다.</summary>
        public bool IsCC;

        /// <summary>남은 시간 비율 0~1 (1 = 방금 걸림).</summary>
        public float RemainRatio => Duration <= 0f ? 1f : Mathf.Clamp01(Remaining / Duration);

        /// <summary>종류/표기명 설정. 값이 실제로 바뀔 때만 표시 문자열을 다시 만든다.</summary>
        public void SetIdentity(StatusEffectType type, string displayName)
        {
            if (EffectType == type && string.Equals(DisplayName, displayName) && ShortLabel.Length > 0) return;

            EffectType = type;
            DisplayName = displayName;

            string source = string.IsNullOrEmpty(displayName) ? type.ToString() : displayName;
            ShortLabel = source.Length <= 2 ? source : source.Substring(0, 2);
        }
    }
}
