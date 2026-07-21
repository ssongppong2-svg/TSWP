// 근거: 조작과 시스템.md — 마우스 휠 클릭 핑: 현재 위치/대상을 팀원에게 표시 (위험/이동/아이템/집합/도움 5종).
// 근거: 온라인 시스템.md — 음성이 불편한 상황에서도 최소한의 협동이 가능해야 한다.
// PingType은 TSWP.Core 한 곳에만 정의된다 (ARCHITECTURE.md §5) — 여기서는 참조만 한다.
// 마커 수명 관리는 Online.PingBroadcaster가 이미 소유하므로, 씬에 있으면 그 목록을 그대로 그린다(중복 구현 금지).
//   PingBroadcaster가 없는 씬에서만 GameEvents.PingRaised를 구독해 자체 목록을 유지한다.
// IMGUI 비용 규칙: Repaint에서만 그리고, 라벨/색상은 정적 배열 상수라 매 프레임 문자열 생성이 없다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Player
{
    /// <summary>
    /// 월드 핑 마커를 화면에 그리는 프로토타입 뷰. 씬에 하나만 두면 된다(플레이어에 붙이지 않는다).
    /// 카메라/브로드캐스터가 없어도 조용히 생략된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PingMarkerView : MonoBehaviour
    {
        /// <summary>PingBroadcaster가 없는 씬에서 쓰는 자체 마커.</summary>
        private sealed class LocalMarker
        {
            public PingType Type;
            public Vector2 WorldPos;
            public int SenderId;
            public float ExpireTime;   // Time.unscaledTime 기준
            public float Lifetime;
        }

        [Header("표시")]
        [Tooltip("PingBroadcaster가 없을 때 사용하는 마커 유지 시간(초). 브로드캐스터가 있으면 그쪽 값이 우선한다.")]
        [SerializeField, Min(0.5f)] private float fallbackLifetime = 5f; // TODO(밸런스): 문서 미정

        [Tooltip("마커 한 개의 크기(px).")]
        [SerializeField] private Vector2 markerSize = new Vector2(96f, 26f);

        [SerializeField] private int fontSize = 13;

        [Tooltip("화면 밖 핑을 화면 가장자리에 붙여서 방향만 알려준다.")]
        [SerializeField] private bool clampToScreen = true;

        [Tooltip("가장자리로 밀 때 남기는 여백(px).")]
        [SerializeField] private float screenEdgePadding = 24f;

        [Tooltip("발신자 id를 함께 표시한다(누가 찍었는지 확인용).")]
        [SerializeField] private bool showSenderId = true;

        [Header("현재 선택된 핑 종류 표시")]
        [Tooltip("화면 하단에 '지금 휠클릭하면 어떤 핑이 나가는지'를 보여준다.")]
        [SerializeField] private bool showSelectedType = true;

        [Tooltip("표시 대상 PingEmitter. 비워 두면 씬에서 처음 찾은 것을 쓴다(로컬 플레이어 1인 프로토타입 전제).")]
        [SerializeField] private PingEmitter selectedTypeSource;

        private readonly List<LocalMarker> _local = new List<LocalMarker>();
        private readonly List<LocalMarker> _pool = new List<LocalMarker>();

        private GUIStyle _style;
        private GUIStyle _smallStyle;
        private int _styleFontSize = -1;

        // 핑 5종 라벨/색 — PingType 선언 순서(Danger, Move, Item, Rally, Help)와 1:1이다.
        private static readonly string[] TypeLabels = { "위험!", "이동", "아이템", "집합", "도움!" };
        private static readonly Color[] TypeColors =
        {
            new Color(0.92f, 0.24f, 0.24f, 0.95f),  // Danger  — 빨강
            new Color(0.30f, 0.72f, 0.98f, 0.95f),  // Move    — 하늘
            new Color(0.98f, 0.82f, 0.25f, 0.95f),  // Item    — 노랑
            new Color(0.36f, 0.85f, 0.45f, 0.95f),  // Rally   — 초록
            new Color(0.80f, 0.44f, 0.95f, 0.95f),  // Help    — 보라
        };

        private static readonly Color BackdropColor = new Color(0.05f, 0.05f, 0.07f, 0.8f);
        private static readonly string[] SenderTags = { "P0", "P1", "P2", "P3", "P4", "P5", "P6", "P7" };

        private void OnEnable() => GameEvents.PingRaised += OnPingRaised;

        private void OnDisable() => GameEvents.PingRaised -= OnPingRaised;

        private void Start()
        {
            // 배선을 잊어도 동작하도록 1회만 탐색한다 (Unity 6: FindObjectOfType 제거 → FindFirstObjectByType).
            if (showSelectedType && selectedTypeSource == null)
                selectedTypeSource = FindFirstObjectByType<PingEmitter>();
        }

        private void OnPingRaised(int senderId, PingType type, Vector2 worldPos)
        {
            // 브로드캐스터가 이미 마커를 보관 중이면 자체 목록을 만들지 않는다 (이중 표시 방지).
            if (Online.PingBroadcaster.Instance != null) return;

            LocalMarker marker;
            if (_pool.Count > 0)
            {
                marker = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                marker = new LocalMarker();
            }

            marker.Type = type;
            marker.WorldPos = worldPos;
            marker.SenderId = senderId;
            marker.Lifetime = fallbackLifetime;
            marker.ExpireTime = Time.unscaledTime + fallbackLifetime;
            _local.Add(marker);
        }

        private void Update()
        {
            if (_local.Count == 0) return;

            float now = Time.unscaledTime;
            for (int i = _local.Count - 1; i >= 0; i--)
            {
                if (_local[i].ExpireTime > now) continue;
                _pool.Add(_local[i]);       // 재사용 — 핑 도배 시에도 할당이 늘지 않는다
                _local.RemoveAt(i);
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            EnsureStyles();
            Color previousColor = GUI.color;

            // 카메라가 없으면 월드 마커만 생략한다 (선택 종류 패널은 계속 보인다).
            Camera cam = Camera.main;
            var broadcaster = Online.PingBroadcaster.Instance;
            if (cam == null)
            {
                // 아무것도 그리지 않는다 — 아래 패널만 처리.
            }
            else if (broadcaster != null)
            {
                // 마커 수명은 브로드캐스터가 소유한다 — 여기서는 그리기만 한다.
                IReadOnlyList<Online.PingMarker> markers = broadcaster.Markers;
                for (int i = 0; i < markers.Count; i++)
                {
                    Online.PingMarker m = markers[i];
                    if (m == null) continue;
                    DrawMarker(cam, m.worldPosition, m.type, (int)m.senderPlayerId, RemainRatio(m.remainingSeconds));
                }
            }
            else
            {
                float now = Time.unscaledTime;
                for (int i = 0; i < _local.Count; i++)
                {
                    LocalMarker m = _local[i];
                    float remain = m.ExpireTime - now;
                    DrawMarker(cam, m.WorldPos, m.Type, m.SenderId,
                               m.Lifetime <= 0f ? 1f : Mathf.Clamp01(remain / m.Lifetime));
                }
            }

            DrawSelectedTypePanel();

            GUI.color = previousColor;
        }

        /// <summary>화면 하단: 지금 휠클릭하면 어떤 핑이 나가는지 + 조작 안내.</summary>
        private void DrawSelectedTypePanel()
        {
            if (!showSelectedType || selectedTypeSource == null) return;

            int index = (int)selectedTypeSource.SelectedType;
            if (index < 0 || index >= TypeLabels.Length) return;

            var panel = new Rect(Screen.width * 0.5f - 130f, Screen.height - 46f, 260f, 34f);
            DrawRect(panel, BackdropColor);
            DrawRect(new Rect(panel.x, panel.y, 4f, panel.height), TypeColors[index]);

            DrawLabel(new Rect(panel.x + 8f, panel.y + 2f, panel.width - 16f, 16f),
                      TypeLabels[index], TypeColors[index], _style);
            DrawLabel(new Rect(panel.x + 8f, panel.y + 17f, panel.width - 16f, 14f),
                      SelectHintText, Color.gray, _smallStyle);
        }

        private const string SelectHintText = "휠클릭 = 핑 찍기 / 1~5 = 종류 변경";

        /// <summary>브로드캐스터는 남은 초만 알려주므로 fallbackLifetime을 기준으로 비율을 낸다(표시용 근사).</summary>
        private float RemainRatio(float remainingSeconds) =>
            fallbackLifetime <= 0f ? 1f : Mathf.Clamp01(remainingSeconds / fallbackLifetime);

        private void DrawMarker(Camera cam, Vector2 worldPos, PingType type, int senderId, float remainRatio)
        {
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            bool behind = screenPos.z < 0f;
            if (behind)
            {
                if (!clampToScreen) return;
                screenPos.x = Screen.width - screenPos.x;
                screenPos.y = Screen.height - screenPos.y;
            }

            float guiX = screenPos.x;
            float guiY = Screen.height - screenPos.y; // IMGUI는 y가 아래로 증가

            if (clampToScreen)
            {
                guiX = Mathf.Clamp(guiX, screenEdgePadding + markerSize.x * 0.5f,
                                   Screen.width - screenEdgePadding - markerSize.x * 0.5f);
                guiY = Mathf.Clamp(guiY, screenEdgePadding + markerSize.y,
                                   Screen.height - screenEdgePadding - markerSize.y);
            }
            else if (guiX < 0f || guiX > Screen.width || guiY < 0f || guiY > Screen.height)
            {
                return;
            }

            int index = (int)type;
            if (index < 0 || index >= TypeColors.Length) index = 0;
            Color color = TypeColors[index];

            // 본체 — 테두리(핑 색) + 어두운 배경
            var box = new Rect(guiX - markerSize.x * 0.5f, guiY - markerSize.y, markerSize.x, markerSize.y);
            DrawRect(box, color);
            DrawRect(new Rect(box.x + 2f, box.y + 2f, box.width - 4f, box.height - 4f), BackdropColor);

            DrawLabel(new Rect(box.x, box.y, box.width, box.height - 4f), TypeLabels[index], color, _style);

            // 지점 표시 — 마커 아래쪽에 작은 사각형을 찍어 실제 핑 위치를 가리킨다.
            DrawRect(new Rect(guiX - 3f, guiY, 6f, 6f), color);

            // 남은 시간 = 아래에서 줄어드는 띠 (문자열 생성 없음)
            float ratio = Mathf.Clamp01(remainRatio);
            DrawRect(new Rect(box.x, box.yMax - 3f, box.width * ratio, 3f), color);

            if (showSenderId && senderId >= 0 && senderId < SenderTags.Length)
            {
                DrawLabel(new Rect(box.x, box.y - 14f, box.width, 14f),
                          SenderTags[senderId], color, _smallStyle);
            }
        }

        private static void DrawRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private static void DrawLabel(Rect rect, string text, Color color, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            GUI.color = Color.white;
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private void EnsureStyles()
        {
            if (_style != null && _styleFontSize == fontSize) return;
            _styleFontSize = fontSize;

            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
            };
            _smallStyle = new GUIStyle(_style) { fontSize = Mathf.Max(9, fontSize - 3) };
        }
    }
}
