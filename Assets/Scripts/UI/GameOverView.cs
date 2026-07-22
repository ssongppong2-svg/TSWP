// 근거: 게임 시작과 선택, 직업, 플레이.md — "부활 횟수가 모두 소진되고 전원이 사망하면 게임이 종료된다."
//       UI 시스템.md — 캔버스 ③ Overlay 계층(전체 화면 패널). 팔레트 시스템.md — 배경 어두운 회색 / 경고 빨강 / 강조 노랑.
//       ARCHITECTURE.md §5 — 부활/게임오버 판정은 Core.SharedReviveSystem 한 곳. UI는 GameEvents.GameOver를 구독만 한다.
//
// GameEvents.GameOver를 아무도 구독하지 않아 죽어도 화면이 그대로였다 — 이 뷰가 그 구멍을 메운다.
// OnGUI 규칙: Repaint 전용 / GUIStyle·문자열은 표시 시점에 1회 생성 / GUILayout 금지.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Art;
using TSWP.Core;

namespace TSWP.UI
{
    /// <summary>
    /// 게임 오버 전체 화면 오버레이. 씬에 두기만 하면 GameEvents.GameOver로 자동 표시된다.
    /// RunManager가 없어도(요약 줄만 생략) 정상 동작한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameOverView : MonoBehaviour
    {
        public static GameOverView Instance { get; private set; }

        [Header("표시")]
        [SerializeField] private UIColorConfig colors;

        [SerializeField, Min(320f)] private float panelWidth = 560f;

        [Tooltip("뒤 화면을 덮는 어둡기. 게임 오버는 결과 화면보다 더 짙게 덮는다.")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.88f;

        [Tooltip("게임 오버 화면이 떠 있는 동안 게임을 정지한다(Time.timeScale = 0).")]
        [SerializeField] private bool pauseWhileVisible = true;

        [Tooltip("제목이 천천히 깜빡인다(정지 중에도 화면이 살아 있음을 보여준다).")]
        [SerializeField] private bool pulseTitle = true;

        [Header("조작")]
        [SerializeField] private KeyCode restartKey = KeyCode.R;

        [Tooltip("이번 런의 결과 화면을 열어 본다. MatchResultView가 씬에 없으면 안내를 숨긴다.")]
        [SerializeField] private KeyCode showResultKey = KeyCode.Tab;

        [Tooltip("게임 오버 화면 미리보기(배선 확인용). None이면 비활성.")]
        [SerializeField] private KeyCode debugPreviewKey = KeyCode.None;

        // ── 상태 ──────────────────────────────────────────────────
        private bool _visible;
        private bool _pauseApplied;
        private float _previousTimeScale = 1f;
        private float _shownAt;

        /// <summary>게임 오버 화면이 떠 있는지.</summary>
        public bool IsVisible => _visible;

        // ── 캐시 ──────────────────────────────────────────────────
        private readonly List<string> _lines = new List<string>();
        private string _titleText = "게임 오버";
        private string _subtitleText = "";
        private string _footerText = "";
        private string _resultHintText = "";

        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _lineStyle;
        private GUIStyle _footerStyle;

        private const float PadX = 24f;
        private const float PadY = 20f;
        private const float TitleHeight = 54f;
        private const float SubtitleHeight = 24f;
        private const float LineHeight = 22f;
        private const float FooterHeight = 28f;
        private const float Gap = 10f;

        // ── 수명 주기 ─────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.GameOver += OnGameOver;
            GameEvents.FlowStateChanged += OnFlowStateChanged;
            if (_visible) ApplyPause(true);
        }

        private void OnDisable()
        {
            GameEvents.GameOver -= OnGameOver;
            GameEvents.FlowStateChanged -= OnFlowStateChanged;
            // 오버레이가 꺼졌는데 게임이 정지된 채로 남으면 조작이 먹통이 된다 — 반드시 되돌린다.
            ApplyPause(false);
        }

        private void OnDestroy()
        {
            ApplyPause(false);
            if (Instance == this) Instance = null;
        }

        // ── 표시/숨김 ─────────────────────────────────────────────
        public void Show()
        {
            _visible = true;
            _shownAt = Time.unscaledTime;

            // 결과 화면과 겹치면 글자가 읽히지 않는다 — 하나만 남긴다.
            if (MatchResultView.Instance != null && MatchResultView.Instance.IsVisible)
                MatchResultView.Instance.Hide();

            RebuildCache();
            ApplyPause(true);
        }

        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            ApplyPause(false);
        }

        private void OnGameOver() => Show();

        private void OnFlowStateChanged(GameFlowState state)
        {
            if (state == GameFlowState.GameOver)
            {
                if (!_visible) Show();
                return;
            }

            // 재시작/로비 복귀 등으로 흐름이 넘어가면 사라져야 한다.
            Hide();
        }

        private void ApplyPause(bool pause)
        {
            if (pause)
            {
                if (_pauseApplied || !pauseWhileVisible) return;
                _previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _pauseApplied = true;
            }
            else
            {
                if (!_pauseApplied) return;
                Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
                _pauseApplied = false;
            }
        }

