// 근거: 방 시스템.md — 클리어 조건 2종(전멸형/목표형)과 문 개방이 방 진행의 전부다.
//       퍼즐 시스템.md — "게임 진행이 막혀서는 안 된다"(소프트락 금지). 막혔을 때의 탈출구가 반드시 있어야 한다.
// 이 HUD는 개발 전용이다. 씬에 없어도 게임 로직에는 아무 영향이 없다 (모든 참조에 null 가드).
// 성능 주의: OnGUI는 Repaint 이벤트에서만 그리고, GUIStyle과 표시 문자열을 캐싱한다
//   (ARCHITECTURE.md 코딩 규칙 — OnGUI 할당으로 프레임이 튀는 전례).
using System.Text;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;
using TSWP.Puzzles;

namespace TSWP.Map
{
    /// <summary>
    /// 현재 방 / 클리어 조건 / 문 / 퍼즐 상태를 화면에 표시하고,
    /// 진행이 막혔을 때 강제로 뚫는 디버그 단축키를 제공하는 개발용 HUD.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomDebugHud : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField] private bool visibleOnStart = true;

        [Tooltip("표시를 껐다 켜는 키.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.F8;

        [SerializeField] private Rect area = new Rect(12f, 540f, 460f, 300f);

        [Tooltip("문자열 재생성 간격(초). 짧을수록 최신이지만 할당이 잦아진다.")]
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;

        [Header("디버그 단축키 (KeyCode.None이면 비활성)")]
        [Tooltip("현재 방을 조건 무시하고 클리어한다 — 문이 열린다.")]
        [SerializeField] private KeyCode forceClearKey = KeyCode.F5;

        [Tooltip("다음 방으로 즉시 이동한다. 막혀 있으면 먼저 강제 클리어한 뒤 다시 시도한다.")]
        [SerializeField] private KeyCode nextRoomKey = KeyCode.F6;

        [Tooltip("현재 방의 퍼즐을 전부 강제로 해결한다 (정상 클리어 경로를 그대로 탄다).")]
        [SerializeField] private KeyCode solvePuzzleKey = KeyCode.F7;

        [Tooltip("강제 클리어할 때 남은 적도 함께 처치한다.")]
        [SerializeField] private bool killEnemiesOnForceClear = true;

        private bool _visible;
        private float _refreshTimer;

        // 캐싱 — OnGUI 안에서는 새로 만들지 않는다.
        private GUIStyle _textStyle;
        private GUIStyle _boxStyle;
        private string _cachedText = "방 정보 수집 중...";
        private string _hintText = "";
        private readonly StringBuilder _sb = new StringBuilder(768);

        private void Awake()
        {
            _visible = visibleOnStart;
            _hintText = BuildHintText();
        }

        private void Start() => Rebuild();

        private void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey)) _visible = !_visible;

            HandleDebugKeys();

