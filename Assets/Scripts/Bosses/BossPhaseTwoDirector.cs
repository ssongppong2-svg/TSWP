// 근거: 보스 시스템.md — 기억 요소 '반전'. 보스 01 해치 퀸: 퀸이 죽으면 끝이 아니라
//       거미 무리(Spider Swarm)가 쏟아지고 레버 퍼즐이 열린다.
// ARCHITECTURE.md §4의 BossFightPhase는 7값 고정이므로 enum을 건드리지 않는다.
//   대신 GameEvents.BossPhaseChanged를 구독해 '처치 연출 진입' 시점에 2막을 연출하는
//   완전 가산형 컴포넌트로 만든다 → BossController도 enum도 수정하지 않는다.
// 보스 01 전용이 아니다 — 감시할 bossId와 소환/퍼즐 참조만 바꾸면 어떤 보스든 2막을 가질 수 있다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;
using TSWP.Enemies;
using TSWP.Puzzles;

namespace TSWP.Bosses
{
    /// <summary>
    /// 보스 처치 직후 '2막'을 여는 연출 감독. 적 무리를 소환하고 후속 퍼즐을 가동한다.
    /// 2막이 열려 있는 동안 BossController는 보상 단계(=GameEvents.BossDefeated)를 보류하므로,
    /// 실질적으로 "체력 0 → 2막 → 2막 클리어 → 진짜 사망" 흐름이 된다.
    /// SYNC: 소환·퍼즐 개시는 호스트 권위.
    /// </summary>
    public sealed class BossPhaseTwoDirector : MonoBehaviour
    {
        [Header("발동 조건")]
        [Tooltip("감시할 보스 id (BossData.bossId). 비우면 어떤 보스든 반응한다.")]
        [SerializeField] private string watchBossId;

        [Tooltip("이 페이즈에 진입하면 2막을 연다. 기본 DeathCinematic —\n" +
                 "Reward(=BossDefeated)까지 기다리면 그 사이에 방이 먼저 클리어될 수 있다.")]
        [SerializeField] private BossFightPhase triggerPhase = BossFightPhase.DeathCinematic;

        [Tooltip("발동까지 지연 시간(초). 0이면 즉시.\n" +
                 "주의: 지연을 주면 그 사이 보스 방이 '전멸'로 판정돼 먼저 클리어될 수 있다.")]
        [SerializeField, Min(0f)] private float delaySeconds = 0f;

        [Header("① 적 무리 소환 (Spider Swarm)")]
        [Tooltip("소환 지점들. 비어 있으면 이 오브젝트 위치에서 소환한다.")]
        [SerializeField] private Transform[] swarmSpawnPoints = Array.Empty<Transform>();

        [Tooltip("소환할 적 (예: 거미). 지점 개수만큼 순환하며 swarmCount만큼 소환한다.")]
        [SerializeField] private EnemyData swarmEnemy;

        [Tooltip("소환할 적 마리 수.")]
        [SerializeField, Min(0)] private int swarmCount = 6; // TODO(밸런스): 문서 미정

        [Tooltip("swarmEnemy 대신 조합으로 소환하고 싶을 때 사용(있으면 이쪽이 우선).")]
        [SerializeField] private EncounterComposition swarmComposition;

        [Header("② 후속 퍼즐 (Lever Puzzle)")]
        [Tooltip("2막에서 가동할 퍼즐. Puzzles 담당이 만드는 레버 퍼즐을 그대로 꽂으면 된다.")]
        [SerializeField] private PuzzleController phaseTwoPuzzle;

        [Tooltip("2막 시작 시 켤 오브젝트들 (레버·문·발판 등). 씬에 미리 배치하고 꺼 둔다.")]
        [SerializeField] private GameObject[] enableOnPhaseTwo = Array.Empty<GameObject>();

        [Header("③ 2막 완료 조건 — 보스의 '진짜 죽음'을 여기까지 미룬다")]
        [Tooltip("소환한 적을 전부 처치해야 2막이 끝난다.\n" +
                 "EncounterComposition으로 소환하면 개체를 추적할 수 없어 이 조건은 자동으로 충족 처리된다.")]
        [SerializeField] private bool requireSwarmCleared = true;

