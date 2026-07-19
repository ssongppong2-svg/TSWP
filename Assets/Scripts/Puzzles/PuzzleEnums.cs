// 근거: 퍼즐 시스템.md — 퍼즐 10종 분류 / 실패 불이익 / 보상 6종 / 실패 연출 5종 / 리커버리 4종 / 난이도 3단계 / 환경 요소 7종 / 트롤 결과 예시.
// 근거: ARCHITECTURE.md §4 TSWP.Puzzles — PuzzleType·PuzzleState는 이 파일 한 곳에만 정의한다 (§5 중복 정의 금지, Map/Bosses는 참조만).
namespace TSWP.Puzzles
{
    /// <summary>
    /// 퍼즐 유형 10종. 값 목록은 ARCHITECTURE.md §4 고정 시그니처를 그대로 따른다.
    /// NOTE(기획 확인 필요): 퍼즐 시스템.md의 분류는 (버튼/레버/발판/점프/폭탄/구조물/시간제한/운반/환경/보스)이고
    ///   계약 enum은 (…/Timed/BombRelay/Mixed)라 '환경'·'보스' 항목이 직접 대응하지 않는다.
    ///   계약이 우선이므로 값은 계약을 따르고, 부족분은 데이터로 표현한다:
    ///   - 시간 제한 퍼즐 → Timed (또는 임의 유형 + PuzzleDefinition.hasTimeLimit)
    ///   - 환경 퍼즐      → PuzzleDefinition.environmentElements 목록으로 표현
    ///   - 보스 퍼즐      → BossPuzzleLink 데이터로 보스와 연결 (유형은 실제 조작 방식으로 지정)
    ///   - BombRelay(폭탄 전달)/Mixed(복합)는 문서 '스트리머 포인트: 폭탄 전달 중 실수', 후반 복합 퍼즐에 해당.
    /// </summary>
    public enum PuzzleType
    {
        Button,        // 버튼 퍼즐 — 여러 버튼 동시 누름
        Lever,         // 레버 퍼즐 — 정해진 순서 또는 동시 조작
        PressurePlate, // 발판 퍼즐 — 발판을 유지해야 문이 열린다
        Jump,          // 점프 퍼즐 — 정확한 점프와 타이밍
        Bomb,          // 폭탄 퍼즐 — 벽/구조물 파괴
        Structure,     // 구조물 퍼즐 — 상자 밀기/구조물 설치로 길 만들기
        Carry,         // 운반 퍼즐 — 오브젝트를 목적지까지 운반 (운반 중 피격 가능)
        Timed,         // 시간 제한 퍼즐 — 제한시간 내 해결
        BombRelay,     // 폭탄 전달(릴레이) 퍼즐 — 폭탄을 팀원에게 넘겨 목표까지 운반
        Mixed,         // 복합 퍼즐 — 후반 구간(보스 기믹 + 환경 + 시간 제한) 조합형
    }

    /// <summary>
    /// 퍼즐 런타임 상태. Failed에서 GameOver로 가는 전이는 만들지 않는다
    /// (퍼즐 시스템.md — "즉시 게임 오버는 지양한다", 제작 금지 사항 "실패 시 즉시 게임 오버").
    /// 전이: Idle → Active → (Solved | Failed) ; Failed → Recovering → Active(재도전).
    /// </summary>
    public enum PuzzleState
    {
        Idle,       // 미시작
        Active,     // 진행 중
        Solved,     // 해결 (종료 상태)
        Failed,     // 실패 — 불이익 적용, 진행 차단 금지
        Recovering, // 리커버리 대기 — 재도전 준비 (소프트락 금지 불변식의 통로)
    }

    /// <summary>
    /// 퍼즐 실패 불이익 9종. 문서의 실패 결과 4종 + 시간 제한 실패 결과 + 트롤 결과 예시를 합친 목록.
    /// GameOver 값은 의도적으로 두지 않는다 (문서상 금지 — 값이 없으면 코드로도 만들 수 없다).
    /// </summary>
    public enum PuzzleFailurePenalty
    {
        SpawnEnemies,       // 적(몬스터) 추가 등장/소환 — 트롤: 버튼을 잘못 누름
        ActivateTrap,       // 함정 작동/활성화 — 트롤: 레버를 반대로 당김
        ReduceReward,       // 보상 감소
        ResetPuzzle,        // 퍼즐 초기화
        DamageHealth,       // 체력 감소
        LoseReward,         // 일부 보상 소실
        CollapseBridge,     // 다리 붕괴 — 트롤: 폭탄을 잘못 던짐
        LockDoor,           // 문이 닫히고 모두 갇힘 — 트롤: 발판에서 내려옴
        RevealHiddenEnemy,  // 숨겨진 적 등장 — 트롤: 상자를 잘못 밀기
    }