            if (!_visible) return;

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer > 0f) return;

            _refreshTimer = refreshInterval;
            Rebuild();
        }

        // ── 디버그 단축키 ─────────────────────────────────────────
        private void HandleDebugKeys()
        {
            if (solvePuzzleKey != KeyCode.None && Input.GetKeyDown(solvePuzzleKey)) DebugSolvePuzzles();
            if (forceClearKey != KeyCode.None && Input.GetKeyDown(forceClearKey)) DebugForceClear();
            if (nextRoomKey != KeyCode.None && Input.GetKeyDown(nextRoomKey)) DebugGoToNextRoom();
        }

        /// <summary>현재 방의 퍼즐을 강제 해결한다 — 정상 클리어 경로(퍼즐 → 목표 → 문)를 그대로 검증할 수 있다.</summary>
        public void DebugSolvePuzzles()
        {
            var room = RoomFlowManager.Instance != null ? RoomFlowManager.Instance.CurrentRoom : null;
            if (room == null)
            {
                Debug.LogWarning("[RoomDebugHud] 현재 방(RoomInstance)이 없어 퍼즐을 해결할 수 없습니다.", this);
                return;
            }

            int solved = room.DebugSolveAllPuzzles();
            Debug.Log($"[RoomDebugHud] 퍼즐 강제 해결 {solved}건.", this);
            Rebuild();
        }

        /// <summary>현재 방을 강제 클리어한다 (문 개방 + 보상 지급까지 정상 경로를 탄다).</summary>
        public void DebugForceClear()
        {
            var manager = RoomManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[RoomDebugHud] RoomManager가 없어 강제 클리어할 수 없습니다.", this);
                return;
            }

            if (killEnemiesOnForceClear) KillRemainingEnemies();

            if (!manager.ForceClearCurrentRoom())
                Debug.Log("[RoomDebugHud] 강제 클리어 대상이 없습니다 (이미 클리어됨 또는 방 없음).", this);

            Rebuild();
        }

        /// <summary>다음 방으로 이동한다. 막혀 있으면 강제 클리어 후 재시도한다.</summary>
        public void DebugGoToNextRoom()
        {
            var flow = RoomFlowManager.Instance;
            if (flow == null)
            {
                Debug.LogWarning("[RoomDebugHud] RoomFlowManager가 없어 방을 이동할 수 없습니다.", this);
                return;
            }

            if (flow.RequestMoveToNext()) { Rebuild(); return; }

            DebugForceClear();
            if (!flow.RequestMoveToNext())
                Debug.LogWarning("[RoomDebugHud] 다음 방이 없습니다 (마지막 방이거나 그래프 연결이 없습니다).", this);

            Rebuild();
        }

        /// <summary>남은 적 진영 전투 개체를 전부 처치한다 (전멸형 방을 뚫기 위한 최후 수단).</summary>
        private void KillRemainingEnemies()
        {
            // Unity 6: FindObjectOfType 계열은 제거됨 — FindObjectsByType 사용.
            var entities = FindObjectsByType<CombatEntity>();
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == null || entity.IsDead) continue;
                if (entity.Team != TeamType.Enemies) continue;
                entity.Kill(null);
            }
        }

        // ── 표시 문자열 ───────────────────────────────────────────
        private string BuildHintText()
        {
            var sb = new StringBuilder(128);
            sb.Append("\n[단축키] ");
            if (solvePuzzleKey != KeyCode.None) sb.Append(solvePuzzleKey).Append(" 퍼즐해결  ");
            if (forceClearKey != KeyCode.None) sb.Append(forceClearKey).Append(" 방강제클리어  ");
            if (nextRoomKey != KeyCode.None) sb.Append(nextRoomKey).Append(" 다음방");
            return sb.ToString();
        }

        private void Rebuild()
        {
            _sb.Clear();
            _sb.Append("=== 방 디버그 (").Append(toggleKey).Append(" 토글) ===\n");

            var manager = RoomManager.Instance;
            var flow = RoomFlowManager.Instance;

            if (manager == null)
            {
                _sb.Append("RoomManager 없음 — 방 흐름이 시작되지 않았습니다.\n");
                _cachedText = _sb.ToString();
                return;
            }

            AppendRoomLine(manager, flow);
            AppendConditionLine(manager);
            AppendPuzzleLines(flow);
            AppendDoorLines(flow);

            _sb.Append(_hintText);
            _cachedText = _sb.ToString();
        }

        private void AppendRoomLine(RoomManager manager, RoomFlowManager flow)
        {
            var node = manager.CurrentRoom;
            if (node == null)
            {
                _sb.Append("현재 방: 없음\n");
                return;
            }

            int total = flow != null ? flow.TotalRoomCount : 0;
            _sb.Append("현재 방: ").Append(node.RoomId + 1);
            if (total > 0) _sb.Append(" / ").Append(total);
            _sb.Append("  종류 ").Append(node.RoomType)
               .Append("  클리어 ").Append(node.IsCleared ? "O" : "X")
               .Append('\n');
        }

        private void AppendConditionLine(RoomManager manager)
        {
            var condition = manager.CurrentCondition;
            if (condition == null)
            {
                _sb.Append("클리어 조건: 없음\n");
                return;
            }

            _sb.Append("조건: ").Append(condition.clearType);

            switch (condition.clearType)
            {
                case RoomClearType.KillAllEnemies:
                    _sb.Append("  남은 적 ").Append(condition.RemainingEnemies)
                       .Append("  스폰종료 ").Append(condition.SpawnFinished ? "O" : "X");
                    break;

                case RoomClearType.ObjectiveComplete:
                    _sb.Append("  목표 '")
                       .Append(string.IsNullOrEmpty(condition.objectiveId) ? "(아무거나)" : condition.objectiveId)
                       .Append("'  달성 ").Append(condition.ObjectiveDone ? "O" : "X");
                    break;
            }

            _sb.Append("  충족 ").Append(condition.IsSatisfied ? "O" : "X").Append('\n');
        }

        private void AppendPuzzleLines(RoomFlowManager flow)
        {
            var room = flow != null ? flow.CurrentRoom : null;
            var puzzles = room != null ? room.Puzzles : null;

            if (puzzles == null || puzzles.Count == 0)
            {
                _sb.Append("퍼즐: 없음\n");
                return;
            }

            _sb.Append("퍼즐 ").Append(puzzles.Count).Append("개\n");
            for (int i = 0; i < puzzles.Count; i++)
            {
                var puzzle = puzzles[i];
                if (puzzle == null) continue;

                string id = puzzle.Definition != null ? puzzle.Definition.PuzzleId : "(정의 없음)";
                _sb.Append("  ").Append(puzzle.name)
                   .Append(" [").Append(id).Append("] : ").Append(puzzle.State)
                   .Append('\n');
            }
        }

        private void AppendDoorLines(RoomFlowManager flow)
        {
            var room = flow != null ? flow.CurrentRoom : null;
            var doors = room != null ? room.Doors : null;

            if (doors == null || doors.Count == 0)
            {
                _sb.Append("문: 없음 (마지막 방이거나 미배선)\n");
                return;
            }

            _sb.Append("문 ").Append(doors.Count).Append("개\n");
            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                if (door == null) continue;

                _sb.Append("  ").Append(door.name)
                   .Append(door.IsOpen ? " [열림]" : " [잠김]")
                   .Append(" → 방 ")
                   .Append(door.TargetRoomId >= 0 ? (door.TargetRoomId + 1).ToString() : "없음")
                   .Append('\n');
            }
        }

        // ── 그리기 ────────────────────────────────────────────────
        private void OnGUI()
        {
            if (!_visible) return;

            // 반드시 Repaint에서만 그린다 — 레이아웃/이벤트 패스에서 그리면 프레임당 여러 번 호출된다.
            if (Event.current.type != EventType.Repaint) return;

            EnsureStyles();

            _boxStyle.Draw(area, GUIContent.none, false, false, false, false);

            var textRect = new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f);
            _textStyle.Draw(textRect, _cachedText, false, false, false, false);
        }

        /// <summary>GUIStyle은 OnGUI 안에서만 GUI.skin에 접근할 수 있다. 최초 1회만 만든다.</summary>
        private void EnsureStyles()
        {
            if (_textStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box);

            _textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                richText = false,
            };
            _textStyle.normal.textColor = Color.white;
        }
    }
}
