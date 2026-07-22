// 근거: 게임 시작과 선택, 직업, 플레이.md — "결과 화면" 항목:
//         MVP / 가장 많은 피해 / 회복 / 처치 / 사망 / 구조 / 아이템 획득 / 트롤(?) / 플레이 시간 / 클리어한 보스 / 획득한 아이템.
//       UI 시스템.md — 캔버스 ③ Overlay 계층(전체 화면 패널). 팔레트 시스템.md — 배경 어두운 회색 / 강조 노랑 / 경고 빨강.
//       ARCHITECTURE.md §5 — 결과 통계 타입은 Core.MatchResult / Core.PlayerMatchStats 한 곳. 여기서는 참조만 한다.
//
// Core.RunManager가 MatchResult를 만들어 GameEvents.MatchFinished로 발행하지만 그리는 코드가 없어
// 15보스를 클리어해도 화면에 아무것도 뜨지 않았다 — 이 뷰가 그 구멍을 메운다.
// OnGUI 규칙(프레임 튐 전례): Repaint 전용 / GUIStyle·문자열은 표시 시점에 1회 생성해 캐싱 / GUILayout 금지.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Art;
using TSWP.Core;
using TSWP.Meta;

namespace TSWP.UI
{
    /// <summary>
    /// 매치 결과 전체 화면 오버레이. 씬에 이 컴포넌트 하나만 두면 GameEvents.MatchFinished로 자동 표시된다.
    /// 다른 시스템이 없어도(RunManager/GameplayHud/LocalPlayerIdentity 미배선) 조용히 축약 표시한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchResultView : MonoBehaviour
    {
        public static MatchResultView Instance { get; private set; }

        [Header("대상")]
        [Tooltip("로컬 플레이어 id. 이 id의 이름만 LocalPlayerIdentity로 해석한다(나머지는 P0/P1…).")]
        [SerializeField] private int localPlayerId = 0;

        [Header("표시")]
        [SerializeField] private UIColorConfig colors;

        [Tooltip("결과 패널 최대 너비(px). 화면이 좁으면 자동으로 줄어든다.")]
        [SerializeField, Min(360f)] private float panelWidth = 880f;

        [Tooltip("뒤 화면을 덮는 어둡기.")]
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.82f;

        [Tooltip("보스/아이템 목록에 나열할 최대 항목 수. 넘으면 '외 N개'로 접는다.")]
        [SerializeField, Min(1)] private int maxListEntries = 10;

        [Tooltip("결과 화면이 떠 있는 동안 게임을 정지한다(Time.timeScale = 0).")]
        [SerializeField] private bool pauseWhileVisible = true;

        [Header("조작")]
        [Tooltip("다시 플레이 요청 키. 실제 재시작은 RestartRequest.Requested 구독자가 수행한다.")]
        [SerializeField] private KeyCode restartKey = KeyCode.R;

        [Tooltip("결과를 닫고 뒷풀이로 넘어가는 키. None이면 닫기 불가(재시작만).")]
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;

        [Tooltip("결과 화면 미리보기(배선 확인용). None이면 비활성.")]
        [SerializeField] private KeyCode debugPreviewKey = KeyCode.None;

        [Header("연동")]
        [Tooltip("GameFlowState.Results 진입만으로도 표시한다(MatchFinished가 오지 않는 경로 대비).")]
        [SerializeField] private bool showOnResultsState = true;

        // ── 상태 ──────────────────────────────────────────────────
        private MatchResult _result;
        private bool _cleared = true;
        private bool _visible;
        private bool _pauseApplied;
        private float _previousTimeScale = 1f;

        /// <summary>결과 화면이 떠 있는지. 게임오버 화면이 겹치지 않게 확인하는 용도.</summary>
        public bool IsVisible => _visible;

        // ── 캐시 (표시 시점에 1회 생성) ────────────────────────────
        private struct AwardRow
        {
            public string Label;
            public string Value;
            public Color Tint;
        }

        private struct PlayerRow
        {
            public string[] Cells;
            public bool IsMvp;
        }

        private readonly List<AwardRow> _awards = new List<AwardRow>();
        private readonly List<PlayerRow> _playerRows = new List<PlayerRow>();
        private readonly Dictionary<string, int> _itemCounts = new Dictionary<string, int>();

        private string _titleText = "";
        private string _metaText = "";
        private string _bossText = "";
        private string _itemText = "";
        private string _footerText = "";
        private Color _titleColor = Color.white;
        private float _panelHeight = 420f;

        private GUIStyle _titleStyle;
        private GUIStyle _metaStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _rowRightStyle;
        private GUIStyle _footerStyle;

        // ── 레이아웃 상수 ─────────────────────────────────────────
        private const float PadX = 24f;
        private const float PadY = 18f;
        private const float TitleHeight = 46f;
        private const float MetaHeight = 24f;
        private const float SectionHeight = 24f;
        private const float RowHeight = 21f;
        private const float ListHeight = 20f;
        private const float FooterHeight = 30f;
        private const float Gap = 10f;

        // 플레이어별 기록 표 — 결과 화면 항목과 1:1 (PlayerMatchStats 필드 순서).
        private static readonly string[] ColumnHeaders = { "플레이어", "피해", "회복", "처치", "사망", "구조", "아이템", "트롤", "핑" };
        private static readonly float[] ColumnWeights = { 2.6f, 1.2f, 1.2f, 0.9f, 0.9f, 0.9f, 1.0f, 0.9f, 0.8f };

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
            GameEvents.MatchFinished += OnMatchFinished;
            GameEvents.FlowStateChanged += OnFlowStateChanged;
            if (_visible) ApplyPause(true);
        }

