// 근거: 조작과 시스템.md — T키 이모트 휠, 기본 7종(😀 😂 😭 👍 👎 💀 🖕).
// 근거: UI 시스템.md — UI 5원칙(2초 안에 이해 / 전투 방해 없이).
// 프로토타입 뷰: 아이콘 에셋이 없어도 텍스트만으로 휠이 보이고 선택된다.
//   에셋(Core.EmoteData)이 하나도 배선되지 않으면 아래 fallback 7종을 그대로 사용한다
//   (씬 배선이 없어도 기능이 실패하지 않는다 — 프로젝트 규칙).
// 선택 확정은 EmoteWheel.SelectEmote 단일 경로만 사용한다 (GameEvents 발행 중복 금지).
// IMGUI 비용 규칙: ① Repaint에서만 그린다 ② GUIStyle 1회 생성 ③ 문자열은 휠이 열릴 때만 재조립
//   ④ GUILayout 미사용(고정 Rect) — 이 프로젝트에서 OnGUI 할당으로 프레임이 튄 전례가 있다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Player
{
    /// <summary>
    /// EmoteWheel의 열림 상태를 화면에 그리는 프로토타입 라디얼 뷰.
    /// 조작: T로 열기/닫기 → 숫자키 1~9로 즉시 선택, 또는 마우스 방향으로 강조 후 Enter 확정, ESC 취소.
    /// </summary>
    [RequireComponent(typeof(EmoteWheel))]
    [DisallowMultipleComponent]
    public class EmoteWheelView : MonoBehaviour
    {
        [Header("배치")]
        [Tooltip("휠 반지름(px). 화면 중앙을 기준으로 이모트가 원형 배치된다.")]
        [SerializeField] private float wheelRadius = 130f;
        [Tooltip("이모트 한 칸의 크기(px).")]
        [SerializeField] private float itemSize = 74f;
        [SerializeField] private int fontSize = 15;

        [Header("조작")]
        [Tooltip("마우스 방향으로 강조한 이모트를 확정하는 키. 좌클릭은 기본 공격과 겹치므로 Enter를 기본값으로 둔다.")]
        [SerializeField] private KeyCode confirmKey = KeyCode.Return;
        [Tooltip("켜면 좌클릭으로도 확정한다. 단 같은 프레임에 기본 공격도 함께 나간다(프로토타입 한계).")]
        [SerializeField] private bool confirmWithMouseClick = false;
        [Tooltip("숫자키 1~N으로 즉시 선택 (1 = 12시 방향부터 시계 방향).")]
        [SerializeField] private bool enableNumberKeys = true;

        [Header("에셋이 없을 때 쓰는 기본 7종 (조작과 시스템.md)")]
        [Tooltip("EmoteWheel에 EmoteData가 하나도 배선되지 않았을 때 사용하는 이모트 id.")]
        [SerializeField]
        private string[] fallbackEmoteIds =
        {
            "emote_smile", "emote_laugh", "emote_cry", "emote_thumbup",
            "emote_thumbdown", "emote_skull", "emote_taunt",
        };

        [Tooltip("위 id에 대응하는 표시 문자열. 이모지 글리프가 없는 폰트에서도 읽히도록 한국어를 함께 적는다.")]
        [SerializeField]
        private string[] fallbackEmoteLabels =
        {
            "😀 웃음", "😂 폭소", "😭 울음", "👍 좋아요",
            "👎 싫어요", "💀 사망", "🖕 도발",
        };

        private EmoteWheel _wheel;

        // 열릴 때 1회만 조립하는 표시 캐시 (OnGUI에서 문자열을 만들지 않는다)
        private readonly List<string> _ids = new List<string>();
        private readonly List<string> _labels = new List<string>();
        private readonly List<Sprite> _sprites = new List<Sprite>();

        private int _highlighted = -1;

        private GUIStyle _itemStyle;
        private GUIStyle _hintStyle;
        private int _styleFontSize = -1;

        private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color ItemColor = new Color(0.12f, 0.13f, 0.16f, 0.92f);
        private static readonly Color HighlightColor = new Color(0.98f, 0.78f, 0.25f, 0.95f);
        private static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f, 1f);

        private const string HintText = "숫자키 1~7 선택 / 마우스 방향 + Enter 확정 / ESC 취소";

        private void Awake() => _wheel = GetComponent<EmoteWheel>();

        private void OnEnable()
        {
            if (_wheel != null) _wheel.OpenStateChanged += OnOpenStateChanged;
        }

        private void OnDisable()
        {
            if (_wheel != null) _wheel.OpenStateChanged -= OnOpenStateChanged;
        }

        /// <summary>휠이 열리는 순간에만 표시 목록을 조립한다 (매 프레임 할당 방지).</summary>
        private void OnOpenStateChanged(bool open)
        {
            if (!open)
            {
                _highlighted = -1;
                return;
            }
            RebuildEntries();
            _highlighted = _ids.Count > 0 ? 0 : -1;
        }

        private void RebuildEntries()
        {
            _ids.Clear();
            _labels.Clear();
            _sprites.Clear();

            IReadOnlyList<EmoteData> available = _wheel != null ? _wheel.AvailableEmotes : null;
            if (available != null && available.Count > 0)
            {
                for (int i = 0; i < available.Count; i++)
                {
                    EmoteData data = available[i];
                    if (data == null || string.IsNullOrEmpty(data.EmoteId)) continue;
                    // TODO: 해금 필터(IsUnlockable) — Meta 보유 이모트 목록 연동 시 여기서 제외한다.
                    _ids.Add(data.EmoteId);
                    _labels.Add(string.IsNullOrEmpty(data.DisplayName) ? data.EmoteId : data.DisplayName);
                    _sprites.Add(data.Sprite);
                }
            }

            if (_ids.Count > 0) return;

            // 에셋 미배선 폴백 — 배선 담당자가 EmoteData 7종을 만들기 전에도 화면에 보인다.
            int count = Mathf.Min(fallbackEmoteIds.Length, fallbackEmoteLabels.Length);
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(fallbackEmoteIds[i])) continue;
                _ids.Add(fallbackEmoteIds[i]);
                _labels.Add(fallbackEmoteLabels[i]);
                _sprites.Add(null);
            }
        }

        private void Update()
        {
            if (_wheel == null || !_wheel.IsOpen) return;
            if (_ids.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _wheel.SetOpen(false);
                return;
            }

            // 숫자키 즉시 선택 — 가장 확실한 프로토타입 조작.
            if (enableNumberKeys)
            {
                int max = Mathf.Min(_ids.Count, 9);
                for (int i = 0; i < max; i++)
                {
                    if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
                    Confirm(i);
                    return;
                }
            }

            // 마우스 방향 강조 — 화면 중앙에서 커서 쪽 각도로 칸을 고른다.
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 delta = (Vector2)Input.mousePosition - center;
            if (delta.sqrMagnitude > 400f) // 중앙 근처(20px)에서는 이전 강조를 유지한다
            {
                // 12시 = 0번, 시계 방향으로 증가.
                float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;
                float step = 360f / _ids.Count;
                _highlighted = Mathf.RoundToInt(angle / step) % _ids.Count;
            }

            bool confirmed = Input.GetKeyDown(confirmKey)
                             || (confirmWithMouseClick && Input.GetMouseButtonDown(0));
            if (confirmed) Confirm(_highlighted);
        }

        /// <summary>선택 확정 — 발동/통계/휠 닫기는 전부 EmoteWheel.SelectEmote가 담당한다.</summary>
        private void Confirm(int index)
        {
            if (index < 0 || index >= _ids.Count) return;
            _wheel.SelectEmote(_ids[index]);
        }

        private void OnGUI()
        {
            // ① Repaint 이벤트에서만 그린다.
            if (Event.current.type != EventType.Repaint) return;
            if (_wheel == null || !_wheel.IsOpen || _ids.Count == 0) return;

            EnsureStyles();

            Color previousColor = GUI.color;

            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float outer = wheelRadius + itemSize;
            DrawRect(new Rect(center.x - outer, center.y - outer, outer * 2f, outer * 2f), BackdropColor);

            float step = 360f / _ids.Count;
            for (int i = 0; i < _ids.Count; i++)
            {
                float rad = (i * step) * Mathf.Deg2Rad;
                // 화면 좌표계는 y가 아래로 증가하므로 12시 방향은 -cos.
                float x = center.x + Mathf.Sin(rad) * wheelRadius - itemSize * 0.5f;
                float y = center.y - Mathf.Cos(rad) * wheelRadius - itemSize * 0.5f;
                var box = new Rect(x, y, itemSize, itemSize);

                bool isHighlighted = i == _highlighted;
                DrawRect(box, isHighlighted ? HighlightColor : ItemColor);

                Rect inner = new Rect(box.x + 2f, box.y + 2f, box.width - 4f, box.height - 4f);
                if (!isHighlighted) DrawRect(inner, ItemColor);

                Sprite sprite = _sprites[i];
                if (sprite != null && sprite.texture != null)
                {
                    DrawSprite(inner, sprite);
                }
                else
                {
                    // 아이콘 에셋이 없어도 무엇인지 알 수 있어야 한다.
                    DrawLabel(inner, _labels[i], isHighlighted ? Color.black : TextColor, _itemStyle);
                }

                // 숫자키 힌트 (1~9만)
                if (i < 9)
                {
                    DrawLabel(new Rect(box.x, box.y - 2f, box.width, 16f),
                              NumberHint(i), isHighlighted ? Color.black : TextColor, _hintStyle);
                }
            }

            // 중앙: 현재 강조 중인 이모트 이름 + 조작 안내
            if (_highlighted >= 0 && _highlighted < _labels.Count)
            {
                DrawLabel(new Rect(center.x - wheelRadius, center.y - 14f, wheelRadius * 2f, 22f),
                          _labels[_highlighted], TextColor, _itemStyle);
            }
            DrawLabel(new Rect(center.x - wheelRadius, center.y + 10f, wheelRadius * 2f, 18f),
                      HintText, TextColor, _hintStyle);

            GUI.color = previousColor;
        }

        /// <summary>숫자키 힌트 문자열 — 상수 배열이라 매 프레임 할당이 없다.</summary>
        private static string NumberHint(int index) => NumberHints[index];

        private static readonly string[] NumberHints = { "1", "2", "3", "4", "5", "6", "7", "8", "9" };

        // ── 그리기 유틸 (할당 없음) ───────────────────────────────
        private static void DrawRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            Rect textureRect = sprite.textureRect;
            Texture texture = sprite.texture;
            var coords = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);

            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(rect, texture, coords);
        }

        private static void DrawLabel(Rect rect, string text, Color color, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            GUI.color = Color.white;
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        /// <summary>② GUIStyle은 1회만 만든다 (OnGUI에서 new GUIStyle = 프레임마다 할당).</summary>
        private void EnsureStyles()
        {
            if (_itemStyle != null && _styleFontSize == fontSize) return;
            _styleFontSize = fontSize;

            _itemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
            };
            _hintStyle = new GUIStyle(_itemStyle) { fontSize = Mathf.Max(9, fontSize - 4) };
        }
    }
}
