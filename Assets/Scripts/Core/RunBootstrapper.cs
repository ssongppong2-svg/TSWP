// 근거: 게임 시작과 선택, 직업, 플레이.md — 로비 → 시작 → 탐험 흐름. / ARCHITECTURE.md §4 (Core = 게임 흐름 FSM·런 관리)
// 문제: RunManager.StartRun을 Online.GameSessionManager만 호출한다. 그 컴포넌트가 없는 프로토타입 씬에서는
//       런이 시작되지 않아 공유 부활 횟수가 0인 채로 플레이가 시작된다(= 첫 사망에 부활 실패).
// 해결: 이 컴포넌트를 씬에 두면 런이 자동으로 시작된다. 로비/세션 경로가 이미 시작한 런은 건드리지 않는다.
// SYNC: 시드는 호스트 권위 — 추후 NGO 도입 시 이 컴포넌트는 싱글플레이/테스트 전용으로 남는다.
using UnityEngine;

namespace TSWP.Core
{
    /// <summary>
    /// 씬에 두면 런을 자동으로 시작하는 부트스트랩.
    /// 실행 순서를 앞당겨(-500) 맵 생성(RoomFlowManager.Start)이 시드를 읽기 전에 StartRun이 끝나도록 한다.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class RunBootstrapper : MonoBehaviour
    {
        [Header("런 시작")]
        [Tooltip("자동으로 런을 시작한다. 끄면 외부에서 TryStartRun()을 호출해야 한다.")]
        [SerializeField] private bool startAutomatically = true;

        [Tooltip("플레이 인원. 공유 부활 횟수(인원×3)와 시작 아이템 수의 기준이 된다. 프로토타입은 1.")]
        [SerializeField, Min(1)] private int playerCount = 1;

        [Tooltip("켜면 매 실행마다 다른 시드를 쓴다. 끄면 아래 고정 시드로 항상 같은 맵이 나온다(디버깅용).")]
        [SerializeField] private bool useRandomSeed = false;

        [Tooltip("고정 시드. useRandomSeed가 꺼져 있을 때만 쓰인다.")]
        [SerializeField] private int fixedSeed = 20260721;

        [Tooltip("씬에 RunManager가 없으면 이 오브젝트에 직접 붙인다 — 배선이 빠져도 런이 실패하지 않게 한다.")]
        [SerializeField] private bool createRunManagerIfMissing = true;

        [Header("프로토타입 규칙 덮어쓰기")]
        [Tooltip("1 이상이면 RunManager의 승리 보스 수를 이 값으로 덮어쓴다. 프로토타입 권장 1 " +
                 "(정식 15보스 규칙으로 되돌리려면 0으로 둔다).")]
        [SerializeField] private int victoryBossCountOverride = 1;

        [Tooltip("0 이상이면 공유 부활 횟수를 이 값으로 강제한다(-1 = 인원×3 규칙). 게임오버 테스트는 0~1 권장.")]
        [SerializeField] private int reviveCountOverride = -1;

        [Header("게임 흐름")]
        [Tooltip("런 시작 후 GameFlowManager 상태를 아래 값으로 바꾼다. UI가 흐름 상태로 표시를 켜고 끄기 때문에 필요하다.")]
        [SerializeField] private bool changeFlowState = true;

        [SerializeField] private GameFlowState initialPlayState = GameFlowState.Exploration;

        /// <summary>이 부트스트랩이 런 시작을 처리했는가 (중복 시작 방지).</summary>
        public bool HasStarted { get; private set; }

        /// <summary>실제로 사용된 시드.</summary>
        public int ResolvedSeed { get; private set; }

        private bool _warnedMissingManager;

        // ── 수명 주기 ─────────────────────────────────────────────
        private void Awake()
        {
            // Awake에서 먼저 시도한다 — 맵/스폰 계열이 Start에서 RunManager.Seed를 읽기 때문.
            // 이 시점에 RunManager.Instance가 아직 비어 있을 수 있어 씬 탐색으로 보완한다.
            if (startAutomatically) TryStartRun();
        }

        private void Start()
        {
            // Awake 시점에 RunManager를 찾지 못했을 경우의 2차 시도 (실행 순서 무관하게 반드시 시작되게 한다).
            if (startAutomatically) TryStartRun();

            // 늦게 구독한 UI(HUD의 부활 횟수·스테이지 등)를 위해 현재 값을 한 번 더 통지한다.
            ResolveRunManager(create: false)?.BroadcastState();

            ApplyFlowState();
        }

        // ── 런 시작 ───────────────────────────────────────────────
        /// <summary>런을 시작한다. 이미 시작된 런이 있으면 아무것도 하지 않는다.</summary>
        /// <returns>이 호출이 실제로 런을 시작했으면 true.</returns>
        public bool TryStartRun()
        {
            if (HasStarted) return false;

            var run = ResolveRunManager(create: createRunManagerIfMissing);
            if (run == null)
            {
                if (!_warnedMissingManager)
                {
                    _warnedMissingManager = true;
                    Debug.LogWarning("[RunBootstrapper] 씬에 RunManager가 없어 런을 시작하지 못했습니다.", this);
                }
                return false;
            }

            // 로비/세션(Online.GameSessionManager) 경로가 이미 시작했다면 그 런을 존중한다.
            if (run.IsRunActive)
            {
                HasStarted = true;
                ApplyPrototypeOverrides(run);
                return false;
            }

            ResolvedSeed = ResolveSeed();
            run.StartRun(Mathf.Max(1, playerCount), ResolvedSeed);
            ApplyPrototypeOverrides(run);
            HasStarted = true;

            Debug.Log($"[RunBootstrapper] 런 시작 — 인원 {playerCount}, 시드 {ResolvedSeed}, " +
                      $"공유 부활 {run.ReviveSystem.Remaining}회, 승리 보스 {run.VictoryBossCount}기");
            return true;
        }

        /// <summary>프로토타입 규칙(승리 보스 수·부활 횟수)을 RunManager에 적용한다.</summary>
        private void ApplyPrototypeOverrides(RunManager run)
        {
            if (victoryBossCountOverride >= 1)
                run.SetVictoryBossCount(victoryBossCountOverride);

            if (reviveCountOverride >= 0)
                run.ReviveSystem.SetRemaining(reviveCountOverride);
        }

        private int ResolveSeed()
        {
            if (!useRandomSeed) return fixedSeed;

            // SYNC: 호스트 권위 — 호스트가 정한 시드를 클라이언트에 전파해야 같은 맵이 나온다.
            int seed = System.Environment.TickCount;
            return seed != 0 ? seed : 1; // 0은 "미설정" 취급하는 코드가 있어 피한다
        }

        /// <summary>런 시작 후 흐름 상태를 인게임으로 옮긴다. GameFlowManager가 없으면 조용히 생략한다.</summary>
        private void ApplyFlowState()
        {
            if (!changeFlowState) return;

            var flow = GameFlowManager.Instance;
            if (flow == null) flow = FindFirstObjectByType<GameFlowManager>();
            if (flow == null) return; // 씬에 없어도 게임 로직은 계속된다

            // 씬 재시작 시 GameFlowManager는 DontDestroyOnLoad로 살아남아 GameOver/Results 상태가 남는다.
            // 여기서 인게임 상태로 되돌려야 HUD 등 흐름 연동 UI가 다시 켜진다.
            flow.ChangeState(initialPlayState);
        }

        private RunManager ResolveRunManager(bool create)
        {
            var run = RunManager.Instance;
            if (run != null) return run;

            run = FindFirstObjectByType<RunManager>();
            if (run != null) return run;

            if (!create) return null;

            // 배선 누락에도 플레이 루프가 죽지 않도록 직접 붙인다 (RunManager.Awake가 Instance를 잡는다).
            return gameObject.AddComponent<RunManager>();
        }
    }
}