        private void OnDisable()
        {
            GameEvents.MatchFinished -= OnMatchFinished;
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

        /// <summary>결과를 표시한다. cleared=false면 제목이 '런 종료'(경고색)로 바뀐다.</summary>
        public void Show(MatchResult result, bool cleared = true)
        {
            _result = result;
            _cleared = cleared;
            _visible = true;

            // 결과와 게임오버가 동시에 뜨면 글자가 겹쳐 읽을 수 없다 — 하나만 남긴다.
            if (GameOverView.Instance != null && GameOverView.Instance.IsVisible)
                GameOverView.Instance.Hide();

            RebuildCache();
            ApplyPause(true);
        }

        /// <summary>RunManager가 있으면 현재까지의 집계로 결과를 표시한다(없으면 빈 결과).</summary>
        public void ShowFromRunManager(bool cleared = true)
        {
            var run = RunManager.Instance;
            Show(run != null ? run.BuildResult() : new MatchResult(), cleared);
        }

        public void Hide()
        {
            if (!_visible) return;
            _visible = false;
            ApplyPause(false);
        }

        private void OnMatchFinished(MatchResult result) => Show(result, true);

        private void OnFlowStateChanged(GameFlowState state)
        {
            if (!showOnResultsState) return;

            // Results 진입만으로도 화면을 띄운다. MatchFinished가 뒤이어 오면 그 결과로 갱신된다.
            if (state == GameFlowState.Results)
            {
                if (!_visible) ShowFromRunManager(true);
                return;
            }

            // 다시 플레이/로비 복귀 등 다른 상태로 넘어가면 결과 화면은 사라져야 한다.
            if (state != GameFlowState.AfterParty) Hide();
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
                if (debugPreviewKey != KeyCode.None && Input.GetKeyDown(debugPreviewKey))
                    ShowFromRunManager(true);
                return;
            }

            // 입력은 Update에서만 처리한다 (OnGUI는 Repaint 전용).
            if (Input.GetKeyDown(restartKey))
            {
                Hide();                 // 정지 해제를 먼저 — 재시작 로직이 Time에 의존할 수 있다.
                RestartRequest.Raise();
                return;
            }

            if (closeKey != KeyCode.None && Input.GetKeyDown(closeKey))
            {
                Hide();
                GameFlowManager.Instance?.BeginAfterParty();   // 문서: 결과 화면 이후 '뒷풀이 시간'
            }
        }

