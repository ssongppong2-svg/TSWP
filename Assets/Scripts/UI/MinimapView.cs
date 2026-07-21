// 근거: UI 시스템.md — 미니맵: 현재 위치 / 팀원 위치 / 목표 방향 / 특수 오브젝트 표시. 화면 우하단 상시 표시.
//   맵 시스템.md — 미니맵은 탐험한 지역만 표시(전장의 안개), 비밀방은 발견 전까지 비표시.
//   노출 판정 로직은 Map.MinimapState가 소유한다 — 여기서는 IsRoomVisible 질의만 한다(중복 구현 금지).
// 프로토타입 단계라 UGUI 프리팹/스프라이트 없이 IMGUI 사각형과 글자로만 그린다.
// IMGUI 비용 규칙(이 프로젝트에서 OnGUI 할당으로 프레임이 튄 전례 있음) — GameplayHud와 동일하게 지킨다:
//   ① Repaint 이벤트에서만 그린다  ② GUIStyle은 1회 생성 후 캐싱  ③ 문자열은 값이 바뀔 때만 재생성
//   ④ GUILayout 금지(고정 Rect의 GUI.*만 사용)  ⑤ 씬 스캔은 매 프레임이 아니라 주기적으로만
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;
using TSWP.Combat;
using TSWP.Map;

namespace TSWP.UI
{
    /// <summary>미니맵 표시 방식.</summary>
    public enum MinimapDisplayMode
    {
        /// <summary>맵 그래프가 있으면 방 그래프, 없으면 월드 레이더.</summary>
        Auto,
        /// <summary>방 그래프(레이어×분기) — 진행 경로 파악용.</summary>
        RoomGraph,
        /// <summary>플레이어 주변 월드 레이더 — 적/팀원/방 경계 파악용.</summary>
        WorldRadar,
    }

    /// <summary>
    /// 화면 우하단 미니맵 뷰. 캔버스 3계층 중 ① Screen Space HUD에 해당한다.
    /// 씬에 이 컴포넌트 하나만 두면 동작한다 — GameplayHud가 있으면 그 뷰모델을 공유하고,
    /// 없으면 자체 MinimapViewModel을 만들어 GameEvents를 구독한다.
    /// Map/Player 배선이 하나도 없어도 씬의 CombatEntity를 훑어 점만이라도 찍는다(프로토타입 허용).
    /// </summary>
    [DisallowMultipleComponent]
    public class MinimapView : MonoBehaviour
    {
        public static MinimapView Instance { get; private set; }

        [Header("대상")]
        [Tooltip("로컬 플레이어 id. 자기 점을 흰색으로 구분하는 데만 쓴다.")]
        [SerializeField] private int localPlayerId = 0;

        [Tooltip("미니맵 중심이 따라갈 대상. 비우면 씬에서 Players 팀 CombatEntity를 자동 탐색한다.")]
        [SerializeField] private Transform followTarget;

        [Header("표시")]
        [SerializeField] private MinimapDisplayMode mode = MinimapDisplayMode.Auto;

        [Tooltip("미니맵 패널 크기(px).")]
        [SerializeField] private Vector2 panelSize = new Vector2(190f, 190f);

        [Tooltip("화면 가장자리 여백(px). 비우면(0 이하) HudLayoutConfig의 screenPadding을 쓴다.")]
        [SerializeField] private Vector2 screenPadding = Vector2.zero;

        [Tooltip("월드 레이더가 담는 반경(월드 유닛). 값이 작을수록 확대된다.")]
        [SerializeField] private float radarWorldRange = 22f;

        [Tooltip("레이더 격자 간격(월드 유닛). 0 이하면 격자를 그리지 않는다.")]
        [SerializeField] private float radarGridStep = 5f;

        [Tooltip("점 한 변의 크기(px).")]
        [SerializeField] private float dotSize = 5f;

        [Tooltip("방 그래프 모드에서 방 한 칸의 크기(px).")]
        [SerializeField] private float roomBoxSize = 14f;

        [Header("옵션")]
        [Tooltip("씬의 CombatEntity를 훑는 주기(초). 0.1~0.5 권장 — 매 프레임 탐색은 비용이 크다.")]
        [SerializeField] private float sceneScanInterval = 0.25f;

