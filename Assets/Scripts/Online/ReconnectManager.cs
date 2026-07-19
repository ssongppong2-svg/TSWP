// 근거: 온라인 시스템.md — 접속이 끊긴 플레이어는 일정 시간 안에 재접속하면 기존 캐릭터로 복귀한다.
//       자발적으로 나간 경우 캐릭터는 즉시 제거된다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Online
{
    /// <summary>끊긴 플레이어의 복귀용 스냅샷.</summary>
    public class ReconnectSession
    {
        public ulong steamId;
        public string displayName;
        public string selectedJobId;

        /// <summary>캐릭터/진행 상황 스냅샷. TODO: 직렬화 대상 확정 (체력/아이템/위치).</summary>
        public object characterSnapshot;

        /// <summary>끊긴 시각 (Time.realtimeSinceStartup 기준).</summary>
        public float disconnectTime;
    }

    /// <summary>
    /// 재접속 대기 관리. steamId를 키로 스냅샷을 보관하고 타임아웃 후 폐기한다.
    /// </summary>
    public class ReconnectManager : MonoBehaviour
    {
        public static ReconnectManager Instance { get; private set; }

        [Header("타임아웃")]
        [Tooltip("이 시간 안에 재접속하면 기존 캐릭터로 복귀한다.")]
        [SerializeField, Min(1f)] private float timeoutSeconds = 120f; // TODO(밸런스): 문서 미정 ("일정 시간")

        private readonly Dictionary<ulong, ReconnectSession> _pending = new();

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
            if (_pending.Count == 0) return;

            float now = Time.realtimeSinceStartup;
            List<ulong> expired = null;

            foreach (var pair in _pending)
            {
                if (now - pair.Value.disconnectTime < timeoutSeconds) continue;
                expired ??= new List<ulong>();
                expired.Add(pair.Key);
            }

            if (expired == null) return;

            for (int i = 0; i < expired.Count; i++)
            {
                _pending.Remove(expired[i]);
                // TODO: 타임아웃된 캐릭터를 월드에서 제거하고 파티에 통지.
            }
        }

        /// <summary>접속이 끊긴 플레이어의 상태를 보관한다 (자발적 종료에는 호출하지 않는다).</summary>
        public void OnDisconnected(LobbyPlayerState player, object snapshot)
        {
            if (player == null) return;
            if (player.connectionState == ConnectionState.Left) return; // 자발적 종료는 즉시 제거

            player.connectionState = ConnectionState.Disconnected;

            _pending[player.steamId] = new ReconnectSession
            {
                steamId = player.steamId,
                displayName = player.displayName,
                selectedJobId = player.selectedJobId,
                characterSnapshot = snapshot,
                disconnectTime = Time.realtimeSinceStartup,
            };
        }

        /// <summary>재접속 시도. 보관된 세션이 있으면 반환하고 대기 목록에서 제거한다.</summary>
        public ReconnectSession TryReconnect(ulong steamId)
        {
            if (!_pending.TryGetValue(steamId, out var session)) return null;

            _pending.Remove(steamId);
            return session;
        }

        public bool IsPending(ulong steamId) => _pending.ContainsKey(steamId);
    }
}
