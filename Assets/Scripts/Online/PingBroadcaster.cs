// 근거: 온라인 시스템.md / 조작과 시스템.md — 핑 시스템(위험/이동/아이템/집합/도움 요청).
//       음성이 불편한 상황에서도 최소한의 협동이 가능해야 한다.
// PingType은 TSWP.Core가 소유한다 — 재정의하지 않는다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Online
{
    /// <summary>월드에 표시되는 핑 마커 1건.</summary>
    public class PingMarker
    {
        public PingType type;
        public Vector2 worldPosition;
        public ulong senderPlayerId;
        public float remainingSeconds;
    }

    /// <summary>
    /// 핑 송수신. 로컬 발신은 GameEvents로 알리고, 네트워크 전파는 도입 시 연결한다.
    /// </summary>
    public class PingBroadcaster : MonoBehaviour
    {
        public static PingBroadcaster Instance { get; private set; }

        [Header("표시")]
        [Tooltip("핑 마커가 유지되는 시간(초).")]
        [SerializeField, Min(0.5f)] private float markerLifetime = 5f; // TODO(밸런스): 문서 미정

        [Tooltip("같은 플레이어의 연속 핑 최소 간격(초) — 도배 방지.")]
        [SerializeField, Min(0f)] private float cooldownSeconds = 0.5f; // TODO(밸런스): 문서 미정

        private readonly List<PingMarker> _markers = new List<PingMarker>();
        private readonly Dictionary<ulong, float> _lastPingTime = new();

        public IReadOnlyList<PingMarker> Markers => _markers;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _markers.Count - 1; i >= 0; i--)
            {
                _markers[i].remainingSeconds -= dt;
                if (_markers[i].remainingSeconds <= 0f)
                    _markers.RemoveAt(i);
            }
        }

        /// <summary>핑 발신. // TODO(NGO): ServerRpc → ClientRpc로 전파.</summary>
        public bool SendPing(ulong senderPlayerId, PingType type, Vector2 worldPosition)
        {
            float now = Time.time;
            if (_lastPingTime.TryGetValue(senderPlayerId, out float last) && now - last < cooldownSeconds)
                return false;

            _lastPingTime[senderPlayerId] = now;

            AddMarker(senderPlayerId, type, worldPosition);

            // UI/통계는 GameEvents로만 통지한다 (단방향).
            GameEvents.RaisePing((int)senderPlayerId, type, worldPosition);
            GameEvents.RaiseStatCounter("ping.used", 1);
            return true;
        }

        /// <summary>원격 핑 수신. // TODO(NGO): ClientRpc 수신 지점.</summary>
        public void ReceivePing(ulong senderPlayerId, PingType type, Vector2 worldPosition)
            => AddMarker(senderPlayerId, type, worldPosition);

        private void AddMarker(ulong senderPlayerId, PingType type, Vector2 worldPosition)
        {
            _markers.Add(new PingMarker
            {
                type = type,
                worldPosition = worldPosition,
                senderPlayerId = senderPlayerId,
                remainingSeconds = markerLifetime,
            });
        }
    }
}