        // ── 입력 ──────────────────────────────────────────────────
        private void Update()
        {
            if (!_visible)
            {
                if (debugPreviewKey != KeyCode.None && Input.GetKeyDown(debugPreviewKey)) Show();
                return;
            }

            if (Input.GetKeyDown(restartKey))
            {
                Hide();                 // 정지 해제를 먼저 — 재시작 로직이 Time에 의존할 수 있다.
                RestartRequest.Raise();
                return;
            }

            if (showResultKey != KeyCode.None && Input.GetKeyDown(showResultKey) && MatchResultView.Instance != null)
            {
                // 사망으로 끝난 런도 '무엇을 했는지'는 봐야 한다 — 클리어가 아니므로 cleared=false.
                Hide();
                MatchResultView.Instance.ShowFromRunManager(false);
            }
        }

        // ── 문자열 캐시 생성 ──────────────────────────────────────
        private void RebuildCache()
        {
            _lines.Clear();

            _titleText = "게임 오버";
            _subtitleText = "공유 부활 횟수가 모두 소진되고 전원이 사망했습니다.";

            var run = RunManager.Instance;
            if (run != null)
            {
                _lines.Add("도달 스테이지        " + run.CurrentStage + " / " + GameRules.TotalBossCount);
                _lines.Add("남은 공유 부활        " + run.ReviveSystem.Remaining);

                var result = run.BuildResult();
                _lines.Add("플레이 시간            " + FormatTime(result.PlayTime));
                _lines.Add("클리어한 보스        " + result.ClearedBossIds.Count + "종");
            }
            else
            {
                // 런 관리자가 없으면 통계가 없다는 사실 자체를 보여준다(조용히 비어 있으면 배선 실수를 못 찾는다).
                _lines.Add("(RunManager가 씬에 없어 요약을 표시할 수 없습니다)");
            }

            _footerText = restartKey + " — 다시 플레이";
            if (!RestartRequest.HasHandler) _footerText += "   (재시작 핸들러 미배선 — 흐름만 전환)";

            _resultHintText = (showResultKey != KeyCode.None && MatchResultView.Instance != null)
                ? showResultKey + " — 결과 보기"
                : "";
        }

        private static string FormatTime(TimeSpan t)
        {
            double total = t.TotalSeconds;
            if (double.IsNaN(total) || total < 0d || t.TotalHours >= 100d) return "--:--";
            return ((int)t.TotalMinutes).ToString("00") + ":" + t.Seconds.ToString("00");
        }

        // ── 그리기 ────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!_visible) return;
            if (Event.current.type != EventType.Repaint) return;

            GUI.depth = -100;   // 낮을수록 위 — HUD/미니맵/배너를 덮는 ③ Overlay 계층.
            EnsureStyles();

            Color bg = UIDraw.Background(colors);
            Color text = UIDraw.Text(colors);
            Color warning = UIDraw.Warning(colors);
            Color accent = UIDraw.Accent(colors);

            UIDraw.FullScreenDim(bg, dimAlpha);

            bool hasHint = !string.IsNullOrEmpty(_resultHintText);
            float height = PadY + TitleHeight + SubtitleHeight + Gap
                           + _lines.Count * LineHeight + Gap
                           + FooterHeight + (hasHint ? LineHeight : 0f) + PadY;

            float w = Mathf.Min(panelWidth, Screen.width - 40f);
            float h = Mathf.Min(height, Screen.height - 24f);
            var panel = new Rect(Mathf.Round((Screen.width - w) * 0.5f), Mathf.Round((Screen.height - h) * 0.5f), w, h);
            UIDraw.Panel(panel, bg, UIDraw.WithAlpha(warning, 0.6f));

            float x = panel.x + PadX;
            float width = panel.width - PadX * 2f;
            float y = panel.y + PadY;

            // 제목 — 정지(timeScale 0) 중에도 화면이 살아 있음을 보이려고 unscaled 시간으로 맥동시킨다.
            Color titleColor = warning;
            if (pulseTitle)
            {
                float t = Mathf.PingPong((Time.unscaledTime - _shownAt) * 1.2f, 1f);
                titleColor = UIDraw.WithAlpha(warning, Mathf.Lerp(0.65f, 1f, t));
            }
            UIDraw.Label(new Rect(x, y, width, TitleHeight), _titleText, _titleStyle, titleColor);
            y += TitleHeight;

            UIDraw.Label(new Rect(x, y, width, SubtitleHeight), _subtitleText, _subtitleStyle, UIDraw.WithAlpha(text, 0.8f));
            y += SubtitleHeight + Gap;

            for (int i = 0; i < _lines.Count; i++)
            {
                UIDraw.Label(new Rect(x, y, width, LineHeight), _lines[i], _lineStyle, text);
                y += LineHeight;
            }
            y += Gap;

            UIDraw.Label(new Rect(x, y, width, FooterHeight), _footerText, _footerStyle, accent);
            y += FooterHeight;

            if (hasHint)
                UIDraw.Label(new Rect(x, y, width, LineHeight), _resultHintText, _lineStyle, UIDraw.WithAlpha(text, 0.7f));
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = UIDraw.MakeLabelStyle(38, TextAnchor.MiddleCenter, true);
            _subtitleStyle = UIDraw.MakeLabelStyle(14, TextAnchor.MiddleCenter, false);
            _lineStyle = UIDraw.MakeLabelStyle(14, TextAnchor.MiddleCenter, false);
            _footerStyle = UIDraw.MakeLabelStyle(16, TextAnchor.MiddleCenter, true);
        }
    }
}