        [Tooltip("후속 퍼즐을 해결해야 2막이 끝난다. 퍼즐이 없으면 자동으로 충족 처리된다.")]
        [SerializeField] private bool requirePuzzleSolved = true;

        [Header("연출")]
        [SerializeField] private string phaseTwoVfxId;

        private bool _armed;   // 트리거를 받았고 지연 대기 중
        private bool _fired;   // 이미 2막을 열었다 (1회성)
        private bool _completed;
        private float _timer;

        // 2막 클리어 판정 상태
        private readonly List<CombatEntity> _swarm = new List<CombatEntity>();
        private int _swarmAlive;
        private bool _swarmCleared;
        private bool _puzzleCleared;
        private bool _puzzleSubscribed;

        /// <summary>2막이 이미 시작됐는지 (다른 시스템의 조회용).</summary>
        public bool HasFired => _fired;

        /// <summary>트리거를 받아 대기 중이거나 이미 시작됐는지 — BossController가 '2막이 있는 보스인지' 판정에 쓴다.</summary>
        public bool IsActive => _armed || _fired;

        /// <summary>2막이 끝났는지 (소환 정리 + 퍼즐 해결). 여기가 true가 되어야 보스가 진짜로 죽는다.</summary>
        public bool IsComplete => _completed;

        /// <summary>감시 대상 보스 id (비어 있으면 아무 보스나).</summary>
        public string WatchBossId => watchBossId;

        /// <summary>2막을 여는 트리거 페이즈.</summary>
        public BossFightPhase TriggerPhase => triggerPhase;

        /// <summary>2막에 남은 적 수 (UI/디버그 조회용).</summary>
        public int RemainingSwarm => _swarmAlive;

        /// <summary>2막이 시작될 때 통지 (방/UI가 구독할 수 있는 확장 지점).</summary>
        public event Action<BossPhaseTwoDirector> PhaseTwoStarted;

        /// <summary>2막이 끝났을 때 통지 — BossController가 구독해 처치 연출을 재개한다.</summary>
        public event Action<BossPhaseTwoDirector> PhaseTwoCompleted;

        private void OnEnable()
        {
            GameEvents.BossPhaseChanged += OnBossPhaseChanged;
        }

        private void OnDisable()
        {
            GameEvents.BossPhaseChanged -= OnBossPhaseChanged;

            // 소환물/퍼즐 구독을 남겨 두면 파괴된 객체의 이벤트를 잡으려다 예외가 난다.
            for (int i = 0; i < _swarm.Count; i++)
                if (_swarm[i] != null) _swarm[i].Died -= OnSwarmMemberDied;
            _swarm.Clear();

            if (_puzzleSubscribed && phaseTwoPuzzle != null)
            {
                phaseTwoPuzzle.Solved -= OnPhaseTwoPuzzleSolved;
                _puzzleSubscribed = false;
            }
        }

        private void OnBossPhaseChanged(string bossId, int phase)
        {
            if (_fired || _armed) return;
            if (!string.IsNullOrEmpty(watchBossId) && bossId != watchBossId) return;
            if (phase != (int)triggerPhase) return;

            if (delaySeconds <= 0f)
            {
                Fire();
                return;
            }

            _armed = true;
            _timer = 0f;
        }

        private void Update()
        {
            if (!_armed || _fired) return;

            _timer += Time.deltaTime;
            if (_timer >= delaySeconds) Fire();
        }

        /// <summary>외부에서 강제로 2막을 여는 진입점 (디버그/치트/대체 트리거용).</summary>
        public void Fire()
        {
            if (_fired) return;
            _fired = true;
            _armed = false;

            SpawnSwarm();
            BeginPuzzle();

            if (!string.IsNullOrEmpty(phaseTwoVfxId))
                Art.VfxSpawner.Instance?.Play(phaseTwoVfxId, transform.position);

            PhaseTwoStarted?.Invoke(this);

            // 소환도 퍼즐도 없는(또는 이미 해결된) 감독은 즉시 완료 처리한다 —
            // 그래야 미완성 배선이 보스전을 영구히 붙잡아 두지 않는다.
            _swarmCleared = !requireSwarmCleared || _swarmAlive <= 0;
            _puzzleCleared = !requirePuzzleSolved || phaseTwoPuzzle == null ||
                             phaseTwoPuzzle.State == PuzzleState.Solved;
            EvaluateCompletion();
        }

