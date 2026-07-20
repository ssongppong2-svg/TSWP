// 근거: UI 시스템.md — 알림은 화면 우측 상단에 표시한다(업적 달성/아이템 획득/보스 등장/사망/부활).
// NotificationManager는 큐만 관리하고 그리지 않았다 — 뷰가 없으면 알림이 존재해도 보이지 않는다.
//
// 프로토타입 원칙: 만든 기능은 화면에 보여야 검증된다.
// UGUI 캔버스 대신 IMGUI로 시작하되, OnGUI 할당 규칙(Repaint 전용 + 스타일/문자열 캐싱)을 지킨다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Art;

namespace TSWP.UI
{
    /// <summary>
    /// 토스트 알림 표시. NotificationManager와 같은 오브젝트에 붙인다.
    /// </summary>
    [RequireComponent(typeof(NotificationManager))]
    public class NotificationView : MonoBehaviour
    {
        [Header("배치")]
        [Tooltip("화면 우측 상단 여백.")]
        [SerializeField] private Vector2 padding = new Vector2(14f, 14f);

        [Tooltip("알림 한 칸의 크기.")]
        [SerializeField] private Vector2 itemSize = new Vector2(260f, 34f);

        [SerializeField, Min(0f)] private float itemSpacing = 6f;

        [Header("표시")]
        [Tooltip("사라지기 시작하는 남은 시간(초). 이 구간에서 서서히 투명해진다.")]
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.6f;

        [SerializeField] private UIColorConfig colors;

        private NotificationManager _manager;
        private GUIStyle _style;
        private Texture2D _solid;

        // 종류별 문구 앞에 붙는 표식 — 아이콘 에셋이 준비되기 전까지의 구분 수단.
        private static readonly Dictionary<NotificationType, string> Prefix = new()
        {
            { NotificationType.AchievementUnlocked, "★" },
            { NotificationType.ItemAcquired, "＋" },
            { NotificationType.BossAppeared, "！" },
            { NotificationType.PlayerDeath, "✖" },
            { NotificationType.PlayerRevived, "◆" },
        };

        private void Awake()
        {
            _manager = GetComponent<NotificationManager>();
        }

        private void OnDestroy()
        {
            if (_solid != null) Destroy(_solid);
        }

        private void OnGUI()
        {
            if (_manager == null) return;

            var list = _manager.ActiveNotifications;
            if (list == null || list.Count == 0) return;

            // Layout/입력 이벤트에서 중복 실행할 이유가 없다.
            if (Event.current.type != EventType.Repaint) return;

            EnsureResources();

            float now = Time.unscaledTime;
            float x = Screen.width - itemSize.x - padding.x;

            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry == null) continue;

                float y = padding.y + i * (itemSize.y + itemSpacing);
                var rect = new Rect(x, y, itemSize.x, itemSize.y);

                // 만료 직전에는 서서히 사라진다 — 갑자기 없어지면 눈에 거슬린다.
                float alpha = 1f;
                float age = now - entry.RaisedTime;
                float remaining = _manager.DisplaySeconds - age;
                if (remaining < fadeOutSeconds)
                    alpha = Mathf.Clamp01(remaining / Mathf.Max(0.01f, fadeOutSeconds));

                DrawItem(rect, entry, alpha);
            }
        }

        private void DrawItem(Rect rect, NotificationEntry entry, float alpha)
        {
            Color previous = GUI.color;

            // 배경
            Color panel = colors != null ? colors.background : new Color(0.13f, 0.13f, 0.15f);
            panel.a = 0.82f * alpha;
            GUI.color = panel;
            GUI.DrawTexture(rect, _solid);

            // 종류별 강조 띠 — 왼쪽에 색으로 구분한다.
            Color accent = GetAccent(entry.Type);
            accent.a = alpha;
            GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x, rect.y, 4f, rect.height), _solid);

            // 문구
            Color text = colors != null ? colors.text : Color.white;
            text.a = alpha;
            GUI.color = text;

            string prefix = Prefix.TryGetValue(entry.Type, out string p) ? p + " " : "";
            var textRect = new Rect(rect.x + 12f, rect.y, rect.width - 18f, rect.height);
            GUI.Label(textRect, prefix + entry.Message, _style);

            GUI.color = previous;
        }

        /// <summary>알림 종류별 색 — 팔레트 시스템.md의 의미를 따른다.</summary>
        private Color GetAccent(NotificationType type)
        {
            if (colors != null)
            {
                switch (type)
                {
                    case NotificationType.AchievementUnlocked: return colors.accent;  // 강조 = 노랑
                    case NotificationType.ItemAcquired: return colors.success;        // 성공 = 초록
                    case NotificationType.BossAppeared: return colors.warning;        // 경고 = 빨강
                    case NotificationType.PlayerDeath: return colors.warning;
                    case NotificationType.PlayerRevived: return colors.success;
                }
            }

            switch (type)
            {
                case NotificationType.AchievementUnlocked: return new Color(1f, 0.85f, 0.2f);
                case NotificationType.ItemAcquired: return new Color(0.35f, 0.8f, 0.4f);
                case NotificationType.BossAppeared: return new Color(0.9f, 0.25f, 0.25f);
                case NotificationType.PlayerDeath: return new Color(0.9f, 0.25f, 0.25f);
                case NotificationType.PlayerRevived: return new Color(0.4f, 0.75f, 0.95f);
                default: return Color.white;
            }
        }

        private void EnsureResources()
        {
            if (_solid == null)
            {
                _solid = new Texture2D(1, 1);
                _solid.SetPixel(0, 0, Color.white);
                _solid.Apply();
            }

            if (_style != null) return;

            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
            };
        }
    }
}
