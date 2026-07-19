// 근거: 아이템 시스템.md(희귀도/획득 방법/중복) / 아이템 시트.md(체크박스 항목) / 팔레트 시스템.md(개발자 등급)
// 아이템 관련 열거형 — 정의 위치는 ARCHITECTURE.md §4·§5 계약을 따른다 (ItemRarity는 여기 한 곳, Art는 색상 매핑만 참조).
using System;

namespace TSWP.Items
{
    /// <summary>희귀도 6단계. 설계 문서상 정식 획득 희귀도는 Common~Legendary 5단계이며,
    /// Developer는 팔레트 시스템.md의 개발자 전용 등급(무지개색)이다 — 무작위 드롭 풀에서 제외.</summary>
    public enum ItemRarity
    {
        Common,    // 일반 (회색)
        Uncommon,  // 고급 (초록)
        Rare,      // 희귀 (파랑)
        Epic,      // 영웅 (보라)
        Legendary, // 전설 (금색)
        Developer, // 개발자 (무지개) — 정식 드롭 제외
    }

    /// <summary>아이템 종류 5종 — 아이템 시트의 '아이템 종류' 택일 항목.</summary>
    public enum ItemType
    {
        Weapon,     // 무기
        Armor,      // 방어구
        Accessory,  // 장신구
        Consumable, // 소비 아이템 — 일회성, 사용 후 소멸
        Relic,      // 유물 — 매우 희귀, 플레이 방식을 크게 바꿈
    }

    /// <summary>획득 방법 8경로 — 아이템 시트에서 체크박스 복수 지정이므로 [Flags] 비트마스크.</summary>
    [Flags]
    public enum AcquisitionMethod
    {
        None = 0,
        StartingItem = 1,        // 시작 아이템
        NormalMonster = 2,       // 일반 몬스터
        EliteMonster = 4,        // 엘리트 몬스터
        Boss = 8,                // 보스 (3~4개 공용 드롭)
        Event = 16,              // 이벤트
        Shop = 32,               // 상점
        SecretRoom = 64,         // 비밀방
        SpecialObject = 128,     // 특수 오브젝트
    }

    /// <summary>중복 획득 시 효과 적용 방식 — 아이템마다 개별 설정한다.</summary>
    public enum StackingBehavior
    {
        EffectStack,      // 효과 중첩
        DurationIncrease, // 지속시간 증가
        DamageIncrease,   // 피해량 증가
        MaxStackLimited,  // 최대 중첩 제한
    }
}
