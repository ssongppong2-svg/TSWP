// 근거: UI 시스템.md — HUD 표시 요소: 체력 / 스킬 쿨타임 / 장착 아이템 / 상태이상 / 공유 부활 횟수 / 현재 방.
//   UI 5원칙(필요한 정보만 / 전투 방해 없이 / 2초 안에 이해 / 스트리머 친화(HUD 끄기) / 크기·투명도 조절).
// 이 컴포넌트가 HudModel을 소유하고 Subscribe한다 — 뷰가 없으면 뷰모델이 갱신조차 되지 않기 때문이다.
// 프로토타입 단계라 UGUI 프리팹 대신 IMGUI로 그린다. IMGUI 비용 규칙(이 프로젝트에서 프레임 튐 전례 있음):
//   ① Repaint 이벤트에서만 그린다  ② GUIStyle은 1회 생성 후 캐싱  ③ 문자열은 값이 바뀔 때만 재생성
//   ④ GUILayout 대신 고정 Rect(GUI.*)만 사용해 레이아웃 할당을 없앤다.
// 확장성: 스킬 슬롯 수는 HudModel.SkillCooldowns.Count(직업/스킬이 늘면 자동), 아이템 슬롯 수는
//   Core.GameRules.EquipSlotCount를 따른다 — 어느 쪽도 코드에 개수를 적지 않는다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.UI
{
    /// <summary>
    /// 플레이 중 상시 표시되는 HUD 뷰. 캔버스 3계층 중 ① Screen Space HUD에 해당한다.
    /// 씬에 이 컴포넌트 하나만 두면 HudModel이 GameEvents를 구독해 자동 갱신된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameplayHud : MonoBehaviour
    {
        public static GameplayHud Instance { get; private set; }

        [Header("대상")]
        [Tooltip("로컬 플레이어 id. 다른 playerId의 이벤트는 파티 정보로만 반영된다.")]
        [SerializeField] private int localPlayerId = 0;

        [Header("레이아웃 (비우면 런타임 기본값 사용 — 에셋 없이도 동작)")]
        [SerializeField] private HudLayoutConfig layout;

        [Header("표시 항목")]
        [SerializeField] private bool showHealth = true;
        [SerializeField] private bool showReviveCount = true;
        [SerializeField] private bool showRoomNumber = true;
        [SerializeField] private bool showStatusEffects = true;
        [SerializeField] private bool showItemSlots = true;
        [SerializeField] private bool showSkillSlots = true;

        [Header("연동")]
        [Tooltip("UIManager에 Hud 패널로 등록한다. 켜면 GameFlowState에 따라 이 오브젝트가 켜지고 꺼진다.")]
        [SerializeField] private bool registerWithUIManager = false;

        /// <summary>HUD 뷰모델. 외부(브리지/부트스트랩)가 값을 밀어 넣는 단일 지점.</summary>
        public HudModel Model { get; } = new HudModel();

        // ── 캐시 (문자열/스타일은 값이 바뀔 때만 재생성) ──────────
        private GUIStyle _label;
        private GUIStyle _labelSmall;
        private GUIStyle _labelCenter;
        private GUIStyle _labelSmallCenter;
        private int _styleFontSize = -1;

        private string _hpText = "-";
        private float _cachedHp = float.NaN;
        private float _cachedMaxHp = float.NaN;

        private string _reviveText = "-";
        private int _cachedRevive = int.MinValue;

        private string _roomText = "-";
        private int _cachedRoomNumber = int.MinValue;
        private int _cachedRoomTotal = int.MinValue;
        private string _cachedRoomLabel;

        private readonly string[] _slotTexts = new string[GameRules.EquipSlotCount];
        private readonly string[] _slotCodes = new string[GameRules.EquipSlotCount];

        // 스킬 슬롯은 개수가 가변이므로 리스트로 캐싱한다 (직업/스킬 추가 시 코드 수정 불필요).
        private readonly List<string> _skillIdCache = new List<string>();
        private readonly List<string> _skillNameCache = new List<string>();
        private readonly List<string> _skillTimeCache = new List<string>();
        private readonly List<float> _skillTimeShown = new List<float>();

        /// <summary>쿨타임 문자열 재생성 간격(초 단위 표시 정밀도). // TODO(밸런스): 문서 미정</summary>
        private const float CooldownTextStep = 0.1f;

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

            Model.Initialize(localPlayerId);
            Model.Subscribe();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Model.Unsubscribe();
        }

        private void Start()
        {
            // 등록은 선택 사항 — 켜지 않으면 흐름 상태와 무관하게 항상 보인다(프로토타입 기본).
            if (registerWithUIManager && UIManager.Instance != null)
                UIManager.Instance.RegisterPanel(UIPanelId.Hud, gameObject);
        }

        private void Update()
        {
            // 만료된 핑 마커 정리 — 아무도 호출하지 않으면 마커가 무한히 쌓인다.
            Model.Minimap.TickExpirePings(Time.time);
        }

        /// <summary>로컬 플레이어 교체 (관전/재접속 대응). // SYNC: 호스트 권위</summary>
        public void SetLocalPlayerId(int playerId)
        {
            localPlayerId = playerId;
            Model.Initialize(playerId);
        }

        // ── 그리기 ────────────────────────────────────────────────
        private void OnGUI()
        {
            // ① Repaint 이벤트에서만 그린다 (Layout/입력 이벤트에서 그리면 비용만 늘고 얻는 게 없다).
            if (Event.current.type != EventType.Repaint) return;

            var config = ResolveLayout();
            var settings = SettingsManager.Instance != null ? SettingsManager.Instance.Ui : null;
            if (settings != null && !settings.hudEnabled) return;   // 스트리머 친화: HUD 끄기

            float scale = settings != null ? settings.uiScale : 1f;
            _alpha = settings != null ? settings.uiOpacity : 1f;

            EnsureStyles(config);

            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float screenW = Screen.width / Mathf.Max(0.01f, scale);
            float screenH = Screen.height / Mathf.Max(0.01f, scale);

            DrawStatusPanel(config);
            DrawSlotRows(config, screenW, screenH);

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        /// <summary>좌상단: 체력 / 부활 횟수 / 방 번호 / 상태이상.</summary>
        private void DrawStatusPanel(HudLayoutConfig c)
        {
            bool anyStatus = showStatusEffects && Model.StatusEffects.Count > 0;

            float height = c.panelPadding * 2f;
            if (showHealth) height += c.lineHeight + c.healthBarHeight + c.elementSpacing;
            if (showReviveCount) height += c.lineHeight;
            if (showRoomNumber) height += c.lineHeight;
            if (anyStatus) height += c.elementSpacing + c.statusIconSize;

            var panel = new Rect(c.screenPadding.x, c.screenPadding.y, c.statusPanelWidth, height);
            DrawRect(panel, c.Panel);

            float x = panel.x + c.panelPadding;
            float w = panel.width - c.panelPadding * 2f;
            float y = panel.y + c.panelPadding;

            if (showHealth)
            {
                RefreshHealthText();
                float ratio = Model.HpRatio;
                Color hpColor = Model.IsDead ? c.Disabled : c.HealthColor(ratio);

                DrawLabel(new Rect(x, y, w, c.lineHeight), _hpText, Model.IsDead ? c.Warning : c.Text, _label);
                y += c.lineHeight;

                var bar = new Rect(x, y, w, c.healthBarHeight);
                DrawRect(bar, c.emptyFill);
                if (ratio > 0f)
                    DrawRect(new Rect(bar.x, bar.y, bar.width * ratio, bar.height), hpColor);
                y += c.healthBarHeight + c.elementSpacing;
            }

            if (showReviveCount)
            {
                RefreshReviveText();
                // 부활 자원이 바닥나면 팀 전멸 = 게임 오버이므로 색으로 즉시 알린다 (2초 안에 이해).
                Color reviveColor = Model.SharedReviveCount <= 0 ? c.Warning
                                  : Model.IsReviveLow ? c.Accent
                                  : c.Text;
                DrawLabel(new Rect(x, y, w, c.lineHeight), _reviveText, reviveColor, _label);
                y += c.lineHeight;
            }

            if (showRoomNumber)
            {
                RefreshRoomText();
                DrawLabel(new Rect(x, y, w, c.lineHeight), _roomText, c.Text, _label);
                y += c.lineHeight;
            }

            if (anyStatus)
            {
                y += c.elementSpacing;
                DrawStatusEffects(c, x, y);
            }
        }

        /// <summary>상태이상 아이콘 줄. 개수는 데이터가 정한다(16종 중 몇 개든).</summary>
        private void DrawStatusEffects(HudLayoutConfig c, float x, float y)
        {
            var effects = Model.StatusEffects;
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                var box = new Rect(x + i * (c.statusIconSize + c.statusIconSpacing), y,
                                   c.statusIconSize, c.statusIconSize);

                DrawRect(box, c.emptyFill);

                if (effect.Icon != null)
                {
                    DrawSprite(box, effect.Icon, Color.white);
                }
                else
                {
                    // 아이콘 에셋이 없어도 무엇이 걸렸는지는 알 수 있어야 한다 (연출 없어도 정보는 유지).
                    // ShortLabel은 적용 시점에 미리 만들어 둔 문자열이라 여기서 할당이 없다.
                    DrawLabel(box, effect.ShortLabel, c.Text, _labelSmallCenter);
                }

                // 남은 시간 = 아래에서 줄어드는 띠 (문자열을 만들지 않아 매 프레임 할당이 없다).
                float remain = Mathf.Clamp01(effect.RemainRatio);
                float barHeight = Mathf.Max(2f, c.statusIconSize * 0.12f);
                var timeBar = new Rect(box.x, box.yMax - barHeight, box.width * remain, barHeight);
                DrawRect(timeBar, effect.IsCC ? c.Warning : c.Accent);
            }
        }

        /// <summary>하단: 아이템 슬롯(좌) / 스킬 슬롯(우).</summary>
        private void DrawSlotRows(HudLayoutConfig c, float screenW, float screenH)
        {
            float rowY = screenH - c.screenPadding.y - c.slotSize - c.lineHeight;

            if (showItemSlots)
            {
                DrawLabel(new Rect(c.screenPadding.x, rowY, c.statusPanelWidth, c.lineHeight),
                          "장착 아이템", c.Text, _labelSmall);
                DrawItemSlots(c, c.screenPadding.x, rowY + c.lineHeight);
            }

            // 스킬이 하나도 없으면(직업 조립 전) 슬롯 줄 자체를 그리지 않는다.
            int skillCount = Model.SkillCooldowns.Count;
            if (showSkillSlots && skillCount > 0)
            {
                int count = skillCount;
                float totalWidth = count * c.slotSize + (count - 1) * c.slotSpacing;
                float x = screenW - c.screenPadding.x - totalWidth;
                DrawLabel(new Rect(x, rowY, totalWidth, c.lineHeight), "스킬", c.Text, _labelSmall);
                DrawSkillSlots(c, x, rowY + c.lineHeight);
            }
        }

        private void DrawItemSlots(HudLayoutConfig c, float x, float y)
        {
            // 슬롯 개수는 규칙 상수 한 곳에서만 온다 (아이템 시스템.md: 장착 5칸).
            for (int i = 0; i < GameRules.EquipSlotCount; i++)
            {
                var box = new Rect(x + i * (c.slotSize + c.slotSpacing), y, c.slotSize, c.slotSize);
                string code = Model.EquippedItemCodes[i];
                bool filled = !string.IsNullOrEmpty(code);

                DrawSlotFrame(c, box, filled ? c.Accent : c.slotBorderColor);

                if (!filled) continue;

                RefreshSlotText(i, code);
                DrawLabel(Inset(box, c.slotBorder + 1f), _slotTexts[i], c.Text, _labelSmallCenter);
            }
        }

        private void DrawSkillSlots(HudLayoutConfig c, float x, float y)
        {
            var skills = Model.SkillCooldowns;
            SyncSkillCaches(skills.Count);

            for (int i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                var box = new Rect(x + i * (c.slotSize + c.slotSpacing), y, c.slotSize, c.slotSize);

                DrawSlotFrame(c, box, skill.IsUsable ? c.Success : c.slotBorderColor);

                var inner = Inset(box, c.slotBorder);
                if (skill.Icon != null)
                {
                    // 사용 불가(쿨타임/침묵)면 회색 처리 — UI 시스템.md 명시.
                    DrawSprite(inner, skill.Icon, skill.IsUsable ? Color.white : c.Disabled);
                }

                // 쿨타임은 아래에서 차오르는 채움으로 표시 (FillRatio 1 = 사용 가능).
                float fill = Mathf.Clamp01(skill.FillRatio);
                if (fill < 1f)
                {
                    float coverHeight = inner.height * (1f - fill);
                    DrawRect(new Rect(inner.x, inner.y, inner.width, coverHeight), c.Disabled);
                }

                RefreshSkillText(i, skill);
                DrawLabel(new Rect(inner.x, inner.y + 2f, inner.width, c.lineHeight),
                          _skillNameCache[i], c.Text, _labelSmallCenter);

                if (skill.RemainingCooldown > 0f)
                {
                    DrawLabel(new Rect(inner.x, inner.center.y - c.lineHeight * 0.5f, inner.width, c.lineHeight),
                              _skillTimeCache[i], c.Accent, _labelCenter);
                }
            }
        }

        // ── 문자열 캐시 (값이 바뀔 때만 재생성) ────────────────────
        private void RefreshHealthText()
        {
            if (Mathf.Approximately(_cachedHp, Model.Hp) && Mathf.Approximately(_cachedMaxHp, Model.MaxHp)) return;
            _cachedHp = Model.Hp;
            _cachedMaxHp = Model.MaxHp;
            _hpText = $"체력  {Model.Hp:0} / {Model.MaxHp:0}";
        }

        private void RefreshReviveText()
        {
            if (_cachedRevive == Model.SharedReviveCount) return;
            _cachedRevive = Model.SharedReviveCount;
            _reviveText = $"공유 부활  {Model.SharedReviveCount}";
        }

        private void RefreshRoomText()
        {
            if (_cachedRoomNumber == Model.CurrentRoomNumber
                && _cachedRoomTotal == Model.TotalRoomCount
                && string.Equals(_cachedRoomLabel, Model.CurrentRoomLabel)) return;

            _cachedRoomNumber = Model.CurrentRoomNumber;
            _cachedRoomTotal = Model.TotalRoomCount;
            _cachedRoomLabel = Model.CurrentRoomLabel;

            _roomText = Model.CurrentRoomNumber <= 0
                ? "방  -"
                : Model.TotalRoomCount > 0
                    ? $"방  {Model.CurrentRoomNumber} / {Model.TotalRoomCount}"
                    : $"방  {Model.CurrentRoomNumber}";

            if (!string.IsNullOrEmpty(Model.CurrentRoomLabel))
                _roomText += $"  ({Model.CurrentRoomLabel})";
        }

        private void RefreshSlotText(int index, string itemCode)
        {
            // 참조 비교로 충분하다 — itemCode는 교체될 때만 바뀐다.
            if (ReferenceEquals(_slotCodes[index], itemCode)) return;
            _slotCodes[index] = itemCode;
            _slotTexts[index] = Shorten(itemCode, 8);
        }

        private void SyncSkillCaches(int count)
        {
            while (_skillNameCache.Count < count)
            {
                _skillIdCache.Add(null);
                _skillNameCache.Add(string.Empty);
                _skillTimeCache.Add(string.Empty);
                _skillTimeShown.Add(float.NaN);
            }
        }

        private void RefreshSkillText(int index, SkillCooldownInfo skill)
        {
            // 원본 skillId를 비교한다 — 매번 Shorten을 호출하면 프레임마다 Substring이 할당된다.
            if (!string.Equals(_skillIdCache[index], skill.SkillId))
            {
                _skillIdCache[index] = skill.SkillId;
                _skillNameCache[index] = Shorten(skill.SkillId, 7);
            }

            // 0.1초 단위로만 문자열을 새로 만든다 (매 프레임 문자열 생성 = GC 부하).
            float remaining = skill.RemainingCooldown;
            float quantized = Mathf.Ceil(remaining / CooldownTextStep) * CooldownTextStep;
            if (!Mathf.Approximately(_skillTimeShown[index], quantized))
            {
                _skillTimeShown[index] = quantized;
                _skillTimeCache[index] = quantized >= 10f ? $"{quantized:0}" : $"{quantized:0.0}";
            }
        }

        private static string Shorten(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }

        // ── 그리기 유틸 (전부 할당 없음) ───────────────────────────
        private void DrawRect(Rect rect, Color color)
        {
            GUI.color = WithAlpha(color);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
        }

        private void DrawSlotFrame(HudLayoutConfig c, Rect box, Color borderColor)
        {
            DrawRect(box, borderColor);
            DrawRect(Inset(box, c.slotBorder), c.emptyFill);
        }

        private void DrawSprite(Rect rect, Sprite sprite, Color tint)
        {
            if (sprite == null || sprite.texture == null) return;

            Rect textureRect = sprite.textureRect;
            Texture texture = sprite.texture;
            var coords = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);

            GUI.color = WithAlpha(tint);
            GUI.DrawTextureWithTexCoords(rect, texture, coords);
        }

        private void DrawLabel(Rect rect, string text, Color color, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            GUI.color = WithAlpha(Color.white);
            style.normal.textColor = WithAlpha(color);
            GUI.Label(rect, text, style);
        }

        private Color WithAlpha(Color color) => new Color(color.r, color.g, color.b, color.a * _alpha);

        private static Rect Inset(Rect rect, float amount) =>
            new Rect(rect.x + amount, rect.y + amount,
                     Mathf.Max(0f, rect.width - amount * 2f), Mathf.Max(0f, rect.height - amount * 2f));

        // ── 설정/스타일 ───────────────────────────────────────────
        /// <summary>레이아웃 SO가 없으면 런타임 기본값을 만든다 — 배선이 없어도 HUD가 실패하지 않는다.</summary>
        private HudLayoutConfig ResolveLayout()
        {
            if (layout == null) layout = HudLayoutConfig.CreateRuntimeDefault();
            return layout;
        }

        /// <summary>② GUIStyle은 1회만 생성한다 (OnGUI에서 new GUIStyle = 프레임마다 할당).
        /// 글자 크기 설정이 바뀐 경우에만 다시 만든다.</summary>
        private void EnsureStyles(HudLayoutConfig c)
        {
            if (_label != null && _styleFontSize == c.fontSize) return;
            _styleFontSize = c.fontSize;

            _label = new GUIStyle(GUI.skin.label) { fontSize = c.fontSize, alignment = TextAnchor.MiddleLeft };
            _labelSmall = new GUIStyle(_label) { fontSize = c.smallFontSize };
            _labelCenter = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _labelSmallCenter = new GUIStyle(_labelSmall) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
