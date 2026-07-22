// 근거: 업적 시스템.md — 업적은 게임 진행의 필수 요소가 아니다. 달성 시 칭호/테두리/이모트를 보상으로 준다.
// GameEvents.StatCounter(counterKey, delta) 단일 경로를 구독해 진행도를 집계한다
// (UI 시스템.md — 게임플레이는 UI/메타를 직접 참조하지 않는다).
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Meta
{
    /// <summary>
    /// 업적 진행 집계와 해금 처리. 카운터 키 매칭으로 동작하므로 게임플레이 코드와 결합하지 않는다.
    /// </summary>
    public class AchievementManager : MonoBehaviour
    {
        public static AchievementManager Instance { get; private set; }

        [Header("업적 목록")]
        [SerializeField] private List<AchievementData> achievements = new List<AchievementData>();

        [Header("칭호 목록")]
        [SerializeField] private List<TitleData> titles = new List<TitleData>();

        [Header("이벤트 → 카운터 브릿지")]
        [Tooltip("EnemyKilled/DamageDealt 같은 고수준 이벤트를 표준 counterKey로 변환해 재발행한다. " +
                 "끄면 GameEvents.RaiseStatCounter를 직접 호출하는 키만 집계된다.")]
        [SerializeField] private bool bridgeGameEvents = true;

        private SaveData _save;
        private PlayerIdentity _identity;

        /// <summary>counterKey → 해당 키를 사용하는 업적들 (매 이벤트마다 전체 순회를 피한다).</summary>
        private readonly Dictionary<string, List<AchievementData>> _byCounterKey = new();

        // 누적 피해/회복은 float이므로 정수 단위로 떨어질 때만 카운터를 발행한다(잔여분 보관).
        private float _damageAccum;
        private float _friendlyDamageAccum;
        private float _healAccum;

        /// <summary>진행도가 바뀔 때마다 증가한다. 뷰가 문자열 캐시를 다시 만들 시점 판단에 쓴다.</summary>
        public int ProgressVersion { get; private set; }

        /// <summary>등록된 업적 목록 (읽기 전용) — 뷰가 진행도 목록을 그릴 때 사용.</summary>
        public IReadOnlyList<AchievementData> Achievements => achievements;

        /// <summary>등록된 칭호 목록 (읽기 전용).</summary>
        public IReadOnlyList<TitleData> Titles => titles;

        /// <summary>연결된 플레이어 정체성. BindIdentity 전에는 null.</summary>
        public PlayerIdentity Identity => _identity;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _save = SaveSystem.Load();
            BuildIndex();
        }

        private void Start()
        {
            // 씬에 LocalPlayerIdentity가 있으면 자동 연결한다(없어도 조용히 생략).
            if (_identity != null) return;

            var local = FindAnyObjectByType<LocalPlayerIdentity>();
            if (local != null) BindIdentity(local.Identity);
        }

        private void OnEnable()
        {
            GameEvents.StatCounter += OnStatCounter;

            // 아래 이벤트들은 counterKey를 직접 발행하지 않으므로 여기서 표준 키로 변환해 재발행한다.
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.SecretRoomFound += OnSecretRoomFound;
            GameEvents.EnemyKilled += OnEnemyKilled;
            GameEvents.DamageDealt += OnDamageDealt;
            GameEvents.HealingDone += OnHealingDone;
            GameEvents.ItemAcquired += OnItemAcquired;
            GameEvents.SkillUsed += OnSkillUsed;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.PlayerRevived += OnPlayerRevived;
            GameEvents.RoomCleared += OnRoomCleared;
            GameEvents.PuzzleFailed += OnPuzzleFailed;
        }

        private void OnDisable()
        {
            GameEvents.StatCounter -= OnStatCounter;

            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.SecretRoomFound -= OnSecretRoomFound;
            GameEvents.EnemyKilled -= OnEnemyKilled;
            GameEvents.DamageDealt -= OnDamageDealt;
            GameEvents.HealingDone -= OnHealingDone;
            GameEvents.ItemAcquired -= OnItemAcquired;
            GameEvents.SkillUsed -= OnSkillUsed;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.PlayerRevived -= OnPlayerRevived;
            GameEvents.RoomCleared -= OnRoomCleared;
            GameEvents.PuzzleFailed -= OnPuzzleFailed;
        }

        private void BuildIndex()
        {
            _byCounterKey.Clear();

            for (int i = 0; i < achievements.Count; i++)
            {
                var data = achievements[i];
                if (data == null || string.IsNullOrEmpty(data.counterKey)) continue;

                if (!_byCounterKey.TryGetValue(data.counterKey, out var list))
                {
                    list = new List<AchievementData>();
                    _byCounterKey[data.counterKey] = list;
                }
                list.Add(data);
            }
        }

        /// <summary>플레이어 정체성을 연결한다 (칭호 보상 지급 대상).</summary>
        public void BindIdentity(PlayerIdentity identity)
        {
            _identity = identity;

            // 저장된 보유 칭호 복원
            if (_identity == null) return;
            for (int i = 0; i < _save.ownedTitleIds.Count; i++)
                _identity.GrantTitle(_save.ownedTitleIds[i]);

            if (!string.IsNullOrEmpty(_save.equippedTitleId))
                _identity.EquipTitle(_save.equippedTitleId);
        }

        // ── 진행 집계 ─────────────────────────────────────────────

        private void OnStatCounter(string counterKey, int delta)
        {
            if (delta <= 0 || string.IsNullOrEmpty(counterKey)) return;
            if (!_byCounterKey.TryGetValue(counterKey, out var list)) return;

            for (int i = 0; i < list.Count; i++)
                AddProgress(list[i], delta);
        }

        // ── 이벤트 → 표준 counterKey 브릿지 ───────────────────────
        // 주의: ping.used / emote.used / revive.count / rescue.count / puzzle.solved / puzzle.troll /
        //       consumable.used / item.autorevive 는 게임플레이 코드가 RaiseStatCounter로 직접 발행한다.
        //       여기서 다시 변환하면 이중 집계가 되므로 절대 추가하지 말 것.

        /// <summary>표준 키로 재발행한다. 자기 자신도 StatCounter 구독자이므로 집계는 OnStatCounter가 처리한다.</summary>
        private void Bridge(string counterKey, int delta)
        {
            if (!bridgeGameEvents || delta <= 0) return;
            GameEvents.RaiseStatCounter(counterKey, delta);
        }

        private void OnBossDefeated(string bossId) => Bridge("boss.defeated", 1);
        private void OnSecretRoomFound(int roomId) => Bridge("secretroom.found", 1);
        private void OnEnemyKilled(int killerId, string enemyId) => Bridge("enemy.killed", 1);
        private void OnItemAcquired(int playerId, string itemCode) => Bridge("item.acquired", 1);
        private void OnSkillUsed(int playerId, string skillId) => Bridge("skill.used", 1);
        private void OnPlayerDied(int playerId) => Bridge("player.death", 1);
        private void OnPlayerRevived(int playerId) => Bridge("player.revived", 1);
        private void OnRoomCleared(int roomId) => Bridge("room.cleared", 1);
        private void OnPuzzleFailed(string puzzleId) => Bridge("puzzle.failed", 1);

        /// <summary>누적 피해 — 1 이상 쌓일 때만 정수 단위로 발행하고 소수 잔여분은 보관한다.</summary>
        private void OnDamageDealt(int attackerId, float amount, bool wasFriendly)
        {
            if (amount <= 0f) return;

            if (wasFriendly)
            {
                _friendlyDamageAccum += amount;
                int whole = Mathf.FloorToInt(_friendlyDamageAccum);
                if (whole <= 0) return;

                _friendlyDamageAccum -= whole;
                Bridge("damage.friendly", whole);
                return;
            }

            _damageAccum += amount;
            int dealt = Mathf.FloorToInt(_damageAccum);
            if (dealt <= 0) return;

            _damageAccum -= dealt;
            Bridge("damage.dealt", dealt);
        }

        private void OnHealingDone(int playerId, float amount)
        {
            if (amount <= 0f) return;

            _healAccum += amount;
            int healed = Mathf.FloorToInt(_healAccum);
            if (healed <= 0) return;

            _healAccum -= healed;
            Bridge("heal.done", healed);
        }

        private void AddProgress(AchievementData data, int delta)
        {
            if (data == null || string.IsNullOrEmpty(data.achievementId)) return;

            var progress = _save.GetOrCreate(data.achievementId);
            if (progress.isUnlocked) return;

            progress.currentCount += delta;
            ProgressVersion++;   // 뷰 캐시 무효화

            if (progress.currentCount < data.targetCount) return;

            Unlock(data, progress);
        }

        private void Unlock(AchievementData data, AchievementProgress progress)
        {
            progress.MarkUnlocked();
            ProgressVersion++;
            GrantRewards(data);

            GameEvents.RaiseAchievementUnlocked(data.achievementId);
            SaveSystem.Save(_save);
        }

        private void GrantRewards(AchievementData data)
        {
            for (int i = 0; i < data.rewards.Count; i++)
            {
                var reward = data.rewards[i];
                if (reward == null || string.IsNullOrEmpty(reward.rewardId)) continue;

                switch (reward.type)
                {
                    case AchievementRewardType.Title:
                        if (!_save.ownedTitleIds.Contains(reward.rewardId))
                            _save.ownedTitleIds.Add(reward.rewardId);
                        _identity?.GrantTitle(reward.rewardId);
                        GameEvents.RaiseTitleEarned(reward.rewardId);
                        break;

                    case AchievementRewardType.ProfileBorder:
                        if (!_save.ownedProfileBorderIds.Contains(reward.rewardId))
                            _save.ownedProfileBorderIds.Add(reward.rewardId);
                        break;

                    case AchievementRewardType.Emote:
                        if (!_save.unlockedEmoteIds.Contains(reward.rewardId))
                            _save.unlockedEmoteIds.Add(reward.rewardId);
                        break;

                    case AchievementRewardType.Skin:
                        // TODO: 스킨 시스템 추후 지원 (업적 시스템.md — "스킨(추후)").
                        break;
                }
            }
        }

        // ── 조회 ──────────────────────────────────────────────────

        public AchievementProgress GetProgress(string achievementId) => _save.GetOrCreate(achievementId);

        public bool IsUnlocked(string achievementId) => _save.GetOrCreate(achievementId).isUnlocked;

        public bool IsEmoteUnlocked(string emoteId) => _save.unlockedEmoteIds.Contains(emoteId);

        /// <summary>업적 정의 조회 — UI가 id 대신 displayName을 표시할 때 사용.</summary>
        public AchievementData FindAchievement(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId)) return null;

            for (int i = 0; i < achievements.Count; i++)
            {
                if (achievements[i] != null && achievements[i].achievementId == achievementId)
                    return achievements[i];
            }
            return null;
        }

        /// <summary>칭호 표시 문구 조회. 없으면 null — PlayerIdentity.GetDisplayName에 그대로 넘길 수 있다.</summary>
        public string GetTitleText(string titleId)
        {
            if (string.IsNullOrEmpty(titleId)) return null;

            var title = FindTitle(titleId);
            return title != null ? title.displayText : null;
        }

        public TitleData FindTitle(string titleId)
        {
            for (int i = 0; i < titles.Count; i++)
            {
                if (titles[i] != null && titles[i].titleId == titleId)
                    return titles[i];
            }
            return null;
        }

        /// <summary>칭호 장착 — 보유 중일 때만 성공하며 즉시 저장된다.</summary>
        public bool EquipTitle(string titleId)
        {
            if (_identity == null || !_identity.EquipTitle(titleId)) return false;

            _save.equippedTitleId = titleId;
            SaveSystem.Save(_save);
            return true;
        }
    }
}
