// 근거: 아이템 시스템.md — 보스 보상(드롭 아이템은 모든 플레이어가 획득 가능, 먼저 집는 플레이어가 소유자),
//       아이템 교체(버린 아이템은 바닥에 떨어지며 다른 플레이어가 획득 가능). 아이템 경쟁도 게임의 일부.
// 근거: 조작과 시스템.md — E키 상호작용 대상 ① 아이템 획득 → Player.IInteractable 구현.
// 근거: 팔레트 시스템.md — 희귀도 색상(회색/초록/파랑/보라/금색/무지개). 색 매핑 원본은 Art.RarityColorConfig이며
//       여기서는 에셋이 없을 때를 위한 폴백만 갖는다 (ItemRarity 정의는 Items 한 곳 — ARCHITECTURE.md §5).
// 프로토타입 방침: 스프라이트/콜라이더가 없어도 스스로 만들어 "보이고 만질 수 있게" 한다.
using UnityEngine;
using TSWP.Art;
using TSWP.Player;

namespace TSWP.Items
{
    /// <summary>월드에 떨어진 아이템. 보스 공용 드롭·시작 아이템·버린 아이템 모두 이 오브젝트로 표현한다.
    /// 소유권 없음 — 선착순 선점(먼저 집는 플레이어가 소유자).</summary>
    public class DroppedItem : MonoBehaviour, IInteractable
    {
        [SerializeField] private ItemDefinition item;

        /// <summary>모든 플레이어 획득 가능 (보스 공용 드롭 등). false는 예약 드롭용 확장 여지.</summary>
        [SerializeField] private bool sharedPickup = true;

        [Header("표시 (프로토타입 — 네모 + 이름표)")]
        [Tooltip("희귀도 색상 원본. 비워 두면 내장 폴백 색을 쓴다 (Art.RarityColorConfig 에셋 권장).")]
        [SerializeField] private RarityColorConfig rarityColors;

        [Tooltip("아이콘이 없을 때 그릴 사각형의 크기(월드 유닛).")]
        [SerializeField] private float placeholderSize = 0.5f;

        [Tooltip("아이템 이름을 월드에 띄운다. 폰트를 못 구하면 조용히 생략된다.")]
        [SerializeField] private bool showNameLabel = true;

        [Tooltip("위아래로 까딱이는 진폭(0이면 정지).")]
        [SerializeField] private float bobAmplitude = 0.12f;

        [SerializeField] private float bobSpeed = 2.5f;

        [Tooltip("픽업 판정 반경. 콜라이더가 없을 때 이 크기로 트리거를 만든다.")]
        [SerializeField] private float pickupRadius = 0.6f;

        /// <summary>선점 완료 플래그. // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        private bool _claimed;

        private SpriteRenderer _renderer;
        private TextMesh _label;
        private Vector3 _basePosition;
        private float _bobPhase;
        private string _promptCache;

        /// <summary>아이콘이 없는 아이템용 공용 흰 사각형 스프라이트 (전 드롭이 공유 — 매번 생성 금지).</summary>
        private static Sprite _placeholderSprite;

        public ItemDefinition Item => item;
        public bool SharedPickup => sharedPickup;
        public bool IsClaimed => _claimed;

        // ── Player.IInteractable (E키) ────────────────────────────

        /// <summary>UI.InteractionPrompt 표시 문구. 매 프레임 조회되므로 문자열을 캐싱한다.</summary>
        public string PromptDescription
        {
            get
            {
                if (_promptCache != null) return _promptCache;
                string label = item != null ? DisplayName(item) : "아이템";
                _promptCache = $"{label} 획득";
                return _promptCache;
            }
        }

        /// <summary>거리 판정은 PlayerInteraction이 하고, 여기서는 선점 여부만 본다.</summary>
        public bool CanInteract(PlayerController user) => !_claimed && item != null;

        /// <summary>E키 실행 — 픽업 단일 지점으로 넘긴다.</summary>
        public void Interact(PlayerController user)
        {
            if (user == null) return;
            Pickup(user.PlayerId);
        }

        // ── 초기화/표시 ───────────────────────────────────────────

        /// <summary>스폰 직후 초기화 (ItemDropManager/PlayerEquipment가 호출).</summary>
        public void Initialize(ItemDefinition definition)
        {
            item = definition;
            _claimed = false;
            _promptCache = null;
            RefreshVisual();
        }

        private void Awake()
        {
            _basePosition = transform.position;
            // 스폰 시점을 흩어 놓아 여러 드롭이 한 몸처럼 까딱이지 않게 한다.
            _bobPhase = Random.value * Mathf.PI * 2f;
        }

        private void Start() => RefreshVisual();

        private void Update()
        {
            if (bobAmplitude <= 0f || _renderer == null) return;

            _bobPhase += Time.deltaTime * bobSpeed;
            float offset = Mathf.Sin(_bobPhase) * bobAmplitude;
            transform.position = _basePosition + new Vector3(0f, offset, 0f);
        }

