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

        private SaveData _save;
        private PlayerIdentity _identity;

        /// <summary>counterKey → 해당 키를 사용하는 업적들 (매 이벤트마다 전체 순회를 피한다).</summary>
        private readonly Dictionary<string, List<AchievementData>> _byCounterKey = new();

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

        private void OnEnable()
        {
            GameEvents.StatCounter += OnStatCounter;
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.SecretRoomFound += OnSecretRoomFound;
        }

        private void OnDisable()
        {
            GameEvents.StatCounter -= OnStatCounter;
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.SecretRoomFound -= OnSecretRoomFound;
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

        // 보스 처치/비밀방 발견은 전용 이벤트가 있으므로 카운터 키로 변환해 재사용한다.
        private void OnBossDefeated(string bossId) => OnStatCounter("boss.defeated", 1);
        private void OnSecretRoomFound(int roomId) => OnStatCounter("secretroom.found", 1);

        private void AddProgress(AchievementData data, int delta)
        {
            if (data == null) return;

            var progress = _save.GetOrCreate(data.achievementId);
            if (progress.isUnlocked) return;

            progress.currentCount += delta;
            if (progress.currentCount < data.targetCount) return;

            Unlock(data, progress);
        }

        private void Unlock(AchievementData data, AchievementProgress progress)
        {
            progress.MarkUnlocked();
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
