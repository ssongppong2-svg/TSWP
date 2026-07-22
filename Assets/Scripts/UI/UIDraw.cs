// 근거: 팔레트 시스템.md — UI 색상: 배경=어두운 회색(순수 검정 지양) / 텍스트=흰색 / 강조=노랑 / 경고=빨강 / 성공=초록.
//       UI 시스템.md — 캔버스 ③ Overlay 계층: 결과 화면·설정 등 전체 화면 패널.
// 오버레이 뷰(결과/게임오버/보스 배너)가 같은 그리기 코드를 각자 복사하지 않도록 모은 유틸.
// OnGUI 규칙(이 프로젝트 전례: 프레임 튐): Repaint 전용 + GUIStyle 캐싱 + GUILayout 금지(고정 Rect만) +
//   런타임 텍스처 할당 금지(Texture2D.whiteTexture 사용).
using UnityEngine;
using TSWP.Art;

namespace TSWP.UI
{
    /// <summary>IMGUI 오버레이 공통 그리기/색상 헬퍼. 상태를 갖지 않는다.</summary>
    public static class UIDraw
    {
        // UIColorConfig(SO)가 배선되지 않아도 화면이 팔레트 의미대로 보이도록 하는 기본값.
        public static readonly Color FallbackBackground = new Color(0.13f, 0.13f, 0.15f, 1f);
        public static readonly Color FallbackText = Color.white;
        public static readonly Color FallbackAccent = new Color(1f, 0.85f, 0.20f, 1f);
        public static readonly Color FallbackWarning = new Color(0.90f, 0.25f, 0.25f, 1f);
        public static readonly Color FallbackSuccess = new Color(0.35f, 0.80f, 0.40f, 1f);
        public static readonly Color FallbackDisabled = new Color(0.45f, 0.45f, 0.48f, 1f);

        public static Color Background(UIColorConfig c) => c != null ? c.background : FallbackBackground;
        public static Color Text(UIColorConfig c) => c != null ? c.text : FallbackText;
        public static Color Accent(UIColorConfig c) => c != null ? c.accent : FallbackAccent;
        public static Color Warning(UIColorConfig c) => c != null ? c.warning : FallbackWarning;
        public static Color Success(UIColorConfig c) => c != null ? c.success : FallbackSuccess;
        public static Color Disabled(UIColorConfig c) => c != null ? c.disabled : FallbackDisabled;

        public static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>단색 사각형. 내장 텍스처만 쓰므로 런타임 할당이 없다.</summary>
        public static void Solid(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>화면 전체를 어둡게 덮는다 (오버레이 진입 연출의 최소 단위).</summary>
        public static void FullScreenDim(Color background, float alpha)
        {
            Solid(new Rect(0f, 0f, Screen.width, Screen.height), WithAlpha(background, alpha));
        }

        /// <summary>패널 배경 + 1px 테두리. 네모와 글자만으로도 경계가 읽히게 한다.</summary>
        public static void Panel(Rect rect, Color background, Color border, float backgroundAlpha = 0.94f)
        {
            Solid(rect, WithAlpha(background, backgroundAlpha));
            Border(rect, border, 1f);
        }

        public static void Border(Rect rect, Color color, float thickness)
        {
            Solid(new Rect(rect.x, rect.y, rect.width, thickness), color);
            Solid(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            Solid(new Rect(rect.x, rect.y, thickness, rect.height), color);
            Solid(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        /// <summary>구분선.</summary>
        public static void Separator(Rect rect, Color color)
        {
            Solid(new Rect(rect.x, rect.y, rect.width, 1f), WithAlpha(color, 0.35f));
        }

        /// <summary>색을 지정해 라벨을 그린다 (GUIStyle.normal.textColor를 매 프레임 바꾸지 않기 위한 경로).</summary>
        public static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            if (string.IsNullOrEmpty(text) || style == null) return;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        /// <summary>라벨 스타일 생성. 반드시 호출부에서 1회만 만들어 캐싱한다.</summary>
        public static GUIStyle MakeLabelStyle(int fontSize, TextAnchor anchor, bool bold)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = anchor,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                richText = false,
                wordWrap = false,
            };
            // GUI.color로 색을 곱하므로 기본색은 흰색으로 고정한다.
            style.normal.textColor = Color.white;
            style.padding = new RectOffset(0, 0, 0, 0);
            return style;
        }

        /// <summary>0~1 진행도 막대 (결과/배너에서 남은 시간·비율 표시용).</summary>
        public static void Bar(Rect rect, float ratio01, Color back, Color fill)
        {
            Solid(rect, back);
            float w = rect.width * Mathf.Clamp01(ratio01);
            if (w > 0f) Solid(new Rect(rect.x, rect.y, w, rect.height), fill);
        }
    }
}
