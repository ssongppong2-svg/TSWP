// 근거: 맵 시스템.md / 방 시스템.md — 방 그래프(논리)와 씬 오브젝트(물리) 분리,
//   RoomManager가 현재 방 활성화/통로 전환 담당 (스펙 unityNotes ③·⑨).
//   클리어 조건 2종: 전멸형(KillAllEnemies — 일반 전투/엘리트/보스), 목표형(ObjectiveComplete — 퍼즐/기믹 연습).
// UI 통지는 전부 TSWP.Core.GameEvents 경유 (RoomEntered/Cleared/Discovered/SecretRoomFound).
// SYNC: 호스트 권위, 추후 NGO NetworkVariable — 현재 방 id, 탐험/클리어 상태, 클리어 판정.
using UnityEngine;
using TSWP.Core;
using TSWP.Combat;

namespace TSWP.Map
{
    /// <summary>
    /// 현재 스테이지 맵의 방 상태 머신.
    /// 진입 → (조건 구성/적 등록) → 클리어 → 통로 이동의 흐름을 소유한다.
    /// 스테이지 전환(보스 처치 → 다음 스테이지)은 Core.RunManager 소관 — 여기서는 방 단위만 다룬다.
    /// </summary>
    public sealed class RoomManager : MonoBehaviour
    {
        public static RoomManager Instance { get; private set; }

        [Header("이동 규칙")]
        [Tooltip("전투 방(전멸형)을 클리어하기 전 출구 봉쇄 여부.")]
        // NOTE(기획 확인 필요): 문서는 '자유 탐험'을 강조하지만 전투 방 이탈 허용 여부는 미정 — 기본 봉쇄.
        [SerializeField] private bool blockExitUntilCleared = true;

        /// <summary>현재 스테이지 그래프. SetGraph로 주입 (시드 재생성 — 멀티 동기화).</summary>
        public MapGraph Graph { get; private set; }
        /// <summary>현재 방 (없으면 null). SYNC: 호스트 권위.</summary>
        public RoomNode CurrentRoom { get; private set; }
        /// <summary>현재 방의 클리어 조건 상태.</summary>
        public RoomClearCondition CurrentCondition { get; private set; }
        /// <summary>미니맵 표시 데이터 — UI(MinimapModel)는 이 상태만 조회한다.</summary>
        public MinimapState Minimap { get; } = new MinimapState();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.PingRaised += OnPingRaised;
            GameEvents.PuzzleSolved += OnPuzzleSolved;
        }

        private void OnDisable()
        {
            GameEvents.PingRaised -= OnPingRaised;
            GameEvents.PuzzleSolved -= OnPuzzleSolved;
        }

        // ── 그래프 주입/스테이지 시작 ─────────────────────────────
        /// <summary>
        /// 새 스테이지 그래프 설정 후 시작 방으로 진입.
        /// 호출측(게임 흐름): RunManager.Seed + StagePlan → MapGenerator.Generate → SetGraph.
        /// </summary>
        public void SetGraph(MapGraph graph)
        {
            Graph = graph;
            CurrentRoom = null;
            CurrentCondition = null;
            Minimap.BuildFromGraph(graph);
            if (graph?.StartRoom != null)
                EnterRoom(graph.StartRoom);
        }

        // ── 방 이동 ───────────────────────────────────────────────
        /// <summary>
        /// 통로를 통한 방 이동 시도. 실패 사유: 미연결/출구 봉쇄/미발견 비밀방.
        /// SYNC: 이동 판정은 호스트 단일 지점 — 추후 클라이언트는 요청만 보낸다.
        /// </summary>
        public bool TryMoveToRoom(int roomId)
        {
            if (Graph == null || CurrentRoom == null) return false;
            var target = Graph.GetRoom(roomId);
            if (target == null) return false;
            if (!CurrentRoom.ConnectedRooms.Contains(target)) return false; // 통로로 연결된 방만 이동 가능

            // 비밀방은 발견(입구 노출) 전 진입 불가 — 발견 판정은 DiscoverSecretRoom이 선행.
            if (target.IsSecret && !target.IsDiscovered) return false;

            // 전투 방 출구 봉쇄 (클리어 전) — 이미 클리어한 방 재통과는 자유.
            if (blockExitUntilCleared && !CurrentRoom.IsCleared &&
                CurrentCondition != null && CurrentCondition.clearType == RoomClearType.KillAllEnemies)
                return false;

            EnterRoom(target);
            return true;
        }

