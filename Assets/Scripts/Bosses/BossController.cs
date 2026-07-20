// 근거: 보스 시스템.md — 전투 흐름 7단계 FSM(등장 연출→일반 패턴→협동 퍼즐→패턴 변화→광폭화→처치 연출→보상),
//       AI(플레이어 위치/체력/행동/퍼즐 진행 고려, 같은 패턴 반복 금지), 광폭화(체력 증가 금지),
//       처치 보상 3~4개 선취득, 30초 법칙(30초 안에 최소 1회 반응할 상황 — 페이싱 워치독).
// 실패 조건(공유 부활 소진 → 게임 오버) 판정은 Core.SharedReviveSystem/게임 흐름 소관 — 여기서 재정의하지 않는다 (ARCHITECTURE.md §5).
// SYNC: 페이즈/패턴 선택/광폭화/보상 드롭은 전부 호스트 권위, 추후 NGO NetworkVariable·RPC.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;

namespace TSWP.Bosses
{
    /// <summary>
    /// 보스전 실행 컨트롤러. BossData(정적 데이터)와 CombatEntity(체력/피격)를 조합해
    /// 7단계 상태 머신(BossFightPhase)을 돌린다. 패턴 선택은 BossPatternSelector(조건 스코어링) 전담.
    /// </summary>
    [RequireComponent(typeof(CombatEntity))]
    public sealed class BossController : MonoBehaviour
    {
        /// <summary>30초 법칙 — 모든 보스전은 30초 안에 최소 한 번 플레이어가 반응할 상황을 만들어야 한다 (보스 시스템.md).</summary>
        public const float ThirtySecondRuleSeconds = 30f;

        [Header("데이터")]
        [SerializeField] private BossData data;

        [Header("연출 시간")]
        [SerializeField] private float introDuration = 3f;          // TODO(밸런스): 문서 미정 — 등장 연출 길이
        [SerializeField] private float deathCinematicDuration = 4f; // TODO(밸런스): 문서 미정 — 처치 연출 길이

        [Header("단계 전이 트리거")]
        [Tooltip("일반 패턴 단계에서 보스 체력이 이 비율 이하가 되면 협동 퍼즐 단계로 진입.")]
        [SerializeField] private float coopPuzzleTriggerHpRatio = 0.7f; // TODO(밸런스): 문서 미정

        [Header("패턴 실행")]
        [Tooltip("패턴 간 기본 간격(초). 난이도·광폭화 속도 배율로 나눠 적용된다.")]
        [SerializeField] private float patternIntervalSeconds = 3f; // TODO(밸런스): 문서 미정

        [Tooltip("패턴 1회의 최대 지속 시간(초). 이 시간을 넘기면 강제 중단한다 — " +
                 "잘못 만든 Runner 하나가 보스전을 영구 정지시키는 것을 막는 안전장치.")]
        [SerializeField] private float maxPatternSeconds = 12f; // TODO(밸런스): 문서 미정

        [Header("보상")]
        [Tooltip("보상 단계에서 방의 목표형 클리어를 통지할지 (전멸형 방이면 아무 일도 일어나지 않는다).")]
        [SerializeField] private bool notifyRoomObjectiveOnReward = true;

        private CombatEntity _entity;
        private Rigidbody2D _body;
        private BossPatternSelector _selector;
        private BossAIContext _context;
        private BossPatternContext _patternContext;
        private CooldownTimer _patternCooldown;
        private readonly List<BossPattern> _candidateBuffer = new();

        // 배열이 아니라 리스트인 이유: BossData의 프리팹으로 런타임에 기믹/퍼즐이 추가될 수 있다.
        private readonly List<IGimmick> _gimmicks = new();
        private readonly List<ICoopPuzzle> _coopPuzzles = new();
        private ICoopPuzzle _activePuzzle;

        // 현재 실행 중인 패턴 (전략 Runner). null이면 다음 패턴을 고를 수 있다.
        private BossPatternRunner _activeRunner;
        private float _runnerElapsed;
        private bool _subscribed;

        // SYNC: 호스트 권위, 추후 NGO NetworkVariable — 현재 페이즈/광폭화 여부.
        private BossFightPhase _phase = BossFightPhase.Intro;
        private bool _started;
        private bool _isEnraged;
        private bool _puzzlePhaseDone;
        private float _phaseTimer;
        private float _secondsSinceLastBeat; // 30초 법칙 워치독 타이머
        private string _lastExecutedPatternId;

