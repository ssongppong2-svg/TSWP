// 근거: UI 시스템.md — 이모트는 캐릭터 머리 위에 표시된다.
// 근거: 조작과 시스템.md — T키 이모트 휠 기본 7종.
// 게임플레이 → 표시 통지는 Core.GameEvents.EmoteUsed 구독으로만 받는다 (게임 로직 직접 참조 금지).
//   따라서 이 뷰는 로컬 발동/원격 수신을 구분하지 않는다 — 네트워크가 EmoteUsed를 재발행하면 그대로 보인다.
// 프로토타입: 아이콘 에셋이 없으면 EmoteData.DisplayName 문자열(이모지+한국어)을 말풍선처럼 그린다.
// IMGUI 비용 규칙: Repaint에서만 그리고, 표시 문자열은 이모트가 바뀌는 순간에만 갱신한다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Player
{
    /// <summary>
    /// 이 플레이어가 사용한 이모트를 머리 위에 일정 시간 표시하는 프로토타입 뷰.
    /// 플레이어 오브젝트에 붙인다 — PlayerController.PlayerId와 일치하는 이벤트만 표시한다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    public class EmoteOverheadView : MonoBehaviour
    {
        [Header("표시")]
        [Tooltip("머리 위 표시 유지 시간(초).")]
        [SerializeField, Min(0.1f)] private float displayDuration = 2.5f; // TODO(밸런스): 문서 미정

        [Tooltip("캐릭터 기준 말풍선 위치 오프셋(월드 단위).")]
        [SerializeField] private Vector2 headOffset = new Vector2(0f, 1.1f);

        [Tooltip("말풍선 크기(px).")]
        [SerializeField] private Vector2 bubbleSize = new Vector2(110f, 30f);

        [SerializeField] private int fontSize = 14;

        [Tooltip("사라지기 직전 페이드에 쓰는 시간 비율(0~1). 0이면 페이드 없음.")]
        [Range(0f, 1f)][SerializeField] private float fadeOutRatio = 0.35f;

        private PlayerController _controller;
        private EmoteWheel _wheel;   // 표시 이름/스프라이트 조회용 (없어도 emoteId로 표시)

        private string _shownLabel;
        private Sprite _shownSprite;
        private float _expireTime;   // Time.unscaledTime 기준 — 일시정지 중에도 자연히 사라진다

        private GUIStyle _style;
        private int _styleFontSize = -1;

        private static readonly Color BubbleColor = new Color(0.08f, 0.09f, 0.12f, 0.85f);
        private static readonly Color BorderColor = new Color(0.98f, 0.78f, 0.25f, 0.9f);
        private static readonly Color TextColor = new Color(0.96f, 0.96f, 0.96f, 1f);

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _wheel = GetComponent<EmoteWheel>();
        }

        private void OnEnable() => GameEvents.EmoteUsed += OnEmoteUsed;

        private void OnDisable() => GameEvents.EmoteUsed -= OnEmoteUsed;

        private void OnEmoteUsed(int playerId, string emoteId)
        {
            if (_controller == null || playerId != _controller.PlayerId) return;
            Show(emoteId);
        }

        /// <summary>이모트 표시 시작. 외부(연출/테스트)에서도 호출 가능.</summary>
        public void Show(string emoteId)
        {
            if (string.IsNullOrEmpty(emoteId)) return;

            // 문자열/스프라이트 조회는 표시가 시작될 때 1회만 한다 (OnGUI에서 탐색 금지).
            _shownLabel = emoteId;
            _shownSprite = null;

            IReadOnlyList<EmoteData> available = _wheel != null ? _wheel.AvailableEmotes : null;
            if (available != null)
            {
                for (int i = 0; i < available.Count; i++)
                {
                    EmoteData data = available[i];
                    if (data == null || !string.Equals(data.EmoteId, emoteId)) continue;
                    if (!string.IsNullOrEmpty(data.DisplayName)) _shownLabel = data.DisplayName;
                    _shownSprite = data.Sprite;
                    break;
                }
            }

            _expireTime = Time.unscaledTime + displayDuration;
        }

        /// <summary>표시 즉시 종료 (사망/방 전환 연출 등에서 호출 가능).</summary>
        public void Clear() => _expireTime = 0f;

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            float now = Time.unscaledTime;
            if (now >= _expireTime || string.IsNullOrEmpty(_shownLabel)) return;

            // 카메라가 없으면 조용히 생략한다 (씬 배선이 없어도 실패하지 않는다).
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = transform.position + new Vector3(headOffset.x, headOffset.y, 0f);
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0f) return; // 카메라 뒤

            EnsureStyle();

            float remain = _expireTime - now;
            float alpha = 1f;
            if (fadeOutRatio > 0f)
            {
                float fadeSpan = displayDuration * fadeOutRatio;
                if (fadeSpan > 0f && remain < fadeSpan) alpha = Mathf.Clamp01(remain / fadeSpan);
            }

            var box = new Rect(
                screenPos.x - bubbleSize.x * 0.5f,
                Screen.height - screenPos.y - bubbleSize.y,
                bubbleSize.x, bubbleSize.y);

            Color previousColor = GUI.color;

            DrawRect(box, WithAlpha(BorderColor, alpha));
            DrawRect(new Rect(box.x + 2f, box.y + 2f, box.width - 4f, box.height - 4f), WithAlpha(BubbleColor, alpha));

            var inner = new Rect(box.x + 4f, box.y + 4f, box.width - 8f, box.height - 8f);
            if (_shownSprite != null && _shownSprite.texture != null)
                DrawSprite(inner, _shownSprite, alpha);
            else
                DrawLabel(inner, _shownLabel, WithAlpha(TextColor, alpha));

            GUI.color = previousColor;
        }

        private static Color WithAlpha(Color color, float alpha) =>
            new Color(color.r, color.g, color.b, color.a * alpha);

        private static void DrawRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private static void DrawSprite(Rect rect, Sprite sprite, float alpha)
        {
            Rect textureRect = sprite.textureRect;
            Texture texture = sprite.texture;
            var coords = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTextureWithTexCoords(rect, texture, coords);
        }

        private void DrawLabel(Rect rect, string text, Color color)
        {
            GUI.color = Color.white;
            _style.normal.textColor = color;
            GUI.Label(rect, text, _style);
        }

        private void EnsureStyle()
        {
            if (_style != null && _styleFontSize == fontSize) return;
            _styleFontSize = fontSize;
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleCenter,
            };
        }
    }
}
