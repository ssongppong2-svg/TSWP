// 근거: UI 시스템.md — 상호작용 UI: 상호작용 가능한 오브젝트 근처에서 사용 키와 설명을 표시.
//   예: [E] 문 열기 / [E] 레버 당기기 / [E] 아이템 줍기. 캔버스 3계층 중 ② World Space에 해당한다.
//   조작과 시스템.md — 상호작용 키는 E.
// 문구는 Player.IInteractable.PromptDescription이 제공한다 (UI는 문구를 만들지 않는다 — InteractionPromptModel 주석).
// 프로토타입 단계라 UGUI 프리팹 없이 IMGUI로 그린다. IMGUI 비용 규칙(프레임 튐 전례 있음)은 GameplayHud와 동일:
//   ① Repaint에서만 그린다 ② GUIStyle 1회 생성 캐싱 ③ 문자열은 대상이 바뀔 때만 재생성 ④ GUILayout 금지
using UnityEngine;
using TSWP.Player;

namespace TSWP.UI
{
    /// <summary>
    /// E키 상호작용 프롬프트 뷰. 씬에 이 컴포넌트 하나만 두면 동작한다.
    /// Player.PlayerInteraction의 TargetChanged를 구독해 InteractionPromptModel에 밀어 넣고,
    /// 대상 오브젝트 머리 위(없으면 화면 하단 중앙)에 "[E] 레버 당기기"를 그린다.
    /// PlayerInteraction이 없어도 조용히 아무것도 그리지 않는다 (씬 배선 없어도 실패 금지).
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionPromptView : MonoBehaviour
    {
        public static InteractionPromptView Instance { get; private set; }

        [Header("대상")]
        [Tooltip("비우면 씬에서 PlayerInteraction을 자동 탐색한다 (로컬 플레이어 1인 기준).")]
        [SerializeField] private PlayerInteraction source;

        [Tooltip("월드 좌표 변환에 쓸 카메라. 비우면 Camera.main.")]
        [SerializeField] private Camera worldCamera;

        [Header("표시")]
        [Tooltip("대상 오브젝트 머리 위에 띄운다. 끄면 항상 화면 하단 중앙에 고정된다.")]
        [SerializeField] private bool anchorToTarget = true;

        [Tooltip("대상 기준 세로 오프셋(월드 유닛). 캐릭터/구조물 머리 위로 띄우는 값.")]
        [SerializeField] private float worldYOffset = 1.2f;

        [Tooltip("하단 고정 모드일 때 화면 아래에서 띄우는 거리(px).")]
        [SerializeField] private float bottomMargin = 140f;

        [Tooltip("프롬프트 상자 높이(px).")]
        [SerializeField] private float boxHeight = 26f;

        [Tooltip("글자 좌우 여백(px).")]
        [SerializeField] private float horizontalPadding = 12f;

        [Header("옵션")]
        [Tooltip("PlayerInteraction 자동 탐색 주기(초). 플레이어가 런타임에 스폰되는 씬 대응.")]
        [SerializeField] private float rebindInterval = 0.5f;

        [Header("레이아웃 색상 (비우면 런타임 기본값 — 에셋 없이도 동작)")]
        [SerializeField] private HudLayoutConfig layout;

        /// <summary>프롬프트 뷰모델. GameplayHud가 있으면 그 모델을 공유한다.</summary>
        public InteractionPromptModel Model { get; private set; }

        private GUIStyle _keyStyle;
        private GUIStyle _textStyle;
        private int _styleFontSize = -1;

        private IInteractable _boundTarget;
        private Transform _targetTransform;
        private string _promptText = string.Empty;   // 전체 문구 (폭 계산/폴백용)
        private string _keyText = string.Empty;      // "[E]" 부분 — 강조색으로 따로 그린다
        private string _descText = string.Empty;     // "레버 당기기" 부분
        private string _cachedDescription;
        private float _nextRebindTime;
        private float _alpha = 1f;

        // ── 수명 주기 ─────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            Model = new InteractionPromptModel();
        }

        private void Start()
        {
            ResolveModel();
            TryBindSource();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Unbind();
        }

        private void Update()
        {
            ResolveModel();

            if (source == null && Time.time >= _nextRebindTime)
            {
                _nextRebindTime = Time.time + Mathf.Max(0.1f, rebindInterval);
                TryBindSource();
            }

            // 대상이 파괴되면 TargetChanged가 오지 않을 수 있으므로 여기서도 확인한다.
            if (source == null)
            {
                if (Model != null && Model.IsVisible) HidePrompt();
                return;
            }

            // 이벤트를 놓친 경우(바인딩 전 변경 등)를 위한 저비용 보정 — 참조 비교뿐이라 할당이 없다.
            if (!ReferenceEquals(source.CurrentTarget, _boundTarget))
                OnTargetChanged(source.CurrentTarget);
        }

        /// <summary>GameplayHud가 있으면 HudModel의 프롬프트 모델을 공유한다(단일 진실 원천).</summary>
        private void ResolveModel()
        {
            var hud = GameplayHud.Instance;
            if (hud == null) return;
            if (ReferenceEquals(Model, hud.Model.InteractionPrompt)) return;
            Model = hud.Model.InteractionPrompt;
        }

        /// <summary>씬에서 PlayerInteraction을 찾아 구독한다.</summary>
        private void TryBindSource()
        {
            // Unity 6: FindObjectOfType 제거 → FindFirstObjectByType 사용.
            var found = source != null ? source : Object.FindFirstObjectByType<PlayerInteraction>();
            if (found == null) return;

            source = found;
            source.TargetChanged += OnTargetChanged;
            OnTargetChanged(source.CurrentTarget);
        }

