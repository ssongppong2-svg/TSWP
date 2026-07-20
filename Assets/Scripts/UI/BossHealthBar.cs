// 근거: UI 시스템.md — 보스전에서 화면 상단에 보스 이름 / 체력 / 분노 상태를 표시하고,
//   기믹 진행 상황이 있으면 별도 게이지를 표시한다. + 위험도(Danger Bar) 표시.
// 데이터 출처는 BossUIModel 하나다. BossUIModel은 GameEvents(BossAppeared/HealthChanged/PhaseChanged/
//   Enraged/Defeated)를 구독하고, 위험도·화면 덮기 연출은 보스 측이 SetDangerLevel/SetScreenOverlay로
//   밀어 넣는다 — 보스가 15기로 늘어도 이 뷰 코드는 바뀌지 않는다.
// Bosses 네임스페이스를 직접 참조하지 않는다 (UI는 게임 로직을 참조하지 않는다 — ARCHITECTURE.md §3-5).
//   따라서 Boss 담당자가 만들 타입이 아직 없어도 이 파일은 항상 컴파일된다.
// IMGUI 비용 규칙은 GameplayHud와 동일: Repaint 전용 / 스타일·문자열 캐싱 / 고정 Rect.
using UnityEngine;

namespace TSWP.UI
{
    /// <summary>보스전 상단 체력바 + 위험도 바 + 기믹 게이지 + 화면 덮기 연출.</summary>
    [DisallowMultipleComponent]
    public class BossHealthBar : MonoBehaviour
    {
        [Header("레이아웃 (비우면 런타임 기본값)")]
        [SerializeField] private HudLayoutConfig layout;

        [Header("표시 항목")]
        [SerializeField] private bool showPhase = true;
        [SerializeField] private bool showDangerBar = true;
        [SerializeField] private bool showGimmickGauge = true;
        [SerializeField] private bool showScreenOverlay = true;

        [Header("연동")]
        [Tooltip("UIManager에 BossBar 패널로 등록한다. 켜면 GameFlowState.BossFight에서만 표시된다.")]
        [SerializeField] private bool registerWithUIManager = false;

        // GameplayHud가 있으면 그 뷰모델을 공유하고(구독 1회), 없으면 자체 인스턴스를 구독한다.
        // 보스 단독 테스트 씬에서도 이 컴포넌트 하나로 동작해야 하기 때문이다.
        private BossUIModel _ownModel;

        /// <summary>표시 원본 뷰모델. 보스 측 푸시 API(SetDangerLevel 등)의 대상이기도 하다.</summary>
        public BossUIModel Model
        {
            get
            {
                var hud = GameplayHud.Instance;
                if (hud != null) return hud.Model.Boss;

                if (_ownModel == null)
                {
                    _ownModel = new BossUIModel();
                    _ownModel.Subscribe();
                }
                return _ownModel;
            }
        }

        private GUIStyle _label;
        private GUIStyle _labelCenter;
        private GUIStyle _labelSmallCenter;
        private int _styleFontSize = -1;

        private string _nameText = string.Empty;
        private string _cachedBossName;
        private bool _cachedEnraged;

        private string _phaseText = string.Empty;
        private int _cachedPhase = int.MinValue;

        private float _alpha = 1f;

        private void Start()
        {
            if (registerWithUIManager && UIManager.Instance != null)
                UIManager.Instance.RegisterPanel(UIPanelId.BossBar, gameObject);
        }

