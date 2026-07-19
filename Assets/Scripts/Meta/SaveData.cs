// 근거: 업적 시스템.md / 이름 시스템.md — 업적 진행도, 보유 칭호, 해금 이모트는 런을 넘어 유지된다.
// JsonUtility 직렬화를 위해 Dictionary 대신 List를 사용한다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Meta
{
    /// <summary>업적 1건의 진행 상태.</summary>
    [Serializable]
    public class AchievementProgress
    {
        public string achievementId;
        public int currentCount;
        public bool isUnlocked;

        /// <summary>해금 시각(UTC ISO 8601 문자열). JsonUtility가 DateTime을 직렬화하지 못해 문자열로 보관한다.</summary>
        public string unlockedAtUtc;

        public void MarkUnlocked()
        {
            isUnlocked = true;
            unlockedAtUtc = DateTime.UtcNow.ToString("o");
        }
    }

    /// <summary>영구 저장 데이터 (계정 단위).</summary>
    [Serializable]
    public class SaveData
    {
        public List<AchievementProgress> achievements = new List<AchievementProgress>();
        public List<string> ownedTitleIds = new List<string>();
        public List<string> unlockedEmoteIds = new List<string>();
        public List<string> ownedProfileBorderIds = new List<string>();
        public string equippedTitleId;
        public string equippedProfileBorderId;

        public AchievementProgress GetOrCreate(string achievementId)
        {
            for (int i = 0; i < achievements.Count; i++)
            {
                if (achievements[i].achievementId == achievementId)
                    return achievements[i];
            }

            var progress = new AchievementProgress { achievementId = achievementId };
            achievements.Add(progress);
            return progress;
        }
    }

    /// <summary>SaveData 저장/로드 유틸.</summary>
    public static class SaveSystem
    {
        private const string FileName = "tswp_save.json";

        private static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static SaveData Load()
        {
            try
            {
                if (!System.IO.File.Exists(FilePath)) return new SaveData();

                string json = System.IO.File.ReadAllText(FilePath);
                return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 저장 데이터 로드 실패 — 새로 시작합니다: {e.Message}");
                return new SaveData();
            }
        }

        public static void Save(SaveData data)
        {
            if (data == null) return;

            try
            {
                string json = JsonUtility.ToJson(data, true);
                System.IO.File.WriteAllText(FilePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] 저장 실패: {e.Message}");
            }
        }
    }
}