        /// <summary>방 진입 처리 — 탐험 기록/미니맵/클리어 조건 구성/이벤트 발행.</summary>
        private void EnterRoom(RoomNode room)
        {
            DeactivateCurrentRoom();
            CurrentRoom = room;

            bool firstVisit = !room.IsExplored;
            room.IsExplored = true; // 미니맵: 탐험한 지역만 표시 (전장의 안개)
            Minimap.MarkExplored(room.RoomId);

            // 클리어 조건 구성 — 이미 클리어한 방 재방문은 조건 없음.
            CurrentCondition = room.IsCleared ? new RoomClearCondition() : BuildClearCondition(room);

            if (firstVisit)
                GameEvents.RaiseRoomDiscovered(room.RoomId);
            GameEvents.RaiseRoomEntered(room.RoomId);

            ActivateRoom(room);

            // 조건 없는 방(시작/휴식/상점/이벤트/비밀방)은 진입 즉시 클리어 처리.
            if (!room.IsCleared && CurrentCondition.clearType == RoomClearType.None)
                HandleRoomCleared();
        }

        /// <summary>방 종류 → 클리어 조건 매핑.</summary>
        private static RoomClearCondition BuildClearCondition(RoomNode room)
        {
            switch (room.RoomType)
            {
                case RoomType.NormalCombat:
                case RoomType.Elite:
                case RoomType.Boss:
                    // 전멸형 — 적 등록은 SpawnManager가 RegisterEnemy로 수행.
                    return new RoomClearCondition { clearType = RoomClearType.KillAllEnemies };

                case RoomType.Puzzle:
                    // 목표형 — 퍼즐 성공 시 클리어. 실패해도 게임 계속 진행(페널티 없음, 출구 봉쇄 없음).
                    return new RoomClearCondition
                    {
                        clearType = RoomClearType.ObjectiveComplete,
                        objectiveId = room.PuzzleId,
                    };

                case RoomType.BossPractice:
                    // 목표형 — 다음 보스 핵심 기믹 체험 완료 (Bosses 시스템이 NotifyObjectiveComplete 호출).
                    return new RoomClearCondition
                    {
                        clearType = RoomClearType.ObjectiveComplete,
                        objectiveId = "boss_practice_" + room.RoomId,
                    };

                default:
                    // 시작/휴식/상점/이벤트/비밀방 — 진행 조건 없음 (이벤트는 무시·참여 선택 가능).
                    return new RoomClearCondition();
            }
        }

        // ── 전멸형: 적 등록/처치 집계 ─────────────────────────────
        /// <summary>
        /// 현재 방의 전멸형 카운트에 적 등록 (Enemies.SpawnManager 호출 지점).
        /// CombatEntity.Died 구독으로 처치를 집계한다.
        /// </summary>
        public void RegisterEnemy(CombatEntity enemy)
        {
            if (enemy == null || CurrentCondition == null) return;
            if (CurrentCondition.clearType != RoomClearType.KillAllEnemies) return;

            CurrentCondition.RemainingEnemies++;
            enemy.Died += OnEnemyDied;
        }

        /// <summary>스폰 등록 종료 통지 — 등록 도중 조기 클리어 방지용. SpawnManager가 스폰 완료 후 호출.</summary>
        public void FinishEnemyRegistration()
        {
            if (CurrentCondition == null) return;
            CurrentCondition.SpawnFinished = true;
            TryClear();
        }

