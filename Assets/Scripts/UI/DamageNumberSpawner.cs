// 근거: UI 시스템.md — 설정 '데미지 숫자 표시'(UISettings.showDamageNumbers) 즉시 반영.
// 근거: 팔레트 시스템.md — 빨강=피해, 노랑=강조, 초록=회복. 색은 정보다.
// 근거: 성능 감사 보고 §7 — IMGUI는 GUIStyle/문자열을 매 호출 만들면 초당 수백 개를 할당한다.
//   따라서 (1) 스타일은 1회 생성, (2) 정수 문자열은 캐시, (3) Repaint 이벤트에서만 그린다,
//   (4) 숫자 하나에 GameObject를 만들지 않고 배열 풀로 재사용한다.
// 외부 패키지 금지(TextMeshPro 불가) → 프로토타입 표시는 IMGUI 단일 창구로 통합한다.
// TODO(UI): 정식 구현에서는 캔버스 3계층 중 '월드' 계층의 비트맵 폰트 스프라이트로 교체한다.
using UnityEngine;

namespace TSWP.UI
{
    /// <summary>
    /// 데미지 숫자 표시 단일 창구. 씬에 1개 배치한다.
    /// 없어도 게임 로직은 정상 동작한다 — 호출은 전부 null 안전하게 무시된다.
    /// </summary>
    public class DamageNumberSpawner : MonoBehaviour
    {
        public static DamageNumberSpawner Instance { get; private set; }

        [Header("풀")]
        [Tooltip("동시에 표시할 수 있는 최대 개수. 초과분은 가장 오래된 것을 밀어낸다.")]
        [SerializeField, Min(8)] private int maxActive = 96; // TODO(밸런스): 문서 미정

        [Header("움직임")]
        [Tooltip("표시 시간(초).")]
        [SerializeField, Min(0.1f)] private float lifetime = 0.8f;        // TODO(밸런스): 문서 미정

        [Tooltip("솟아오르는 초기 속도(월드 단위/초).")]
        [SerializeField, Min(0f)] private float riseSpeed = 2.6f;         // TODO(밸런스): 문서 미정

        [Tooltip("감속(중력). 클수록 빨리 멈춘다.")]
        [SerializeField, Min(0f)] private float gravity = 4.5f;           // TODO(밸런스): 문서 미정

        [Tooltip("대상 머리 위로 띄우는 높이(월드 단위).")]
        [SerializeField] private float spawnHeightOffset = 0.7f;          // TODO(밸런스): 문서 미정

        [Tooltip("같은 자리에 겹칠 때 좌우로 흩뿌리는 폭(월드 단위).")]
        [SerializeField, Min(0f)] private float scatterRadius = 0.45f;    // TODO(밸런스): 문서 미정

        [Tooltip("이 거리 안에 이미 숫자가 있으면 겹친 것으로 보고 더 흩뿌린다.")]
        [SerializeField, Min(0.01f)] private float overlapDistance = 0.4f;

        [Tooltip("수명의 이 비율을 넘기면 서서히 사라진다.")]
        [Range(0.1f, 1f)][SerializeField] private float fadeStartRatio = 0.55f;

        [Header("표시")]
        [SerializeField, Min(8)] private int fontSize = 16;               // TODO(밸런스): 문서 미정
        [Tooltip("치명타 글자 크기 배수 — 더 크게 보여야 한다.")]
        [SerializeField, Min(1f)] private float criticalFontMultiplier = 1.6f;
        [Tooltip("치명타가 튀어나오는 초기 확대 배율.")]
        [SerializeField, Min(1f)] private float criticalPopScale = 1.5f;

