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

        public int Seed { get; private set; }
        /// <summary>현재 스테이지 (1~GameRules.TotalBossCount).</summary>
        public int CurrentStage { get; private set; } = 1;
        public SharedReviveSystem ReviveSystem { get; } = new();
        public System.Random Rng { get; private set; }

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
            ReviveSystem.Initialize(playerCount);
            GameEvents.RaiseStageChanged(CurrentStage);
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
            if (CurrentStage < GameRules.TotalBossCount)
            {
                CurrentStage++;
                GameEvents.RaiseStageChanged(CurrentStage);
            }
            else
            {
                // 15보스 전부 클리어 → 결과 화면
                GameFlowManager.Instance?.ShowResults();
                GameEvents.RaiseMatchFinished(BuildResult());
            }
        }
    }
}