        // ── 2막 완료 판정 ─────────────────────────────────────────
        private void OnSwarmMemberDied(CombatEntity entity)
        {
            entity.Died -= OnSwarmMemberDied;
            _swarmAlive--;
            if (_swarmAlive > 0) return;

            _swarmCleared = true;
            EvaluateCompletion();
        }

        private void OnPhaseTwoPuzzleSolved(PuzzleController puzzle)
        {
            _puzzleCleared = true;
            EvaluateCompletion();
        }

        private void EvaluateCompletion()
        {
            if (_completed || !_fired) return;
            if (!_swarmCleared || !_puzzleCleared) return;

            _completed = true;
            PhaseTwoCompleted?.Invoke(this); // BossController가 받아 처치 연출 → 보상 단계로 진행
        }

        // ── ① 적 무리 ─────────────────────────────────────────────
        private void SpawnSwarm()
        {
            var spawner = SpawnManager.Instance;
            if (spawner == null)
            {
                Debug.LogWarning($"[BossPhaseTwoDirector] '{name}': SpawnManager가 없어 2막 소환을 생략했습니다.", this);
                return;
            }

            Difficulty difficulty = GameFlowManager.Instance != null
                ? GameFlowManager.Instance.SelectedDifficulty
                : Difficulty.Human;

            // 조합이 있으면 조합 우선 — SpawnEncounter가 배치 규칙과 등록 종료 통지를 함께 처리한다.
            if (swarmComposition != null)
            {
                spawner.SpawnEncounter(swarmComposition, difficulty);
                return;
            }

            if (swarmEnemy == null || swarmCount <= 0)
            {
                Debug.LogWarning($"[BossPhaseTwoDirector] '{name}': 소환할 적(EnemyData/EncounterComposition)이 없습니다.", this);
                return;
            }

            // 지정 위치 소환 — 보스 방은 '연출상 위치가 정해진' 경우라 SpawnAt이 맞다.
            // SpawnAt이 RoomManager 전멸 카운트에 등록하므로, 거미를 정리해야 방이 클리어된다.
            for (int i = 0; i < swarmCount; i++)
            {
                Vector2 position = ResolveSpawnPosition(i);
                var spawned = spawner.SpawnAt(swarmEnemy, difficulty, position);
                if (spawned == null || spawned.Combat == null) continue;

                // 2막 클리어 판정을 위해 개체를 추적한다 (전부 죽으면 거미 무리 정리 완료).
                _swarm.Add(spawned.Combat);
                _swarmAlive++;
                spawned.Combat.Died += OnSwarmMemberDied;
            }
        }

        private Vector2 ResolveSpawnPosition(int index)
        {
            if (swarmSpawnPoints.Length == 0) return transform.position;

            // 지점을 순환 사용 — 무작위를 쓰지 않아 클라이언트 간 결과가 같다.
            var point = swarmSpawnPoints[index % swarmSpawnPoints.Length];
            return point != null ? (Vector2)point.transform.position : (Vector2)transform.position;
        }

        // ── ② 후속 퍼즐 ───────────────────────────────────────────
        private void BeginPuzzle()
        {
            for (int i = 0; i < enableOnPhaseTwo.Length; i++)
                if (enableOnPhaseTwo[i] != null) enableOnPhaseTwo[i].SetActive(true);

            // 퍼즐이 없어도 2막은 성립한다(거미 무리만으로도 진행 가능) — 조용히 생략한다.
            if (phaseTwoPuzzle == null) return;

            if (!_puzzleSubscribed)
            {
                _puzzleSubscribed = true;
                phaseTwoPuzzle.Solved += OnPhaseTwoPuzzleSolved;
            }

            // 오브젝트를 먼저 켠 뒤 시작해야 퍼즐 요소들이 실제로 조작 가능해진다(위 SetActive 순서 유지).
            phaseTwoPuzzle.Begin();
        }
    }
}