        private void Unbind()
        {
            if (source == null) return;
            source.TargetChanged -= OnTargetChanged;
        }

        /// <summary>대상 변경 통지 — 문구/앵커 캐시는 여기서만 갱신한다(매 프레임 문자열 생성 금지).</summary>
        private void OnTargetChanged(IInteractable target)
        {
            _boundTarget = target;

            if (target == null)
            {
                HidePrompt();
                return;
            }

            // IInteractable은 인터페이스라 위치를 모른다 — MonoBehaviour 구현이면 Transform을 빌려 쓴다.
            _targetTransform = target is MonoBehaviour behaviour ? behaviour.transform : null;

            string description = target.PromptDescription;
            Model?.Show(description);

            if (!string.Equals(_cachedDescription, description))
            {
                _cachedDescription = description;
                string keyLabel = Model != null ? Model.KeyLabel : InteractionPromptModel.DefaultKeyLabel;
                _keyText = $"[{keyLabel}]";
                _descText = description;
                _promptText = Model != null ? Model.GetPromptText() : $"{_keyText} {description}";
            }
        }

        private void HidePrompt()
        {
            _boundTarget = null;
            _targetTransform = null;
            _cachedDescription = null;
            _promptText = string.Empty;
            _keyText = string.Empty;
            _descText = string.Empty;
            Model?.Hide();
        }

        // ── 그리기 ────────────────────────────────────────────────
        private void OnGUI()
        {
            // ① Repaint에서만 그린다.
            if (Event.current.type != EventType.Repaint) return;
            if (Model == null || !Model.IsVisible) return;
            if (string.IsNullOrEmpty(_promptText)) return;

            var settings = SettingsManager.Instance != null ? SettingsManager.Instance.Ui : null;
            if (settings != null && !settings.hudEnabled) return;

            var c = ResolveLayout();
            EnsureStyles(c);

            float scale = settings != null ? settings.uiScale : 1f;
            _alpha = settings != null ? settings.uiOpacity : 1f;

            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float screenW = Screen.width / Mathf.Max(0.01f, scale);
            float screenH = Screen.height / Mathf.Max(0.01f, scale);

            // 폭 계산 — 키 부분과 설명 부분을 따로 재서 색을 다르게 칠한다.
            float keyWidth = _keyStyle.CalcSize(new GUIContent(_keyText)).x;
            float descWidth = _textStyle.CalcSize(new GUIContent(_descText)).x;
            float gap = 6f;
            float textWidth = keyWidth + gap + descWidth;
            float boxWidth = textWidth + horizontalPadding * 2f;

            Vector2 center;
            if (anchorToTarget && TryGetTargetScreenPos(scale, screenH, out Vector2 anchored))
                center = anchored;
            else
                center = new Vector2(screenW * 0.5f, screenH - bottomMargin);

            var box = new Rect(center.x - boxWidth * 0.5f, center.y - boxHeight * 0.5f, boxWidth, boxHeight);

            // 화면 밖으로 새지 않게 가장자리에서 붙잡는다.
            box.x = Mathf.Clamp(box.x, 4f, Mathf.Max(4f, screenW - boxWidth - 4f));
            box.y = Mathf.Clamp(box.y, 4f, Mathf.Max(4f, screenH - boxHeight - 4f));

            DrawRect(box, c.Panel);
            DrawFrame(box, c.Accent, 1f);

            float x = box.x + horizontalPadding;
            DrawLabel(new Rect(x, box.y, keyWidth, box.height), _keyText, c.Accent, _keyStyle);
            DrawLabel(new Rect(x + keyWidth + gap, box.y, descWidth, box.height), _descText, c.Text, _textStyle);

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        /// <summary>대상의 머리 위 화면 좌표(스케일 보정 후, GUI 좌표계). 카메라 뒤면 false.</summary>
        private bool TryGetTargetScreenPos(float scale, float screenH, out Vector2 result)
        {
            result = default;
            if (_targetTransform == null) return false;

            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return false;

            Vector3 world = _targetTransform.position + Vector3.up * worldYOffset;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return false;    // 카메라 뒤

            // Screen 좌표는 아래가 0, GUI 좌표는 위가 0 → 뒤집고 uiScale로 나눈다.
            float s = Mathf.Max(0.01f, scale);
            result = new Vector2(sp.x / s, screenH - sp.y / s);
            return true;
        }

        // ── 그리기 유틸 (전부 할당 없음) ───────────────────────────
        private void DrawRect(Rect rect, Color color)
        {
            GUI.color = WithAlpha(color);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private void DrawFrame(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void DrawLabel(Rect rect, string text, Color color, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            GUI.color = WithAlpha(Color.white);
            style.normal.textColor = WithAlpha(color);
            GUI.Label(rect, text, style);
        }

        private Color WithAlpha(Color color) => new Color(color.r, color.g, color.b, color.a * _alpha);

        // ── 설정/스타일 ───────────────────────────────────────────
        private HudLayoutConfig ResolveLayout()
        {
            if (layout == null) layout = HudLayoutConfig.CreateRuntimeDefault();
            return layout;
        }

        /// <summary>② GUIStyle은 1회만 생성한다.</summary>
        private void EnsureStyles(HudLayoutConfig c)
        {
            if (_textStyle != null && _styleFontSize == c.fontSize) return;
            _styleFontSize = c.fontSize;

            _textStyle = new GUIStyle(GUI.skin.label) { fontSize = c.fontSize, alignment = TextAnchor.MiddleLeft };
            _keyStyle = new GUIStyle(_textStyle) { fontStyle = FontStyle.Bold };
        }
    }
}
