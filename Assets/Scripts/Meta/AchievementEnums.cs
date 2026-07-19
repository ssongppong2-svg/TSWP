// 근거: 업적 시스템.md — 등급 5단계, 종류 6종, 보상 4종. 이름 시스템.md — 칭호 색상 3종, 획득 경로 4종.
// 근거: ARCHITECTURE.md §4 TSWP.Meta — 이 열거형들은 이 파일 한 곳에만 정의한다.
namespace TSWP.Meta
{
    /// <summary>업적 등급 5단계. 등급이 높을수록 달성 난이도와 보상 가치가 크다.</summary>
    public enum AchievementGrade
    {
        Common,    // 일반
        Rare,      // 희귀
        Heroic,    // 영웅
        Legendary, // 전설
        Developer, // 개발자 — 개발자 이벤트 전용
    }

    /// <summary>업적 종류 6종.</summary>
    public enum AchievementCategory
    {
        Boss,        // 보스 — 첫 처치, 15보스 클리어, 노데스 등
        Job,         // 직업 — 직업별 전용 업적
        Coop,        // 협동 — 함께 달성해야 하는 업적
        Troll,       // 트롤 — 아군 공격, 절벽 밀기 등 웃긴 실수
        Exploration, // 탐험 — 비밀방 발견, 모든 이벤트 조우
        Special,     // 특수 — 조건이 특이한 업적
    }

    /// <summary>업적 보상 4종. 스킨은 추후 지원 예정.</summary>
    public enum AchievementRewardType
    {
        Title,         // 칭호
        ProfileBorder, // 프로필 테두리
        Emote,         // 이모트 (Core.EmoteData 해금)
        Skin,          // 스킨 — TODO: 추후 지원
    }

    /// <summary>
    /// 칭호 표시 색상 3종. 실제 Color 값은 TSWP.Art.TitleColorConfig가 보유한다
    /// (문서: 색상은 추후 변경 가능 — 코드에 하드코딩하지 않는다).
    /// </summary>
    public enum TitleColorType
    {
        Default,   // 기본 — 흰색
        Legendary, // 전설 — 금색
        Developer, // 개발자 — 보라색
    }

    /// <summary>칭호 획득 경로 4종.</summary>
    public enum TitleSource
    {
        Achievement,    // 업적
        Event,          // 이벤트
        Season,         // 시즌
        DeveloperEvent, // 개발자 이벤트
    }
}
