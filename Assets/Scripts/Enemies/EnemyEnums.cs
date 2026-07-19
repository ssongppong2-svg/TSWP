// 근거: 적 시스템.md — 적 분류 5등급(일반/특수/엘리트/미니보스/보스), 역할군 9종(모든 적은 최소 1개 보유).
// enum 값·순서는 ARCHITECTURE.md §4 계약에 고정되어 있다 — 변경 금지.
using System;

namespace TSWP.Enemies
{
    /// <summary>
    /// 적 등급 5단계. 등급에 따라 패턴 조합 사용 여부(엘리트 이상)·희귀 드롭 배율이 달라진다.
    /// 난이도는 숫자(체력)가 아니라 행동·조합·패턴으로 만든다 (적 시스템.md 설계 철학 ④).
    /// </summary>
    public enum EnemyGrade
    {
        Normal,   // 일반 — 가장 많이 등장, 체력 낮음, 기본 공격만 사용
        Special,  // 특수 — 고유 능력 보유 (힐러/자폭/저격수/소환사/버퍼/디버퍼)
        Elite,    // 엘리트 — 체력 증가·신규 패턴·희귀 드롭 확률 증가·일부 상태이상 면역
        MiniBoss, // 미니 보스 — 보스전 연습 역할, 고유 패턴·특수 공격, 높은 보상
        Boss,     // 보스 — 실제 로직은 TSWP.Bosses(BossData/BossController)가 담당 (분류용)
    }

    /// <summary>
    /// 역할군 9종 — 모든 적은 최소 1개 보유 (EnemyData.OnValidate가 강제).
    /// 문서 대응(계약 §4 고정 명칭): 근접(Melee)/원거리(Ranged)/탱커(Tank)/암살(Assassin)/
    /// 힐러(Healer)/소환(Summoner)/지원(Buffer)/군중 제어 CC(Debuffer)/자폭(SelfDestruct).
    /// 다중 역할 조합(방패병+저격수 등)이 새로운 위협을 만든다 (설계 철학 ③).
    /// </summary>
    [Flags]
    public enum EnemyRole
    {
        None         = 0,
        Melee        = 1,   // 근접
        Ranged       = 2,   // 원거리
        Tank         = 4,   // 탱커
        Assassin     = 8,   // 암살
        Healer       = 16,  // 힐러
        Summoner     = 32,  // 소환
        Buffer       = 64,  // 지원(버프)
        Debuffer     = 128, // 군중 제어(CC)/약화
        SelfDestruct = 256, // 자폭
    }
}
