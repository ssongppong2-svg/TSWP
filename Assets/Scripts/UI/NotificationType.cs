// 근거: UI 시스템.md — 알림은 화면 우측 상단에 표시.
//   예시 5종: 업적 달성 / 아이템 획득 / 보스 등장 / 플레이어 사망 / 플레이어 부활.
// 문서상 '예시'이므로 확장 가능하게 두되, 현재 확정된 5종만 정의한다 (ARCHITECTURE.md §4).
using System;
using UnityEngine;

namespace TSWP.UI
{
    /// <summary>알림 종류 5종.</summary>
    public enum NotificationType
    {
        AchievementUnlocked, // 업적 달성
        ItemAcquired,        // 아이템 획득
        BossAppeared,        // 보스 등장
        PlayerDeath,         // 플레이어 사망
        PlayerRevived,       // 플레이어 부활
    }

    /// <summary>토스트 알림 1건. NotificationManager 큐의 원소.</summary>
    [Serializable]
    public sealed class NotificationEntry
    {
        public NotificationType Type;
        /// <summary>표시 문구. 아이콘/색상은 뷰가 Type으로 결정한다 (Art.UIColorConfig 연동 예정).</summary>
        public string Message;
        /// <summary>선택 아이콘 (업적/아이템 아이콘 등). 없으면 뷰가 Type 기본 아이콘 사용.</summary>
        public Sprite Icon;
        /// <summary>발행 시각 (Time.unscaledTime) — 만료 판정용.</summary>
        public float RaisedTime;
        /// <summary>연관 대상 식별자 (achievementId / itemCode / bossId / playerId 문자열).</summary>
        public string SourceId;

        public NotificationEntry(NotificationType type, string message, string sourceId, Sprite icon, float raisedTime)
        {
            Type = type;
            Message = message;
            SourceId = sourceId;
            Icon = icon;
            RaisedTime = raisedTime;
        }
    }
}