        private Difficulty _difficulty = Difficulty.Human; // SYNC: 방장 선택 난이도, 호스트 권위
        private float _attackMultiplier = 1f;
        private float _patternSpeedMultiplier = 1f;

        public BossData Data => data;
        public BossFightPhase Phase => _phase;
        public bool IsEnraged => _isEnraged;
        public CombatEntity Entity => _entity;

        private float HpRatio => _entity != null && _entity.MaxHp > 0f ? _entity.CurrentHp / _entity.MaxHp : 0f;

        // ── 초기화 ────────────────────────────────────────────────
        private void Awake()
        {
            // BossData는 이 컨트롤러의 모든 동작(패턴·체력·페이즈·이벤트 id)의 근거다.
            // 없는 채로 굴리면 피격·페이즈 전환 등 임의의 시점에 NullReference가 터져 원인을 찾기 어렵다.
            // 시작 시점에 크게 실패시키고 컴포넌트를 꺼서, 나머지 씬은 정상 동작하게 둔다.
            if (data == null)
            {
                Debug.LogError(
                    $"[BossController] '{name}': BossData가 없어 보스를 비활성화합니다. " +
                    "인스펙터에서 data를 지정하거나 씬 빌더가 에셋을 생성하도록 하세요.", this);
                enabled = false;
                return;
            }

            _entity = GetComponent<CombatEntity>();
            _body = GetComponent<Rigidbody2D>(); // 없어도 된다 — 돌진 패턴이 transform 이동으로 폴백한다
            _selector = new BossPatternSelector();
            _context = new BossAIContext();
            _patternCooldown = new CooldownTimer(patternIntervalSeconds);

            _patternContext = new BossPatternContext();
            _patternContext.Bind(this, _entity, _body, _context);

            _entity.SetTeam(TeamType.Enemies);

            // 기믹/협동 퍼즐 플러그인 수집 (보스 프리팹·보스 방 하위 컴포넌트 — 스펙 unityNotes ④).
            // 비활성 오브젝트도 포함한다(기믹은 대개 꺼진 채로 배치된다).
            CollectPluginsFrom(gameObject);
        }

        private void OnEnable()
        {
            // Awake에서 데이터 부재로 비활성화된 경우 _entity가 null이다 — 구독하지 않는다.
            if (data == null || _entity == null) return;

            _subscribed = true;
            _entity.Damaged += OnEntityDamaged;
            _entity.Died += OnEntityDied;
            GameEvents.DifficultyChanged += OnDifficultyChanged;
            GameEvents.SkillUsed += OnPlayerSkillUsed; // 보스 AI '행동' 요소 — 이벤트 기반 수집 (ARCHITECTURE.md §3-8)

            for (int i = 0; i < _gimmicks.Count; i++)
                _gimmicks[i].Completed += OnGimmickCompleted;
            for (int i = 0; i < _coopPuzzles.Count; i++)
            {
                _coopPuzzles[i].Solved += OnPuzzleSolved;
                _coopPuzzles[i].ProgressChanged += OnPuzzleProgressChanged;
            }
        }

        private void OnDisable()
        {
            // 구독한 적이 없으면 해제할 것도 없다 (데이터 부재로 Awake에서 꺼진 경우).
            if (!_subscribed || _entity == null) return;

            _subscribed = false;
            _entity.Damaged -= OnEntityDamaged;
            _entity.Died -= OnEntityDied;
            GameEvents.DifficultyChanged -= OnDifficultyChanged;
            GameEvents.SkillUsed -= OnPlayerSkillUsed;

            for (int i = 0; i < _gimmicks.Count; i++)
                _gimmicks[i].Completed -= OnGimmickCompleted;
            for (int i = 0; i < _coopPuzzles.Count; i++)
            {
                _coopPuzzles[i].Solved -= OnPuzzleSolved;
                _coopPuzzles[i].ProgressChanged -= OnPuzzleProgressChanged;
            }
        }

        // ── 플러그인 등록 ─────────────────────────────────────────
        /// <summary>
        /// 대상 오브젝트 하위의 IGimmick/ICoopPuzzle을 전부 등록한다.
        /// 보스 프리팹 하위 컴포넌트와 BossData의 프리팹 인스턴스 양쪽에서 쓰인다.
        /// </summary>
        private void CollectPluginsFrom(GameObject root)
        {
            if (root == null) return;

            var gimmicks = root.GetComponentsInChildren<IGimmick>(true);
            for (int i = 0; i < gimmicks.Length; i++) RegisterGimmick(gimmicks[i]);

            var puzzles = root.GetComponentsInChildren<ICoopPuzzle>(true);
            for (int i = 0; i < puzzles.Length; i++) RegisterCoopPuzzle(puzzles[i]);
        }

