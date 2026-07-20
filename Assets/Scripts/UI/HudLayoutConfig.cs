// 근거: UI 시스템.md — UI 5원칙(필요한 정보만/전투 방해 없이/2초 안에 이해/스트리머 친화/크기·투명도 조절).
//   HUD의 배치·크기·색은 전부 이 SO가 소유한다. 뷰(GameplayHud/BossHealthBar)에 매직 넘버를 두지 않는다.
// 색상은 Art의 기존 SO(UIColorConfig / HealthBarColorConfig)를 재사용한다 — 중복 정의 금지(ARCHITECTURE.md §5).
//   두 SO가 비어 있어도 아래 폴백 색으로 동작한다 (연출 에셋이 없어도 HUD는 실패하지 않는다).
using UnityEngine;
using TSWP.Art;

namespace TSWP.UI
{
    /// <summary>
    /// HUD 레이아웃/색상 데이터. 에셋을 만들지 않아도 GameplayHud가 런타임 기본 인스턴스를 생성하므로
    /// 씬 배선 없이 바로 동작하고, 에셋을 할당하면 코드 수정 없이 전부 조정된다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/UI/Hud Layout Config", fileName = "HudLayoutConfig")]
    public class HudLayoutConfig : ScriptableObject
    {
        [Header("색상 소스 (Art 재사용 — 비우면 폴백 색 사용)")]
        [SerializeField] private UIColorConfig colors;
        [SerializeField] private HealthBarColorConfig healthColors;

        [Header("공통 여백/글자")]
        [Tooltip("화면 가장자리 여백(px).")]
        public Vector2 screenPadding = new Vector2(14f, 14f);

        [Tooltip("패널 내부 여백(px).")]
        public float panelPadding = 8f;

        [Tooltip("한 줄 높이(px).")]
        public float lineHeight = 18f;

        [Tooltip("요소 사이 간격(px).")]
        public float elementSpacing = 6f;

        public int fontSize = 13;
        public int smallFontSize = 11;

        [Header("좌상단 상태 패널")]
        public float statusPanelWidth = 260f;

        [Tooltip("체력바 높이(px).")]
        public float healthBarHeight = 16f;

        [Header("슬롯 (아이템/스킬 공용)")]
        [Tooltip("슬롯 한 변의 크기(px). 슬롯 개수는 절대 하드코딩하지 않는다 — 데이터가 개수를 정한다.")]
        public float slotSize = 44f;

        [Tooltip("슬롯 사이 간격(px).")]
        public float slotSpacing = 6f;

        [Tooltip("슬롯 테두리 두께(px).")]
        public float slotBorder = 2f;

        [Header("상태이상 아이콘")]
        public float statusIconSize = 26f;
        public float statusIconSpacing = 4f;

        [Header("보스 바 (상단 중앙)")]
        [Tooltip("화면 폭 대비 보스 바 폭 비율.")]
        [Range(0.2f, 1f)] public float bossBarWidthRatio = 0.5f;

        public float bossBarHeight = 20f;
        public float bossBarTopMargin = 18f;

        [Tooltip("위험도(Danger) 바 높이(px).")]
        public float dangerBarHeight = 8f;

        [Tooltip("기믹 게이지 높이(px).")]
        public float gimmickBarHeight = 8f;

        [Header("임계값")]
        [Tooltip("이 비율 이하에서 체력 수치를 경고색으로 표시한다.")]
        [Range(0f, 1f)] public float lowHealthRatio = 0.3f;

        [Header("폴백 색상 (Art SO 미할당 시 사용)")]
        public Color fallbackPanel = new Color(0.08f, 0.08f, 0.10f, 0.72f);
        public Color fallbackText = Color.white;
        public Color fallbackAccent = new Color(1f, 0.85f, 0.2f, 1f);
        public Color fallbackWarning = new Color(0.9f, 0.25f, 0.25f, 1f);
        public Color fallbackSuccess = new Color(0.35f, 0.8f, 0.4f, 1f);
        public Color fallbackDisabled = new Color(0.45f, 0.45f, 0.48f, 1f);

        [Header("전용 색상")]
        [Tooltip("게이지 빈 부분 / 빈 슬롯 배경.")]
        public Color emptyFill = new Color(0.22f, 0.22f, 0.26f, 1f);

        [Tooltip("슬롯 테두리.")]
        public Color slotBorderColor = new Color(0.55f, 0.55f, 0.60f, 1f);

        [Tooltip("보스 체력바 색 (평상시).")]
        public Color bossFill = new Color(0.78f, 0.22f, 0.22f, 1f);

        [Tooltip("보스 체력바 색 (광폭화).")]
        public Color bossEnragedFill = new Color(1f, 0.45f, 0.1f, 1f);

        [Tooltip("위험도 바 색.")]
        public Color dangerFill = new Color(1f, 0.35f, 0.15f, 1f);

        [Tooltip("기믹 게이지 색.")]
        public Color gimmickFill = new Color(0.35f, 0.7f, 1f, 1f);

        // ── 색상 해석 (Art SO 우선, 없으면 폴백) ──────────────────
        public Color Panel => colors != null ? colors.background * PanelAlpha(colors.background) : fallbackPanel;
        public Color Text => colors != null ? colors.text : fallbackText;
        public Color Accent => colors != null ? colors.accent : fallbackAccent;
        public Color Warning => colors != null ? colors.warning : fallbackWarning;
        public Color Success => colors != null ? colors.success : fallbackSuccess;
        public Color Disabled => colors != null ? colors.disabled : fallbackDisabled;

        /// <summary>체력 비율 → 색 (팔레트 시스템.md의 5구간). Art SO가 없으면 초록↔빨강 보간.</summary>
        public Color HealthColor(float ratio01)
        {
            if (healthColors != null) return healthColors.Evaluate(ratio01);
            ratio01 = Mathf.Clamp01(ratio01);
            return ratio01 > lowHealthRatio ? fallbackSuccess : fallbackWarning;
        }

        // UIColorConfig.background는 불투명이므로 HUD 패널용으로만 알파를 낮춘다.
        private Color PanelAlpha(Color source)
        {
            return new Color(1f, 1f, 1f, fallbackPanel.a / Mathf.Max(0.001f, source.a));
        }

        /// <summary>에셋을 만들지 않은 씬에서도 HUD가 뜨도록 하는 런타임 기본 인스턴스.</summary>
        public static HudLayoutConfig CreateRuntimeDefault()
        {
            var instance = CreateInstance<HudLayoutConfig>();
            instance.name = "HudLayoutConfig (Runtime Default)";
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }
    }
}
