// 근거: 게임 시작과 선택, 직업, 플레이.md / 조작과 시스템.md / 전투 시스템.md
// 공유 열거형 — 정의 위치는 ARCHITECTURE.md §4·§5 계약을 따른다 (중복 정의 금지).
namespace TSWP.Core
{
    /// <summary>진영. 아군 판정은 레이어가 아닌 이 값 비교로 한다 (아군사격 상시 존재).</summary>
    public enum TeamType
    {
        Players,
        Enemies,
        Neutral,
    }

    /// <summary>난이도 4종. 방장만 선택할 수 있다.</summary>
    public enum Difficulty
    {
        SuperCoward, // 슈퍼 겁쟁이 — 입문자용, 적 능력치 감소·퍼즐 시간 증가
        Human,       // 인간 — 기준 난이도
        God,         // 신 — 적 강화·패턴 추가·보상 증가
        Meme,        // 밈 — 무작위 특수 규칙 적용
    }

    /// <summary>전체 게임 흐름 상태. GameFlowManager 상태 머신이 소유한다.</summary>
    public enum GameFlowState
    {
        MainMenu,
        Lobby,
        Starting,      // 전원 준비 완료 → 시작 전환
        Tutorial,      // 인게임 오버레이 (별도 씬 아님, 스킵 가능)
        StartItemDrop, // 시작 아이템 드롭 — floor(인원×3/5)개, 자유 경쟁
        Exploration,
        BossFight,
        GameOver,      // 부활 소진 + 전원 사망
        Results,
        AfterParty,    // 뒷풀이 — 방장이 다시 플레이/로비 이동 선택
    }

    /// <summary>핑 5종. 마우스 휠 클릭으로 발동.</summary>
    public enum PingType
    {
        Danger, // 위험
        Move,   // 이동
        Item,   // 아이템
        Rally,  // 집합
        Help,   // 도움 요청
    }
}