        /// <summary>
        /// 기믹 플러그인을 런타임에 추가한다 (보스 방이 자기 기믹을 스스로 등록하는 경우 등).
        /// 중복 등록은 무시되므로 여러 번 불러도 안전하다.
        /// </summary>
        public void RegisterGimmick(IGimmick gimmick)
        {
            if (gimmick == null || _gimmicks.Contains(gimmick)) return;
            _gimmicks.Add(gimmick);
            if (_subscribed) gimmick.Completed += OnGimmickCompleted;
        }

        /// <summary>협동 퍼즐 플러그인을 런타임에 추가한다. 중복 등록은 무시된다.</summary>
        public void RegisterCoopPuzzle(ICoopPuzzle puzzle)
        {
            if (puzzle == null || _coopPuzzles.Contains(puzzle)) return;
            _coopPuzzles.Add(puzzle);
            if (!_subscribed) return;
            puzzle.Solved += OnPuzzleSolved;
            puzzle.ProgressChanged += OnPuzzleProgressChanged;
        }

        /// <summary>
        /// BossData에 등록된 기믹/퍼즐 프리팹을 보스 하위에 생성해 플러그인으로 편입한다.
        /// 이걸 하지 않으면 GimmickEntry.gimmickPrefab / CoopPuzzleEntry.puzzlePrefab 필드가
        /// 아무도 읽지 않는 죽은 데이터가 된다.
        /// </summary>
        private void InstantiateDataPlugins()
        {
            for (int i = 0; i < data.Gimmicks.Count; i++)
            {
                var prefab = data.Gimmicks[i]?.GimmickPrefab;
                if (prefab == null) continue;
                CollectPluginsFrom(Instantiate(prefab, transform));
            }

            for (int i = 0; i < data.CoopPuzzles.Count; i++)
            {
                var prefab = data.CoopPuzzles[i]?.PuzzlePrefab;
                if (prefab == null) continue;
                CollectPluginsFrom(Instantiate(prefab, transform));
            }
        }

        /// <summary>
        /// 보스전 시작 — 보스 방 진입 시 호출한다. TODO(Map 연동): RoomManager(Boss 방 활성화)가 호출.
        /// 흐름 상태 전환(GameFlowState.BossFight)은 게임 흐름 소유자(RoomManager/GameFlowManager) 소관.
        /// </summary>
        public void BeginFight()
        {
            if (_started || data == null) return;
            _started = true;

            InstantiateDataPlugins();  // BossData의 기믹/퍼즐 프리팹 → 실제 플러그인 인스턴스
            ApplyDifficultyScaling();  // 난이도는 체력/공격력/패턴 속도 3종만 조정 — 기믹 불변
            _selector.ClearHistory();

            EnterPhase(BossFightPhase.Intro);
        }

        /// <summary>약점 직업 여부 (jobId 문자열 비교 — 직업 enum 금지). 약점 보너스는 가산적으로만 —
        /// 해당 직업이 없어도 클리어 가능해야 한다 (보스 시스템.md '약점 직업'). TODO(Jobs 연동): 보너스 적용 지점.</summary>
        public bool IsWeaknessJob(string jobId)
        {
            if (data == null || string.IsNullOrEmpty(jobId)) return false;
            for (int i = 0; i < data.WeaknessJobIds.Count; i++)
                if (data.WeaknessJobIds[i] == jobId) return true;
            return false;
        }

