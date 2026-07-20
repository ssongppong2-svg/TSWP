// 근거: 팔레트 시스템.md — 체력바 색상 구간(1.0 초록 / 0.7 연두 / 0.4 노랑 / 0.2 빨강 / 0.05 깜빡임).
//       "색만 보고 즉시 위험을 판단할 수 있어야 한다" — 적 머리 위 체력바가 그 판단 근거를 제공한다.
// 근거: 적 시스템.md — 공략 방법이 존재해야 한다(남은 체력이 보여야 마무리 판단이 가능).
// 근거: ARCHITECTURE.md §3-5 — 이 표시는 '적' 정보라 HUD가 아니다. GameEvents를 태우지 않고
//       CombatEntity를 직접 읽는다(월드 캔버스 계층 = 적 머리 위 표시).
//
// 설계 메모:
//  - SpriteRenderer 2장(배경/채움)으로 만든다. OnGUI는 적 수만큼 매 프레임 IMGUI 할당이 생겨 8인 난전에 부적합.
//  - 스프라이트는 1x1 흰 텍스처를 정적 공유한다 — 적이 몇 마리든 텍스처/스프라이트는 1개.
//  - 채움 스프라이트의 피벗을 왼쪽(0, 0.5)으로 두어 localScale.x 하나로 좌→우 감소를 표현한다.
//  - 색상 설정(HealthBarColorConfig)이 없어도 내장 폴백 색으로 동작한다 (연출 부재로 로직이 실패하지 않는다).
using UnityEngine;
using TSWP.Art;
using TSWP.Combat;

