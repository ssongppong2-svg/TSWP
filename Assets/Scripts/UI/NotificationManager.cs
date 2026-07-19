// 근거: UI 시스템.md — 알림 UI: 업적 달성, 아이템 획득, 보스 등장, 플레이어 사망/부활을 우측 상단에 표시한다.
//       원칙: 필요한 정보만, 전투를 방해하지 않게, 2초 안에 이해할 수 있게.
// 게임플레이는 GameEvents만 발행하고 UI가 구독한다 (단방향).
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.UI
{
    /// <summary>표시 대기 중인 알림 1건.</summary>
    public sealed class NotificationEntry
    {
        public NotificationType Type;
        public string Message;
        public float RemainingSeconds;
    }

    /// <summary>
    /// 토스트 알림 큐. 실제 렌더링은 뷰 컴포넌트가 ActiveNotifications를 읽어 수행한다.
    /// </summary>
    public class NotificationManager : MonoBehaviour
    {
        public static NotificationManager Instance { get; private set; }

        [Header("표시")]
        [Tooltip("알림 1건이 화면에 머무는 시간(초).")]
        [SerializeField, Min(0.5f)] private float displaySeconds = 3f; // TODO(밸런스): 문서 미정

        [Tooltip("동시에 표시할 최대 알림 수 — 전투를 방해하지 않도록 제한한다.")]
        [SerializeField, Min(1)] private int maxVisible = 4;           // TODO(밸런스): 문서 미정

        private readonly List<NotificationEntry> _active = new List<NotificationEntry>();
        private readonly Queue<NotificationEntry> _pending = new Queue<NotificationEntry>();

        public IReadOnlyList<NotificationEntry> ActiveNotifications => _active;

        /// <summary>알림이 추가/제거될 때 발행 — 뷰가 갱신 시점을 안다.</summary>
        public event Action Changed;

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
            GameEvents.AchievementUnlocked += OnAchievementUnlocked;
            GameEvents.ItemAcquired += OnItemAcquired;
            GameEvents.BossAppeared += OnBossAppeared;
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.PlayerRevived += OnPlayerRevived;
        }

        private void OnDisable()
        {
            GameEvents.AchievementUnlocked -= OnAchievementUnlocked;
            GameEvents.ItemAcquired -= OnItemAcquired;
            GameEvents.BossAppeared -= OnBossAppeared;
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.PlayerRevived -= OnPlayerRevived;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool changed = false;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                _active[i].RemainingSeconds -= dt;
                if (_active[i].RemainingSeconds > 0f) continue;

                _active.RemoveAt(i);
                changed = true;
            }

            while (_active.Count < maxVisible && _pending.Count > 0)
            {
                _active.Add(_pending.Dequeue());
                changed = true;
            }

            if (changed) Changed?.Invoke();
        }

        /// <summary>알림 표시 요청.</summary>
        public void Push(NotificationType type, string message)
        {
            var entry = new NotificationEntry
            {
                Type = type,
                Message = message,
                RemainingSeconds = displaySeconds,
            };

            if (_active.Count < maxVisible)
            {
                _active.Add(entry);
                Changed?.Invoke();
            }
            else
            {
                _pending.Enqueue(entry);
            }
        }

        // ── GameEvents 구독 핸들러 ────────────────────────────────
        // TODO(표시): id를 표시 문구로 변환 — AchievementData/ItemDefinition/BossData 조회 후 displayName 사용.

        private void OnAchievementUnlocked(string achievementId)
            => Push(NotificationType.AchievementUnlocked, $"업적 달성: {achievementId}");

        private void OnItemAcquired(int playerId, string itemCode)
            => Push(NotificationType.ItemAcquired, $"아이템 획득: {itemCode}");

        private void OnBossAppeared(string bossId)
            => Push(NotificationType.BossAppeared, $"보스 등장: {bossId}");

        private void OnPlayerDied(int playerId)
            => Push(NotificationType.PlayerDeath, $"플레이어 {playerId} 사망");

        private void OnPlayerRevived(int playerId)
            => Push(NotificationType.PlayerRevived, $"플레이어 {playerId} 부활");
    }
}