        // ── FSM 갱신 ──────────────────────────────────────────────
        private void Update()
        {
            if (!_started) return;

            float dt = Time.deltaTime;
            _phaseTimer += dt;
            _patternCooldown.Tick(dt);
            if (IsCombatPhase(_phase))
                _secondsSinceLastBeat += dt;

            switch (_phase)
            {
                case BossFightPhase.Intro:
                    // TODO(연출): introCinematicPrefab 재생, BGM 전환(AudioManager), 카메라 연출.
                    if (_phaseTimer >= introDuration)
                        EnterPhase(BossFightPhase.NormalPattern);
                    break;

                case BossFightPhase.NormalPattern:
                    TickCombat(dt);
                    // ③ 협동 퍼즐 단계 진입 트리거 (체력 기반 — 30초 워치독으로도 강제 진입 가능).
                    if (!_puzzlePhaseDone && HpRatio <= coopPuzzleTriggerHpRatio)
                        EnterPhase(BossFightPhase.CoopPuzzle);
                    break;

                case BossFightPhase.CoopPuzzle:
                    // 퍼즐 해결(Solved 이벤트) 대기 — 퍼즐 중 보스는 방해성 보조 패턴만 사용.
                    // TODO(패턴): 퍼즐 방해 전용 패턴 카테고리 후보 축소 (PuzzleInProgress/PuzzleProgressAbove 조건 활용).
                    TickCombat(dt);
                    break;

                case BossFightPhase.PatternChange:
                    TickCombat(dt);
                    // ⑤ 광폭화 진입 (필요 시 — 광폭화 없는 보스는 이 단계가 최종 전투 단계).
                    if (data.HasEnrage && !_isEnraged && HpRatio <= data.Enrage.TriggerHealthRatio)
                        EnterPhase(BossFightPhase.Enrage);
                    break;

                case BossFightPhase.Enrage:
                    TickCombat(dt);
                    break;

                case BossFightPhase.DeathCinematic:
                    // TODO(연출): deathCinematicPrefab 재생 — 스트리머가 리액션할 만한 장면 (테스트 체크리스트).
                    if (_phaseTimer >= deathCinematicDuration)
                        EnterPhase(BossFightPhase.Reward);
                    break;

                case BossFightPhase.Reward:
                    // 보상 드롭 후 대기 — 다음 방 이동은 RunManager(BossDefeated 구독)/RoomManager 소관.
                    break;
            }

            // ── 30초 법칙 워치독 (스펙 unityNotes ⑧) ──
            // 30초간 '반응할 상황'(새 패턴/퍼즐 시작/광폭화/기믹 완료/페이즈 전환)이 없으면 강제로 상황을 만든다.
            if (IsCombatPhase(_phase) && _secondsSinceLastBeat >= ThirtySecondRuleSeconds)
                ForcePacingBeat();
        }

        private static bool IsCombatPhase(BossFightPhase phase) =>
            phase == BossFightPhase.NormalPattern || phase == BossFightPhase.CoopPuzzle ||
            phase == BossFightPhase.PatternChange || phase == BossFightPhase.Enrage;

        // ── 단계 전이 ─────────────────────────────────────────────
        private void EnterPhase(BossFightPhase next)
        {
            _phase = next; // SYNC: 호스트 권위 — 페이즈 전이는 호스트가 결정해 전파
            _phaseTimer = 0f;
            NotifyBeat(); // 페이즈 전환 자체가 '반응할 상황'

            GameEvents.RaiseBossPhaseChanged(data.BossId, (int)next); // UI 보스 연출/페이즈 표시는 GameEvents 경유

            switch (next)
            {
                case BossFightPhase.Intro:
                    GameEvents.RaiseBossAppeared(data.BossId);                 // UI: 보스 등장 배너/보스 체력바 생성
                    GameEvents.RaiseBossHealthChanged(data.BossId, HpRatio);   // 초기 체력바 값
                    break;

                case BossFightPhase.NormalPattern:
                    _patternCooldown.Reset(); // 즉시 첫 패턴 사용 가능
                    break;

                case BossFightPhase.CoopPuzzle:
                    BeginCoopPuzzle();
                    break;

                case BossFightPhase.PatternChange:
                    // TODO(연출): 패턴 변화 연출 (형태 변화/맵 변화 등 — 기억 요소 후보).
                    _selector.ClearHistory(); // 새 국면 — 패턴 이력 리셋
                    break;

                case BossFightPhase.Enrage:
                    ApplyEnrage();
                    break;

                case BossFightPhase.DeathCinematic:
                    CleanupPluginsOnDeath();
                    break;

                case BossFightPhase.Reward:
                    GameEvents.RaiseBossDefeated(data.BossId); // RunManager가 구독 → 클리어 기록·스테이지 전진(다음 방 이동)
                    DropRewards();
                    break;
            }
        }