namespace TSWP.Enemies
{
    /// <summary>
    /// 적 머리 위 체력바. EnemyController가 스폰 시 자동으로 붙이거나 프리팹에 미리 달아 둔다.
    /// 대상 CombatEntity가 없으면 조용히 비활성화된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyHealthBar : MonoBehaviour
    {
        [Header("색상")]
        [Tooltip("팔레트 문서의 체력 구간 색. 비우면 내장 폴백(초록/연두/노랑/빨강)을 사용한다.")]
        [SerializeField] private HealthBarColorConfig colorConfig;

        [Tooltip("체력바 배경(빈 칸) 색.")]
        [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);

        [Header("크기/위치")]
        [Tooltip("체력바 크기(월드 유닛). 캐릭터 32px·PPU 16 기준 폭 1.2 정도가 자연스럽다.")]
        [SerializeField] private Vector2 size = new Vector2(1.2f, 0.14f); // TODO(밸런스): 문서 미정

        [Tooltip("머리 위 높이(월드 유닛). autoOffsetFromCollider가 켜져 있으면 콜라이더 상단에서 자동 계산한다.")]
        [SerializeField] private float heightOffset = 0.9f;

        [Tooltip("콜라이더 상단 기준으로 높이를 자동 계산한다 (적 크기가 제각각이어도 머리 위에 붙는다).")]
        [SerializeField] private bool autoOffsetFromCollider = true;

        [Tooltip("자동 계산 시 콜라이더 상단에서 추가로 띄우는 여백.")]
        [SerializeField] private float autoOffsetPadding = 0.25f;

        [Header("표시 규칙")]
        [Tooltip("켜면 피해를 입은 뒤에만 잠시 표시한다. 끄면 항상 표시한다.")]
        [SerializeField] private bool showOnlyWhenDamaged = true;

        [Tooltip("마지막 피격 후 이 시간이 지나면 숨긴다(초).")]
        [SerializeField, Min(0f)] private float hideDelay = 3f; // TODO(밸런스): 문서 미정

        [Tooltip("체력이 가득 차 있지 않으면 숨김 시간과 무관하게 계속 표시한다.")]
        [SerializeField] private bool keepVisibleWhileWounded = false;

        [Header("렌더링")]
        [Tooltip("적 본체 스프라이트보다 이만큼 위에 그린다.")]
        [SerializeField] private int sortingOrderOffset = 20;

        // ── 런타임 ────────────────────────────────────────────────
        private CombatEntity _entity;
        private SpriteRenderer _background;
        private SpriteRenderer _fill;
        private Transform _root;

        private float _hideAt;
        private bool _visible;
        private bool _initialized;

        /// <summary>1x1 흰 픽셀 스프라이트 — 모든 적이 공유한다(적 수와 무관하게 1개).</summary>
        private static Sprite _sharedPixel;

        // ── 초기화 ────────────────────────────────────────────────
        private void Awake() => EnsureBuilt();

        /// <summary>
        /// 표시 대상 지정. EnemyController가 스폰 조립 시 호출한다.
        /// 호출하지 않으면 Awake에서 같은 오브젝트의 CombatEntity를 찾아 쓴다.
        /// </summary>
        public void SetOwner(CombatEntity entity)
        {
            if (_entity == entity) return;

            if (_entity != null) _entity.Damaged -= OnOwnerDamaged;
            _entity = entity;
            if (_entity != null && isActiveAndEnabled) _entity.Damaged += OnOwnerDamaged;

            EnsureBuilt();
            ApplyVisible(!showOnlyWhenDamaged);
        }

        /// <summary>색상 설정 주입 (씬/프리팹에 SO를 꽂지 못했을 때 통합 담당자가 코드로 주입).</summary>
        public void SetColorConfig(HealthBarColorConfig config) => colorConfig = config;

        /// <summary>즉시 표시하고 숨김 타이머를 다시 시작한다.</summary>
        public void Show()
        {
            _hideAt = Time.time + hideDelay;
            ApplyVisible(true);
        }

        /// <summary>즉시 숨긴다 (사망 연출 등).</summary>
        public void Hide() => ApplyVisible(false);

        private void EnsureBuilt()
        {
            if (_initialized) return;
            _initialized = true;

            if (_entity == null) _entity = GetComponentInParent<CombatEntity>();

            // 본체 스프라이트를 먼저 조회한다 — 체력바 자식을 만든 뒤 조회하면 자기 자신을 잡는다.
            SpriteRenderer ownerRenderer = GetComponentInChildren<SpriteRenderer>();

            if (autoOffsetFromCollider) heightOffset = ResolveHeightOffset();

            var rootGo = new GameObject("HealthBar");
            _root = rootGo.transform;
            _root.SetParent(transform, false);
            _root.localPosition = new Vector3(0f, heightOffset, 0f);
            _root.localRotation = Quaternion.identity;
            _root.localScale = Vector3.one;

            _background = CreateBar("Bar_BG", backgroundColor, 0);
            _fill = CreateBar("Bar_Fill", Color.green, 1);

            if (ownerRenderer != null)
            {
                // 본체와 같은 정렬 레이어에 두고 앞쪽에 그린다.
                _background.sortingLayerID = ownerRenderer.sortingLayerID;
                _fill.sortingLayerID = ownerRenderer.sortingLayerID;
                _background.sortingOrder = ownerRenderer.sortingOrder + sortingOrderOffset;
                _fill.sortingOrder = ownerRenderer.sortingOrder + sortingOrderOffset + 1;
            }

            ApplyVisible(!showOnlyWhenDamaged);
        }

        private SpriteRenderer CreateBar(string barName, Color color, int order)
        {
            var go = new GameObject(barName);
            go.transform.SetParent(_root, false);
            // 피벗이 왼쪽이므로 왼쪽 끝으로 밀어 두면 중앙 정렬이 된다.
            go.transform.localPosition = new Vector3(-size.x * 0.5f, 0f, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSharedPixel();
            renderer.color = color;
            renderer.sortingOrder = order;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return renderer;
        }

        /// <summary>콜라이더 상단에서 머리 위 높이를 계산한다. 콜라이더가 없으면 인스펙터 값을 유지한다.</summary>
        private float ResolveHeightOffset()
        {
            var collider = GetComponentInChildren<Collider2D>();
            if (collider == null) return heightOffset;

            // bounds는 월드 기준이다. 체력바 루트는 자식이라 localPosition이 부모 스케일로 다시 곱해지므로
            // 월드 높이를 부모 스케일로 나눠 로컬 값으로 환산해야 한다.
            // (EnemyData.bodyScale로 크기를 바꾼 적에서 체력바가 더 높이 떠 버리는 것을 막는다.)
            float top = collider.bounds.max.y - transform.position.y + autoOffsetPadding;

            float scaleY = transform.lossyScale.y;
            if (Mathf.Abs(scaleY) < 0.0001f) return top; // 0 스케일 방어 — 나눗셈 폭주 방지
            return top / scaleY;
        }

        private static Sprite GetSharedPixel()
        {
            if (_sharedPixel != null) return _sharedPixel;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,          // 픽셀아트 — 보간 금지 (도트 시스템.md)
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "EnemyHealthBarPixel",
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            // pixelsPerUnit = 1 → localScale이 곧 월드 크기. 피벗 왼쪽 → scale.x로 좌→우 감소.
            _sharedPixel = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0f, 0.5f), 1f);
            _sharedPixel.name = "EnemyHealthBarPixel";
            _sharedPixel.hideFlags = HideFlags.HideAndDontSave;
            return _sharedPixel;
        }