        [Tooltip("씬 스캔으로 적/팀원 점을 찍는다. 끄면 뷰모델 데이터만 사용한다.")]
        [SerializeField] private bool scanSceneEntities = true;

        [Tooltip("표시 방식 전환 디버그 키. None이면 전환하지 않는다.")]
        // TODO: Input System 도입 후 Player.IPlayerInput 뒤로 옮긴다 (여기 직접 폴링은 프로토타입 편의).
        [SerializeField] private KeyCode toggleModeKey = KeyCode.M;

        [Tooltip("UIManager에 Minimap 패널로 등록한다. 켜면 GameFlowState/설정에 따라 켜지고 꺼진다.")]
        [SerializeField] private bool registerWithUIManager = false;

        [Header("레이아웃 색상 (비우면 런타임 기본값 — 에셋 없이도 동작)")]
        [SerializeField] private HudLayoutConfig layout;

        // ── 색 (프로토타입 고정값 — 팔레트 확정 시 Art SO로 이관) ──
        private static readonly Color SelfColor = Color.white;
        private static readonly Color TeammateColor = new Color(0.36f, 0.85f, 0.45f, 1f);
        private static readonly Color EnemyColor = new Color(0.92f, 0.24f, 0.24f, 1f);
        private static readonly Color NeutralColor = new Color(0.7f, 0.7f, 0.75f, 1f);
        private static readonly Color GridColor = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color RoomBoundsColor = new Color(0.45f, 0.75f, 1f, 0.75f);
        private static readonly Color LinkColor = new Color(0.65f, 0.65f, 0.72f, 0.7f);

        // 핑 5종 색 — PingType 선언 순서(Danger, Move, Item, Rally, Help)와 1:1 (Player.PingMarkerView와 동일).
        private static readonly Color[] PingColors =
        {
            new Color(0.92f, 0.24f, 0.24f, 1f),
            new Color(0.30f, 0.72f, 0.98f, 1f),
            new Color(0.98f, 0.82f, 0.25f, 1f),
            new Color(0.36f, 0.85f, 0.45f, 1f),
            new Color(0.80f, 0.44f, 0.95f, 1f),
        };

        // 방 종류 라벨 — Map.RoomType 선언 순서와 1:1
        // (Start, NormalCombat, Elite, Event, Shop, Rest, Puzzle, Secret, BossPractice, Boss).
        private static readonly string[] RoomLabels =
        { "S", "전", "정", "?", "상", "휴", "퍼", "비", "연", "B" };

        private static readonly Color[] RoomColors =
        {
            new Color(0.45f, 0.75f, 1f, 1f),    // Start
            new Color(0.55f, 0.55f, 0.60f, 1f), // NormalCombat
            new Color(0.95f, 0.55f, 0.20f, 1f), // Elite
            new Color(0.60f, 0.80f, 0.45f, 1f), // Event
            new Color(0.98f, 0.82f, 0.25f, 1f), // Shop
            new Color(0.36f, 0.85f, 0.45f, 1f), // Rest
            new Color(0.45f, 0.70f, 0.95f, 1f), // Puzzle
            new Color(0.80f, 0.44f, 0.95f, 1f), // Secret
            new Color(0.70f, 0.45f, 0.45f, 1f), // BossPractice
            new Color(0.90f, 0.20f, 0.20f, 1f), // Boss
        };

        // ── 캐시 ──────────────────────────────────────────────────
        private GUIStyle _label;
        private GUIStyle _labelCenter;
        private int _styleFontSize = -1;

        private MinimapViewModel _model;
        private MinimapViewModel _ownModel;   // GameplayHud가 없을 때만 생성/구독한다

        private readonly List<CombatEntity> _entities = new List<CombatEntity>();
        private float _nextScanTime;

        private RoomInstance _boundsRoom;
        private Bounds _roomBounds;
        private bool _hasRoomBounds;

        private string _titleText = "미니맵";
        private int _cachedVisibleRooms = int.MinValue;
        private int _cachedVisibleRoomsShown = int.MinValue;
        private int _cachedEnemyCount = int.MinValue;
        private MinimapDisplayMode _cachedTitleMode = (MinimapDisplayMode)(-1);