        // ── 패턴 실행 ─────────────────────────────────────────────
        private void TickCombat(float dt)
        {
            // ① 실행 중인 패턴이 있으면 그것부터 굴린다 — 패턴이 겹쳐 실행되지 않게 한다.
            if (_activeRunner != null)
            {
                _runnerElapsed += dt;
                _activeRunner.Tick(_patternContext, dt);

                // 안전장치: Runner가 끝나지 않는 버그가 있어도 보스전이 멈추지 않게 강제 중단한다.
                if (!_activeRunner.IsFinished && _runnerElapsed >= maxPatternSeconds)
                {
                    Debug.LogWarning($"[BossController] '{name}': 패턴 '{_lastExecutedPatternId}'이 " +
                                     $"{maxPatternSeconds}초를 넘겨 강제 중단했습니다.", this);
                    _activeRunner.Interrupt(_patternContext);
                }

                if (!_activeRunner.IsFinished) return;
                _activeRunner = null;
            }

            // ② 다음 패턴 선택
            if (!_patternCooldown.TryUse()) return;

            UpdateContext();
            var pattern = _selector.Select(BuildCandidates(), _context, _isEnraged);
            if (pattern == null) return; // 유효 후보 없음 — 다음 간격에 재시도 (기본 공격 대체는 TODO)

            ExecutePattern(pattern);

            // 다음 패턴 간격 = 기본 간격 ÷ (난이도 패턴 속도 × 광폭화 공속) — 속도 배율은 간격 축소로 반영.
            float speed = EffectiveGlobalSpeed();
            _patternCooldown.SetDuration(patternIntervalSeconds / Mathf.Max(0.01f, speed));
        }

        /// <summary>현재 상태의 패턴 후보 목록 (기본 패턴 + 광폭화 시 신규 패턴).</summary>
        private IReadOnlyList<BossPattern> BuildCandidates()
        {
            _candidateBuffer.Clear();
            for (int i = 0; i < data.Patterns.Count; i++)
                _candidateBuffer.Add(data.Patterns[i]);
            if (_isEnraged)
            {
                for (int i = 0; i < data.Enrage.NewPatterns.Count; i++)
                    _candidateBuffer.Add(data.Enrage.NewPatterns[i]);
            }
            return _candidateBuffer;
        }

        /// <summary>AI 컨텍스트 갱신 — 보스 AI 4요소(플레이어 위치/체력/행동/퍼즐 진행) + 패턴 이력.</summary>
        private void UpdateContext()
        {
            _context.PlayerPositions.Clear();
            // TODO(최적화): 씬 전수 탐색 대신 플레이어 레지스트리(Player 시스템)로 교체.
            var entities = FindObjectsByType<CombatEntity>(FindObjectsSortMode.None);
            foreach (var e in entities)
            {
                if (e.Team == TeamType.Players && !e.IsDead)
                    _context.PlayerPositions.Add(e.transform.position);
            }

            _context.BossPosition = transform.position;
            _context.BossHealthRatio = HpRatio;
            _context.PuzzleActive = _activePuzzle != null && !_activePuzzle.IsSolved;
            _context.PuzzleProgress = _activePuzzle != null ? _activePuzzle.Progress : (_puzzlePhaseDone ? 1f : 0f);
            _context.RecentPatternIds = _selector.RecentPatternIds;
        }

        private void ExecutePattern(BossPattern pattern)
        {
            bool enhanced = _isEnraged && pattern.EnhancedInEnrage;

            // 최종 수치 조립 — 난이도 공격 배율 × 광폭화 강화 배율 (패턴 자체 수치는 SO 소유).
            float damage = pattern.BaseDamage * _attackMultiplier
                           * (enhanced ? data.Enrage.EnhancedPatternDamageMultiplier : 1f);
            float speed = EffectiveGlobalSpeed()
                          * pattern.GetSpeedMultiplier(_difficulty)
                          * (enhanced ? data.Enrage.EnhancedPatternSpeedMultiplier : 1f);

            // 협동 기믹 패턴이면 기믹 플러그인 작동 (기믹이 스스로 진행하므로 Runner는 없어도 된다).
            if (pattern.Category == BossPatternCategory.CoopGimmick)
                ActivateGimmick(pattern.TargetGimmickId);

            // 실행은 전적으로 전략(BossPatternBehaviour → BossPatternRunner)에 위임한다.
            // 여기에 패턴별 분기(switch)를 넣는 순간 보스가 늘어날 때마다 이 파일을 고쳐야 하므로 금지.
            var runner = pattern.CreateRunner();
            if (runner != null)
            {
                _patternContext.SetExecution(pattern, damage, speed, _isEnraged);
                _activeRunner = runner;
                _runnerElapsed = 0f;
                runner.Begin(_patternContext);
            }
            else if (pattern.Category != BossPatternCategory.CoopGimmick)
            {
                // 데이터 구성 오류 — 선택은 됐지만 실행할 동작이 없다. 전투는 계속 굴러간다.
                Debug.LogWarning($"[BossController] '{name}': 패턴 '{pattern.PatternId}'에 " +
                                 "실행 전략(BossPattern.behaviour)이 없어 아무 동작도 하지 않았습니다.", this);
            }

            // 새로운 패턴 등장은 '반응할 상황' — 직전과 다른 패턴일 때만 비트 갱신 (30초 법칙).
            if (pattern.PatternId != _lastExecutedPatternId)
                NotifyBeat();
            _lastExecutedPatternId = pattern.PatternId;
        }

