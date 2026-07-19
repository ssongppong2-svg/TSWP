// 근거: 맵 시스템.md — "각 방은 통로로 연결된다". 방 그래프의 간선(통로) 데이터.
// 순수 C# — 씬과 무관. 시드 재생성만으로 전 클라이언트가 동일 간선을 얻는다.
using System;

namespace TSWP.Map
{
    /// <summary>
    /// 방을 잇는 통로(그래프 간선). From→To는 DAG 진행 방향(시작→보스)이다.
    /// 플레이어 이동 자체는 양방향 허용 — 방향성은 생성/검증(모든 경로 보스 도달)에만 쓴다.
    /// </summary>
    [Serializable]
    public struct RoomConnection
    {
        public int FromRoomId;
        public int ToRoomId;

        public RoomConnection(int fromRoomId, int toRoomId)
        {
            FromRoomId = fromRoomId;
            ToRoomId = toRoomId;
        }
    }
}
