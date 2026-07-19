// 근거: UI 시스템.md — 게임플레이 → UI/메타 단방향 이벤트 허브.
// UI·업적·통계는 이 허브만 구독한다 (게임 로직 직접 참조 금지 — ARCHITECTURE.md §3-5).
// 페이로드는 원시 타입/ID 위주로 유지해 시스템 간 결합을 낮춘다.
using System;
using UnityEngine;

namespace TSWP.Core
{
    public static class GameEvents
    {
        // ── 게임 흐름 ──────────────────────────────────────────────
        public static event Action<GameFlowState> FlowStateChanged;
        public static event Action<Difficulty> DifficultyChanged;
        public static event Action<int> StageChanged;               // stageIndex (1~15)
        public static event Action GameOver;
        public static event Action<MatchResult> MatchFinished;

        // ── 플레이어 생존/체력 ─────────────────────────────────────
        public static event Action<int, float, float> PlayerHealthChanged; // playerId, hp, maxHp
        public static event Action<int> PlayerDied;                 // playerId
        public static event Action<int> PlayerRevived;              // playerId
        public static event Action<int> ReviveCountChanged;         // 남은 공유 부활 횟수

        // ── 전투/성장 ──────────────────────────────────────────────
        public static event Action<int, float, bool> DamageDealt;   // attackerPlayerId, amount, wasFriendly(트롤 통계용)
        public static event Action<int, float> HealingDone;         // playerId, amount
        public static event Action<int, string> EnemyKilled;        // killerPlayerId, enemyId
        public static event Action<int, int> GoldGained;            // playerId, amount
        public static event Action<int, int> ExpGained;             // playerId, amount
        public static event Action<int, string> SkillUsed;          // playerId, skillId

        // ── 보스 ──────────────────────────────────────────────────
        public static event Action<string> BossAppeared;            // bossId
        public static event Action<string, float> BossHealthChanged; // bossId, hpRatio
        public static event Action<string, int> BossPhaseChanged;   // bossId, phase(BossFightPhase의 int 값)
        public static event Action<string> BossEnraged;             // bossId
        public static event Action<string> BossDefeated;            // bossId

        // ── 아이템 ────────────────────────────────────────────────
        public static event Action<int, string> ItemAcquired;       // playerId, itemCode
        public static event Action<int, string> ItemDropped;        // playerId, itemCode (버리기/교체)

        // ── 맵/방/퍼즐 ────────────────────────────────────────────
        public static event Action<int> RoomEntered;                // roomId
        public static event Action<int> RoomCleared;                // roomId
        public static event Action<int> RoomDiscovered;             // roomId (미니맵: 탐험 지역만 표시)
        public static event Action<int> SecretRoomFound;            // roomId
        public static event Action<string> PuzzleSolved;            // puzzleId
        public static event Action<string> PuzzleFailed;            // puzzleId

        // ── 소셜/커뮤니케이션 ─────────────────────────────────────
        public static event Action<int, PingType, Vector2> PingRaised;      // senderPlayerId, type, worldPos
        public static event Action<int, string> EmoteUsed;          // playerId, emoteId
        public static event Action<int, bool> VoiceSpeakingChanged; // playerId, isSpeaking

        // ── 메타(업적/칭호) ───────────────────────────────────────
        public static event Action<string> AchievementUnlocked;     // achievementId
        public static event Action<string> TitleEarned;             // titleId
        /// <summary>업적 카운터형 조건 집계용 범용 카운터 (예: "revive.count", "ping.used").</summary>
        public static event Action<string, int> StatCounter;        // counterKey, delta

        // ── Raise 헬퍼 ────────────────────────────────────────────
        public static void RaiseFlowStateChanged(GameFlowState s) => FlowStateChanged?.Invoke(s);
        public static void RaiseDifficultyChanged(Difficulty d) => DifficultyChanged?.Invoke(d);
        public static void RaiseStageChanged(int stage) => StageChanged?.Invoke(stage);
        public static void RaiseGameOver() => GameOver?.Invoke();
        public static void RaiseMatchFinished(MatchResult r) => MatchFinished?.Invoke(r);

        public static void RaisePlayerHealthChanged(int id, float hp, float maxHp) => PlayerHealthChanged?.Invoke(id, hp, maxHp);
        public static void RaisePlayerDied(int id) => PlayerDied?.Invoke(id);
        public static void RaisePlayerRevived(int id) => PlayerRevived?.Invoke(id);
        public static void RaiseReviveCountChanged(int remaining) => ReviveCountChanged?.Invoke(remaining);

        public static void RaiseDamageDealt(int attackerId, float amount, bool friendly) => DamageDealt?.Invoke(attackerId, amount, friendly);
        public static void RaiseHealingDone(int id, float amount) => HealingDone?.Invoke(id, amount);
        public static void RaiseEnemyKilled(int killerId, string enemyId) => EnemyKilled?.Invoke(killerId, enemyId);
        public static void RaiseGoldGained(int id, int amount) => GoldGained?.Invoke(id, amount);
        public static void RaiseExpGained(int id, int amount) => ExpGained?.Invoke(id, amount);
        public static void RaiseSkillUsed(int id, string skillId) => SkillUsed?.Invoke(id, skillId);

        public static void RaiseBossAppeared(string bossId) => BossAppeared?.Invoke(bossId);
        public static void RaiseBossHealthChanged(string bossId, float ratio) => BossHealthChanged?.Invoke(bossId, ratio);
        public static void RaiseBossPhaseChanged(string bossId, int phase) => BossPhaseChanged?.Invoke(bossId, phase);
        public static void RaiseBossEnraged(string bossId) => BossEnraged?.Invoke(bossId);
        public static void RaiseBossDefeated(string bossId) => BossDefeated?.Invoke(bossId);

        public static void RaiseItemAcquired(int id, string itemCode) => ItemAcquired?.Invoke(id, itemCode);
        public static void RaiseItemDropped(int id, string itemCode) => ItemDropped?.Invoke(id, itemCode);

        public static void RaiseRoomEntered(int roomId) => RoomEntered?.Invoke(roomId);
        public static void RaiseRoomCleared(int roomId) => RoomCleared?.Invoke(roomId);
        public static void RaiseRoomDiscovered(int roomId) => RoomDiscovered?.Invoke(roomId);
        public static void RaiseSecretRoomFound(int roomId) => SecretRoomFound?.Invoke(roomId);
        public static void RaisePuzzleSolved(string puzzleId) => PuzzleSolved?.Invoke(puzzleId);
        public static void RaisePuzzleFailed(string puzzleId) => PuzzleFailed?.Invoke(puzzleId);

        public static void RaisePing(int senderId, PingType type, Vector2 worldPos) => PingRaised?.Invoke(senderId, type, worldPos);
        public static void RaiseEmoteUsed(int id, string emoteId) => EmoteUsed?.Invoke(id, emoteId);
        public static void RaiseVoiceSpeakingChanged(int id, bool speaking) => VoiceSpeakingChanged?.Invoke(id, speaking);

        public static void RaiseAchievementUnlocked(string id) => AchievementUnlocked?.Invoke(id);
        public static void RaiseTitleEarned(string id) => TitleEarned?.Invoke(id);
        public static void RaiseStatCounter(string key, int delta) => StatCounter?.Invoke(key, delta);
    }
}