        [Header("색 (팔레트 시스템.md)")]
        [Tooltip("빨강 = 피해.")]
        [SerializeField] private Color damageColor = new Color(0.90f, 0.27f, 0.22f);
        [Tooltip("노랑 = 강조(치명타).")]
        [SerializeField] private Color criticalColor = new Color(1.00f, 0.85f, 0.22f);
        [Tooltip("초록 = 회복.")]
        [SerializeField] private Color healColor = new Color(0.36f, 0.84f, 0.42f);
        [Tooltip("아군 오사 — 팔레트에 배정이 없어 구분용 자홍으로 둔다.")]
        [SerializeField] private Color friendlyFireColor = new Color(0.88f, 0.42f, 0.85f); // TODO(밸런스): 문서 미정

        [Tooltip("글자 뒤 그림자 — 픽셀아트 배경 위에서도 읽히게 한다.")]
        [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.75f);

        // ── 풀 ────────────────────────────────────────────────────
        // 앞쪽 _activeCount개가 활성, 뒤쪽은 재사용 대기. 만료 시 마지막 것과 자리를 바꿔 압축한다.
        private DamageNumber[] _items;
        private int _activeCount;

        private Camera _camera;

        // IMGUI 자원은 1회만 만든다 (매 호출 new GUIStyle = 관리+네이티브 양쪽 할당).
        private GUIStyle _style;

        // 정수 문자열 캐시 — 0~999는 할당 없이 표시된다.
        private const int CachedNumberMax = 999;
        private static string[] _numberCache;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _items = new DamageNumber[maxActive];
            for (int i = 0; i < _items.Length; i++)
                _items[i] = new DamageNumber();

            EnsureNumberCache();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── 공개 API ──────────────────────────────────────────────

        /// <summary>피해 숫자 표시. DamageSystem이 피해 적용 직후 호출한다.</summary>
        public void ShowDamage(Vector3 worldPosition, float amount, bool isCritical, bool friendly)
        {
            DamageNumberKind kind = friendly
                ? DamageNumberKind.FriendlyFire
                : (isCritical ? DamageNumberKind.Critical : DamageNumberKind.Damage);
            Show(worldPosition, amount, kind);
        }

        /// <summary>회복 숫자 표시 (초록).</summary>
        public void ShowHeal(Vector3 worldPosition, float amount)
            => Show(worldPosition, amount, DamageNumberKind.Heal);

        /// <summary>숫자 하나를 띄운다. 표시 설정이 꺼져 있거나 값이 0이면 무시된다.</summary>
        public void Show(Vector3 worldPosition, float amount, DamageNumberKind kind)
        {
            if (_items == null) return;
            if (amount <= 0f) return;

            // 설정 '데미지 숫자 표시' 즉시 반영 (SettingsManager가 없으면 표시).
            var settings = SettingsManager.Instance;
            if (settings != null && settings.Ui != null && !settings.Ui.showDamageNumbers) return;

            // 0.5 이상은 최소 1로 올린다 — 1 미만 피해를 '0'으로 보여주면 밸런스 확인이 어렵다.
            int rounded = Mathf.Max(1, Mathf.RoundToInt(amount));

            Vector3 spawn = worldPosition + new Vector3(0f, spawnHeightOffset, 0f);
            spawn.x += ResolveScatterX(spawn);

            DamageNumber item = Rent();
            item.Setup(spawn,
                       new Vector2(Random.Range(-0.35f, 0.35f), riseSpeed),
                       kind,
                       FormatNumber(rounded),
                       lifetime);
        }

        /// <summary>씬 전환 등에서 화면을 비운다.</summary>
        public void ClearAll() => _activeCount = 0;

        // ── 내부 ──────────────────────────────────────────────────

        /// <summary>같은 자리에 이미 숫자가 있으면 좌우로 밀어 겹침을 줄인다.</summary>
        private float ResolveScatterX(Vector3 spawn)
        {
            int nearby = 0;
            float sqrRange = overlapDistance * overlapDistance;
            for (int i = 0; i < _activeCount; i++)
            {
                if ((_items[i].WorldPosition - spawn).sqrMagnitude <= sqrRange)
                    nearby++;
            }
            if (nearby == 0) return Random.Range(-scatterRadius * 0.25f, scatterRadius * 0.25f);

            // 겹칠수록 좌우로 번갈아 넓게 흩뿌린다.
            float side = (nearby % 2 == 0) ? 1f : -1f;
            float spread = scatterRadius * Mathf.Min(1f, 0.4f + nearby * 0.25f);
            return side * spread + Random.Range(-0.08f, 0.08f);
        }

