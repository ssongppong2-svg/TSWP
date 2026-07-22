// 근거: 게임 시작과 선택, 직업, 플레이.md — 게임 오버 = 공유 부활 소진 + 전원 사망.
// 문제: SharedReviveSystem.IsGameOver(aliveCount)를 아무도 호출하지 않아 죽어도 게임 오버가 오지 않는다.
// 해결: 이 컴포넌트가 생존 플레이어 수를 세어 판정을 단일 지점(SharedReviveSystem.IsGameOver)에 위임하고,
//       성립하면 RunManager.EndRun(false) → GameFlowState.GameOver + GameEvents.GameOver로 이어 준다.
//       판정 규칙 자체는 여기서 재정의하지 않는다 (ARCHITECTURE.md §5 — 부활/게임오버 판정은 Core 한 곳).
// SYNC: 호스트 권위 — 추후 NGO에서는 호스트만 판정하고 결과를 전파한다.
using UnityEngine;
using TSWP.Combat;

namespace TSWP.Core
{
    /// <summary>
    /// 생존 플레이어 수를 추적해 게임 오버를 판정한다.
    /// 씬에 이 컴포넌트 하나만 두면 되고, 플레이어가 아직 스폰되지 않았으면 판정을 보류한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunOutcomeWatcher : MonoBehaviour
    {
        [Header("판정")]
        [Tooltip("생존자 수를 다시 세는 간격(초). 사망 이벤트가 오면 간격과 무관하게 다음 프레임에 즉시 검사한다.")]
        [SerializeField, Min(0.05f)] private float checkInterval = 0.25f;

        [Tooltip("게임 오버 판정 로그를 남긴다.")]
        [SerializeField] private bool logDecisions = true;

        [Tooltip("런이 시작되지 않은 채 이 시간이 지나면 경고를 남긴다(배선 누락 조기 발견용). 0이면 경고하지 않는다.")]
        [SerializeField, Min(0f)] private float warnIfRunNotStartedAfter = 3f;

        /// <summary>마지막으로 집계한 생존 플레이어 수.</summary>
        public int AlivePlayerCount { get; private set; }

        /// <summary>마지막으로 집계한 플레이어 전투 유닛 총수(사망 포함).</summary>
        public int TrackedPlayerCount { get; private set; }

        private float _timer;
        private bool _checkQueued;
        private bool _sawAnyPlayer;   // 플레이어가 한 번이라도 존재했는가 (스폰 전 오판 방지)
        private bool _warnedNotStarted;
        private float _enabledTime;

        private void OnEnable()
        {
            GameEvents.PlayerDied += OnPlayerDied;
            _enabledTime = Time.unscaledTime;
        }

        private void OnDisable()
        {
            GameEvents.PlayerDied -= OnPlayerDied;
        }

        private void Update()
        {
            var run = RunManager.Instance;
            if (run == null) return;

            if (!run.IsRunActive)
            {
                WarnIfRunNeverStarted(run);
                return;
            }

            // 사망 통지는 다음 프레임에 검사한다 — CombatEntity.Die는 PlayerDied를 먼저 발행하고
            // 그 뒤에 즉시부활(TryReviveShared)을 시도하므로, 같은 프레임에 세면 부활 전 상태를 본다.
            if (_checkQueued)
            {
                _checkQueued = false;
                _timer = checkInterval;
                Evaluate(run);
                return;
            }

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = checkInterval;
            Evaluate(run);
        }

        // ── 판정 ──────────────────────────────────────────────────
        private void Evaluate(RunManager run)
        {
            AlivePlayerCount = CountAlivePlayers();

            if (!_sawAnyPlayer) return;         // 아직 플레이어가 스폰되지 않음 — 판정 보류
            if (AlivePlayerCount > 0) return;   // 한 명이라도 살아 있으면 계속 진행

            // 게임오버 규칙은 SharedReviveSystem 한 곳이 소유한다 (여기서는 호출만).
            if (!run.ReviveSystem.IsGameOver(AlivePlayerCount)) return;

            if (logDecisions)
                Debug.Log("[RunOutcomeWatcher] 전원 사망 + 공유 부활 소진 → 게임 오버", this);

            run.EndRun(false);
        }

        /// <summary>살아 있는 플레이어 전투 유닛 수. 팀/소유자 id로 판별한다(레이어 아님 — ARCHITECTURE.md §3-6).</summary>
        private int CountAlivePlayers()
        {
            var entities = FindObjectsByType<CombatEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            int alive = 0;
            int tracked = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == null) continue;
                if (entity.Team != TeamType.Players || entity.OwnerPlayerId < 0) continue;

                tracked++;
                if (!entity.IsDead) alive++;
            }

            TrackedPlayerCount = tracked;
            if (tracked > 0) _sawAnyPlayer = true;
            return alive;
        }

        private void OnPlayerDied(int playerId) => _checkQueued = true;

        /// <summary>런이 시작되지 않으면 부활 횟수가 0이라 첫 사망에서 부활이 실패한다 — 배선 누락을 알린다.</summary>
        private void WarnIfRunNeverStarted(RunManager run)
        {
            if (_warnedNotStarted || warnIfRunNotStartedAfter <= 0f) return;
            if (run.IsRunFinished) return; // 이미 끝난 런이라 비활성인 것은 정상
            if (Time.unscaledTime - _enabledTime < warnIfRunNotStartedAfter) return;

            _warnedNotStarted = true;
            Debug.LogWarning("[RunOutcomeWatcher] 런이 시작되지 않았습니다 — 씬에 RunBootstrapper를 두거나 " +
                             "RunManager.StartRun을 호출하세요. (공유 부활 횟수가 0인 채로 플레이됩니다)", this);
        }
    }
}
