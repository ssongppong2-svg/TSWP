// 근거: 맵 시스템.md / 방 시스템.md — 방 종류 10종, 생물 군계 8종, 구조물 9종.
// 시그니처는 ARCHITECTURE.md §4(TSWP.Map) 계약 그대로 고정한다.
// 환경 요소/위험 요소(EnvironmentElementType)는 별도 enum을 만들지 않고
// TSWP.Combat.HazardType을 재사용한다 (ARCHITECTURE.md §5 — HazardType은 Combat 한 곳에만 정의).
namespace TSWP.Map
{
    /// <summary>
    /// 방 종류 10종. 맵 시스템.md 9종 + 방 시스템.md의 '보스 기믹 연습' 방.
    /// NormalCombat이 가장 많이 등장하고, Boss는 스테이지 마지막 방이다.
    /// </summary>
    public enum RoomType
    {
        Start,        // 시작 방
        NormalCombat, // 일반 전투 방 — 가장 많이 등장, 기본 몬스터, 보상: 경험치/골드/소량 아이템
        Elite,        // 엘리트 방 — 강력한 몬스터, 높은 난이도·높은 보상
        Event,        // 이벤트 방 — 무작위 이벤트(보물/함정/NPC/저주/축복/미니게임/상인), 무시·참여 선택 가능
        Shop,         // 상점 — 골드로 구매, 판매 품목 랜덤, 미니맵 표시
        Rest,         // 휴식 방 — 안전 공간(적 미등장), 체력 회복·팀 정비·보스 준비
        Puzzle,       // 퍼즐 방 — 협동 퍼즐, 실패해도 게임 계속 진행(페널티 없음)
        Secret,       // 비밀방 — 발견 전 미니맵 비표시, 희귀 아이템/특별 이벤트
        BossPractice, // 보스 기믹 연습 방 — 보스전 직전 고정 배치, 다음 보스 핵심 기믹 체험
        Boss,         // 보스 방 — 스테이지 마지막, 처치 시 아이템 3~4개 드롭
    }

    /// <summary>
    /// 생물 군계 8종. 맵당 1개. 각 군계는 고유한 적/퍼즐/함정/배경을 가진다 (BiomeDefinition SO로 데이터화).
    /// 문서상 '예시' 목록이므로 SO 에셋 추가만으로 콘텐츠가 늘어나는 구조를 유지한다.
    /// </summary>
    public enum BiomeType
    {
        Forest,    // 숲
        Cave,      // 동굴
        Snowfield, // 설원
        Desert,    // 사막
        Ruins,     // 폐허
        Castle,    // 성
        Volcano,   // 용암 지대
        Abyss,     // 늪/심연
    }

    /// <summary>
    /// 구조물 9종 (두 문서의 구조물 목록 합집합 — 계약 §4 고정).
    /// 파괴/상호작용/건축가 설치 가능 여부는 StructureDefinition SO가 정의한다.
    /// </summary>
    public enum StructureType
    {
        WoodenCrate,    // 나무 상자 — 일반 공격으로 파괴 가능
        BombCrate,      // 폭탄 상자 — 폭탄 연계 오브젝트
        Chest,          // 상자(보상)
        Door,           // 문
        Lever,          // 레버
        Button,         // 버튼
        PressurePlate,  // 압력 발판(밟는 버튼)
        Ladder,         // 사다리 — 지형 이동 보조
        MovingPlatform, // 움직이는 발판
    }
}
