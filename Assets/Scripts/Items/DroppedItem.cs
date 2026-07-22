// 근거: 아이템 시스템.md — 보스 보상(드롭 아이템은 모든 플레이어가 획득 가능, 먼저 집는 플레이어가 소유자),
//       아이템 교체(버린 아이템은 바닥에 떨어지며 다른 플레이어가 획득 가능). 아이템 경쟁도 게임의 일부.
// 근거: 조작과 시스템.md — E키 상호작용 대상 ① 아이템 획득 → Player.IInteractable 구현.
// 근거: 팔레트 시스템.md — 희귀도 색상(회색/초록/파랑/보라/금색/무지개). 색 매핑 원본은 Art.RarityColorConfig이며
//       여기서는 에셋이 없을 때를 위한 폴백만 갖는다 (ItemRarity 정의는 Items 한 곳 — ARCHITECTURE.md §5).
// 프로토타입 방침: 스프라이트/콜라이더가 없어도 스스로 만들어 "보이고 만질 수 있게" 한다.
// 게임 필(숨겨진 보정): 아이템 자석 — 근처의 살아있는 플레이어에게 약하게 빨려가 자동 획득된다 (E키 상호작용은 폴백 유지).
//       transform.position은 Update의 bob이 매 프레임 덮어쓰므로 자석은 반드시 _basePosition만 움직인다.
//       스폰 직후 잠깐은 흡입하지 않아 드롭이 흩어지는 연출이 먼저 보인다. 값은 보수적으로, 전부 인스펙터/정적 필드로 노출.
//       버린 아이템이 버린 사람에게 곧장 되빨려 가 '버리기/팀원 양도'가 무력화되지 않도록,
//       스폰 순간 겹쳐 있던 플레이어(=버린 사람 추정)는 자석 반경을 한 번 벗어날 때까지 자석 대상에서 제외한다.
using UnityEngine;
using TSWP.Art;
using TSWP.Combat;
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

        [Header("자석 (Game Feel)")]
        [Tooltip("근처 플레이어에게 빨려가 자동 획득되는 자석 동작을 켠다. 꺼도 E키 픽업은 그대로 된다.")]
        [SerializeField] private bool magnetEnabled = true;

        [Tooltip("이 반경(월드 유닛) 안의 살아있는 최근접 플레이어에게 빨려간다.")]
        [SerializeField] private float magnetRadius = 1.8f;

        [Tooltip("흡입 최고 속도(유닛/초).")]
        [SerializeField] private float magnetMaxSpeed = 7f;

        [Tooltip("흡입 가속도(유닛/초²). 속도를 누적하므로 멀면 천천히 시작해 가까울수록 빨라진다.")]
        [SerializeField] private float magnetAcceleration = 24f;

        [Tooltip("대상과 이 거리 이하면 E키 없이 자동 획득한다.")]
        [SerializeField] private float autoPickupRadius = 0.4f;

        [Tooltip("스폰 직후 이 시간(초) 동안은 흡입하지 않는다 — 드롭이 흩어지는 게 눈에 보이게.")]
        [SerializeField] private float magnetActivationDelay = 0.35f;

        [Tooltip("스폰 순간 이 반경(월드 유닛) 안에 있던 최근접 생존 플레이어(=버린 사람 추정)는 " +
                 "자석 반경을 한 번 벗어날 때까지 자석·자동 획득 대상에서 제외한다. 0 이하면 제외 없음. " +
                 "E키 픽업은 제외와 무관하게 항상 가능하다.")]
        [SerializeField] private float dropperExclusionRadius = 0.8f;

        /// <summary>자석 대상 재탐색 주기(초). 매 프레임 물리 질의를 피한다 (8인×드롭 다수 전제) — 0.1~0.15 권장.</summary>
        public static float MagnetScanInterval = 0.12f;

        /// <summary>자동 획득 실패(슬롯 가득 등) 후 재시도까지의 쿨다운(초) — 프레임당 재시도 스팸 방지.</summary>
        public static float AutoPickupRetryCooldown = 1f;

        /// <summary>선점 완료 플래그. // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        private bool _claimed;

        private SpriteRenderer _renderer;
        private TextMesh _label;
        private Vector3 _basePosition;
        private float _bobPhase;
        private string _promptCache;

        // 자석 런타임 상태
        private PlayerController _magnetTarget;   // 주기 스캔으로 캐싱한 최근접 생존 플레이어
        private CombatEntity _magnetTargetEntity; // 스캔 사이의 사망 확인용 (사망자는 흡입 중단)
        private PlayerController _magnetExcluded; // 버린 사람 추정 — 자석 반경을 한 번 벗어나면 해제
        private float _magnetSpeed;               // 누적 흡입 속도 — 대상 상실/반경 이탈 시 리셋
        private float _magnetDelayRemaining;      // 스폰 직후 흡입 유예 잔여 시간
        private float _scanTimer;                 // 다음 대상 스캔까지 남은 시간
        private float _pickupRetryTimer;          // 자동 획득 실패(슬롯 가득) 후 재시도 쿨다운

        /// <summary>자석 스캔용 공용 버퍼 (전 드롭 공유 — 스캔마다 할당 금지). 8인 + 주변 트리거 여유분.</summary>
        private static readonly Collider2D[] _magnetScanBuffer = new Collider2D[32];

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
            // 자석 상태 초기화 — 버린 아이템(SwapAndDrop)도 유예를 다시 받아 곧바로 되빨려 오지 않는다.
            _magnetSpeed = 0f;
            _pickupRetryTimer = 0f;
            _magnetDelayRemaining = magnetActivationDelay;
            // 버린 사람 제외 — 유예(0.35초)만으로는 제자리 버리기가 즉시 되빨려 와 버리기·양도가 무력화된다.
            CaptureMagnetExclusion();
            RefreshVisual();
        }

        private void Awake()
        {
            _basePosition = transform.position;
            // 스폰 시점을 흩어 놓아 여러 드롭이 한 몸처럼 까딱이지 않게 한다.
            _bobPhase = Random.value * Mathf.PI * 2f;
            // 자석 유예 시작 + 스캔 위상 분산 (드롭 무리가 같은 프레임에 몰려 스캔하지 않게).
            _magnetDelayRemaining = magnetActivationDelay;
            _scanTimer = Random.value * MagnetScanInterval;
        }

        private void Start() => RefreshVisual();

        private void Update()
        {
            float dt = Time.deltaTime;

            // 자석은 _basePosition만 움직인다 — transform은 아래 bob이 최종 결정한다 (직접 이동 금지).
            bool magnetMoved = UpdateMagnet(dt);

            if (bobAmplitude > 0f && _renderer != null)
            {
                _bobPhase += dt * bobSpeed;
                float offset = Mathf.Sin(_bobPhase) * bobAmplitude;
                transform.position = _basePosition + new Vector3(0f, offset, 0f);
            }
            else if (magnetMoved)
            {
                // bob이 꺼져 있으면 자석 이동을 여기서 직접 반영한다.
                transform.position = _basePosition;
            }
        }

        // ── 자석 (Game Feel) ─────────────────────────────────────

        /// <summary>흡입/자동 획득 처리. _basePosition을 움직였으면 true (transform 반영은 Update 몫).</summary>
        private bool UpdateMagnet(float dt)
        {
            if (!magnetEnabled || _claimed || item == null) return false;

            // 스폰 직후 유예 — 드롭이 흩어지는 연출이 먼저 보인다.
            if (_magnetDelayRemaining > 0f)
            {
                _magnetDelayRemaining -= dt;
                return false;
            }

            // 자동 획득 실패(슬롯 가득 등) 쿨다운 — 흡입·자동 획득만 멈춘다 (E키 픽업은 그대로 가능).
            if (_pickupRetryTimer > 0f)
            {
                _pickupRetryTimer -= dt;
                return false;
            }

            // 주기 스캔 — 매 프레임 물리 질의 금지 (8인×드롭 다수 전제).
            _scanTimer -= dt;
            if (_scanTimer <= 0f)
            {
                _scanTimer += MagnetScanInterval;
                ScanForMagnetTarget();
            }

            PlayerController target = _magnetTarget;
            if (target == null) return StopMagnet();                                     // 대상 없음/파괴됨
            if (_magnetTargetEntity != null && _magnetTargetEntity.IsDead) return StopMagnet(); // 스캔 사이 사망

            Vector3 targetPos = target.transform.position;
            float dx = targetPos.x - _basePosition.x;
            float dy = targetPos.y - _basePosition.y;
            float sqrDist = dx * dx + dy * dy;

            // 자동 획득 — E키와 같은 조건(item != null && !_claimed, 위에서 확인)으로 픽업 단일 지점만 호출.
            if (sqrDist <= autoPickupRadius * autoPickupRadius)
            {
                Pickup(target.PlayerId);
                if (_claimed) return false; // 성공 — Destroy 예약됨. 이후 어떤 상태도 만지지 않는다.

                // 실패 (슬롯 가득/중첩 상한) — 쿨다운 동안 재시도를 멈춰 프레임당 스팸을 막는다.
                _pickupRetryTimer = AutoPickupRetryCooldown;
                return StopMagnet();
            }

            // 반경 이탈 — 속도만 리셋하고 그 자리에 멈춘다 (원위치 복귀는 어색해서 금지).
            if (sqrDist > magnetRadius * magnetRadius) return StopMagnet();

            // 흡입 — 속도를 누적(가속)하므로 멀리서는 천천히, 붙을수록 빨라진다.
            _magnetSpeed = Mathf.Min(_magnetSpeed + magnetAcceleration * dt, magnetMaxSpeed);
            _basePosition = Vector3.MoveTowards(
                _basePosition,
                new Vector3(targetPos.x, targetPos.y, _basePosition.z), // z 유지 — 2D 평면 이동만
                _magnetSpeed * dt);
            return true;
        }

        /// <summary>흡입 중단 공통 처리 — 속도만 리셋한다. false 반환은 "이번 프레임 이동 없음".</summary>
        private bool StopMagnet()
        {
            _magnetSpeed = 0f;
            return false;
        }

        /// <summary>
        /// 스폰 순간 몸이 겹쳐 있던 최근접 플레이어를 '버린 사람'으로 추정해 자석 대상에서 제외한다.
        /// 드롭은 버린 사람의 발밑(프리팹 경로: 제자리, 매니저 폴백: +0.63u)에 스폰되므로 이 추정은 안전하고,
        /// 보스 드롭처럼 아무도 겹치지 않은 스폰에서는 아무도 제외되지 않는다. 제외는 그 플레이어가
        /// magnetRadius를 한 번 벗어나면 풀린다(ScanForMagnetTarget) — E키 픽업은 제외와 무관하게 가능.
        /// </summary>
        private void CaptureMagnetExclusion()
        {
            _magnetExcluded = null;
            if (!magnetEnabled || dropperExclusionRadius <= 0f) return;

            var filter = new ContactFilter2D { useTriggers = true }; // struct — 힙 할당 없음
            int count = Physics2D.OverlapCircle(
                (Vector2)transform.position, dropperExclusionRadius, filter, _magnetScanBuffer);

            float nearestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider2D col = _magnetScanBuffer[i];
                if (col == null) continue;

                PlayerController player = col.GetComponent<PlayerController>();
                if (player == null) player = col.GetComponentInParent<PlayerController>();
                if (player == null) continue;

                float dx = player.transform.position.x - transform.position.x;
                float dy = player.transform.position.y - transform.position.y;
                float sqr = dx * dx + dy * dy;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    _magnetExcluded = player;
                }
            }
        }

        /// <summary>반경 안 최근접 '살아있는' 플레이어를 캐싱한다. 할당 없는 질의 (공용 버퍼 + ContactFilter2D).</summary>
        private void ScanForMagnetTarget()
        {
            _magnetTarget = null;
            _magnetTargetEntity = null;

            // 버린 사람 제외 해제 — 자석 반경을 한 번 벗어났으면 다시 일반 대상이 된다.
            if (_magnetExcluded != null)
            {
                float ex = _magnetExcluded.transform.position.x - _basePosition.x;
                float ey = _magnetExcluded.transform.position.y - _basePosition.y;
                if (ex * ex + ey * ey > magnetRadius * magnetRadius) _magnetExcluded = null;
            }

            // 플레이어 콜라이더 구성(트리거 여부/레이어)이 불명이므로 트리거 포함, 전 레이어로 안전하게 질의한다.
            var filter = new ContactFilter2D { useTriggers = true }; // struct — 힙 할당 없음
            int count = Physics2D.OverlapCircle(
                (Vector2)_basePosition, magnetRadius, filter, _magnetScanBuffer);

            float nearestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider2D col = _magnetScanBuffer[i];
                if (col == null) continue;

                // 콜라이더가 자식(히트박스 등)에 붙어 있을 수 있어 GetComponentInParent로 폴백한다.
                PlayerController player = col.GetComponent<PlayerController>();
                if (player == null) player = col.GetComponentInParent<PlayerController>();
                if (player == null) continue;

                // 버린 사람은 반경을 한 번 벗어날 때까지 자석·자동 획득 대상이 아니다 (버리기·양도 보호).
                if (player == _magnetExcluded) continue;

                CombatEntity entity = player.GetComponent<CombatEntity>();
                if (entity == null) entity = player.GetComponentInParent<CombatEntity>();
                if (entity != null && entity.IsDead) continue; // 사망자는 흡입 대상에서 제외

                float dx = player.transform.position.x - _basePosition.x;
                float dy = player.transform.position.y - _basePosition.y;
                float sqr = dx * dx + dy * dy;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    _magnetTarget = player;
                    _magnetTargetEntity = entity;
                }
            }
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
