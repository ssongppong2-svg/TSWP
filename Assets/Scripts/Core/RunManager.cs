// 근거: 맵 시스템.md(스테이지 1~15) / 게임 시작과 선택, 직업, 플레이.md(부활·결과) / 보스 시스템.md(보스 1회 등장)
// 한 번의 런(출발→15보스 or 게임오버)을 관리한다. 통계는 GameEvents 구독으로 누적.
// SYNC: 시드·스테이지·부활 횟수는 호스트 권위, 추후 NGO 동기화.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Core
{
    public sealed class RunManager : MonoBehaviour
    {
        public static RunManager Instance { get; private set; }

        [Header("프로토타입 옵션 (정식 규칙은 15보스 전부 처치)")]
        [Tooltip("이 수만큼 보스를 처치하면 승리로 처리한다. 기본값은 GameRules.TotalBossCount(15) — " +
                 "프로토타입에서는 1로 낮춰 한 바퀴를 완주할 수 있게 한다. RunBootstrapper가 덮어쓸 수 있다.")]
        [SerializeField, Min(1)] private int prototypeVictoryBossCount = GameRules.TotalBossCount;

        [Tooltip("0 이상이면 공유 부활 횟수를 StartRun 직후 이 값으로 강제한다(-1 = 인원×3 규칙 그대로). 게임오버 테스트용.")]
        [SerializeField] private int prototypeReviveOverride = -1;

        public int Seed { get; private set; }
        /// <summary>현재 스테이지 (1~GameRules.TotalBossCount).</summary>
        public int CurrentStage { get; private set; } = 1;
        public SharedReviveSystem ReviveSystem { get; } = new();
        public System.Random Rng { get; private set; }

        /// <summary>런이 진행 중인가. StartRun에서 true, EndRun에서 false.</summary>
        public bool IsRunActive { get; private set; }

        /// <summary>런이 종료됐는가 (승리·게임오버 모두 포함). 결과가 만들어졌으면 true.</summary>
        public bool IsRunFinished => LastResult != null;

        /// <summary>마지막 런의 승패. EndRun 이후에만 의미가 있다.</summary>
        public bool IsVictory { get; private set; }

        /// <summary>마지막으로 확정된 결과 (결과 화면이 이 값을 그린다). 종료 전에는 null.</summary>
        public MatchResult LastResult { get; private set; }

        /// <summary>이번 런에서 처치한 보스 수.</summary>
        public int ClearedBossCount => _clearedBossIds.Count;

        /// <summary>승리에 필요한 보스 처치 수 (1~15로 보정).</summary>
        public int VictoryBossCount => Mathf.Clamp(prototypeVictoryBossCount, 1, GameRules.TotalBossCount);

        private readonly Dictionary<int, PlayerMatchStats> _stats = new();
        private readonly List<string> _clearedBossIds = new();
        private readonly List<string> _acquiredItemIds = new();
        private float _runStartTime;

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
            GameEvents.DamageDealt += OnDamageDealt;
            GameEvents.HealingDone += OnHealingDone;
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.ItemAcquired += OnItemAcquired;
            GameEvents.PingRaised += OnPingRaised;
            GameEvents.BossDefeated += OnBossDefeated;
        }

        private void OnDisable()
        {
            GameEvents.DamageDealt -= OnDamageDealt;
            GameEvents.HealingDone -= OnHealingDone;
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.ItemAcquired -= OnItemAcquired;
            GameEvents.PingRaised -= OnPingRaised;
            GameEvents.BossDefeated -= OnBossDefeated;
        }

        /// <summary>런 시작. 시드는 호스트가 생성해 전파 — 전 클라이언트가 동일 맵을 재생성한다.</summary>
        public void StartRun(int playerCount, int seed)
        {
            Seed = seed;
            Rng = new System.Random(seed);
            CurrentStage = 1;
            _stats.Clear();
            _clearedBossIds.Clear();
            _acquiredItemIds.Clear();
            _runStartTime = Time.time;

            IsRunActive = true;
            IsVictory = false;
            LastResult = null;

            ReviveSystem.Initialize(playerCount);

            // 프로토타입: 부활 횟수가 많으면 게임오버까지 도달하기 어렵다 — 인스펙터로 낮출 수 있게 한다.
            if (prototypeReviveOverride >= 0)
                ReviveSystem.SetRemaining(prototypeReviveOverride);

            GameEvents.RaiseStageChanged(CurrentStage);
        }

        /// <summary>
        /// 승리 조건(보스 처치 수)을 코드/부트스트랩에서 덮어쓴다. 프로토타입 전용 —
        /// 정식 규칙은 GameRules.TotalBossCount(15)다.
        /// </summary>
        public void SetVictoryBossCount(int count)
            => prototypeVictoryBossCount = Mathf.Clamp(count, 1, GameRules.TotalBossCount);

        /// <summary>
        /// 현재 런 상태를 다시 통지한다 (값은 바꾸지 않는다).
        /// StartRun이 UI보다 먼저 실행되면 UI가 초기 이벤트를 놓치므로 Start 시점에 한 번 더 불러 준다.
        /// </summary>
        public void BroadcastState()
        {
            GameEvents.RaiseStageChanged(CurrentStage);
            GameEvents.RaiseReviveCountChanged(ReviveSystem.Remaining);
        }

        /// <summary>
        /// 런 종료 — 결과를 확정하고 흐름 상태를 전환한다. 중복 호출은 무시한다.
        /// 승리: Results 상태 + MatchFinished / 패배: GameOver 상태 + GameOver + MatchFinished.
        /// </summary>
        public void EndRun(bool victory)
        {
            if (IsRunFinished) return; // 이미 확정된 런 — 두 번 종료하지 않는다

            IsRunActive = false;
            IsVictory = victory;
            LastResult = BuildResult();

            if (victory)
            {
                GameFlowManager.Instance?.ShowResults();
            }
            else
            {
                GameFlowManager.Instance?.ChangeState(GameFlowState.GameOver);
                GameEvents.RaiseGameOver();
            }

            // 결과 데이터는 승패와 무관하게 같은 창구로 흘린다 (결과 화면/뒷풀이가 이 하나만 구독하면 된다).
            GameEvents.RaiseMatchFinished(LastResult);
        }

        public PlayerMatchStats GetStats(int playerId)
        {
            if (!_stats.TryGetValue(playerId, out var s))
            {
                s = new PlayerMatchStats { PlayerId = playerId };
                _stats[playerId] = s;
            }
            return s;
        }

        public MatchResult BuildResult()
        {
            var result = new MatchResult
            {
                PlayTime = TimeSpan.FromSeconds(Time.time - _runStartTime),
            };
            result.PerPlayerStats.AddRange(_stats.Values);
            result.ClearedBossIds.AddRange(_clearedBossIds);
            result.AcquiredItemIds.AddRange(_acquiredItemIds);
            result.MvpPlayerId = PickMvp();
            return result;
        }

        // TODO(밸런스): MVP 산식 문서 미정 — 임시로 피해량 최고 플레이어.
        private int PickMvp()
        {
            int mvp = -1;
            float best = float.MinValue;
            foreach (var s in _stats.Values)
            {
                if (s.DamageDealt <= best) continue;
                best = s.DamageDealt;
                mvp = s.PlayerId;
            }
            return mvp;
        }

        // ── 통계 누적 ─────────────────────────────────────────────
        private void OnDamageDealt(int attackerId, float amount, bool friendly)
        {
            var s = GetStats(attackerId);
            s.DamageDealt += amount;
            if (friendly) s.TrollScore++; // TODO(밸런스): 트롤 산정 방식 문서 미정
        }

        private void OnHealingDone(int playerId, float amount) => GetStats(playerId).HealingDone += amount;
        private void OnEnemyKilled(int killerId, string enemyId) => GetStats(killerId).Kills++;
        private void OnPlayerDied(int playerId) => GetStats(playerId).Deaths++;

        private void OnItemAcquired(int playerId, string itemCode)
        {
            GetStats(playerId).ItemsAcquired++;
            _acquiredItemIds.Add(itemCode);
        }

        private void OnPingRaised(int senderId, PingType type, Vector2 pos) => GetStats(senderId).PingsUsed++;

        private void OnBossDefeated(string bossId)
        {
            _clearedBossIds.Add(bossId); // 각 보스는 런에서 1회만 등장 (보스 시스템.md)

            // 승리 판정 — 정식 규칙은 15보스 전부(VictoryBossCount 기본값), 프로토타입은 1기만으로도 승리.
            if (_clearedBossIds.Count >= VictoryBossCount)
            {
                EndRun(true);
                return;
            }

            if (CurrentStage < GameRules.TotalBossCount)
            {
                CurrentStage++;
                GameEvents.RaiseStageChanged(CurrentStage);
            }
        }
    }
}
