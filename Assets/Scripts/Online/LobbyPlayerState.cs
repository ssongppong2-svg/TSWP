// 근거: 온라인 시스템.md — 로비에서 직업 선택과 준비 상태를 변경한다. 방장은 난이도 선택과 시작 권한을 가진다.
using System;

namespace TSWP.Online
{
    /// <summary>로비 내 플레이어 1명의 상태.</summary>
    [Serializable]
    public class LobbyPlayerState
    {
        /// <summary>네트워크 클라이언트 ID. // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        public ulong playerId;

        /// <summary>Steam ID — 재접속 매칭 키로도 사용한다.</summary>
        public ulong steamId;

        /// <summary>표시 이름 (Steam 닉네임, 게임 내 변경 불가).</summary>
        public string displayName;

        /// <summary>선택한 직업 id. 중복 선택이 허용되므로 배타 검증을 하지 않는다.</summary>
        public string selectedJobId;

        public bool isReady;
        public bool isHost;

        public ConnectionState connectionState = ConnectionState.Connected;
    }
}
