// 근거: 맵 시스템.md / 방 시스템.md — 방 그래프(논리)와 씬 오브젝트(물리)의 분리.
//   RoomNode/RoomManager가 '논리', 이 컴포넌트가 그 방의 '물리'다.
//   흐름: 진입 → (적 생성 / 퍼즐 시작 / 보스 시작) → 클리어 감시 → 문 개방 → 보상.
// 클리어 판정은 RoomManager(RoomClearCondition)가 단일 지점으로 소유한다 — 여기서 재구현하지 않는다.
// 연출 컴포넌트(퍼즐/보스/보상 매니저)가 없어도 방 진행이 막히면 안 된다 → 전부 null 체크 후 조용히 생략.
// SYNC: 호스트 권위 — 스폰/보상 지급은 호스트 단일 지점.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Bosses;
using TSWP.Combat;
using TSWP.Core;
using TSWP.Enemies;
using TSWP.Items;
using TSWP.Player;
using TSWP.Puzzles;

namespace TSWP.Map
{
    /// <summary>
    /// 씬에 배치되는 실제 방 1개. 방 종류/적 생성 지점/출구/클리어 여부를 가진다.
    /// RoomFlowManager가 Bind → Activate를 호출하고, 클리어 통지(NotifyCleared)를 받는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomInstance : MonoBehaviour
    {
        [Header("방 데이터")]
        [Tooltip("이 방의 구성(적/클리어 조건/보상). 비어 있으면 fallbackRoomType만 사용하는 빈 방이 된다.")]
        [SerializeField] private RoomDefinition definition;

        [Tooltip("definition이 없을 때 사용할 방 종류.")]
        [SerializeField] private RoomType fallbackRoomType = RoomType.NormalCombat;

        [Header("배치 마커")]
        [Tooltip("플레이어 시작 위치. 비우면 플레이어를 옮기지 않는다.")]
        [SerializeField] private Transform playerSpawnPoint;

        [Tooltip("이 방 전용 적 생성 지점. 비어 있으면 자식 SpawnPoint를 자동 수집한다.")]
        [SerializeField] private List<SpawnPoint> enemySpawnPoints = new List<SpawnPoint>();

        [Tooltip("이 방의 출구. 비어 있으면 자식 RoomDoor를 자동 수집한다.")]
        [SerializeField] private List<RoomDoor> doors = new List<RoomDoor>();

        [Header("진입 감지")]
        [Tooltip("체크하면 트리거에 플레이어가 닿을 때 콘텐츠(적/퍼즐/보스)가 시작된다. " +
                 "끄면 방 활성화 즉시 시작한다(결정론적 — 기본 권장).")]
        [SerializeField] private bool startContentOnPlayerTrigger = false;

        [Header("스폰 규칙")]
        [Tooltip("씬 전역 Enemies.SpawnManager를 사용한다. 방을 씬에 직접 배치하고 " +
                 "SpawnPoint들이 SpawnManager의 자식일 때만 켠다. 방 프리팹을 런타임 생성하면 반드시 꺼야 한다.")]
        [SerializeField] private bool useGlobalSpawnManager = false;

        [Tooltip("플레이어로부터 이 거리 안에는 스폰하지 않는다 (갑툭튀 금지 — 적 시스템.md).")]
        [SerializeField, Min(0f)] private float minSpawnDistanceFromPlayer = 6f; // TODO(밸런스): 문서 미정

        [Tooltip("화면 밖 판정 카메라. 비우면 Camera.main.")]
        [SerializeField] private Camera viewCamera;

        // ── 런타임 상태 ───────────────────────────────────────────
        private readonly List<EnemyController> _spawnedEnemies = new List<EnemyController>();
        private RoomNode _node;
        private bool _initialized;
        private bool _contentStarted;

        /// <summary>이 방에 배정된 방 id (-1 = 미배정).</summary>
        public int RoomId { get; private set; } = -1;

        /// <summary>방 번호 (UI 표시용 — 1부터 시작).</summary>
        public int RoomNumber => RoomId + 1;

        public RoomDefinition Definition => definition;
        public RoomType RoomType => definition != null ? definition.RoomType : fallbackRoomType;
        public Transform PlayerSpawnPoint => playerSpawnPoint;
        public IReadOnlyList<RoomDoor> Doors => doors;
        public IReadOnlyList<SpawnPoint> EnemySpawnPoints => enemySpawnPoints;
        public IReadOnlyList<EnemyController> SpawnedEnemies => _spawnedEnemies;

        /// <summary>현재 활성 방인지.</summary>
        public bool IsActive { get; private set; }

        /// <summary>클리어 여부 — 논리 진실은 RoomNode.IsCleared, 이 값은 그 반영이다.</summary>
        public bool IsCleared { get; private set; }

        /// <summary>플레이어 진입(콘텐츠 시작) 통지.</summary>
        public event Action<RoomInstance> PlayerEntered;

        /// <summary>클리어 통지 (문 개방·보상 지급 이후).</summary>
        public event Action<RoomInstance> Cleared;

        private void Awake() => EnsureInitialized();

        /// <summary>자식 마커 자동 수집. 비활성 상태에서 호출될 수 있으므로 Awake에 의존하지 않는다.</summary>
        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            if (enemySpawnPoints.Count == 0) GetComponentsInChildren(true, enemySpawnPoints);
            if (doors.Count == 0) GetComponentsInChildren(true, doors);
        }

