// 근거: 온라인 시스템.md — 로비 공개/비공개, 접속 상태, 채팅 채널, 신고 사유.
// PingType은 TSWP.Core가 소유한다 (ARCHITECTURE.md §5) — 여기서 재정의하지 않는다.
namespace TSWP.Online
{
    /// <summary>로비 공개 범위.</summary>
    public enum LobbyVisibility
    {
        Public,  // 공개 — 자유 참가
        Private, // 비공개 — 초대 또는 방 코드 필요
    }

    /// <summary>플레이어 접속 상태.</summary>
    public enum ConnectionState
    {
        Connected,    // 정상 접속
        Disconnected, // 끊김 — 재접속 대기 (캐릭터 유지)
        Reconnecting, // 재접속 중
        Left,         // 자발적 종료 — 캐릭터 즉시 제거
    }

    /// <summary>채팅 채널.</summary>
    public enum ChatChannel
    {
        All,          // 전체 채팅
        System,       // 시스템 메시지
        ServerNotice, // 서버 알림
    }

    /// <summary>신고 사유 4종.</summary>
    public enum ReportReason
    {
        Abuse,     // 욕설
        Cheating,  // 핵 사용
        BadManner, // 비매너
        Other,     // 기타
    }
}
