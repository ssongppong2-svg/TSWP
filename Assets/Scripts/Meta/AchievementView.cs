// 근거: 업적 시스템.md — 업적은 수집과 도전의 재미를 위한 시스템이다(진행 필수 요소 아님).
//       달성 순간의 토스트는 UI.NotificationManager/NotificationView가 담당한다(GameEvents.AchievementUnlocked 구독 확인함).
//       여기서는 "지금 얼마나 찼는가"를 보여주는 진행도 목록과 "[칭호] 닉네임" 표시를 담당한다.
//
// 프로토타입 원칙: 만든 기능은 화면에 보여야 검증된다. UGUI 대신 IMGUI로 시작하되
// OnGUI 규칙을 지킨다 — Repaint 이벤트에서만 그리고, 문자열/스타일은 캐싱한다(과거 프레임 튐 전례).
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;
using TSWP.Art;

namespace TSWP.Meta
{
    /// <summary>
    /// 업적 진행도 패널 + 표시명 나이스플레이트. AchievementManager와 같은 오브젝트에 붙이는 것을 권장한다.
    /// 매니저가 없으면 아무것도 그리지 않는다(씬 배선이 없어도 게임은 정상 동작).
    /// </summary>
    public class AchievementView : MonoBehaviour
    {
        [Header("조작")]
        [Tooltip("업적 진행도 목록 열기/닫기.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F2;

        [Tooltip("보유 칭호 순환 장착 (패널이 열려 있을 때).")]
        [SerializeField] private KeyCode cycleTitleKey = KeyCode.F3;

        [Tooltip("선택한 업적의 counterKey를 +1 발행 (패널이 열려 있을 때). 배선 검증용.")]
        [SerializeField] private KeyCode debugAddKey = KeyCode.F4;

        [Header("표시")]
        [Tooltip("패널이 닫혀 있어도 좌측 상단에 \"[칭호] 닉네임\"을 항상 표시한다.")]
        [SerializeField] private bool alwaysShowNameplate = true;

        [SerializeField] private Vector2 panelPosition = new Vector2(14f, 40f);
        [SerializeField] private float panelWidth = 340f;
        [SerializeField, Min(14f)] private float rowHeight = 22f;

        [SerializeField] private UIColorConfig colors;

        // 등급 색 — TODO(Art): AchievementGrade 색상 SO가 생기면 그쪽으로 옮긴다.
        private static readonly Color[] GradeColors =
        {
            new Color(0.75f, 0.75f, 0.78f), // Common    회색
            new Color(0.35f, 0.70f, 0.95f), // Rare      파랑
            new Color(0.65f, 0.40f, 0.95f), // Heroic    보라
            new Color(1.00f, 0.84f, 0.20f), // Legendary 금색
            new Color(1.00f, 0.35f, 0.75f), // Developer 분홍
        };

        /// <summary>한 행의 표시용 캐시. 진행도 버전이 바뀔 때만 다시 만든다.</summary>
        private struct Row
        {
            public string label;      // "이름  3/5"
            public string counterKey;
            public float ratio;
            public bool unlocked;
            public Color gradeColor;
        }

        private AchievementManager _manager;
        private LocalPlayerIdentity _boundIdentity;
        private readonly List<Row> _rows = new List<Row>();

        private bool _open;
        private int _selected;
        private int _cachedVersion = -1;
        private int _cachedCount = -1;
        private string _nameplate;
        private string _headerText;

        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;

        private void Awake()
        {
            _manager = GetComponent<AchievementManager>();
        }

        private void Start()
        {
            if (_manager == null) _manager = AchievementManager.Instance;

            _headerText = $"업적 진행도  (닫기 {toggleKey} / 칭호 {cycleTitleKey} / +1 {debugAddKey})";
            TryBindIdentity();
        }

        private void OnDestroy()
        {
            if (_boundIdentity != null)
                _boundIdentity.DisplayNameChanged -= RefreshNameplate;
        }

        /// <summary>LocalPlayerIdentity는 나중에 생성될 수도 있으므로 지연 구독한다.</summary>
        private void TryBindIdentity()
        {
            var identity = LocalPlayerIdentity.Instance;
            if (identity == _boundIdentity) return;

            if (_boundIdentity != null) _boundIdentity.DisplayNameChanged -= RefreshNameplate;

            _boundIdentity = identity;
            if (_boundIdentity != null) _boundIdentity.DisplayNameChanged += RefreshNameplate;

            RefreshNameplate();
        }

        private void Update()
        {
            if (_boundIdentity == null) TryBindIdentity();
            if (_manager == null) _manager = AchievementManager.Instance;

            // 입력은 Update에서만 처리한다 — OnGUI에 버튼을 두면 Repaint 전용 규칙을 지킬 수 없다.
            if (Input.GetKeyDown(toggleKey)) _open = !_open;
            if (!_open || _manager == null) return;

            int count = _manager.Achievements.Count;
            if (count == 0) return;

            if (Input.GetKeyDown(KeyCode.DownArrow)) _selected = (_selected + 1) % count;
            if (Input.GetKeyDown(KeyCode.UpArrow)) _selected = (_selected - 1 + count) % count;

            if (Input.GetKeyDown(cycleTitleKey))
                LocalPlayerIdentity.Instance?.EquipNextOwnedTitle();

            if (Input.GetKeyDown(debugAddKey))
            {
                var data = _manager.Achievements[Mathf.Clamp(_selected, 0, count - 1)];
                if (data != null && !string.IsNullOrEmpty(data.counterKey))
                    GameEvents.RaiseStatCounter(data.counterKey, 1);
            }
        }

        private void RefreshNameplate()
        {
            _nameplate = LocalPlayerIdentity.ResolveDisplayName("Player (LocalPlayerIdentity 없음)");
        }

        // ── 그리기 ────────────────────────────────────────────────

        private void OnGUI()
        {
            // Layout/입력 이벤트에서 중복 실행할 이유가 없다 (OnGUI 할당 최소화).
            if (Event.current.type != EventType.Repaint) return;
            if (!_open && !alwaysShowNameplate) return;

            EnsureStyles();

            Color text = colors != null ? colors.text : Color.white;

            if (alwaysShowNameplate)
            {
                Color nameColor = LocalPlayerIdentity.Instance != null
                    ? LocalPlayerIdentity.Instance.TitleColor
                    : text;

                var plate = new Rect(panelPosition.x, 12f, panelWidth, 22f);
                DrawSolid(plate, WithAlpha(colors != null ? colors.background : new Color(0.13f, 0.13f, 0.15f), 0.65f));

                GUI.color = nameColor;
                GUI.Label(Inset(plate), _nameplate, _labelStyle);
                GUI.color = Color.white;
            }

            if (!_open) return;

            if (_manager == null)
            {
                var warn = new Rect(panelPosition.x, panelPosition.y, panelWidth, rowHeight);
                DrawSolid(warn, WithAlpha(colors != null ? colors.background : new Color(0.13f, 0.13f, 0.15f), 0.85f));
                GUI.color = colors != null ? colors.warning : new Color(0.9f, 0.25f, 0.25f);
                GUI.Label(Inset(warn), "AchievementManager가 씬에 없습니다.", _labelStyle);
                GUI.color = Color.white;
                return;
            }

            RebuildCacheIfNeeded();

            float height = rowHeight * (_rows.Count + 1) + 10f;
            var panel = new Rect(panelPosition.x, panelPosition.y, panelWidth, height);
            DrawSolid(panel, WithAlpha(colors != null ? colors.background : new Color(0.13f, 0.13f, 0.15f), 0.88f));

            // 헤더
            GUI.color = colors != null ? colors.accent : new Color(1f, 0.85f, 0.2f);
            GUI.Label(Inset(new Rect(panel.x, panel.y + 4f, panel.width, rowHeight)), _headerText, _headerStyle);
            GUI.color = Color.white;

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var rect = new Rect(panel.x + 6f, panel.y + 6f + rowHeight * (i + 1), panel.width - 12f, rowHeight - 2f);

                // 선택 행 강조 — F4 대상이 어디인지 보이게 한다.
                if (i == _selected)
                    DrawSolid(rect, new Color(1f, 1f, 1f, 0.10f));

                // 진행 게이지 (배경 위에 채움)
                if (!row.unlocked && row.ratio > 0f)
                {
                    var fill = new Rect(rect.x, rect.yMax - 3f, rect.width * row.ratio, 3f);
                    DrawSolid(fill, WithAlpha(colors != null ? colors.accent : new Color(1f, 0.85f, 0.2f), 0.9f));
                }
                else if (row.unlocked)
                {
                    var fill = new Rect(rect.x, rect.yMax - 3f, rect.width, 3f);
                    DrawSolid(fill, WithAlpha(colors != null ? colors.success : new Color(0.35f, 0.8f, 0.4f), 0.9f));
                }

                GUI.color = row.unlocked
                    ? (colors != null ? colors.success : new Color(0.35f, 0.8f, 0.4f))
                    : row.gradeColor;

                GUI.Label(new Rect(rect.x + 4f, rect.y, rect.width - 8f, rect.height), row.label, _labelStyle);
                GUI.color = Color.white;
            }
        }

        /// <summary>진행도 버전이 바뀌었을 때만 문자열을 다시 만든다 (매 프레임 문자열 결합 금지).</summary>
        private void RebuildCacheIfNeeded()
        {
            int version = _manager.ProgressVersion;
            int count = _manager.Achievements.Count;
            if (version == _cachedVersion && count == _cachedCount) return;

            _cachedVersion = version;
            _cachedCount = count;
            _rows.Clear();

            for (int i = 0; i < count; i++)
            {
                var data = _manager.Achievements[i];
                if (data == null) continue;

                var progress = _manager.GetProgress(data.achievementId);
                bool unlocked = progress.isUnlocked;
                int current = Mathf.Min(progress.currentCount, data.targetCount);

                // 히든 업적은 달성 전까지 내용을 감춘다 (업적 시스템.md).
                string display = (data.isHidden && !unlocked)
                    ? "??? (히든)"
                    : (string.IsNullOrEmpty(data.displayName) ? data.achievementId : data.displayName);

                string mark = unlocked ? "✔ " : "· ";
                string counter = string.IsNullOrEmpty(data.counterKey) ? "(키 없음)" : data.counterKey;

                _rows.Add(new Row
                {
                    label = $"{mark}{display}   {current}/{data.targetCount}   [{counter}]",
                    counterKey = data.counterKey,
                    ratio = data.targetCount > 0 ? Mathf.Clamp01((float)current / data.targetCount) : 0f,
                    unlocked = unlocked,
                    gradeColor = GradeColors[Mathf.Clamp((int)data.grade, 0, GradeColors.Length - 1)],
                });
            }

            if (_selected >= _rows.Count) _selected = 0;
        }

        private void EnsureStyles()
        {
            if (_labelStyle != null) return;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                richText = false,
            };
            _labelStyle.normal.textColor = Color.white;

            _headerStyle = new GUIStyle(_labelStyle) { fontStyle = FontStyle.Bold };
        }

        private static void DrawSolid(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);   // 내장 텍스처 — 런타임 할당이 없다
            GUI.color = previous;
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static Rect Inset(Rect r) => new Rect(r.x + 6f, r.y, r.width - 12f, r.height);
    }
}