        // ── 배선 ─────────────────────────────────────────────────
        /// <summary>방 id와 논리 노드를 연결한다 (RoomFlowManager가 호출).</summary>
        public void Bind(int roomId, RoomNode node)
        {
            EnsureInitialized();
            RoomId = roomId;
            _node = node;
            IsCleared = node != null && node.IsCleared;
        }

        // ── 활성화 ────────────────────────────────────────────────
        /// <summary>
        /// 방 활성화. 오브젝트를 켜고 문을 잠근 뒤(클리어된 방이면 열고) 콘텐츠 시작 조건을 건다.
        /// RoomManager.EnterRoom → GameEvents.RoomEntered 흐름 안에서 동기 호출되는 것을 전제로 한다.
        /// </summary>
        public void Activate()
        {
            EnsureInitialized();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            IsActive = true;
            _contentStarted = false;
            _spawnedEnemies.Clear();

            ApplyClearConditionOverride();

            // 재방문(이미 클리어) → 즉시 개방, 콘텐츠 재시작 없음.
            if (IsCleared)
            {
                SetDoorsOpen(true);
                return;
            }

            SetDoorsOpen(false);

            if (!startContentOnPlayerTrigger)
                StartContent();
        }

        /// <summary>방 비활성화 — 남은 적을 정리하고 오브젝트를 끈다.</summary>
        public void Deactivate()
        {
            IsActive = false;

            // 이전 방의 적이 따라다니지 않도록 정리한다(풀링 도입 전 임시 — 스펙 unityNotes ③).
            for (int i = 0; i < _spawnedEnemies.Count; i++)
            {
                var enemy = _spawnedEnemies[i];
                if (enemy != null) Destroy(enemy.gameObject);
            }
            _spawnedEnemies.Clear();

            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary>
        /// RoomDefinition이 클리어 조건을 덮어쓰도록 지정했다면 RoomManager가 구성한 조건에 반영한다.
        /// RoomManager는 수정하지 않고, 공개된 CurrentCondition 상태만 갱신한다.
        /// </summary>
        private void ApplyClearConditionOverride()
        {
            if (definition == null) return;
            if (!definition.TryResolveClearType(out RoomClearType clearType)) return;

            var manager = RoomManager.Instance;
            var condition = manager != null ? manager.CurrentCondition : null;
            if (condition == null) return;
            if (manager.CurrentRoom == null || manager.CurrentRoom.RoomId != RoomId) return; // 현재 방일 때만

            condition.clearType = clearType;
            condition.objectiveId = definition.ObjectiveId;
        }

        // ── 진입 감지 ─────────────────────────────────────────────
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!startContentOnPlayerTrigger || !IsActive || _contentStarted) return;
            if (other.GetComponentInParent<PlayerController>() == null) return;
            StartContent();
        }

        /// <summary>콘텐츠 시작 (외부 강제 호출도 허용 — 튜토리얼/디버그).</summary>
        public void StartContent()
        {
            if (_contentStarted || IsCleared) return;
            _contentStarted = true;

            PlayerEntered?.Invoke(this);

            // 순서가 중요하다: 보스를 '먼저' 전멸형 카운트에 등록해야
            // 조우가 비었을 때 FinishEnemyRegistration이 보스 등록 전에 방을 클리어시키지 않는다.
            var boss = GetComponentInChildren<BossController>(true);
            if (boss != null)
                RoomEncounterSpawner.RegisterForClear(RoomManager.Instance, boss.Entity);

            SpawnEnemies();   // 내부에서 반드시 FinishEnemyRegistration까지 수행한다
            BeginPuzzle();

            if (boss != null) boss.BeginFight();
        }

        // ── 콘텐츠: 적 생성 ───────────────────────────────────────
        private void SpawnEnemies()
        {
            var encounter = definition != null ? definition.Encounter : null;
            Difficulty difficulty = GameFlowManager.Instance != null
                ? GameFlowManager.Instance.SelectedDifficulty
                : Difficulty.Human;

            if (useGlobalSpawnManager && SpawnManager.Instance != null)
            {
                // SpawnEncounter 내부에서 RegisterEnemy + FinishEnemyRegistration까지 처리한다.
                // 단 composition이 null이면 즉시 return하므로 등록 종료 통지를 여기서 보완한다
                // (없으면 SpawnFinished가 false로 남아 방이 영영 열리지 않는다).
                if (encounter != null)
                {
                    SpawnManager.Instance.SpawnEncounter(encounter, difficulty);
                }
                else
                {
                    // `?.`는 Unity의 == 오버로드를 우회해 파괴된 객체를 통과시킨다 — 명시적 null 검사를 쓴다.
                    var manager = RoomManager.Instance;
                    if (manager != null) manager.FinishEnemyRegistration();
                }
                return;
            }

            RoomEncounterSpawner.SpawnEncounter(
                encounter,
                enemySpawnPoints,
                difficulty,
                CreateRng(0x52_4F_4F_4D), // "ROOM" — 다른 소비자와 난수 수열이 겹치지 않도록 분리
                minSpawnDistanceFromPlayer,
                viewCamera,
                _spawnedEnemies);
        }

        // ── 콘텐츠: 퍼즐 ──────────────────────────────────────────
        private void BeginPuzzle()
        {
            var puzzle = GetComponentInChildren<PuzzleController>(true);
            if (puzzle == null) return; // 퍼즐이 없는 방 — 조용히 생략
            puzzle.Begin();
            // 성공 통지는 PuzzleController.Solve → GameEvents.PuzzleSolved →
            // RoomManager.NotifyObjectiveComplete로 이미 연결되어 있다.
        }

        // 보스는 SpawnManager를 거치지 않으므로 StartContent가 전멸형 카운트에 직접 등록한다.
        // 이게 없으면 보스를 처치해도 SpawnFinished/RemainingEnemies가 맞지 않아 방이 클리어되지 않는다.

        // ── 클리어 ────────────────────────────────────────────────
        /// <summary>
        /// 클리어 통지 (RoomFlowManager가 GameEvents.RoomCleared를 받아 호출).
        /// 문을 열고 보상을 1회 지급한다.
        /// </summary>
        public void NotifyCleared()
        {
            if (IsCleared) return;
            IsCleared = true;

            SetDoorsOpen(true);
            GrantReward();
            Cleared?.Invoke(this);
        }

        private void SetDoorsOpen(bool open)
        {
            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                if (door != null) door.SetOpen(open);
            }
        }

        // ── 보상 ─────────────────────────────────────────────────
        private void GrantReward()
        {
            if (definition == null || definition.RewardType == RoomRewardType.None) return;

            Vector2 origin = playerSpawnPoint != null ? (Vector2)playerSpawnPoint.position : (Vector2)transform.position;
            var drops = ItemDropManager.Instance;

            switch (definition.RewardType)
            {
                case RoomRewardType.ItemDrop:
                    if (drops == null) return; // 드롭 매니저가 없는 씬 — 조용히 생략
                    for (int i = 0; i < definition.RewardAmount; i++)
                        drops.SpawnDrop(definition.RewardAcquisition, origin);
                    break;

                case RoomRewardType.BossDrop:
                    if (drops == null) return;
                    drops.SpawnBossDrops(origin); // 보스 드롭 3~4개 규칙은 GameRules가 소유
                    break;

                case RoomRewardType.Gold:
                    GrantGold(definition.RewardAmount);
                    break;

                case RoomRewardType.Heal:
                    HealParty(definition.RewardAmount);
                    break;
            }
        }

        private static void GrantGold(int amount)
        {
            if (amount <= 0) return;
            var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                GameEvents.RaiseGoldGained(players[i].PlayerId, amount);
        }

        private static void HealParty(int amount)
        {
            if (amount <= 0) return;
            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == null || entity.IsDead) continue;
                if (entity.Team != TeamType.Players) continue;
                entity.Heal(amount);
            }
        }

        // ── 유틸 ─────────────────────────────────────────────────
        /// <summary>방 콘텐츠 시드 기반 난수. 같은 시드면 전 클라이언트 동일 결과 (SYNC).</summary>
        private System.Random CreateRng(int salt)
        {
            int seed = _node != null ? _node.ContentSeed : RoomId;
            var run = RunManager.Instance;
            if (_node == null && run != null) seed ^= run.Seed;
            return new System.Random(seed ^ salt);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (playerSpawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(playerSpawnPoint.position, 0.5f);
            }
            Gizmos.color = Color.magenta;
            for (int i = 0; i < doors.Count; i++)
                if (doors[i] != null) Gizmos.DrawLine(transform.position, doors[i].transform.position);
        }
#endif
    }
}