        /// <summary>난이도 패턴 속도 × 광폭화 공격 속도 (광폭화는 체력을 절대 올리지 않는다).</summary>
        private float EffectiveGlobalSpeed() =>
            _patternSpeedMultiplier * (_isEnraged ? data.Enrage.AttackSpeedMultiplier : 1f);

        // ── 협동 퍼즐 ─────────────────────────────────────────────
        private void BeginCoopPuzzle()
        {
            _activePuzzle = null;
            foreach (var puzzle in _coopPuzzles)
            {
                if (puzzle.IsSolved) continue;
                _activePuzzle = puzzle;
                break;
            }

            if (_activePuzzle == null)
            {
                // 데이터 구성 오류 방어 — OnValidate가 퍼즐 1개 이상을 강제하므로 프리팹 배치 누락 상황.
                Debug.LogWarning($"[BossController] '{name}': ICoopPuzzle 구현이 없어 협동 퍼즐 단계를 건너뜀.", this);
                _puzzlePhaseDone = true;
                EnterPhase(BossFightPhase.PatternChange);
                return;
            }

            // NOTE(기획 확인 필요): 퍼즐형(Puzzle) 유형 보스는 퍼즐 해결 전 피해 불가 —
            //   TODO(Combat 연동): DamageSystem 측 피해 게이트 훅 필요 (CombatEntity 무적은 유한 타이머 전용이라 부적합).
            _activePuzzle.Begin(this);
        }

        private void OnPuzzleSolved(ICoopPuzzle puzzle)
        {
            _puzzlePhaseDone = true;
            NotifyBeat();
            if (_phase == BossFightPhase.CoopPuzzle)
                EnterPhase(BossFightPhase.PatternChange); // ③ → ④ 패턴 변화
        }

        private void OnPuzzleProgressChanged(ICoopPuzzle puzzle, float progress)
        {
            // 퍼즐 진행은 이벤트로만 수신해 AI 컨텍스트에 반영 (Update 폴링 금지 — ARCHITECTURE.md §3-8).
            _context.PuzzleProgress = progress;
        }

        // ── 기믹 ─────────────────────────────────────────────────
        /// <summary>
        /// 기믹 작동. gimmickId를 지정하면 그 기믹을, 비우면 작동 중이 아닌 첫 기믹을 쓴다.
        /// id로 지정할 수 있으므로 보스가 기믹을 여러 개 가져도 패턴 데이터가 대상을 고를 수 있다.
        /// </summary>
        private void ActivateGimmick(string gimmickId)
        {
            IGimmick fallback = null;

            for (int i = 0; i < _gimmicks.Count; i++)
            {
                var gimmick = _gimmicks[i];
                if (gimmick.IsRunning) continue;

                if (!string.IsNullOrEmpty(gimmickId))
                {
                    if (gimmick.GimmickId != gimmickId) continue;
                }
                else if (fallback == null)
                {
                    fallback = gimmick;
                }

                if (string.IsNullOrEmpty(gimmickId)) break;

                gimmick.Activate(this);
                NotifyBeat(); // 기믹 시작도 '반응할 상황'
                return;
            }

            if (fallback == null) return;
            fallback.Activate(this);
            NotifyBeat();
        }

        private void OnGimmickCompleted(IGimmick gimmick) => NotifyBeat();