        private int _enemyCount;
        private float _alpha = 1f;

        /// <summary>현재 사용 중인 뷰모델 (외부 브리지가 팀원 위치를 밀어 넣는 지점).</summary>
        public MinimapViewModel Model => _model;

        /// <summary>표시 방식 교체 (설정 UI/디버그용).</summary>
        public void SetMode(MinimapDisplayMode value) => mode = value;

        /// <summary>미니맵 중심 대상 교체 (관전/재접속 대응).</summary>
        public void SetFollowTarget(Transform target) => followTarget = target;

        // ── 수명 주기 ─────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            ResolveModel();
            if (registerWithUIManager && UIManager.Instance != null)
                UIManager.Instance.RegisterPanel(UIPanelId.Minimap, gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // 자체 생성한 뷰모델만 해제한다 — HudModel 소유 뷰모델은 GameplayHud가 해제한다.
            _ownModel?.Unsubscribe();
            _ownModel = null;
        }

        private void Update()
        {
            ResolveModel();

            if (toggleModeKey != KeyCode.None && Input.GetKeyDown(toggleModeKey))
            {
                mode = mode == MinimapDisplayMode.WorldRadar
                    ? MinimapDisplayMode.RoomGraph
                    : MinimapDisplayMode.WorldRadar;
            }

            // 자체 뷰모델일 때만 핑 만료를 처리한다 (HudModel 뷰모델은 GameplayHud가 이미 처리 — 이중 호출 금지).
            _ownModel?.TickExpirePings(Time.time);

            if (Time.time >= _nextScanTime)
            {
                _nextScanTime = Time.time + Mathf.Max(0.05f, sceneScanInterval);
                RefreshSceneEntities();
                RefreshRoomBounds();
            }

            // 자기 위치는 매 프레임 뷰모델에 반영한다 — 다른 UI(핑/목표 방향)도 같은 값을 쓴다.
            Transform target = ResolveFollowTarget();
            if (target != null && _model != null)
                _model.SelfPosition = target.position;
        }

        /// <summary>GameplayHud가 있으면 그 뷰모델을 공유하고, 없으면 자체 뷰모델을 만들어 구독한다.</summary>
        private void ResolveModel()
        {
            var hud = GameplayHud.Instance;
            if (hud != null)
            {
                if (!ReferenceEquals(_model, hud.Model.Minimap))
                {
                    _model = hud.Model.Minimap;
                    // 뒤늦게 HUD가 생겼다면 자체 뷰모델은 중복 구독이 되므로 즉시 해제한다.
                    _ownModel?.Unsubscribe();
                    _ownModel = null;
                }
                return;
            }

            if (_model != null) return;
            _ownModel = new MinimapViewModel();
            _ownModel.Subscribe();
            _model = _ownModel;
        }

        /// <summary>씬의 CombatEntity 목록 갱신. 매 프레임이 아니라 sceneScanInterval 주기로만 훑는다.</summary>
        private void RefreshSceneEntities()
        {
            _entities.Clear();
            _enemyCount = 0;
            if (!scanSceneEntities) return;

            // Unity 6: FindObjectOfType/FindObjectsOfType 제거 → FindObjectsByType 사용.
            var found = Object.FindObjectsByType<CombatEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                var entity = found[i];
                if (entity == null) continue;
                _entities.Add(entity);
                if (entity.Team == TeamType.Enemies && !entity.IsDead) _enemyCount++;
            }
        }

