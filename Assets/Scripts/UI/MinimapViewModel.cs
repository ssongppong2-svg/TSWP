// 근거: UI 시스템.md — 미니맵: 현재 위치 / 팀원 위치 / 목표 방향 / 특수 오브젝트 표시,
//   숨겨진 방은 발견 전까지 표시되지 않는다.
// 방 노출 판정 로직은 Map.MinimapState가 소유한다 (중복 구현 금지). 여기서는 표시용 뷰모델만 보관하고,
// 발견/입장 정보는 GameEvents(RoomDiscovered/RoomEntered/SecretRoomFound/PingRaised) 구독으로만 갱신한다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.UI
{
    /// <summary>미니맵 특수 오브젝트 마커 종류.</summary>
    public enum MinimapMarkerType
    {
        Self,
        Teammate,
        Shop,
        Boss,
        SecretRoom,
        Objective,
        Ping,
    }

    /// <summary>미니맵 마커 1개.</summary>
    [Serializable]
    public struct MinimapMarker
    {
        public MinimapMarkerType Type;
        public Vector2 WorldPos;
        /// <summary>플레이어 마커면 playerId, 핑이면 발신자 playerId, 그 외 -1.</summary>
        public int OwnerPlayerId;
        /// <summary>방 단위 마커(비밀방 등)의 방 id. 월드 좌표가 없는 마커는 이 값으로만 위치를 안다. 없으면 -1.</summary>
        public int RoomId;
        /// <summary>핑 마커일 때만 유효 (PingType은 Core 한 곳에만 정의 — 재정의 금지).</summary>
        public PingType PingKind;
        /// <summary>생성 시각(Time.time) — 핑 만료 처리용.</summary>
        public float SpawnTime;
    }

    /// <summary>미니맵 표시용 뷰모델. GameEvents 구독으로만 갱신된다.</summary>
    public sealed class MinimapViewModel
    {
        /// <summary>핑 마커 표시 지속 시간(초). // TODO(밸런스): 문서 미정</summary>
        public float PingLifetime = 5f;

        /// <summary>현재 위치 (로컬 플레이어).</summary>
        public Vector2 SelfPosition;

        /// <summary>팀원 위치 (playerId → 월드 좌표). // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        public readonly Dictionary<int, Vector2> TeammatePositions = new Dictionary<int, Vector2>();

        /// <summary>목표 방향 (정규화 벡터). 보스 방/다음 목표 방을 가리킨다.</summary>
        public Vector2 ObjectiveDirection;

        /// <summary>특수 오브젝트 + 핑 마커 목록.</summary>
        public readonly List<MinimapMarker> Markers = new List<MinimapMarker>();

        /// <summary>발견(표시 허용)된 방 id 집합. 비밀방은 SecretRoomFound 전까지 들어오지 않는다.</summary>
        public readonly HashSet<int> DiscoveredRoomIds = new HashSet<int>();

        /// <summary>현재 방 id.</summary>
        public int CurrentRoomId = -1;

        public event Action Changed;

        private bool _subscribed;

        public void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            GameEvents.RoomDiscovered += OnRoomDiscovered;
            GameEvents.RoomEntered += OnRoomEntered;
            GameEvents.SecretRoomFound += OnSecretRoomFound;
            GameEvents.PingRaised += OnPingRaised;
        }

        public void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            GameEvents.RoomDiscovered -= OnRoomDiscovered;
            GameEvents.RoomEntered -= OnRoomEntered;
            GameEvents.SecretRoomFound -= OnSecretRoomFound;
            GameEvents.PingRaised -= OnPingRaised;
        }

        private void OnRoomDiscovered(int roomId)
        {
            if (DiscoveredRoomIds.Add(roomId)) Changed?.Invoke();
        }

        private void OnRoomEntered(int roomId)
        {
            CurrentRoomId = roomId;
            DiscoveredRoomIds.Add(roomId);
            Changed?.Invoke();
        }

        // 비밀방은 발견 시점부터만 미니맵에 노출된다 (UI 시스템.md).
        private void OnSecretRoomFound(int roomId)
        {
            DiscoveredRoomIds.Add(roomId);
            Markers.Add(new MinimapMarker
            {
                Type = MinimapMarkerType.SecretRoom,
                OwnerPlayerId = -1,
                // SecretRoomFound는 roomId만 전달한다(월드 좌표 없음) — 위치는 방 그래프에서 찾는다.
                RoomId = roomId,
                SpawnTime = Time.time,
            });
            Changed?.Invoke();
        }

        private void OnPingRaised(int senderId, PingType type, Vector2 worldPos)
        {
            Markers.Add(new MinimapMarker
            {
                Type = MinimapMarkerType.Ping,
                WorldPos = worldPos,
                OwnerPlayerId = senderId,
                RoomId = CurrentRoomId,
                PingKind = type,
                SpawnTime = Time.time,
            });
            Changed?.Invoke();
        }

        /// <summary>만료된 핑 마커 제거. 미니맵 뷰가 매 프레임 호출한다.</summary>
        public void TickExpirePings(float now)
        {
            bool removed = false;
            for (int i = Markers.Count - 1; i >= 0; i--)
            {
                if (Markers[i].Type != MinimapMarkerType.Ping) continue;
                if (now - Markers[i].SpawnTime < PingLifetime) continue;
                Markers.RemoveAt(i);
                removed = true;
            }
            if (removed) Changed?.Invoke();
        }

        /// <summary>팀원 위치 갱신 (Player 시스템 → UI 단방향).</summary>
        public void SetTeammatePosition(int playerId, Vector2 worldPos)
        {
            TeammatePositions[playerId] = worldPos;
            Changed?.Invoke();
        }

        public void Clear()
        {
            TeammatePositions.Clear();
            Markers.Clear();
            DiscoveredRoomIds.Clear();
            CurrentRoomId = -1;
            Changed?.Invoke();
        }
    }
}