        // ── 광폭화 ────────────────────────────────────────────────
        private void ApplyEnrage()
        {
            _isEnraged = true; // SYNC: 호스트 권위
            GameEvents.RaiseBossEnraged(data.BossId); // UI: 광폭화 경고 연출

            // 광폭화 = 공속·이속 증가 + 신규 패턴 + 기존 패턴 강화. 체력은 절대 증가시키지 않는다 (보스 시스템.md).
            // 공속은 EffectiveGlobalSpeed()가 패턴 간격/시전에 반영.
            // TODO(이동): 보스 이동 컴포넌트에 data.Enrage.MoveSpeedMultiplier 적용 (이동 로직은 뼈대 미구현).
            // TODO(연출): 광폭화 전조 연출 (색상 변화/포효 등 — 기억 요소 후보).
        }

        // ── 피격/사망 ─────────────────────────────────────────────
        private void OnEntityDamaged(DamageInfo info)
        {
            // SYNC: 보스 체력은 호스트 권위 — UI 체력바는 GameEvents 경유 갱신.
            GameEvents.RaiseBossHealthChanged(data.BossId, HpRatio);
        }

        private void OnEntityDied(CombatEntity entity)
        {
            if (_phase == BossFightPhase.DeathCinematic || _phase == BossFightPhase.Reward) return;
            EnterPhase(BossFightPhase.DeathCinematic); // 어느 전투 단계에서든 사망 → ⑥ 처치 연출
        }

        private void CleanupPluginsOnDeath()
        {
            foreach (var gimmick in _gimmicks)
                if (gimmick.IsRunning) gimmick.Interrupt();
            if (_activePuzzle != null && !_activePuzzle.IsSolved)
                _activePuzzle.Cancel();
        }

        // ── 보상 ─────────────────────────────────────────────────
        private void DropRewards()
        {
            // 보스 처치 시 아이템 3~4개 드롭 — 먼저 획득한 사람이 소유(선취득). 보상 획득 과정도 게임의 일부.
            // 개수 굴림: GameRules.RollBossDropCount(RunManager.Instance.Rng) → 3~4개.
            // TODO(Items 연동): ItemDropManager를 통해 보스 위치 주변에 DroppedItem 스폰 —
            //   선착순 선점 판정은 DroppedItem 단일 지점 (호스트 권위 — 스펙 unityNotes ⑥).
            // SYNC: 드롭 목록·픽업 결과는 호스트 권위.
        }

        // ── 30초 법칙 ─────────────────────────────────────────────
        private void NotifyBeat() => _secondsSinceLastBeat = 0f;

        /// <summary>
        /// 페이싱 워치독 — 30초간 새 상황이 없으면 강제로 상황을 만든다 (30초 법칙, 스펙 unityNotes ⑧).
        /// 플레이어가 30초 이상 같은 행동만 반복하는 상황은 지양한다 (보스 시스템.md).
        /// </summary>
        private void ForcePacingBeat()
        {
            NotifyBeat();
            if (_phase == BossFightPhase.NormalPattern && !_puzzlePhaseDone)
            {
                EnterPhase(BossFightPhase.CoopPuzzle); // 퍼즐 시작으로 국면 전환
                return;
            }
            // 이미 퍼즐을 지난 국면 — 즉시 새 패턴을 강제한다 (선택기의 반복 방지로 직전과 다른 패턴 보장).
            _patternCooldown.Reset();
            // TODO(연출): 맵 변화/보스 대사 등 비전투 상황 연출도 비트 후보 (환경형/심리형 보스).
        }

        // ── 외부 이벤트 수신 ──────────────────────────────────────
        private void OnDifficultyChanged(Difficulty difficulty)
        {
            _difficulty = difficulty;
            if (_started) ApplyDifficultyScaling();
        }

        private void OnPlayerSkillUsed(int playerId, string skillId)
        {
            _context.RecordAction(playerId, skillId, Time.time); // '행동' 요소 — 조건(RecentPlayerActionMatches) 입력
        }

        /// <summary>난이도별 배율 적용 — 체력/공격력/패턴 속도 3종만. 핵심 기믹은 변경하지 않는다 (보스 시스템.md '난이도').</summary>
        private void ApplyDifficultyScaling()
        {
            var scaling = data.GetScaling(_difficulty);
            _attackMultiplier = scaling.AttackMultiplier;
            _patternSpeedMultiplier = scaling.PatternSpeedMultiplier;
            _entity.SetMaxHp(data.BaseMaxHp * scaling.HpMultiplier, keepRatio: true);
        }
    }
}