        /// <summary>현재 방 경계 갱신 — 방이 바뀔 때만 콜라이더를 합산한다(주기 스캔 중 1회).</summary>
        private void RefreshRoomBounds()
        {
            var flow = RoomFlowManager.Instance;
            RoomInstance room = flow != null ? flow.CurrentRoom : null;

            if (ReferenceEquals(room, _boundsRoom) && _hasRoomBounds) return;
            _boundsRoom = room;
            _hasRoomBounds = false;
            if (room == null) return;

            var colliders = room.GetComponentsInChildren<Collider2D>();
            if (colliders == null || colliders.Length == 0) return;

            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) bounds.Encapsulate(colliders[i].bounds);
            _roomBounds = bounds;
            _hasRoomBounds = true;
        }

        /// <summary>따라갈 대상 확인. 지정이 없으면 Players 팀 엔티티 중 첫 번째를 쓴다.</summary>
        private Transform ResolveFollowTarget()
        {
            if (followTarget != null) return followTarget;

            for (int i = 0; i < _entities.Count; i++)
            {
                var entity = _entities[i];
                if (entity == null || entity.Team != TeamType.Players) continue;
                followTarget = entity.transform;
                return followTarget;
            }
            return null;
        }

        // ── 그리기 ────────────────────────────────────────────────
        private void OnGUI()
        {
            // ① Repaint에서만 그린다.
            if (Event.current.type != EventType.Repaint) return;

            var settings = SettingsManager.Instance != null ? SettingsManager.Instance.Ui : null;
            if (settings != null && (!settings.hudEnabled || !settings.showMinimap)) return;

            var c = ResolveLayout();
            EnsureStyles(c);

            float scale = settings != null ? settings.uiScale : 1f;
            _alpha = settings != null ? settings.uiOpacity : 1f;

            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            float screenW = Screen.width / Mathf.Max(0.01f, scale);
            float screenH = Screen.height / Mathf.Max(0.01f, scale);

            Vector2 pad = screenPadding.x > 0f || screenPadding.y > 0f ? screenPadding : c.screenPadding;
            var panel = new Rect(screenW - pad.x - panelSize.x, screenH - pad.y - panelSize.y,
                                 panelSize.x, panelSize.y);

            DrawRect(panel, c.Panel);
            DrawFrame(panel, c.slotBorderColor, 1f);

            var header = new Rect(panel.x + c.panelPadding, panel.y + 2f,
                                  panel.width - c.panelPadding * 2f, c.lineHeight);
            var body = new Rect(panel.x + c.panelPadding, header.yMax,
                                panel.width - c.panelPadding * 2f,
                                panel.height - header.height - c.panelPadding - 2f);

            MinimapDisplayMode resolved = ResolveMode();
            RefreshTitle(resolved);
            DrawLabel(header, _titleText, c.Text, _label);

            DrawRect(body, c.emptyFill);

            if (resolved == MinimapDisplayMode.RoomGraph) DrawRoomGraph(c, body);
            else DrawWorldRadar(c, body, settings);

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
        }

        private MinimapDisplayMode ResolveMode()
        {
            if (mode != MinimapDisplayMode.Auto) return mode;

            var rm = RoomManager.Instance;
            bool hasGraph = rm != null && rm.Graph != null && rm.Graph.Rooms.Count > 0;
            return hasGraph ? MinimapDisplayMode.RoomGraph : MinimapDisplayMode.WorldRadar;
        }

        // ── 방 그래프 모드 ────────────────────────────────────────
        /// <summary>레이어(세로) × 분기 슬롯(가로)로 방을 배치한다. 노출 판정은 MinimapState가 소유.</summary>
        private void DrawRoomGraph(HudLayoutConfig c, Rect body)
        {
            var rm = RoomManager.Instance;
            MapGraph graph = rm != null ? rm.Graph : null;
            if (graph == null || graph.Rooms.Count == 0)
            {
                DrawLabel(body, "맵 없음", c.Disabled, _labelCenter);
                return;
            }

            MinimapState state = rm.Minimap;

            // 배치 기준은 전체 방으로 계산한다 — 방을 발견할 때마다 이미 그린 방이 움직이면 읽기 어렵다.
            int maxLayer = 0;
            int maxIndex = 0;
            for (int i = 0; i < graph.Rooms.Count; i++)
            {
                var node = graph.Rooms[i];
                if (node.Layer > maxLayer) maxLayer = node.Layer;
                if (node.IndexInLayer > maxIndex) maxIndex = node.IndexInLayer;
            }

            float half = roomBoxSize * 0.5f;
            float usableW = Mathf.Max(1f, body.width - roomBoxSize);
            float usableH = Mathf.Max(1f, body.height - roomBoxSize);
            float stepX = maxIndex > 0 ? usableW / maxIndex : 0f;
            float stepY = maxLayer > 0 ? usableH / maxLayer : 0f;

            int currentRoomId = _model != null ? _model.CurrentRoomId : -1;
            if (currentRoomId < 0 && rm.CurrentRoom != null) currentRoomId = rm.CurrentRoom.RoomId;

            // ① 통로 먼저 (양쪽 방이 모두 노출된 경우에만 — 미탐험 방으로 가는 선은 정보 누설이다)
            for (int i = 0; i < graph.Connections.Count; i++)
            {
                var link = graph.Connections[i];
                var from = graph.GetRoom(link.FromRoomId);
                var to = graph.GetRoom(link.ToRoomId);
                if (from == null || to == null) continue;
                if (!IsVisible(state, from.RoomId) || !IsVisible(state, to.RoomId)) continue;

                Vector2 a = GraphPos(body, from, stepX, stepY, half);
                Vector2 b = GraphPos(body, to, stepX, stepY, half);
                DrawElbow(a, b, LinkColor);
            }

            // ② 방 상자
            int visibleCount = 0;
            for (int i = 0; i < graph.Rooms.Count; i++)
            {
                var node = graph.Rooms[i];
                if (!IsVisible(state, node.RoomId)) continue;   // 비밀방은 발견 전 여기서 걸러진다
                visibleCount++;

                Vector2 center = GraphPos(body, node, stepX, stepY, half);
                var box = new Rect(center.x - half, center.y - half, roomBoxSize, roomBoxSize);

                int typeIndex = (int)node.RoomType;
                Color roomColor = typeIndex >= 0 && typeIndex < RoomColors.Length ? RoomColors[typeIndex] : NeutralColor;
                // 미탐험(상점/보스 미리보기)은 흐리게 — 이미 다녀온 방과 구분된다.
                if (!state.IsExplored(node.RoomId)) roomColor *= 0.55f;

                DrawRect(box, roomColor);

                string labelText = typeIndex >= 0 && typeIndex < RoomLabels.Length ? RoomLabels[typeIndex] : "?";
                DrawLabel(box, labelText, Color.black, _labelCenter);

                if (node.RoomId == currentRoomId)
                {
                    DrawFrame(Expand(box, 2f), SelfColor, 2f);   // 현재 위치
                }
            }

            _cachedVisibleRooms = visibleCount;

            if (visibleCount == 0)
                DrawLabel(body, "미탐험", c.Disabled, _labelCenter);
        }

        private static bool IsVisible(MinimapState state, int roomId)
            => state == null || state.IsRoomVisible(roomId);   // 상태가 없으면(테스트 씬) 전부 보여준다

        private static Vector2 GraphPos(Rect body, RoomNode node, float stepX, float stepY, float half)
        {
            // 레이어 0(시작)이 아래, 마지막 레이어(보스)가 위로 오도록 뒤집는다.
            float x = body.x + half + node.IndexInLayer * stepX;
            float y = body.yMax - half - node.Layer * stepY;
            return new Vector2(x, y);
        }

        // ── 월드 레이더 모드 ──────────────────────────────────────
        /// <summary>플레이어 주변을 위에서 내려다본 점 지도. 맵 데이터가 없어도 동작한다.</summary>
        private void DrawWorldRadar(HudLayoutConfig c, Rect body, UISettings settings)
        {
            Transform target = ResolveFollowTarget();
            Vector2 center = target != null ? (Vector2)target.position
                           : _model != null ? _model.SelfPosition : Vector2.zero;

            float range = Mathf.Max(1f, radarWorldRange);
            float unitsToPx = Mathf.Min(body.width, body.height) * 0.5f / range;

            // 격자 — 이동하고 있다는 감각을 준다 (점만 있으면 정지해 보인다).
            if (radarGridStep > 0f)
            {
                float stepPx = radarGridStep * unitsToPx;
                if (stepPx >= 4f)
                {
                    float offsetX = Mathf.Repeat(center.x, radarGridStep) * unitsToPx;
                    float offsetY = Mathf.Repeat(center.y, radarGridStep) * unitsToPx;
                    for (float x = body.center.x - offsetX; x > body.x; x -= stepPx)
                        DrawRect(new Rect(x, body.y, 1f, body.height), GridColor);
                    for (float x = body.center.x - offsetX + stepPx; x < body.xMax; x += stepPx)
                        DrawRect(new Rect(x, body.y, 1f, body.height), GridColor);
                    for (float y = body.center.y + offsetY; y > body.y; y -= stepPx)
                        DrawRect(new Rect(body.x, y, body.width, 1f), GridColor);
                    for (float y = body.center.y + offsetY + stepPx; y < body.yMax; y += stepPx)
                        DrawRect(new Rect(body.x, y, body.width, 1f), GridColor);
                }
            }

            // 방 경계 — 현재 방의 콜라이더 합산 영역.
            if (_hasRoomBounds)
            {
                Vector2 min = WorldToPanel(body, center, unitsToPx, _roomBounds.min);
                Vector2 max = WorldToPanel(body, center, unitsToPx, _roomBounds.max);
                var rect = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                                           Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
                DrawClippedFrame(rect, body, RoomBoundsColor, 1f);
            }

            // 씬 엔티티 — 적(빨강) / 팀원(초록) / 자기(흰색).
            for (int i = 0; i < _entities.Count; i++)
            {
                var entity = _entities[i];
                if (entity == null || entity.IsDead) continue;

                Color color;
                float size = dotSize;
                if (entity.Team == TeamType.Enemies) color = EnemyColor;
                else if (entity.Team == TeamType.Players)
                {
                    bool isSelf = target != null && entity.transform == target;
                    color = isSelf ? SelfColor : TeammateColor;
                    if (isSelf) size = dotSize + 2f;
                }
                else color = NeutralColor;

                DrawDot(body, center, unitsToPx, entity.transform.position, size, color);
            }

            // 뷰모델의 팀원 위치 — 네트워크 경유로만 들어오는 원격 플레이어 (씬 스캔으로는 안 잡힌다).
            if (_model != null)
            {
                foreach (var pair in _model.TeammatePositions)
                {
                    if (pair.Key == localPlayerId) continue;
                    DrawDot(body, center, unitsToPx, pair.Value, dotSize, TeammateColor);
                }

                // 핑 마커 (설정에서 끌 수 있다 — UI 시스템.md 설정 7종).
                bool showPings = settings == null || settings.showPings;
                if (showPings)
                {
                    var markers = _model.Markers;
                    for (int i = 0; i < markers.Count; i++)
                    {
                        var marker = markers[i];
                        if (marker.Type != MinimapMarkerType.Ping) continue;
                        int index = (int)marker.PingKind;
                        Color color = index >= 0 && index < PingColors.Length ? PingColors[index] : NeutralColor;
                        DrawDot(body, center, unitsToPx, marker.WorldPos, dotSize + 3f, color);
                    }
                }

                // 목표 방향 — 패널 가장자리에 화살표 대신 사각 표식(프로토타입).
                if (_model.ObjectiveDirection.sqrMagnitude > 0.0001f)
                {
                    Vector2 dir = _model.ObjectiveDirection.normalized;
                    float edge = Mathf.Min(body.width, body.height) * 0.5f - dotSize;
                    var pos = new Vector2(body.center.x + dir.x * edge, body.center.y - dir.y * edge);
                    DrawRect(new Rect(pos.x - 3f, pos.y - 3f, 6f, 6f), c.Accent);
                }
            }

            // 자기 위치가 스캔되지 않았을 때도 중심 표식은 있어야 한다 (빈 화면 방지).
            if (target == null)
                DrawRect(new Rect(body.center.x - 2f, body.center.y - 2f, 4f, 4f), c.Disabled);

            DrawFrame(body, c.slotBorderColor, 1f);
        }

        private void DrawDot(Rect body, Vector2 center, float unitsToPx, Vector2 world, float size, Color color)
        {
            Vector2 p = WorldToPanel(body, center, unitsToPx, world);
            float half = size * 0.5f;
            if (p.x + half < body.x || p.x - half > body.xMax) return;   // 범위 밖은 그리지 않는다
            if (p.y + half < body.y || p.y - half > body.yMax) return;
            DrawRect(new Rect(p.x - half, p.y - half, size, size), color);
        }

        private static Vector2 WorldToPanel(Rect body, Vector2 center, float unitsToPx, Vector2 world)
        {
            Vector2 delta = (world - center) * unitsToPx;
            return new Vector2(body.center.x + delta.x, body.center.y - delta.y); // GUI의 y는 아래로 증가
        }

        // ── 문자열 캐시 (③ 값이 바뀔 때만 재생성) ──────────────────
        private void RefreshTitle(MinimapDisplayMode resolved)
        {
            if (resolved == MinimapDisplayMode.RoomGraph)
            {
                if (_cachedTitleMode == resolved && _cachedVisibleRooms == _cachedVisibleRoomsShown) return;
                _cachedTitleMode = resolved;
                _cachedVisibleRoomsShown = _cachedVisibleRooms;
                _titleText = _cachedVisibleRooms > 0 ? $"미니맵  탐험 {_cachedVisibleRooms}" : "미니맵";
                return;
            }

            if (_cachedTitleMode == resolved && _cachedEnemyCount == _enemyCount) return;
            _cachedTitleMode = resolved;
            _cachedEnemyCount = _enemyCount;
            _titleText = _enemyCount > 0 ? $"미니맵  적 {_enemyCount}" : "미니맵";
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

        /// <summary>패널 밖으로 나간 변은 잘라내고 그리는 테두리 (방 경계용).</summary>
        private void DrawClippedFrame(Rect rect, Rect clip, Color color, float thickness)
        {
            float x0 = Mathf.Max(rect.x, clip.x);
            float x1 = Mathf.Min(rect.xMax, clip.xMax);
            if (x1 <= x0) return;

            if (rect.y >= clip.y && rect.y <= clip.yMax)
                DrawRect(new Rect(x0, rect.y, x1 - x0, thickness), color);
            if (rect.yMax >= clip.y && rect.yMax <= clip.yMax)
                DrawRect(new Rect(x0, rect.yMax - thickness, x1 - x0, thickness), color);

            float y0 = Mathf.Max(rect.y, clip.y);
            float y1 = Mathf.Min(rect.yMax, clip.yMax);
            if (y1 <= y0) return;

            if (rect.x >= clip.x && rect.x <= clip.xMax)
                DrawRect(new Rect(rect.x, y0, thickness, y1 - y0), color);
            if (rect.xMax >= clip.x && rect.xMax <= clip.xMax)
                DrawRect(new Rect(rect.xMax - thickness, y0, thickness, y1 - y0), color);
        }

        /// <summary>두 점을 ㄱ자(세로→가로)로 잇는다. 회전 행렬을 건드리지 않아 비용이 없다.</summary>
        private void DrawElbow(Vector2 a, Vector2 b, Color color)
        {
            float y0 = Mathf.Min(a.y, b.y);
            float y1 = Mathf.Max(a.y, b.y);
            DrawRect(new Rect(a.x, y0, 1f, y1 - y0), color);

            float x0 = Mathf.Min(a.x, b.x);
            float x1 = Mathf.Max(a.x, b.x);
            DrawRect(new Rect(x0, b.y, x1 - x0, 1f), color);
        }

        private void DrawLabel(Rect rect, string text, Color color, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            GUI.color = WithAlpha(Color.white);
            style.normal.textColor = WithAlpha(color);
            GUI.Label(rect, text, style);
        }

        private Color WithAlpha(Color color) => new Color(color.r, color.g, color.b, color.a * _alpha);

        private static Rect Expand(Rect rect, float amount) =>
            new Rect(rect.x - amount, rect.y - amount, rect.width + amount * 2f, rect.height + amount * 2f);

        // ── 설정/스타일 ───────────────────────────────────────────
        private HudLayoutConfig ResolveLayout()
        {
            if (layout == null) layout = HudLayoutConfig.CreateRuntimeDefault();
            return layout;
        }

        /// <summary>② GUIStyle은 1회만 생성한다 (OnGUI에서 new GUIStyle = 프레임마다 할당).</summary>
        private void EnsureStyles(HudLayoutConfig c)
        {
            if (_label != null && _styleFontSize == c.smallFontSize) return;
            _styleFontSize = c.smallFontSize;

            _label = new GUIStyle(GUI.skin.label) { fontSize = c.smallFontSize, alignment = TextAnchor.MiddleLeft };
            _labelCenter = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
        }
    }
}