        // ── 문자열 캐시 생성 ──────────────────────────────────────
        private void RebuildCache()
        {
            _awards.Clear();
            _playerRows.Clear();

            Color text = UIDraw.Text(colors);
            Color accent = UIDraw.Accent(colors);
            Color warning = UIDraw.Warning(colors);
            Color disabled = UIDraw.Disabled(colors);

            _titleText = _cleared ? "게임 클리어!" : "런 종료";
            _titleColor = _cleared ? accent : warning;

            var stats = _result != null ? _result.PerPlayerStats : null;
            int playerCount = stats != null ? stats.Count : 0;
            int bossCount = _result != null && _result.ClearedBossIds != null ? _result.ClearedBossIds.Count : 0;
            string playTime = _result != null ? FormatTime(_result.PlayTime) : "--:--";

            _metaText = "플레이 시간 " + playTime
                        + "     ·     클리어한 보스 " + bossCount + " / " + GameRules.TotalBossCount
                        + "     ·     참가 " + playerCount + "명";

            // ① MVP + 최다 기록 (문서의 결과 화면 항목 순서 그대로)
            int mvpId = _result != null ? _result.MvpPlayerId : -1;
            _awards.Add(new AwardRow
            {
                Label = "MVP",
                Value = mvpId >= 0 ? ResolveName(mvpId) : "-",
                Tint = accent,
            });

            AddBestAward("가장 많은 피해", s => s.DamageDealt, text, disabled);
            AddBestAward("가장 많은 회복", s => s.HealingDone, text, disabled);
            AddBestAward("가장 많은 처치", s => s.Kills, text, disabled);
            AddBestAward("가장 많이 사망", s => s.Deaths, warning, disabled);
            AddBestAward("가장 많은 구조", s => s.Rescues, text, disabled);
            AddBestAward("가장 많은 아이템 획득", s => s.ItemsAcquired, text, disabled);
            AddBestAward("가장 많은 트롤(?)", s => s.TrollScore, warning, disabled);

            // ② 플레이어별 표
            if (stats != null)
            {
                for (int i = 0; i < stats.Count; i++)
                {
                    var s = stats[i];
                    if (s == null) continue;

                    _playerRows.Add(new PlayerRow
                    {
                        IsMvp = s.PlayerId == mvpId,
                        Cells = new[]
                        {
                            (s.PlayerId == mvpId ? "★ " : "   ") + ResolveName(s.PlayerId),
                            FormatNumber(s.DamageDealt),
                            FormatNumber(s.HealingDone),
                            s.Kills.ToString(),
                            s.Deaths.ToString(),
                            s.Rescues.ToString(),
                            s.ItemsAcquired.ToString(),
                            s.TrollScore.ToString(),
                            s.PingsUsed.ToString(),
                        },
                    });
                }
            }

            // ③ 클리어한 보스 / ④ 획득한 아이템
            _bossText = BuildBossText();
            _itemText = BuildItemText();

            // ⑤ 하단 안내
            string restartHint = restartKey + " — 다시 플레이";
            if (!RestartRequest.HasHandler) restartHint += " (재시작 핸들러 미배선 — 흐름만 전환)";
            _footerText = closeKey != KeyCode.None
                ? restartHint + "        " + closeKey + " — 뒷풀이로"
                : restartHint;

            RecalculateHeight();
        }

        private void AddBestAward(string label, Func<PlayerMatchStats, float> selector, Color color, Color emptyColor)
        {
            PlayerMatchStats best = null;
            float bestValue = 0f;

            var stats = _result != null ? _result.PerPlayerStats : null;
            if (stats != null)
            {
                for (int i = 0; i < stats.Count; i++)
                {
                    var s = stats[i];
                    if (s == null) continue;

                    float v = selector(s);
                    if (best != null && v <= bestValue) continue;
                    best = s;
                    bestValue = v;
                }
            }

            // 아무도 기록이 없으면 '-'로 둔다 (0을 1등이라고 부르면 결과가 거짓말이 된다).
            if (best == null || bestValue <= 0f)
            {
                _awards.Add(new AwardRow { Label = label, Value = "-", Tint = emptyColor });
                return;
            }

            _awards.Add(new AwardRow
            {
                Label = label,
                Value = ResolveName(best.PlayerId) + "   " + FormatNumber(bestValue),
                Tint = color,
            });
        }