        private void OnDestroy()
        {
            // 자체 구독을 만든 경우에만 해제한다 (GameplayHud 소유 모델은 GameplayHud가 해제).
            _ownModel?.Unsubscribe();
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            var model = Model;
            if (model == null || !model.IsVisible) return;

            var settings = SettingsManager.Instance != null ? SettingsManager.Instance.Ui : null;
            if (settings != null && !settings.hudEnabled) return;

            var c = ResolveLayout();
            EnsureStyles(c);

            float scale = settings != null ? settings.uiScale : 1f;
            _alpha = settings != null ? settings.uiOpacity : 1f;

            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float screenW = Screen.width / Mathf.Max(0.01f, scale);
            float screenH = Screen.height / Mathf.Max(0.01f, scale);

            // 화면 덮기 연출(예: 시야를 가리는 거미줄)은 바 아래에 깔아 정보 가독성을 해치지 않는다.
            if (showScreenOverlay && model.ScreenOverlayAmount > 0f)
            {
                var overlay = model.ScreenOverlayColor;
                overlay.a *= model.ScreenOverlayAmount;
                DrawRect(new Rect(0f, 0f, screenW, screenH), overlay);
            }

            float barWidth = screenW * c.bossBarWidthRatio;
            float x = (screenW - barWidth) * 0.5f;
            float y = c.bossBarTopMargin;

            RefreshNameText(model);
            DrawLabel(new Rect(x, y, barWidth, c.lineHeight), _nameText,
                      model.IsEnraged ? c.Warning : c.Text, _labelCenter);
            y += c.lineHeight;

            // 체력바
            var hpBar = new Rect(x, y, barWidth, c.bossBarHeight);
            DrawRect(hpBar, c.emptyFill);
            float ratio = Mathf.Clamp01(model.HpRatio);
            if (ratio > 0f)
            {
                DrawRect(new Rect(hpBar.x, hpBar.y, hpBar.width * ratio, hpBar.height),
                         model.IsEnraged ? c.bossEnragedFill : c.bossFill);
            }
            y += c.bossBarHeight;

            // 위험도 바 — 임박한 위험을 색+길이로 즉시 전달 (2초 안에 이해).
            if (showDangerBar && model.HasDangerBar)
            {
                y += 2f;
                var dangerBar = new Rect(x, y, barWidth, c.dangerBarHeight);
                DrawRect(dangerBar, c.emptyFill);
                float danger = Mathf.Clamp01(model.DangerLevel);
                if (danger > 0f)
                    DrawRect(new Rect(dangerBar.x, dangerBar.y, dangerBar.width * danger, dangerBar.height), c.dangerFill);

                if (!string.IsNullOrEmpty(model.DangerLabel))
                {
                    DrawLabel(new Rect(x, y - 1f, barWidth, c.dangerBarHeight + 2f),
                              model.DangerLabel, c.Text, _labelSmallCenter);
                }
                y += c.dangerBarHeight;
            }

            // 기믹 게이지 — 기믹이 있는 보스에서만 표시된다.
            if (showGimmickGauge && model.HasGimmickGauge)
            {
                y += 2f;
                var gimmickBar = new Rect(x, y, barWidth, c.gimmickBarHeight);
                DrawRect(gimmickBar, c.emptyFill);
                float gauge = Mathf.Clamp01(model.GimmickGauge);
                if (gauge > 0f)
                    DrawRect(new Rect(gimmickBar.x, gimmickBar.y, gimmickBar.width * gauge, gimmickBar.height), c.gimmickFill);
                y += c.gimmickBarHeight;
            }

            if (showPhase)
            {
                RefreshPhaseText(model);
                DrawLabel(new Rect(x, y + 1f, barWidth, c.lineHeight), _phaseText, c.Accent, _labelSmallCenter);
            }

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        // ── 문자열 캐시 ───────────────────────────────────────────
        private void RefreshNameText(BossUIModel model)
        {
            string source = string.IsNullOrEmpty(model.BossName) ? model.BossId : model.BossName;
            bool enraged = model.IsEnraged;

            // 입력값(이름/분노)을 비교한다 — 합친 문자열로 비교하면 매 프레임 문자열이 새로 만들어진다.
            if (string.Equals(_cachedBossName, source) && _cachedEnraged == enraged) return;
            _cachedBossName = source;
            _cachedEnraged = enraged;
            _nameText = enraged ? source + "  [광폭화]" : source;
        }

        private void RefreshPhaseText(BossUIModel model)
        {
            if (_cachedPhase == model.PhaseIndex) return;
            _cachedPhase = model.PhaseIndex;
            // 단계 이름(Bosses.BossFightPhase)을 직접 참조하지 않는다 — 보스 enum 변경에 UI가 끌려가지 않도록 숫자만 표시.
            _phaseText = $"단계 {model.PhaseIndex}";
        }

        // ── 그리기 유틸 ───────────────────────────────────────────
        private void DrawRect(Rect rect, Color color)
        {
            GUI.color = WithAlpha(color);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private void DrawLabel(Rect rect, string text, Color color, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            GUI.color = WithAlpha(Color.white);
            style.normal.textColor = WithAlpha(color);
            GUI.Label(rect, text, style);
        }

        private Color WithAlpha(Color color) => new Color(color.r, color.g, color.b, color.a * _alpha);

        private HudLayoutConfig ResolveLayout()
        {
            if (layout == null) layout = HudLayoutConfig.CreateRuntimeDefault();
            return layout;
        }

        private void EnsureStyles(HudLayoutConfig c)
        {
            if (_label != null && _styleFontSize == c.fontSize) return;
            _styleFontSize = c.fontSize;
            _label = new GUIStyle(GUI.skin.label) { fontSize = c.fontSize, alignment = TextAnchor.MiddleLeft };
            _labelCenter = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _labelSmallCenter = new GUIStyle(_label) { fontSize = c.smallFontSize, alignment = TextAnchor.MiddleCenter };
        }
    }
}
