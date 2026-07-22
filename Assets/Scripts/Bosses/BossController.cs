// 근거: 보스 시스템.md — 전투 흐름 7단계 FSM(등장 연출→일반 패턴→협동 퍼즐→패턴 변화→광폭화→처치 연출→보상),
//       AI(플레이어 위치/체력/행동/퍼즐 진행 고려, 같은 패턴 반복 금지), 광폭화(체력 증가 금지),
//       처치 보상 3~4개 선취득, 30초 법칙(30초 안에 최소 1회 반응할 상황 — 페이싱 워치독).
// 실패 조건(공유 부활 소진 → 게임 오버) 판정은 Core.SharedReviveSystem/게임 흐름 소관 — 여기서 재정의하지 않는다 (ARCHITECTURE.md §5).
// SYNC: 페이즈/패턴 선택/광폭화/보상 드롭은 전부 호스트 권위, 추후 NGO NetworkVariable·RPC.
//
// 플레이 루프 계약(프로토타입):
//   ① 전투 시작 — 방 시스템(RoomInstance.StartContent)의 BeginFight 또는 startTrigger(근접 감지/즉시).
//   ② 패턴 — 선택 → 예고 → 실행 → 쿨다운(패턴이 '끝난 시점'부터 다시 센다).
//   ③ 2막 — BossPhaseTwoDirector가 있으면 체력 0이 곧 사망이 아니다. 2막 클리어까지 보상 단계를 보류한다.
//   ④ 승리 통지 — 보상 단계 진입 시 GameEvents.RaiseBossDefeated(bossId). RunManager가 이를 구독해 런을 끝낸다.
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

        [Header("전투 시작 트리거")]
        [Tooltip("보스전을 언제 시작할지.\n" +
                 "Manual = 방 시스템(RoomInstance.StartContent)이 BeginFight()를 부를 때까지 대기.\n" +
                 "PlayerProximity = 감지 반경 안에 플레이어가 들어오면 스스로 시작 (방 없는 테스트 씬에서도 동작).\n" +
                 "어느 쪽이든 BeginFight()는 1회만 실행되므로 둘이 겹쳐도 안전하다.")]
        [SerializeField] private BossStartTrigger startTrigger = BossStartTrigger.PlayerProximity;

        [Tooltip("PlayerProximity 감지 반경(월드 유닛).")]
        [SerializeField, Min(0f)] private float startDetectRadius = 14f; // TODO(밸런스): 문서 미정

        [Tooltip("감지 검사 간격(초). 매 프레임 물리 질의를 하지 않기 위한 간격이다.")]
        [SerializeField, Min(0.05f)] private float startDetectInterval = 0.25f;

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

        [Header("기믹 자동 작동")]
        [Tooltip("전투 단계에 들어갈 때 대기 중인 기믹을 하나 자동으로 작동시킨다.\n" +
                 "CoopGimmick 카테고리 패턴을 만들지 않아도 기믹이 굴러가게 하는 프로토타입 편의 스위치.")]
        [SerializeField] private bool autoActivateGimmicks = true;

        [Header("플러그인 수집 (기믹/협동 퍼즐)")]
        [Tooltip("부모(=보스 방) 하위의 IGimmick/ICoopPuzzle도 함께 수집한다. " +
                 "보스 프리팹 밖(방 바닥의 레버·발판 등)에 기믹을 두고도 배선 없이 연결된다.")]
        [SerializeField] private bool collectPluginsFromParent = true;

        [Tooltip("추가로 훑을 루트들. 보스와 부모 밖에 있는 기믹/퍼즐을 여기에 꽂는다.")]
        [SerializeField] private Transform[] extraPluginRoots = System.Array.Empty<Transform>();

        [Header("2막 (Phase 2) — 체력 0 = 진짜 죽음이 아닐 수 있다")]
        [Tooltip("비우면 자동 탐색한다(자식 → 씬 전체에서 이 보스의 bossId를 감시하는 것).")]
        [SerializeField] private BossPhaseTwoDirector phaseTwoDirector;

        [Tooltip("2막이 있는 보스는 2막이 끝날 때까지 처치 연출을 멈추고 기다린다.\n" +
                 "즉, 2막을 클리어해야 보상 단계(GameEvents.BossDefeated)로 넘어간다.")]
        [SerializeField] private bool waitForPhaseTwo = true;

        [Tooltip("2막이 이 시간을 넘겨도 끝나지 않으면 강제로 마무리한다(0 = 무제한). " +
                 "런이 영영 끝나지 않는 상황을 막는 안전장치.")]
        [SerializeField, Min(0f)] private float phaseTwoTimeoutSeconds = 90f; // TODO(밸런스): 문서 미정

        [Header("프로토타입 편의 (밸런스 값 아님 — 테스트 속도용)")]
        [Tooltip("최대 체력 배율. 최종 체력 = (overrideMaxHp 또는 BossData.baseMaxHp) × 난이도 배율 × 이 값. " +
                 "0.1로 두면 1850 체력 보스가 185가 되어 금방 검증할 수 있다.")]
        [SerializeField, Min(0.01f)] private float healthMultiplier = 1f;

        [Tooltip("0보다 크면 BossData.baseMaxHp 대신 이 값을 기준 체력으로 쓴다(난이도 배율은 그대로 적용).")]
        [SerializeField, Min(0f)] private float overrideMaxHp = 0f;

        [Header("디버그 (프로토타입 전용)")]
        [Tooltip("아래 단축키를 사용할지. 정식 빌드에서는 꺼 둔다.")]
        [SerializeField] private bool enableDebugKeys = true;

        [Tooltip("보스를 즉시 처치한다(→ 2막이 있으면 2막으로 진입).")]
        [SerializeField] private KeyCode debugKillKey = KeyCode.F9;

        [Tooltip("2막을 즉시 강제 개시한다.")]
        [SerializeField] private KeyCode debugPhaseTwoKey = KeyCode.F10;

        [Tooltip("광폭화를 즉시 강제한다.")]
        [SerializeField] private KeyCode debugEnrageKey = KeyCode.F11;

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

        // 전투 시작 트리거 상태 (BeginFight 전에만 쓰인다).
        private float _startDetectTimer;
        private readonly List<CombatEntity> _startDetectBuffer = new();

        // 2막 게이트 — 체력 0 이후 '진짜 사망'까지 기다리는 구간.
        private BossPhaseTwoDirector _gatedDirector;
        private bool _phaseTwoPending;
        private float _phaseTwoTimer;

        public BossData Data => data;
        public BossFightPhase Phase => _phase;
        public bool IsEnraged => _isEnraged;
        public CombatEntity Entity => _entity;

        /// <summary>보스전이 시작됐는지 (BeginFight 호출 이후 true).</summary>
        public bool IsFightStarted => _started;

        /// <summary>2막(Phase 2)이 끝나기를 기다리는 중인지 — 이 동안에는 보상 단계로 넘어가지 않는다.</summary>
        public bool IsWaitingForPhaseTwo => _phaseTwoPending;

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

            // 보스 방(부모)까지 훑는다 — 레버·발판·게이지는 보스 몸통이 아니라 방 바닥에 놓이는 것이 자연스럽다.
            // 중복 등록은 RegisterGimmick/RegisterCoopPuzzle이 걸러내므로 두 번 훑어도 안전하다.
            if (collectPluginsFromParent && transform.parent != null)
                CollectPluginsFrom(transform.parent.gameObject);

            for (int i = 0; i < extraPluginRoots.Length; i++)
                if (extraPluginRoots[i] != null) CollectPluginsFrom(extraPluginRoots[i].gameObject);
        }

        private void Start()
        {
            // Immediate는 씬 시작 즉시 전투를 연다 — 보스 단독 테스트 씬에서 아무 배선 없이 확인하기 위한 옵션.
            // Awake가 아니라 Start인 이유: 다른 매니저(SpawnManager/RunManager)의 Awake가 먼저 끝나야 한다.
            if (startTrigger == BossStartTrigger.Immediate)
                BeginFight();
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

            UnsubscribePhaseTwo();
            FlushPendingVictoryOnDisable();
        }

        /// <summary>
        /// 이미 죽은 보스가 보상 단계에 닿기 전에 방이 꺼지면(플레이어가 먼저 다음 방으로 이동) 승리 통지가 영영 오지 않는다.
        /// 플레이 중 비활성화일 때만 강제로 마무리해 '한 바퀴'가 반드시 닫히게 한다.
        /// (씬 언로드/에디터 종료 시에는 scene.isLoaded가 false이므로 발행하지 않는다.)
        /// </summary>
        private void FlushPendingVictoryOnDisable()
        {
            if (!Application.isPlaying) return;
            if (!_started || _entity == null || !_entity.IsDead) return;
            if (_phase == BossFightPhase.Reward) return;
            if (!gameObject.scene.isLoaded) return;

            Debug.LogWarning($"[BossController] '{name}': 보상 단계 전에 비활성화되어 처치 통지를 강제로 발행합니다.", this);
            _phase = BossFightPhase.Reward;
            _phaseTwoPending = false;
            GameEvents.RaiseBossDefeated(data.BossId);
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
        /// 보스전 시작 — 보스 방 진입 시 호출한다 (Map.RoomInstance.StartContent가 호출).
        /// startTrigger가 PlayerProximity/Immediate면 이 컴포넌트가 스스로도 호출한다. 두 번 불려도 안전하다.
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
            // 디버그 키는 전투 시작 전에도 받는다 (F9로 곧바로 처치 → 결과 화면까지 확인할 수 있게).
            if (enableDebugKeys) TickDebugKeys();

            if (!_started)
            {
                TickStartTrigger(Time.deltaTime);
                return;
            }

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
                    // ⑥-a 2막이 열려 있으면 그것이 끝날 때까지 '진짜 죽음'을 미룬다.
                    //     (해치 퀸: 퀸이 쓰러져도 거미 무리와 레버 퍼즐을 정리해야 전투가 끝난다.)
                    if (_phaseTwoPending)
                    {
                        _phaseTwoTimer += dt;
                        if (phaseTwoTimeoutSeconds > 0f && _phaseTwoTimer >= phaseTwoTimeoutSeconds)
                        {
                            Debug.LogWarning($"[BossController] '{name}': 2막이 {phaseTwoTimeoutSeconds}초를 넘겨 " +
                                             "강제로 마무리했습니다 (런이 멈추지 않게 하는 안전장치).", this);
                            CompletePhaseTwoGate();
                        }
                        break;
                    }

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

        // ── 전투 시작 트리거 ──────────────────────────────────────
        /// <summary>
        /// 시작 전 폴링. 방 시스템이 없는 씬(보스 단독 테스트/샌드박스)에서도 보스가 스스로 전투를 열게 한다.
        /// 매 프레임이 아니라 startDetectInterval 간격으로만 물리 질의를 한다.
        /// </summary>
        private void TickStartTrigger(float dt)
        {
            if (startTrigger != BossStartTrigger.PlayerProximity || startDetectRadius <= 0f) return;

            _startDetectTimer -= dt;
            if (_startDetectTimer > 0f) return;
            _startDetectTimer = startDetectInterval;

            // 살아있는 플레이어 진영 유닛만 센다 (팀 판정은 TeamType 비교 — ARCHITECTURE.md §3-6).
            BossCombatUtil.CollectPlayers(transform.position, startDetectRadius, _startDetectBuffer);
            if (_startDetectBuffer.Count == 0) return;

            BeginFight();
        }

        // ── 디버그 단축키 ─────────────────────────────────────────
        private void TickDebugKeys()
        {
            // 입력은 레거시 UnityEngine.Input (ARCHITECTURE.md §1 — Input System 교체는 추후).
            if (debugKillKey != KeyCode.None && Input.GetKeyDown(debugKillKey)) DebugKillBoss();
            if (debugPhaseTwoKey != KeyCode.None && Input.GetKeyDown(debugPhaseTwoKey)) DebugForcePhaseTwo();
            if (debugEnrageKey != KeyCode.None && Input.GetKeyDown(debugEnrageKey)) DebugForceEnrage();
        }

        /// <summary>보스를 즉시 처치한다. 2막이 있으면 2막으로 넘어간다 (처치 → 결과 흐름 검증용).</summary>
        [ContextMenu("디버그: 보스 즉시 처치")]
        public void DebugKillBoss()
        {
            if (!_started) BeginFight();
            if (_entity == null || _entity.IsDead) return;
            Debug.Log($"[BossController] '{name}': 디버그 즉시 처치.", this);
            _entity.Kill(null); // 사망 파이프라인을 그대로 탄다 → OnEntityDied → 처치 연출
        }

        /// <summary>2막(Spider Swarm + Lever Puzzle)을 즉시 강제 개시한다.</summary>
        [ContextMenu("디버그: 2막 강제 개시")]
        public void DebugForcePhaseTwo()
        {
            var director = ResolvePhaseTwoDirector();
            if (director == null)
            {
                Debug.LogWarning($"[BossController] '{name}': 2막 감독(BossPhaseTwoDirector)이 씬에 없습니다.", this);
                return;
            }
            Debug.Log($"[BossController] '{name}': 디버그 2막 강제 개시.", this);
            director.Fire();
        }

        /// <summary>광폭화를 즉시 강제한다 (체력 조건 무시).</summary>
        [ContextMenu("디버그: 광폭화 강제")]
        public void DebugForceEnrage()
        {
            if (!_started || _isEnraged || data == null || !data.HasEnrage) return;
            Debug.Log($"[BossController] '{name}': 디버그 광폭화 강제.", this);
            EnterPhase(BossFightPhase.Enrage);
        }

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
                    TryAutoActivateGimmick();
                    break;

                case BossFightPhase.CoopPuzzle:
                    BeginCoopPuzzle();
                    break;

                case BossFightPhase.PatternChange:
                    // TODO(연출): 패턴 변화 연출 (형태 변화/맵 변화 등 — 기억 요소 후보).
                    _selector.ClearHistory(); // 새 국면 — 패턴 이력 리셋
                    TryAutoActivateGimmick();
                    break;

                case BossFightPhase.Enrage:
                    ApplyEnrage();
                    TryAutoActivateGimmick();
                    break;

                case BossFightPhase.DeathCinematic:
                    // 실행 중이던 패턴을 반드시 끊는다 — 빼먹으면 돌진 중 사망한 보스가
                    // linearVelocity를 유지한 채 영원히 미끄러진다(OnCleanup이 호출되지 않기 때문).
                    InterruptActivePattern();
                    CleanupPluginsOnDeath();
                    BeginPhaseTwoGate();
                    break;

                case BossFightPhase.Reward:
                    GameEvents.RaiseBossDefeated(data.BossId); // RunManager가 구독 → 클리어 기록·승리 판정
                    NotifyRoomObjective();
                    DropRewards();
                    break;
            }
        }

        /// <summary>실행 중인 패턴을 강제 종료한다 (정리 훅까지 태운다).</summary>
        private void InterruptActivePattern()
        {
            if (_activeRunner == null) return;
            _activeRunner.Interrupt(_patternContext);
            _activeRunner = null;
            _runnerElapsed = 0f;
        }

        /// <summary>대기 중인 기믹을 하나 작동시킨다 (기믹이 없으면 아무 일도 없다).</summary>
        private void TryAutoActivateGimmick()
        {
            if (!autoActivateGimmicks || _gimmicks.Count == 0) return;
            ActivateGimmick(null);
        }

        /// <summary>목표형 보스 방의 클리어 통지. 전멸형 방이면 RoomManager가 조용히 무시한다.</summary>
        private void NotifyRoomObjective()
        {
            if (!notifyRoomObjectiveOnReward) return;
            var room = Map.RoomManager.Instance;
            if (room == null) return; // 방 시스템이 없는 테스트 씬 — 조용히 생략
            room.NotifyObjectiveComplete(data.BossId);
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

                // 패턴이 '끝난 시점'부터 쿨다운을 다시 센다.
                // 실행 중에 쿨다운이 같이 흐르면 긴 패턴(돌진 등)이 끝나자마자 다음 패턴이 나가
                // 플레이어가 반격할 틈이 사라진다 — 예고/빈틈이 있어야 공정하다(보스 시스템.md).
                RestartPatternCooldown();
                return;
            }

            // ② 다음 패턴 선택
            // 간격을 먼저 확정한다 — CooldownTimer.TryUse가 '현재 Duration'을 Remaining에 넣으므로
            // 나중에 SetDuration을 부르면 속도 변화(난이도/광폭화)가 한 사이클 늦게 반영된다.
            float speed = EffectiveGlobalSpeed();
            _patternCooldown.SetDuration(patternIntervalSeconds / Mathf.Max(0.01f, speed));

            if (!_patternCooldown.TryUse()) return;

            UpdateContext();
            var pattern = _selector.Select(BuildCandidates(), _context, _isEnraged);
            if (pattern == null) return; // 유효 후보 없음 — 다음 간격에 재시도 (기본 공격 대체는 TODO)

            ExecutePattern(pattern);
        }

        /// <summary>패턴 간격 타이머를 지금부터 다시 시작한다.</summary>
        private void RestartPatternCooldown()
        {
            float speed = EffectiveGlobalSpeed();
            _patternCooldown.SetDuration(patternIntervalSeconds / Mathf.Max(0.01f, speed));
            _patternCooldown.Reset();
            _patternCooldown.TryUse(); // Remaining = Duration — 다음 패턴까지 이만큼 쉰다
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
            var entities = FindObjectsByType<CombatEntity>();
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

        // ── 2막 (Phase 2) ─────────────────────────────────────────
        /// <summary>
        /// 감시할 2막 감독을 찾는다. 인스펙터 지정 → 보스 하위 → 씬 전체(bossId 일치) 순.
        /// 없으면 null이고, 그때는 보스 체력 0이 곧 사망이다(기존 동작 그대로).
        /// </summary>
        private BossPhaseTwoDirector ResolvePhaseTwoDirector()
        {
            if (phaseTwoDirector != null) return phaseTwoDirector;

            var local = GetComponentInChildren<BossPhaseTwoDirector>(true);
            if (local != null)
            {
                phaseTwoDirector = local;
                return local;
            }

            // Unity 6: FindObjectOfType는 제거됨 — FindObjectsByType 사용 (비활성 포함).
            var all = FindObjectsByType<BossPhaseTwoDirector>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                var candidate = all[i];
                if (candidate == null) continue;
                // watchBossId가 비어 있으면 '아무 보스나' 감시하는 감독이므로 이 보스에도 해당한다.
                if (!string.IsNullOrEmpty(candidate.WatchBossId) && candidate.WatchBossId != data.BossId) continue;
                phaseTwoDirector = candidate;
                return candidate;
            }
            return null;
        }

        /// <summary>
        /// 처치 연출 진입 시 2막 게이트를 건다. 2막이 실제로 열렸고 아직 끝나지 않았다면
        /// 보상 단계(=BossDefeated)를 그때까지 보류한다.
        /// </summary>
        private void BeginPhaseTwoGate()
        {
            _phaseTwoPending = false;
            _phaseTwoTimer = 0f;
            if (!waitForPhaseTwo) return;

            // EnterPhase가 GameEvents.BossPhaseChanged를 먼저 발행하므로,
            // 지연 0인 감독은 이 시점에 이미 Fire()된 상태다.
            var director = ResolvePhaseTwoDirector();
            if (director == null || !director.IsActive || director.IsComplete) return;

            _gatedDirector = director;
            _gatedDirector.PhaseTwoCompleted += OnPhaseTwoCompleted;
            _phaseTwoPending = true;

            Debug.Log($"[BossController] '{name}': 체력이 0이 됐지만 2막이 남았습니다 — 2막 클리어까지 전투가 계속됩니다.", this);
        }

        private void OnPhaseTwoCompleted(BossPhaseTwoDirector director) => CompletePhaseTwoGate();

        /// <summary>2막 종료 — 여기서부터 처치 연출 시간이 흐르고, 그 뒤 보상 단계로 넘어간다.</summary>
        private void CompletePhaseTwoGate()
        {
            if (!_phaseTwoPending) return;
            UnsubscribePhaseTwo();
            _phaseTwoPending = false;
            _phaseTimer = 0f;   // 처치 연출을 2막이 끝난 시점부터 센다
            NotifyBeat();
        }

        private void UnsubscribePhaseTwo()
        {
            if (_gatedDirector == null) return;
            _gatedDirector.PhaseTwoCompleted -= OnPhaseTwoCompleted;
            _gatedDirector = null;
        }

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
            TryAutoActivateGimmick(); // 쉬고 있는 기믹이 있으면 그것도 함께 연다
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

            // 기준 체력은 데이터(BossData.baseMaxHp)가 소유한다. 여기서 하드코딩하지 않고,
            // 테스트용 조정은 인스펙터 값(overrideMaxHp/healthMultiplier)으로만 한다.
            float baseHp = overrideMaxHp > 0f ? overrideMaxHp : data.BaseMaxHp;
            _entity.SetMaxHp(baseHp * scaling.HpMultiplier * healthMultiplier, keepRatio: true);
            GameEvents.RaiseBossHealthChanged(data.BossId, HpRatio); // 체력바 즉시 갱신
        }
    }
}