    /// <summary>퍼즐 해결 보상 6종.</summary>
    public enum PuzzleRewardType
    {
        Item,        // 아이템
        Gold,        // 골드
        SecretRoom,  // 비밀방 개방
        Event,       // 이벤트 발생
        Shortcut,    // 지름길 개방
        HiddenChest, // 숨겨진 상자
    }

    /// <summary>
    /// 실패 연출 5종. 트롤 원칙 ④ "같은 실수를 반복하지 않도록 피드백을 제공한다" —
    /// 실패 원인을 즉시 이해시키는 것이 목적이다.
    /// </summary>
    public enum FailureFeedbackType
    {
        ScreenShake, // 화면 흔들림
        SoundEffect, // 효과음
        WarningIcon, // 경고 아이콘
        NpcReaction, // NPC 반응
        BossLaugh,   // 보스의 웃음
    }

    /// <summary>
    /// 리커버리(재도전) 방식 4종. "게임 진행이 막혀서는 안 된다" — 어떤 값이든 재도전 경로를 보장해야 한다.
    /// </summary>
    public enum RecoveryMethod
    {
        WaitTimer,        // 일정 시간 대기 후 재도전
        SpawnNewLever,    // 새로운 레버 생성
        ResetButtons,     // 버튼 초기화
        OpenAlternatePath,// 다른 경로 개방
    }

    /// <summary>
    /// 퍼즐 난이도 곡선 3단계 (등장 구간). Core.Difficulty(난이도 선택)와는 별개 축이다.
    /// </summary>
    public enum DifficultyPhase
    {
        Early, // 초반 — 기본 조작 학습
        Mid,   // 중반 — 협동과 역할 분담
        Late,  // 후반 — 보스 기믹 + 환경 + 시간 제한
    }

    /// <summary>퍼즐이 활용하는 환경 요소 7종 (환경 활용 목록).</summary>
    public enum EnvironmentElementType
    {
        BreakableBridge, // 부서지는 다리
        MovingPlatform,  // 움직이는 발판
        Water,           // 물
        Lava,            // 용암
        Wind,            // 바람
        FallingRock,     // 낙석
        Explosive,       // 폭발물
    }

    /// <summary>
    /// 트롤(오조작) 동작 분류. 문서의 트롤 결과 예시 5종에 대응한다.
    /// 각 퍼즐 요소는 '정답 동작'과 이 '오답 동작' 두 갈래 결과를 가진다.
    /// </summary>
    public enum WrongActionType
    {
        WrongButtonPress, // 버튼을 잘못 누름     → 몬스터 추가 소환
        ReverseLeverPull, // 레버를 반대로 당김   → 문 대신 함정이 열림
        WrongBombThrow,   // 폭탄을 잘못 던짐     → 다리가 무너짐
        LeavePlate,       // 발판에서 내려옴      → 문이 닫히고 모두 갇힘
        WrongBoxPush,     // 상자를 잘못 밀기     → 숨겨진 적 등장
        DropCarryObject,  // 운반물을 떨어뜨림    → 운반 퍼즐 진행도 손실 (피격/실수)
    }

    /// <summary>
    /// 퍼즐이 요구/우대하는 직업 능력. 직업 enum은 만들지 않는다(ARCHITECTURE.md §5 — jobId 문자열 주도).
    /// 각 값은 TSWP.Player의 능력 인터페이스에 1:1 대응하며, 퍼즐은 직업이 아니라 능력만 질의한다.
    ///   PlatformBuild=IPlatformBuilder / WallBreak=IWallBreaker / TrapBlock=ITrapBlocker
    ///   RangedActivate=IRangedActivator / PoisonSupport=IPoisonSupport
    /// 문서: "특정 직업이 없어도 다른 방법으로 해결 가능해야 한다" — 이 값은 '필수'가 아닌 '우대'다.
    /// </summary>
    [System.Flags]
    public enum PuzzleAbility
    {
        None           = 0,
        PlatformBuild  = 1,  // 건축가 — 발판 설치
        WallBreak      = 2,  // 폭탄마 — 벽 파괴
        TrapBlock      = 4,  // 방패병 — 함정 막기
        RangedActivate = 8,  // 궁수 — 먼 거리 스위치 작동
        PoisonSupport  = 16, // 의사 — 독 지역에서 팀 생존 지원
    }

    /// <summary>보스 퍼즐 성공 시 효과. "보스에게 큰 피해를 주거나 다음 페이즈로 진행할 수 있다".</summary>
    public enum BossPuzzleSuccessEffect
    {
        BigDamageToBoss, // 보스에게 큰 피해
        AdvancePhase,    // 다음 페이즈로 진행
    }
}
