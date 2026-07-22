// 근거: 보스 시스템.md — 보스 유형 4종(1~3개 조합), 전투 흐름 7단계, 기믹 예시 7종,
//       협동 퍼즐 예시 5종, 심리형 효과 6종, 권장 패턴 구성 5종.
// 환경 해저드는 Combat.HazardType 한 곳에만 정의한다 — 여기서 재정의 금지 (ARCHITECTURE.md §5).
using System;

namespace TSWP.Bosses
{
    /// <summary>
    /// 보스 유형 4종. 모든 보스는 최소 1개, 최대 3개의 유형을 조합해 가진다 (BossData.OnValidate로 강제).
    /// </summary>
    [Flags]
    public enum BossType
    {
        Combat = 1,        // 전투형 — 회피/공격 타이밍/패턴 숙련도를 시험. 실력 비중이 높다
        Puzzle = 2,        // 퍼즐형 — 협동 퍼즐을 해결해야만 보스에게 피해를 줄 수 있다
        Environment = 4,   // 환경형 — 맵 자체가 계속 변화 (다리 붕괴/용암 상승/물 범람/독 안개/낙석/벽 생성)
        Psychological = 8, // 심리형 — 혼란과 기만 (가짜 아이템/위치 변경/시야 제한/음성 교란/분신/위장)
    }

    /// <summary>
    /// 보스전 전투 흐름 7단계. BossController의 상태 머신이 순서대로 전이한다.
    /// 광폭화(Enrage)는 '필요 시'에만 — 광폭화 없는 보스는 PatternChange에서 곧장 처치 연출로 간다.
    /// </summary>
    public enum BossFightPhase
    {
        Intro,          // ① 보스 등장 연출
        NormalPattern,  // ② 일반 패턴
        CoopPuzzle,     // ③ 협동 퍼즐
        PatternChange,  // ④ 패턴 변화
        Enrage,         // ⑤ 광폭화 (필요 시)
        DeathCinematic, // ⑥ 처치 연출
        Reward,         // ⑦ 보상 획득 (3~4개 아이템 드롭, 선취득)
    }

    /// <summary>핵심 기믹 예시 7종. 모든 보스는 최소 1개의 핵심 기믹을 가진다. 난이도 변경 시에도 기믹은 불변.</summary>
    public enum GimmickType
    {
        WeakPointExpose, // 약점 노출
        ButtonActivate,  // 버튼 작동
        StructureDestroy,// 구조물 파괴
        Reposition,      // 위치 이동
        RoleSplit,       // 역할 분담
        BombUse,         // 폭탄 사용
        Reflect,         // 반사
    }

    /// <summary>협동 퍼즐 유형 5종. 모든 보스는 협동 퍼즐을 반드시 포함하며, 혼자 해결하기 어렵도록 설계한다.</summary>
    public enum CoopPuzzleType
    {
        SimultaneousLevers,  // 동시에 레버 당기기
        PlateHold,           // 발판 유지
        BombRelay,           // 폭탄 전달
        ProtectAlly,         // 팀원 보호
        MultiPositionAttack, // 여러 위치에서 동시에 공격
    }

    /// <summary>
    /// 심리형 보스 전용 효과 6종.
    /// 음성 교란은 보이스챗(Vivox) 연동 필요 — TODO(Online): 클라이언트 로컬 연출과 호스트 상태 구분 설계.
    /// </summary>
    public enum PsychologicalEffectType
    {
        FakeItem,          // 가짜 아이템 생성
        SwapAllyPositions, // 팀원 위치 변경
        VisionLimit,       // 시야 제한
        VoiceScramble,     // 음성 교란
        CloneCreate,       // 분신 생성
        AllyDisguise,      // 아군처럼 위장
    }

    /// <summary>
    /// 보스전 시작 트리거. 문서 규정이 아니라 '씬에 놓기만 해도 전투가 굴러가게' 하는 배선 옵션이다.
    /// 정식 흐름은 방 시스템(RoomInstance.StartContent)이 BeginFight를 호출하는 Manual이다.
    /// </summary>
    public enum BossStartTrigger
    {
        Manual,          // 외부가 BeginFight()를 호출할 때까지 대기 (방 시스템 연동 시 이 값)
        PlayerProximity, // 감지 반경 안에 살아있는 플레이어가 들어오면 스스로 시작
        Immediate,       // 씬 시작 즉시 시작 (보스 단독 테스트 씬용)
    }

    /// <summary>권장 패턴 구성 5종. 모든 보스는 최소 5개의 행동 패턴을 가진다 (BossData.OnValidate로 강제).</summary>
    public enum BossPatternCategory
    {
        BasicAttack, // 일반 공격
        AreaAttack,  // 범위 공격
        Movement,    // 이동기
        SpecialSkill,// 특수 기술
        CoopGimmick, // 협동 기믹
    }
}