        private string BuildBossText()
        {
            var ids = _result != null ? _result.ClearedBossIds : null;
            if (ids == null || ids.Count == 0) return "(없음)";

            string joined = "";
            int shown = 0;
            for (int i = 0; i < ids.Count && shown < maxListEntries; i++)
            {
                if (string.IsNullOrEmpty(ids[i])) continue;
                joined += (shown == 0 ? "" : ", ") + ids[i];
                shown++;
            }

            int rest = ids.Count - shown;
            if (rest > 0) joined += "  외 " + rest + "종";
            return string.IsNullOrEmpty(joined) ? "(없음)" : joined;
        }

        private string BuildItemText()
        {
            _itemCounts.Clear();

            var ids = _result != null ? _result.AcquiredItemIds : null;
            if (ids == null || ids.Count == 0) return "(없음)";

            // 같은 아이템을 여러 번 먹으면 "code x3"로 접는다 — 8인 런에서 줄이 폭발하지 않게.
            for (int i = 0; i < ids.Count; i++)
            {
                string code = ids[i];
                if (string.IsNullOrEmpty(code)) continue;
                _itemCounts.TryGetValue(code, out int count);
                _itemCounts[code] = count + 1;
            }

            string joined = "";
            int shown = 0;
            foreach (var pair in _itemCounts)
            {
                if (shown >= maxListEntries) break;
                joined += (shown == 0 ? "" : ", ") + pair.Key + (pair.Value > 1 ? " x" + pair.Value : "");
                shown++;
            }

            int rest = _itemCounts.Count - shown;
            if (rest > 0) joined += "  외 " + rest + "종";
            return string.IsNullOrEmpty(joined) ? "(없음)" : joined + "   (총 " + ids.Count + "개)";
        }

        private void RecalculateHeight()
        {
            int rows = Mathf.Max(1, _playerRows.Count);
            _panelHeight = PadY
                           + TitleHeight + MetaHeight + Gap
                           + SectionHeight + _awards.Count * RowHeight + Gap
                           + SectionHeight + RowHeight + rows * RowHeight + Gap
                           + SectionHeight + ListHeight
                           + SectionHeight + ListHeight + Gap
                           + FooterHeight + PadY;
        }

        /// <summary>표시 이름 해석. 파티 정보 → 로컬 정체성 → "P{id}" 순으로 폴백한다.</summary>
        private string ResolveName(int playerId)
        {
            if (playerId < 0) return "-";

            var hud = GameplayHud.Instance;
            if (hud != null)
            {
                var members = hud.Model.PartyMembers;
                for (int i = 0; i < members.Count; i++)
                {
                    var m = members[i];
                    if (m == null || m.PlayerId != playerId) continue;
                    if (!string.IsNullOrEmpty(m.PlayerName)) return m.PlayerName;
                    break;
                }
            }

            // SYNC: 원격 플레이어 이름은 추후 NGO 수신 — 지금은 로컬 1인분만 실제 닉네임이 나온다.
            if (playerId == localPlayerId) return LocalPlayerIdentity.ResolveDisplayName("P" + playerId);
            return "P" + playerId;
        }

        private static string FormatNumber(float value)
        {
            if (value <= 0f) return "0";
            return value >= 1000f ? Mathf.Round(value).ToString("N0") : Mathf.Round(value).ToString();
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
            Color accent = UIDraw.Accent(colors);

            UIDraw.FullScreenDim(bg, dimAlpha);

            float w = Mathf.Min(panelWidth, Screen.width - 40f);
            float h = Mathf.Min(_panelHeight, Screen.height - 24f);
            var panel = new Rect(Mathf.Round((Screen.width - w) * 0.5f), Mathf.Round((Screen.height - h) * 0.5f), w, h);
            UIDraw.Panel(panel, bg, UIDraw.WithAlpha(_titleColor, 0.55f));

            float x = panel.x + PadX;
            float width = panel.width - PadX * 2f;
            float y = panel.y + PadY;

            // 제목 + 요약
            UIDraw.Label(new Rect(x, y, width, TitleHeight), _titleText, _titleStyle, _titleColor);
            y += TitleHeight;
            UIDraw.Label(new Rect(x, y, width, MetaHeight), _metaText, _metaStyle, text);
            y += MetaHeight + Gap;

            // 최다 기록
            y = DrawSection(x, y, width, "최다 기록", accent);
            float valueX = x + Mathf.Min(240f, width * 0.34f);
            for (int i = 0; i < _awards.Count; i++)
            {
                var a = _awards[i];
                UIDraw.Label(new Rect(x, y, valueX - x - 8f, RowHeight), a.Label, _rowStyle, UIDraw.WithAlpha(text, 0.75f));
                UIDraw.Label(new Rect(valueX, y, width - (valueX - x), RowHeight), a.Value, _rowStyle, a.Tint);
                y += RowHeight;
            }
            y += Gap;

            // 플레이어별 기록 표
            y = DrawSection(x, y, width, "플레이어별 기록", accent);
            y = DrawTable(x, y, width, text, accent);
            y += Gap;

            // 클리어한 보스 / 획득 아이템
            y = DrawSection(x, y, width, "클리어한 보스", accent);
            UIDraw.Label(new Rect(x, y, width, ListHeight), _bossText, _rowStyle, text);
            y += ListHeight;

            y = DrawSection(x, y, width, "획득한 아이템", accent);
            UIDraw.Label(new Rect(x, y, width, ListHeight), _itemText, _rowStyle, text);
            y += ListHeight + Gap;

            // 하단 안내
            UIDraw.Label(new Rect(x, y, width, FooterHeight), _footerText, _footerStyle, accent);
        }