        // ── 구독 ──────────────────────────────────────────────────
        private void OnEnable()
        {
            EnsureBuilt();
            if (_entity != null) _entity.Damaged += OnOwnerDamaged;
        }

        private void OnDisable()
        {
            if (_entity != null) _entity.Damaged -= OnOwnerDamaged;
        }

        private void OnOwnerDamaged(DamageInfo info) => Show();

        // ── 갱신 ──────────────────────────────────────────────────
        // LateUpdate: 이동/물리가 끝난 뒤 값을 읽어 한 프레임 지연을 없앤다.
        // 숨겨져 있을 때는 아무것도 하지 않아 8인 난전에서 비용이 0에 수렴한다.
        private void LateUpdate()
        {
            if (_entity == null || _fill == null) return;

            if (_entity.IsDead)
            {
                ApplyVisible(false);
                return;
            }

            float ratio = _entity.MaxHp > 0f ? Mathf.Clamp01(_entity.CurrentHp / _entity.MaxHp) : 0f;

            if (showOnlyWhenDamaged && _visible)
            {
                bool wounded = keepVisibleWhileWounded && ratio < 1f;
                if (!wounded && Time.time >= _hideAt) ApplyVisible(false);
            }

            if (!_visible) return;

            _fill.transform.localScale = new Vector3(size.x * ratio, size.y, 1f);
            _fill.color = EvaluateColor(ratio);

            // 위험 구간 깜빡임 — 접근성 '플래시 효과 감소'가 켜져 있으면 깜빡이지 않는다.
            _fill.enabled = !ShouldBlinkOff(ratio);
        }

        private Color EvaluateColor(float ratio)
        {
            if (colorConfig != null) return colorConfig.Evaluate(ratio);

            // 폴백 — 팔레트 시스템.md의 구간을 그대로 내장한다 (SO 미할당이어도 규칙은 지킨다).
            if (ratio >= 1.00f) return new Color(0.30f, 0.85f, 0.30f); // 초록
            if (ratio >= 0.70f) return new Color(0.60f, 0.90f, 0.30f); // 연두
            if (ratio >= 0.40f) return new Color(0.95f, 0.85f, 0.25f); // 노랑
            return new Color(0.90f, 0.25f, 0.25f);                      // 빨강
        }

        /// <summary>지금 깜빡임의 '꺼짐' 구간인가. 접근성 설정이 켜져 있으면 항상 false.</summary>
        private bool ShouldBlinkOff(float ratio)
        {
            bool reduceFlash = false;
            var settings = UI.SettingsManager.Instance;
            if (settings != null && settings.Accessibility != null)
                reduceFlash = settings.Accessibility.reduceFlashEffects;

            if (reduceFlash) return false;

            if (colorConfig != null) return colorConfig.ShouldBlink(ratio, Time.time, false);

            // 폴백: 5% 이하에서 0.4초 주기 깜빡임 (팔레트 시스템.md 기본값)
            if (ratio > 0.05f) return false;
            return Mathf.Repeat(Time.time, 0.4f) >= 0.2f;
        }

        private void ApplyVisible(bool value)
        {
            if (_root == null) return;
            if (_visible == value && _root.gameObject.activeSelf == value) return;

            _visible = value;
            _root.gameObject.SetActive(value);
            if (value && _fill != null) _fill.enabled = true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (size.x <= 0f) size.x = 0.1f;
            if (size.y <= 0f) size.y = 0.02f;
        }
#endif
    }
}