        private void OnEnemyDied(CombatEntity enemy)
        {
            enemy.Died -= OnEnemyDied;
            if (CurrentCondition == null) return;
            CurrentCondition.RemainingEnemies--;
            TryClear();
        }

        // ── 목표형: 목표 달성 통지 ────────────────────────────────
        /// <summary>
        /// 목표형 클리어 통지 (Puzzles/Bosses 시스템 호출 지점).
        /// objectiveId가 비어 있으면 id 대조 없이 수용한다 (콘텐츠 굴림 전 뼈대 단계 허용).
        /// </summary>
        public void NotifyObjectiveComplete(string objectiveId)
        {
            if (CurrentCondition == null) return;
            if (CurrentCondition.clearType != RoomClearType.ObjectiveComplete) return;
            if (!string.IsNullOrEmpty(CurrentCondition.objectiveId) &&
                CurrentCondition.objectiveId != objectiveId) return;

            CurrentCondition.ObjectiveDone = true;
            TryClear();
        }

        /// <summary>퍼즐 성공 이벤트 → 현재 방이 해당 퍼즐의 목표형이면 클리어 연결.</summary>
        private void OnPuzzleSolved(string puzzleId) => NotifyObjectiveComplete(puzzleId);

        // ── 클리어 판정 ───────────────────────────────────────────
        private void TryClear()
        {
            if (CurrentRoom == null || CurrentRoom.IsCleared) return;
            if (CurrentCondition == null || !CurrentCondition.IsSatisfied) return;
            HandleRoomCleared();
        }

        /// <summary>클리어 확정 — 상태 기록 + GameEvents 발행. SYNC: 호스트 단일 판정 지점.</summary>
        private void HandleRoomCleared()
        {
            if (CurrentRoom == null || CurrentRoom.IsCleared) return;
            CurrentRoom.IsCleared = true;
            GameEvents.RaiseRoomCleared(CurrentRoom.RoomId);
            // 보스 방 클리어 → 보상/스테이지 전환은 Bosses(BossDefeated 발행)·Core.RunManager 소관.
        }

        // ── 비밀방 발견 ───────────────────────────────────────────
        /// <summary>
        /// 비밀방 발견 처리 (숨겨진 입구 상호작용/벽 파괴 등에서 호출).
        /// 발견 시점부터 미니맵 노출 + 진입 가능. 보상(희귀 아이템/특별 이벤트)은 Items/이벤트 시스템 소관.
        /// </summary>
        public void DiscoverSecretRoom(int roomId)
        {
            var room = Graph?.GetRoom(roomId);
            if (room == null || !room.IsSecret || room.IsDiscovered) return;

            room.IsDiscovered = true;
            Minimap.MarkSecretDiscovered(roomId);
            GameEvents.RaiseSecretRoomFound(roomId);
        }

        // ── 씬 오브젝트 활성화/비활성화 (논리·물리 분리) ───────────
        private void ActivateRoom(RoomNode room)
        {
            // TODO: BiomeDefinition.roomPrefabs에서 ContentSeed 기반 결정론 선택 → 인스턴스화/활성화.
            // TODO: Tilemap(2D URP) 배치, 구조물(StructureDefinition)·함정(Combat.EnvironmentHazard) 스폰.
            // TODO: 전투 방이면 Enemies.SpawnManager에 스폰 요청 → RegisterEnemy/FinishEnemyRegistration 흐름.
            // TODO(연출): 방 전환 페이드/카메라 이동.
        }

        private void DeactivateCurrentRoom()
        {
            if (CurrentRoom == null) return;
            // TODO: 이전 방 씬 오브젝트 비활성화/풀 반환 (프리팹 또는 Addressables — 스펙 unityNotes ③).
        }

        // ── 미니맵 핑 위임 ────────────────────────────────────────
        private void OnPingRaised(int senderPlayerId, PingType type, Vector2 worldPos)
            => Minimap.AddPing(senderPlayerId, type, worldPos, Time.time);
    }
}
