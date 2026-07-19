// 근거: 업적 시스템.md — 업적은 게임 진행의 필수 요소가 아니다. 수집과 도전의 재미를 위한 시스템이다.
//       카운트형 조건(예: 100회 부활)과 단발형 조건이 있으며, 보상으로 칭호/테두리/이모트/(추후)스킨을 준다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Meta
{
    /// <summary>업적 보상 1건.</summary>
    [Serializable]
    public class AchievementReward
    {
        public AchievementRewardType type = AchievementRewardType.Title;

        [Tooltip("지급할 칭호/테두리/이모트/스킨 식별자.")]
        public string rewardId;
    }

    /// <summary>
    /// 업적 정의 SO. 진행 판정은 AchievementManager가 GameEvents.StatCounter를 구독해 수행한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Meta/Achievement", fileName = "Achievement_")]
    public class AchievementData : ScriptableObject
    {
        [Header("식별")]
        public string achievementId;
        public string displayName;
        [TextArea] public string description;

        [Header("분류")]
        public AchievementGrade grade = AchievementGrade.Common;
        public AchievementCategory category = AchievementCategory.Special;

        [Header("달성 조건")]
        [Tooltip("GameEvents.StatCounter의 counterKey. 예: revive.count / ping.used / heal.count / boss.defeated / secretroom.found / puzzle.troll")]
        public string counterKey;

        [Tooltip("목표 횟수. 단발형 업적은 1.")]
        [Min(1)] public int targetCount = 1;

        [Header("표시")]
        public Sprite icon;

        [Tooltip("달성 전까지 내용을 숨기는 히든 업적인지.")]
        public bool isHidden;

        [Header("보상")]
        public List<AchievementReward> rewards = new List<AchievementReward>();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(achievementId))
                Debug.LogWarning($"[AchievementData] '{name}': achievementId가 비어 있습니다.", this);

            if (string.IsNullOrEmpty(counterKey))
                Debug.LogWarning($"[AchievementData] '{name}': counterKey가 비어 있어 진행도가 집계되지 않습니다.", this);

            // 개발자 등급은 개발자 이벤트 전용 — 일반 획득 경로가 있으면 안 된다.
            if (grade == AchievementGrade.Developer && category != AchievementCategory.Special)
                Debug.LogWarning($"[AchievementData] '{name}': 개발자 등급은 Special 분류를 권장합니다.", this);
        }
#endif
    }
}
