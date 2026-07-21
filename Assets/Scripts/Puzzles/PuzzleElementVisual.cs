// 근거: 퍼즐 시스템.md — 설계 철학 ② "설명 없이도 이해 가능해야 한다", 트롤 원칙 ④ "같은 실수를 반복하지 않도록 피드백을 제공한다".
//       프로토타입 단계에서는 상태 변화가 '색과 위치'로 즉시 보이면 충분하다 (에셋 없이도 동작해야 한다).
// 근거: ARCHITECTURE.md §3-3 — MonoBehaviour가 아니므로 파일명 1:1 규칙 대상이 아니지만, 대표 타입명으로 파일을 맞춰 둔다.
//
// 이 클래스는 MonoBehaviour가 아니라 각 퍼즐 요소가 [SerializeField]로 품는 헬퍼다.
// 스프라이트가 하나도 없으면 런타임에 흰 사각형을 자동 생성하므로, 씬에 빈 GameObject만 놓아도 눈에 보인다.
using System;
using UnityEngine;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 퍼즐 요소의 프로토타입 시각 피드백(색/눌림/회전). 호스트가 Bind → Tick만 호출하면 된다.
    /// </summary>
    [Serializable]
    public class PuzzleElementVisual
    {
        [Tooltip("상태에 따라 색이 바뀔 스프라이트. 비우면 자식에서 찾고, 그래도 없으면 흰 사각형을 자동 생성한다.")]
        [SerializeField] private SpriteRenderer target;

        [Tooltip("스프라이트를 자동 생성할 때의 크기(유닛).")]
        [SerializeField] private Vector2 autoCreateSize = Vector2.one;

        [Tooltip("스프라이트를 자동 생성할 때의 로컬 위치 오프셋.")]
        [SerializeField] private Vector3 autoCreateLocalOffset = Vector3.zero;

        [Header("색")]
        [Tooltip("기본(비활성) 색.")]
        [SerializeField] private Color idleColor = new Color(0.60f, 0.62f, 0.70f, 1f);

        [Tooltip("활성(눌림/당김/점유/운반 중) 색.")]
        [SerializeField] private Color activeColor = new Color(0.35f, 0.90f, 0.45f, 1f);

        [Tooltip("오조작(트롤) 섬광 색.")]
        [SerializeField] private Color wrongColor = new Color(0.95f, 0.30f, 0.25f, 1f);

        [Header("움직임 (자식 스프라이트에만 적용 — 본체를 돌리면 콜라이더가 같이 돈다)")]
        [Tooltip("활성 상태에서 스프라이트가 이동할 로컬 오프셋. 버튼이 '내려가는' 표현.")]
        [SerializeField] private Vector3 activeLocalOffset = Vector3.zero;

        [Tooltip("활성 상태에서 스프라이트가 회전할 각도(도). 레버가 '넘어가는' 표현.")]
        [SerializeField] private float activeLocalAngle = 0f;

        [Header("반응 속도")]
        [Tooltip("섬광 유지 시간(초).")]
        [SerializeField, Min(0f)] private float flashSeconds = 0.4f;

        [Tooltip("색/위치 보간 속도. 클수록 즉각적이다.")]
        [SerializeField, Min(0.1f)] private float lerpSpeed = 14f;

        private Transform _tr;
        private Vector3 _restLocalPos;
        private float _restLocalAngle;
        private bool _canTransform;   // 본체 트랜스폼이면 이동/회전 금지 (콜라이더 보호)
        private bool _bound;

        private bool _active;
        private float _flashTimer;
        private Color _flashColor;
        private bool _hasOverride;
        private Color _overrideColor;

        /// <summary>현재 사용 중인 스프라이트 렌더러 (없을 수 있다).</summary>
        public SpriteRenderer Renderer => target;

        public bool IsActive => _active;

        /// <summary>
        /// 호스트 컴포넌트에 결합한다. Awake에서 1회 호출.
        /// 스프라이트가 전혀 없으면 자식 오브젝트로 흰 사각형을 만들어 붙인다(에셋 불필요).
        /// </summary>
        public void Bind(Component host)
        {
            if (_bound || host == null) return;
            _bound = true;

            if (target == null)
                target = host.GetComponentInChildren<SpriteRenderer>();

            if (target == null)
            {
                var go = new GameObject("PuzzleVisual");
                go.transform.SetParent(host.transform, false);
                go.transform.localScale = new Vector3(
                    Mathf.Max(0.05f, autoCreateSize.x),
                    Mathf.Max(0.05f, autoCreateSize.y),
                    1f);

                target = go.AddComponent<SpriteRenderer>();
                target.sprite = PuzzlePrimitiveSprite.White;
            }
            else if (target.sprite == null)
            {
                target.sprite = PuzzlePrimitiveSprite.White;
            }

            _tr = target.transform;
            _canTransform = _tr != host.transform; // 본체면 위치/회전은 건드리지 않는다
            _restLocalPos = _tr.localPosition;
            _restLocalAngle = _tr.localEulerAngles.z;

            target.color = idleColor;
        }

        /// <summary>활성/비활성 상태 지정 (눌림·당김·점유·운반 등).</summary>
        public void SetActive(bool active) => _active = active;

        /// <summary>
        /// 요소 종류별 기본 연출 제안 (버튼은 내려가고, 레버는 넘어간다).
        /// 인스펙터에서 값을 지정한 경우에는 건드리지 않는다 — 0인 항목만 채운다.
        /// </summary>
        public void SuggestMotion(Vector3 offset, float angle)
        {
            if (activeLocalOffset == Vector3.zero) activeLocalOffset = offset;
            if (Mathf.Approximately(activeLocalAngle, 0f)) activeLocalAngle = angle;
        }

        /// <summary>상태 색을 무시하고 지정 색으로 고정 (해결/전달 완료 등).</summary>
        public void SetOverrideColor(Color color)
        {
            _hasOverride = true;
            _overrideColor = color;
        }

        public void ClearOverrideColor() => _hasOverride = false;

        /// <summary>오조작 섬광 (기본 색은 wrongColor).</summary>
        public void Flash() => Flash(wrongColor);

        public void Flash(Color color)
        {
            _flashColor = color;
            _flashTimer = Mathf.Max(0.05f, flashSeconds);

            // 섬광은 즉시 보여야 의미가 있다 — 보간을 기다리지 않고 바로 칠한다.
            if (target != null) target.color = color;
        }

        /// <summary>호스트의 Update에서 매 프레임 호출한다.</summary>
        public void Tick(float dt)
        {
            if (!_bound || target == null) return;

            Color goal = _hasOverride ? _overrideColor : (_active ? activeColor : idleColor);

            if (_flashTimer > 0f)
            {
                _flashTimer -= dt;
                goal = _flashColor;
            }

            float t = 1f - Mathf.Exp(-lerpSpeed * dt); // 프레임레이트 독립 보간
            target.color = Color.Lerp(target.color, goal, t);

            if (!_canTransform || _tr == null) return;

            Vector3 wantPos = _restLocalPos + (_active ? activeLocalOffset : Vector3.zero);
            float wantAngle = _restLocalAngle + (_active ? activeLocalAngle : 0f);

            _tr.localPosition = Vector3.Lerp(_tr.localPosition, wantPos, t);
            float z = Mathf.LerpAngle(_tr.localEulerAngles.z, wantAngle, t);
            _tr.localEulerAngles = new Vector3(0f, 0f, z);
        }

        /// <summary>초기 상태로 되돌린다 (퍼즐 리셋 시).</summary>
        public void ResetVisual()
        {
            _active = false;
            _flashTimer = 0f;
            _hasOverride = false;

            if (target != null) target.color = idleColor;
            if (_canTransform && _tr != null)
            {
                _tr.localPosition = _restLocalPos;
                _tr.localEulerAngles = new Vector3(0f, 0f, _restLocalAngle);
            }
        }

        /// <summary>렌더러 표시/숨김 (폭탄 폭발 후 등).</summary>
        public void SetVisible(bool visible)
        {
            if (target != null) target.enabled = visible;
        }
    }

    /// <summary>
    /// 에셋 없이 쓰는 흰 사각형 스프라이트. 프로토타입 전용 — 실제 도트 에셋이 붙으면 쓰이지 않는다.
    /// </summary>
    internal static class PuzzlePrimitiveSprite
    {
        private static Sprite _white;

        public static Sprite White
        {
            get
            {
                if (_white != null) return _white;

                // 16px 정사각 = PPU 16 기준 1유닛 (도트 시스템.md — PPU 16 권장)
                const int size = 16;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "PuzzlePrimitiveWhite",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                var pixels = new Color32[size * size];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(pixels);
                tex.Apply();

                _white = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
                _white.name = "PuzzlePrimitiveWhite";
                _white.hideFlags = HideFlags.HideAndDontSave;
                return _white;
            }
        }
    }
}
