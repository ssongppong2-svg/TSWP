// 근거: 보스 시스템.md — 9필수 요소 ⑨ '등장 연출' / 기억 요소 "압도적인 등장". 보스는 런에서 1회만 등장한다.
//       UI 시스템.md — 알림(우측 상단 토스트)과 별개로, 보스전 진입은 화면 중앙 큰 배너로 알린다.
//       팔레트 시스템.md — 강조=노랑 / 경고=빨강 / 성공=초록.
//
// 보스 등장이 토스트 한 줄로만 지나가면 보스전에 들어간 줄도 모른다 — 중앙 배너로 전환을 각인시킨다.
// OnGUI 규칙: Repaint 전용 / GUIStyle·문자열 캐싱 / GUILayout 금지. 시간은 Time.unscaledTime(정지 중에도 진행).
using UnityEngine;
using TSWP.Art;
using TSWP.Bosses;
using TSWP.Core;

namespace TSWP.UI
{
    /// <summary>
    /// 보스 등장/광폭화/격파 시 화면 중앙에 잠깐 뜨는 큰 배너.
    /// 씬에 두기만 하면 GameEvents로 자동 동작하며, 아무 배선이 없어도 bossId를 그대로 표시한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossAppearBanner : MonoBehaviour
    {
        [Header("이름 해석 (선택)")]
        [Tooltip("bossId → 표시 이름 변환용. 비우면 이벤트로 받은 bossId를 그대로 보여준다.")]
        [SerializeField] private BossData[] bossNameTable;

        [Header("표시")]
        [SerializeField] private UIColorConfig colors;

        [Tooltip("배너가 떠 있는 시간(초). 페이드 포함.")]
        [SerializeField, Min(0.2f)] private float displaySeconds = 2.6f;

        [Tooltip("나타나고 사라지는 데 쓰는 시간(초).")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.4f;

        [Tooltip("화면 세로 위치 비율(0=최상단, 1=최하단).")]
        [SerializeField, Range(0f, 1f)] private float verticalAnchor = 0.36f;

        [Tooltip("배너 띠 높이(px).")]
        [SerializeField, Min(40f)] private float bandHeight = 108f;

        [Tooltip("낮을수록 다른 IMGUI 위에 그려진다. 결과/게임오버 오버레이(-100)보다는 뒤에 둔다.")]
        [SerializeField] private int guiDepth = -50;

        [Header("표시할 순간")]
        [SerializeField] private bool showOnAppear = true;
        [SerializeField] private bool showOnEnrage = true;
        [SerializeField] private bool showOnDefeat = true;

        // ── 상태 ──────────────────────────────────────────────────
        private string _mainText = "";
        private string _subText = "";
        private Color _mainColor = Color.white;
        private float _startTime;
        private bool _active;

        private GUIStyle _mainStyle;
        private GUIStyle _subStyle;

        private void OnEnable()
        {
            GameEvents.BossAppeared += OnBossAppeared;
            GameEvents.BossEnraged += OnBossEnraged;
            GameEvents.BossDefeated += OnBossDefeated;
        }

        private void OnDisable()
        {
            GameEvents.BossAppeared -= OnBossAppeared;
            GameEvents.BossEnraged -= OnBossEnraged;
            GameEvents.BossDefeated -= OnBossDefeated;
            _active = false;
        }

        private void Update()
        {
            if (!_active) return;
            if (Time.unscaledTime - _startTime >= displaySeconds) _active = false;
        }

        // ── 이벤트 ────────────────────────────────────────────────
        private void OnBossAppeared(string bossId)
        {
            if (!showOnAppear) return;
            ShowBanner(ResolveName(bossId), "보스 등장", UIDraw.Warning(colors));
        }

        private void OnBossEnraged(string bossId)
        {
            if (!showOnEnrage) return;
            ShowBanner(ResolveName(bossId), "광폭화!", UIDraw.Warning(colors));
        }

        private void OnBossDefeated(string bossId)
        {
            if (!showOnDefeat) return;
            ShowBanner(ResolveName(bossId), "격파!", UIDraw.Success(colors));
        }

        /// <summary>배너를 직접 띄운다(보스 외의 연출에도 재사용 가능).</summary>
        public void ShowBanner(string mainText, string subText, Color mainColor)
        {
            _mainText = string.IsNullOrEmpty(mainText) ? "???" : mainText;
            _subText = subText;
            _mainColor = mainColor;
            _startTime = Time.unscaledTime;
            _active = true;
        }

        /// <summary>bossId → BossData.DisplayName. 표가 없거나 못 찾으면 bossId를 그대로 쓴다.</summary>
        private string ResolveName(string bossId)
        {
            if (bossNameTable == null || string.IsNullOrEmpty(bossId)) return bossId;

            for (int i = 0; i < bossNameTable.Length; i++)
            {
                var data = bossNameTable[i];
                if (data == null || data.BossId != bossId) continue;
                return string.IsNullOrEmpty(data.DisplayName) ? bossId : data.DisplayName;
            }
            return bossId;
        }

        // ── 그리기 ────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!_active) return;
            if (Event.current.type != EventType.Repaint) return;

            GUI.depth = guiDepth;   // 결과/게임오버 오버레이보다 뒤, HUD보다는 앞.
            EnsureStyles();

            float age = Time.unscaledTime - _startTime;
            float remaining = displaySeconds - age;
            float alpha = 1f;
            if (fadeSeconds > 0f)
            {
                alpha = Mathf.Min(age / fadeSeconds, remaining / fadeSeconds);
                alpha = Mathf.Clamp01(alpha);
            }
            if (alpha <= 0f) return;

            Color bg = UIDraw.Background(colors);
            float y = Mathf.Round(Screen.height * verticalAnchor - bandHeight * 0.5f);
            var band = new Rect(0f, y, Screen.width, bandHeight);

            // 어두운 띠 + 위아래 강조선 — 네모와 글자만으로도 "무슨 일이 났다"가 읽히게.
            UIDraw.Solid(band, UIDraw.WithAlpha(bg, 0.78f * alpha));
            UIDraw.Solid(new Rect(band.x, band.y, band.width, 2f), UIDraw.WithAlpha(_mainColor, 0.9f * alpha));
            UIDraw.Solid(new Rect(band.x, band.yMax - 2f, band.width, 2f), UIDraw.WithAlpha(_mainColor, 0.9f * alpha));

            UIDraw.Label(new Rect(band.x, band.y + 12f, band.width, 54f), _mainText, _mainStyle,
                UIDraw.WithAlpha(_mainColor, alpha));

            UIDraw.Label(new Rect(band.x, band.y + 66f, band.width, 28f), _subText, _subStyle,
                UIDraw.WithAlpha(UIDraw.Text(colors), 0.85f * alpha));
        }

        private void EnsureStyles()
        {
            if (_mainStyle != null) return;

            _mainStyle = UIDraw.MakeLabelStyle(44, TextAnchor.MiddleCenter, true);
            _subStyle = UIDraw.MakeLabelStyle(18, TextAnchor.MiddleCenter, true);
        }
    }
}