        private DamageNumber Rent()
        {
            if (_activeCount < _items.Length)
                return _items[_activeCount++];

            // 가득 찼다 — 가장 오래된(=수명 진행이 가장 많은) 것을 재사용한다.
            int oldest = 0;
            float best = -1f;
            for (int i = 0; i < _items.Length; i++)
            {
                float progress = _items[i].Lifetime > 0f ? _items[i].Elapsed / _items[i].Lifetime : 1f;
                if (progress > best) { best = progress; oldest = i; }
            }
            return _items[oldest];
        }

        private void Update()
        {
            if (_activeCount == 0) return;

            // 히트스톱 중에도 숫자는 흘러야 읽힌다 → unscaled.
            float dt = Time.unscaledDeltaTime;

            for (int i = _activeCount - 1; i >= 0; i--)
            {
                DamageNumber item = _items[i];
                item.Tick(dt, gravity, fadeStartRatio, criticalPopScale);
                if (!item.IsExpired) continue;

                // 만료 → 마지막 활성 요소와 자리를 바꿔 압축한다 (할당 0).
                int last = _activeCount - 1;
                _items[i] = _items[last];
                _items[last] = item;
                _activeCount--;
            }
        }

        private void OnGUI()
        {
            if (_activeCount == 0) return;

            // OnGUI는 Layout/Repaint + 입력 이벤트마다 호출된다 — 그리기는 Repaint에서 한 번만.
            if (Event.current.type != EventType.Repaint) return;

            Camera cam = ResolveCamera();
            if (cam == null) return;

            EnsureStyle();

            int screenHeight = Screen.height;

            for (int i = 0; i < _activeCount; i++)
            {
                DamageNumber item = _items[i];

                Vector3 screen = cam.WorldToScreenPoint(item.WorldPosition);
                if (screen.z < 0f) continue; // 카메라 뒤

                int size = Mathf.Max(8, Mathf.RoundToInt(
                    fontSize * item.Scale * (item.Kind == DamageNumberKind.Critical ? criticalFontMultiplier : 1f)));
                _style.fontSize = size;

                var rect = new Rect(screen.x - 100f, screenHeight - screen.y - size, 200f, size + 6f);

                // 그림자 먼저 — 픽셀아트 배경 위 가독성.
                Color shadow = shadowColor;
                shadow.a *= item.Alpha;
                _style.normal.textColor = shadow;
                GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), item.Text, _style);

                Color color = ResolveColor(item.Kind);
                color.a *= item.Alpha;
                _style.normal.textColor = color;
                GUI.Label(rect, item.Text, _style);
            }
        }

        private Color ResolveColor(DamageNumberKind kind)
        {
            switch (kind)
            {
                case DamageNumberKind.Critical: return criticalColor;
                case DamageNumberKind.Heal: return healColor;
                case DamageNumberKind.FriendlyFire: return friendlyFireColor;
                default: return damageColor;
            }
        }

        private Camera ResolveCamera()
        {
            if (_camera == null) _camera = Camera.main;
            return _camera;
        }

        private void EnsureStyle()
        {
            if (_style != null) return;
            _style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                richText = false,
            };
        }

        private static void EnsureNumberCache()
        {
            if (_numberCache != null) return;
            _numberCache = new string[CachedNumberMax + 1];
            for (int i = 0; i <= CachedNumberMax; i++)
                _numberCache[i] = i.ToString();
        }

        private static string FormatNumber(int value)
        {
            EnsureNumberCache();
            if (value >= 0 && value <= CachedNumberMax) return _numberCache[value];
            return value.ToString(); // 네 자리 이상은 드물다 — 이때만 할당한다
        }
    }
}