        /// <summary>
        /// 스프라이트/콜라이더/이름표를 필요한 만큼 만들어 붙인다.
        /// 이미 프리팹에 붙어 있으면 그것을 그대로 쓰고 색만 갱신한다.
        /// </summary>
        private void RefreshVisual()
        {
            _basePosition = transform.position;

            // ① 스프라이트 — 아이콘이 있으면 아이콘, 없으면 흰 네모(희귀도 색으로 물들인다).
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();

            bool hasIcon = item != null && item.icon != null;
            _renderer.sprite = hasIcon ? item.icon : GetPlaceholderSprite();
            _renderer.color = item != null ? GetRarityColor(item.rarity) : Color.white;
            _renderer.sortingOrder = 5; // 지형보다 위에 보이게

            if (!hasIcon)
            {
                // 폴백 스프라이트는 1x1 PPU 1이라 크기를 여기서 정한다.
                transform.localScale = new Vector3(placeholderSize, placeholderSize, 1f);
            }

            // ② 픽업용 트리거 콜라이더 — PlayerInteraction의 OverlapCircle이 잡을 수 있어야 한다.
            if (GetComponent<Collider2D>() == null)
            {
                var circle = gameObject.AddComponent<CircleCollider2D>();
                circle.isTrigger = true;
                // localScale이 걸려 있으므로 월드 반경 기준으로 되돌려 계산한다.
                float scale = Mathf.Abs(transform.localScale.x);
                circle.radius = scale > 0.0001f ? pickupRadius / scale : pickupRadius;
            }

            // ③ 이름표 — 무엇이 떨어졌는지 한눈에 보이게 (프로토타입 수준).
            RefreshLabel();
        }

        private void RefreshLabel()
        {
            if (!showNameLabel || item == null) return;

            if (_label == null)
            {
                Font font = GetLabelFont();
                if (font == null) return; // 폰트를 못 구하면 조용히 생략 (게임 로직 영향 없음)

                var labelGo = new GameObject("NameLabel");
                labelGo.transform.SetParent(transform, false);

                // 폴백 네모 때문에 부모 스케일이 작아져 있으므로, 글자 크기가 아이템 크기에
                // 휘둘리지 않도록 부모 스케일을 상쇄한다.
                float parentScale = Mathf.Abs(transform.localScale.x);
                float inverse = parentScale > 0.0001f ? 1f / parentScale : 1f;
                labelGo.transform.localScale = new Vector3(inverse, inverse, 1f);
                labelGo.transform.localPosition = new Vector3(0f, 0.75f * inverse, 0f);

                _label = labelGo.AddComponent<TextMesh>();
                _label.font = font;
                _label.fontSize = 48;
                _label.characterSize = 0.09f;
                _label.anchor = TextAnchor.LowerCenter;
                _label.alignment = TextAlignment.Center;

                var meshRenderer = labelGo.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                {
                    meshRenderer.sharedMaterial = font.material;
                    meshRenderer.sortingOrder = 6;
                }
            }

            _label.text = DisplayName(item);
            _label.color = GetRarityColor(item.rarity);
        }

        private static Font _labelFont;
        private static bool _labelFontResolved;

        private static Font GetLabelFont()
        {
            // 내장 폰트 조회는 실패 시 로그를 남기므로 드롭마다 반복하지 않도록 1회만 시도한다.
            if (_labelFontResolved) return _labelFont;
            _labelFontResolved = true;

            _labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // Unity 6 내장 폰트
            if (_labelFont == null)
                _labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");     // 구버전 이름
            return _labelFont;
        }

        private static string DisplayName(ItemDefinition definition)
            => string.IsNullOrEmpty(definition.itemName) ? definition.itemCode : definition.itemName;

        /// <summary>희귀도 색. 설정 에셋이 있으면 그 값, 없으면 팔레트 문서 기준 폴백.</summary>
        private Color GetRarityColor(ItemRarity rarity)
        {
            if (rarityColors != null) return rarityColors.Get(rarity);

            // TODO(아트): Art.RarityColorConfig 에셋이 생기면 위 참조를 채우고 이 폴백은 무시된다.
            switch (rarity)
            {
                case ItemRarity.Common: return new Color(0.72f, 0.72f, 0.72f);
                case ItemRarity.Uncommon: return new Color(0.42f, 0.85f, 0.36f);
                case ItemRarity.Rare: return new Color(0.32f, 0.60f, 0.98f);
                case ItemRarity.Epic: return new Color(0.70f, 0.38f, 0.95f);
                case ItemRarity.Legendary: return new Color(1.00f, 0.76f, 0.20f);
                case ItemRarity.Developer: return new Color(1.00f, 0.45f, 0.85f);
                default: return Color.white;
            }
        }

        private static Sprite GetPlaceholderSprite()
        {
            if (_placeholderSprite != null) return _placeholderSprite;

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, // 픽셀아트 규격 — 보간 금지
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            _placeholderSprite = Sprite.Create(
                texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            _placeholderSprite.hideFlags = HideFlags.HideAndDontSave;
            return _placeholderSprite;
        }

        // ── 픽업 ──────────────────────────────────────────────────

        /// <summary>픽업 판정 단일 지점 — 선착순 선점. 이 메서드 외의 경로로 아이템을 넘기지 말 것.
        /// // SYNC: 호스트 권위 판정 예정 — 동시 픽업 레이스는 호스트가 최초 요청 1건만 승인하고 나머지는 기각한다.</summary>
        public void Pickup(int playerId)
        {
            if (_claimed || item == null) return;

            _claimed = true; // 선점 — 후속 요청 차단

            bool equipped = ItemDropManager.Instance != null
                && ItemDropManager.Instance.ResolvePickup(this, playerId);

            if (!equipped)
            {
                // 장착 실패 (슬롯 가득/중첩 상한/소지 상한) — 선점 해제.
                // 슬롯 가득의 경우 UI가 교체 흐름(PlayerEquipment.SwapAndDrop)을 유도한다.
                _claimed = false;
                return;
            }

            // 획득 연출 — VfxSpawner가 없으면 조용히 생략된다.
            var spawner = VfxSpawner.Instance;
            if (spawner != null) spawner.Play(VfxId.Buff, transform.position);

            Destroy(gameObject);
        }
    }
}
