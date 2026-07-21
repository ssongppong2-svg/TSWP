// 근거: 퍼즐 시스템.md — 퍼즐 요소는 상태가 즉시 읽혀야 한다(설계 철학 ②).
//       프로토타입 검증용 개발 HUD로, 실제 UI(TSWP.UI)와는 무관하다.
// 성능 주의: 과거 OnGUI 할당으로 프레임이 튄 전례가 있어
//   ① Repaint 이벤트에서만 그리고 ② GUIStyle과 표시 문자열을 캐싱하며 ③ 문자열 재생성은 Update에서 간격을 두고 수행한다.
using UnityEngine;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 씬의 모든 퍼즐 컨트롤러/요소 상태와 최근 퍼즐 이벤트를 화면에 표시하는 개발용 HUD.
    /// 씬에 없어도 게임 로직에는 아무 영향이 없다.
    /// </summary>
    public class PuzzleDebugHud : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private bool visibleOnStart = true;

        [Tooltip("표시를 껐다 켜는 키.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F3;

        [SerializeField] private Rect area = new Rect(12f, 12f, 460f, 520f);

        [Tooltip("문자열 재생성 간격(초). 짧을수록 최신이지만 할당이 잦아진다.")]
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

        [Tooltip("씬의 퍼즐 오브젝트를 다시 찾는 간격(초).")]
        [SerializeField, Min(0.5f)] private float rescanInterval = 2f;

        private bool _visible;
        private float _refreshTimer;
        private float _rescanTimer;
        private int _lastLogVersion = -1;

        private PuzzleController[] _controllers = System.Array.Empty<PuzzleController>();
        private PuzzleElement[] _elements = System.Array.Empty<PuzzleElement>();

        // 캐싱 — OnGUI에서는 새로 만들지 않는다.
        private GUIStyle _textStyle;
        private GUIStyle _boxStyle;
        private string _cachedText = "퍼즐 정보 수집 중...";
        private readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(1024);

        private void Awake()
        {
            _visible = visibleOnStart;
        }

        private void Start()
        {
            Rescan();
            Rebuild();
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey)) _visible = !_visible;
            if (!_visible) return;

            _rescanTimer -= Time.unscaledDeltaTime;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = rescanInterval;
                Rescan();
            }

            _refreshTimer -= Time.unscaledDeltaTime;
            bool logChanged = _lastLogVersion != PuzzleLog.Version;

            if (_refreshTimer <= 0f || logChanged)
            {
                _refreshTimer = refreshInterval;
                _lastLogVersion = PuzzleLog.Version;
                Rebuild();
            }
        }

        /// <summary>Unity 6: FindObjectOfType 계열은 제거됨 — FindObjectsByType 사용.</summary>
        private void Rescan()
        {
            _controllers = FindObjectsByType<PuzzleController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _elements = FindObjectsByType<PuzzleElement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        private void Rebuild()
        {
            _sb.Clear();
            _sb.Append("=== 퍼즐 디버그 (").Append(toggleKey).Append(" 토글) ===\n");

            _sb.Append("\n[컨트롤러 ").Append(_controllers.Length).Append("]\n");
            for (int i = 0; i < _controllers.Length; i++)
            {
                var c = _controllers[i];
                if (c == null) continue;

                _sb.Append("  ").Append(c.name)
                   .Append(" : ").Append(c.State)
                   .Append(" / 참여 ").Append(c.ParticipantCount)
                   .Append(" / 재도전 ").Append(c.RetryCount);

                if (c.Definition != null && c.Definition.HasTimeLimit)
                    _sb.Append(" / 남은시간 ").Append(c.TimerRemaining.ToString("0.0"));

                _sb.Append('\n');
            }

            _sb.Append("\n[요소 ").Append(_elements.Length).Append("]\n");
            for (int i = 0; i < _elements.Length; i++)
            {
                var e = _elements[i];
                if (e == null) continue;

                _sb.Append("  ").Append(e.name)
                   .Append(" (").Append(e.GetType().Name).Append(") : ")
                   .Append(e.DebugStatus)
                   .Append('\n');
            }

            _sb.Append("\n[최근 이벤트]\n");
            if (PuzzleLog.Count == 0)
            {
                _sb.Append("  (아직 없음 — 요소를 조작해 보세요)\n");
            }
            else
            {
                for (int i = 0; i < PuzzleLog.Count; i++)
                    _sb.Append("  ").Append(PuzzleLog.GetLine(i)).Append('\n');
            }

            _cachedText = _sb.ToString();
        }

        private void OnGUI()
        {
            if (!_visible) return;

            // 반드시 Repaint에서만 그린다 — 레이아웃/이벤트 패스에서 그리면 프레임당 여러 번 호출된다.
            if (Event.current.type != EventType.Repaint) return;

            EnsureStyles();

            _boxStyle.Draw(area, GUIContent.none, false, false, false, false);

            var textRect = new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f);
            _textStyle.Draw(textRect, _cachedText, false, false, false, false);
        }

        /// <summary>GUIStyle은 OnGUI 안에서만 GUI.skin에 접근할 수 있다. 최초 1회만 만든다.</summary>
        private void EnsureStyles()
        {
            if (_textStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box);

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                richText = false,
            };
            _textStyle.normal.textColor = Color.white;
        }
    }
}
