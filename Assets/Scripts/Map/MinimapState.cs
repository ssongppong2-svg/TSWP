// 근거: 맵 시스템.md — 미니맵은 탐험한 지역만 표시(전장의 안개), 비밀방은 발견 전까지 비표시.
//   표시 내용: 플레이어, 팀원, 상점, 보스 방, 핑.
// 데이터 전용 — 그리기는 UI 폴더(MinimapModel/뷰)가 GameEvents 구독 + 이 상태 조회로 수행한다.
// SYNC: 호스트 권위, 추후 NGO NetworkVariable — 탐험/발견 집합, 플레이어 마커, 핑은 파티 공유.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Map
{
    /// <summary>미니맵 핑 마커 1개 (PingType은 Core 한 곳에만 정의 — 재정의 금지).</summary>
    public struct MinimapPing
    {
        public int SenderPlayerId;
        public PingType Type;
        public Vector2 WorldPos;
        /// <summary>발동 시각 (Time.time) — UI가 만료 처리에 사용.</summary>
        public float RaisedTime;
    }

    /// <summary>
    /// 미니맵 표시 데이터 (순수 C#). 방 단위 fog-of-war:
    /// 탐험(IsExplored)한 방만 노출, 비밀방은 발견(IsDiscovered) 전까지 데이터 자체에서 제외.
    /// </summary>
    public sealed class MinimapState
    {
        private readonly HashSet<int> _exploredRoomIds = new HashSet<int>();
        private readonly HashSet<int> _secretRoomIds = new HashSet<int>();
        private readonly HashSet<int> _discoveredSecretIds = new HashSet<int>();
        private readonly HashSet<int> _shopRoomIds = new HashSet<int>();

        public int BossRoomId { get; private set; } = -1;

        // NOTE(기획 확인 필요): 문서는 미니맵에 '상점, 보스 방'을 표시한다고 하면서 '탐험한 지역만 표시'라고도 함.
        //   미탐험 상점/보스 방을 미리 보여줄지 여부 — 기본은 미리 표시(길찾기 편의, 테스트 체크리스트 '길찾기가 어렵지 않은가').
        public bool ShowShopAndBossBeforeExplored = true;

        /// <summary>플레이어/팀원 위치 마커 (playerId → 월드 좌표). Player 시스템이 갱신.</summary>
        public readonly Dictionary<int, Vector2> PlayerPositions = new Dictionary<int, Vector2>();

        /// <summary>활성 핑 목록. RoomManager가 GameEvents.PingRaised 구독으로 추가, UI가 만료 제거.</summary>
        public readonly List<MinimapPing> ActivePings = new List<MinimapPing>();

        /// <summary>새 맵 그래프 기준으로 초기화 (스테이지 전환 시 호출).</summary>
        public void BuildFromGraph(MapGraph graph)
        {
            _exploredRoomIds.Clear();
            _secretRoomIds.Clear();
            _discoveredSecretIds.Clear();
            _shopRoomIds.Clear();
            ActivePings.Clear();
            BossRoomId = -1;
            if (graph == null) return;

            foreach (var room in graph.Rooms)
            {
                if (room.IsSecret) _secretRoomIds.Add(room.RoomId);
                if (room.RoomType == RoomType.Shop) _shopRoomIds.Add(room.RoomId);
                if (room.RoomType == RoomType.Boss) BossRoomId = room.RoomId;
                if (room.IsExplored) _exploredRoomIds.Add(room.RoomId); // 재접속 복원 대응
                if (room.IsSecret && room.IsDiscovered) _discoveredSecretIds.Add(room.RoomId);
            }
        }

        /// <summary>방 탐험 기록 (진입 시 RoomManager가 호출).</summary>
        public void MarkExplored(int roomId) => _exploredRoomIds.Add(roomId);

        /// <summary>비밀방 발견 기록 — 이 시점부터 미니맵에 노출된다.</summary>
        public void MarkSecretDiscovered(int roomId)
        {
            if (_secretRoomIds.Contains(roomId))
                _discoveredSecretIds.Add(roomId);
        }

        public bool IsExplored(int roomId) => _exploredRoomIds.Contains(roomId);

        /// <summary>
        /// 미니맵 노출 여부 판정 — UI는 이 질의만 사용한다.
        /// 비밀방은 발견 전 무조건 비표시(탐험 여부와 무관), 그 외에는 탐험한 지역만 표시.
        /// </summary>
        public bool IsRoomVisible(int roomId)
        {
            if (_secretRoomIds.Contains(roomId) && !_discoveredSecretIds.Contains(roomId))
                return false; // 비밀방: 발견 전 비표시 (맵 시스템.md)
            if (_exploredRoomIds.Contains(roomId))
                return true;  // 전장의 안개: 탐험한 지역만 표시
            if (ShowShopAndBossBeforeExplored && (_shopRoomIds.Contains(roomId) || roomId == BossRoomId))
                return true;  // 상점/보스 방 마커 (문서: 미니맵 표시 내용)
            return false;
        }

        /// <summary>현재 노출 대상 방 id 열거 (UI 갱신용).</summary>
        public IEnumerable<int> GetVisibleRoomIds(MapGraph graph)
        {
            if (graph == null) yield break;
            foreach (var room in graph.Rooms)
            {
                if (IsRoomVisible(room.RoomId))
                    yield return room.RoomId;
            }
        }

        /// <summary>플레이어 마커 갱신 (Player 시스템 호출 지점). SYNC: 팀원 위치는 네트워크 동기화 대상.</summary>
        public void SetPlayerPosition(int playerId, Vector2 worldPos) => PlayerPositions[playerId] = worldPos;

        /// <summary>핑 추가 (GameEvents.PingRaised → RoomManager 경유).</summary>
        public void AddPing(int senderPlayerId, PingType type, Vector2 worldPos, float raisedTime)
        {
            ActivePings.Add(new MinimapPing
            {
                SenderPlayerId = senderPlayerId,
                Type = type,
                WorldPos = worldPos,
                RaisedTime = raisedTime,
            });
        }
    }
}