        private float DrawSection(float x, float y, float width, string label, Color accent)
        {
            UIDraw.Label(new Rect(x, y, width, SectionHeight), label, _sectionStyle, accent);
            UIDraw.Separator(new Rect(x, y + SectionHeight - 4f, width, 1f), accent);
            return y + SectionHeight;
        }

        private float DrawTable(float x, float y, float width, Color text, Color accent)
        {
            float weightSum = 0f;
            for (int i = 0; i < ColumnWeights.Length; i++) weightSum += ColumnWeights[i];

            // 헤더
            float cx = x;
            for (int c = 0; c < ColumnHeaders.Length; c++)
            {
                float cw = width * (ColumnWeights[c] / weightSum);
                var style = c == 0 ? _rowStyle : _rowRightStyle;
                UIDraw.Label(new Rect(cx, y, cw - 6f, RowHeight), ColumnHeaders[c], style, UIDraw.WithAlpha(text, 0.6f));
                cx += cw;
            }
            y += RowHeight;

            if (_playerRows.Count == 0)
            {
                UIDraw.Label(new Rect(x, y, width, RowHeight), "(집계된 통계가 없습니다 — RunManager가 씬에 있는지 확인)",
                    _rowStyle, UIDraw.Disabled(colors));
                return y + RowHeight;
            }

            for (int r = 0; r < _playerRows.Count; r++)
            {
                var row = _playerRows[r];

                // MVP 행은 옅게 강조 — 표에서 눈이 먼저 가야 한다.
                if (row.IsMvp) UIDraw.Solid(new Rect(x - 4f, y, width + 8f, RowHeight), UIDraw.WithAlpha(accent, 0.10f));
                else if ((r & 1) == 1) UIDraw.Solid(new Rect(x - 4f, y, width + 8f, RowHeight), new Color(1f, 1f, 1f, 0.03f));

                cx = x;
                for (int c = 0; c < row.Cells.Length && c < ColumnWeights.Length; c++)
                {
                    float cw = width * (ColumnWeights[c] / weightSum);
                    var style = c == 0 ? _rowStyle : _rowRightStyle;
                    Color color = row.IsMvp && c == 0 ? accent : text;
                    UIDraw.Label(new Rect(cx, y, cw - 6f, RowHeight), row.Cells[c], style, color);
                    cx += cw;
                }
                y += RowHeight;
            }

            return y;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;

            _titleStyle = UIDraw.MakeLabelStyle(30, TextAnchor.MiddleCenter, true);
            _metaStyle = UIDraw.MakeLabelStyle(14, TextAnchor.MiddleCenter, false);
            _sectionStyle = UIDraw.MakeLabelStyle(14, TextAnchor.MiddleLeft, true);
            _rowStyle = UIDraw.MakeLabelStyle(13, TextAnchor.MiddleLeft, false);
            _rowRightStyle = UIDraw.MakeLabelStyle(13, TextAnchor.MiddleRight, false);
            _footerStyle = UIDraw.MakeLabelStyle(15, TextAnchor.MiddleCenter, true);
        }
    }
}
