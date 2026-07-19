// 근거: 온라인 시스템.md — 채팅은 전체 채팅과 시스템 메시지/서버 알림을 지원한다.
//       단, 주 소통 수단은 음성 채팅이며 채팅은 보조 수단이다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Online
{
    /// <summary>채팅 메시지 1건.</summary>
    [Serializable]
    public class ChatMessage
    {
        public ChatChannel channel = ChatChannel.All;

        /// <summary>발신자. System/ServerNotice 채널은 0(없음).</summary>
        public ulong senderPlayerId;

        public string senderName;
        public string text;

        /// <summary>수신 시각(UTC ISO 8601).</summary>
        public string timestampUtc;
    }

    /// <summary>
    /// 채팅 송수신. 실제 전송은 네트워크 도입 시 연결한다.
    /// </summary>
    public class ChatSystem : MonoBehaviour
    {
        public static ChatSystem Instance { get; private set; }

        [Header("보관")]
        [Tooltip("화면에 유지할 최대 메시지 수.")]
        [SerializeField, Min(10)] private int maxHistory = 100;

        private readonly List<ChatMessage> _history = new List<ChatMessage>();

        public IReadOnlyList<ChatMessage> History => _history;

        /// <summary>새 메시지가 도착했을 때 발행 — 채팅 UI가 구독한다.</summary>
        public event Action<ChatMessage> MessageReceived;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>플레이어가 메시지를 보낸다. // TODO(NGO): ServerRpc로 브로드캐스트.</summary>
        public void SendChat(ulong senderPlayerId, string senderName, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            Receive(new ChatMessage
            {
                channel = ChatChannel.All,
                senderPlayerId = senderPlayerId,
                senderName = senderName,
                text = text,
                timestampUtc = DateTime.UtcNow.ToString("o"),
            });
        }

        /// <summary>시스템 메시지(입장/퇴장/사망 등).</summary>
        public void SendSystem(string text) => SendNotice(ChatChannel.System, text);

        /// <summary>서버 알림(점검/공지 등).</summary>
        public void SendServerNotice(string text) => SendNotice(ChatChannel.ServerNotice, text);

        private void SendNotice(ChatChannel channel, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            Receive(new ChatMessage
            {
                channel = channel,
                senderPlayerId = 0,
                text = text,
                timestampUtc = DateTime.UtcNow.ToString("o"),
            });
        }

        /// <summary>메시지 수신 처리. // TODO(NGO): ClientRpc 수신 지점에서 호출.</summary>
        public void Receive(ChatMessage message)
        {
            if (message == null) return;

            _history.Add(message);
            if (_history.Count > maxHistory)
                _history.RemoveAt(0);

            MessageReceived?.Invoke(message);
        }
    }
}
