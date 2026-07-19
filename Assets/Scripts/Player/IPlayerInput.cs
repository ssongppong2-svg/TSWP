// 근거: 조작과 시스템.md — 기본 조작 8종 (A/D 이동, Space 점프, LShift 달리기, 좌클릭 공격, Q 스킬, E 상호작용, 휠클릭 핑, T 이모트).
// 입력 추상화 — 설정 메뉴에 '키 변경(리바인딩)'과 '컨트롤러 지원'이 명시되어 있어(조작과 시스템.md 설정-조작)
// 추후 Unity Input System으로 교체한다. 게임플레이 코드는 이 인터페이스만 참조한다 (ARCHITECTURE.md §1).
namespace TSWP.Player
{
    /// <summary>
    /// 플레이어 입력 추상화. 프레임마다 폴링으로 읽는다.
    /// *Pressed = 해당 프레임에 눌림(에지), *Held = 누르고 있는 동안 유지.
    /// 시그니처는 ARCHITECTURE.md §4 고정 — 멤버 추가/변경 금지.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>좌우 이동 축 (-1 = A/왼쪽, +1 = D/오른쪽, 0 = 입력 없음).</summary>
        float MoveAxis { get; }

        /// <summary>점프 (Space) — 이번 프레임 눌림.</summary>
        bool JumpPressed { get; }

        /// <summary>점프 키 유지 (Space) — 가변 점프 높이에 사용. 일찍 떼면 낮게 뛴다.</summary>
        bool JumpHeld { get; }

        /// <summary>
        /// 대쉬 (마우스 우클릭) — 이번 프레임 눌림.
        /// NOTE(문서 갱신 필요): 조작과 시스템.md v1.1의 조작 8종에 없는 신규 조작이다.
        /// 게임 성경.md "재미가 현실성보다 우선한다"에 따라 조작감 강화를 위해 추가했다.
        /// </summary>
        bool DashPressed { get; }

        /// <summary>달리기 (Left Shift) — 누르고 있는 동안 true. 스태미나 없음, 언제든 사용 가능 (조작과 시스템.md).</summary>
        bool RunHeld { get; }

        /// <summary>기본 공격 (마우스 좌클릭) — 이번 프레임 눌림. 직업별 공격은 Jobs가 수행.</summary>
        bool AttackPressed { get; }

        /// <summary>직업 스킬 (Q) — 이번 프레임 눌림. 모든 스킬은 쿨타임 보유 (Jobs.SkillCaster가 검사).</summary>
        bool SkillPressed { get; }

        /// <summary>상호작용 (E) — 이번 프레임 눌림. 대상 8종은 IInteractable 참조.</summary>
        bool InteractPressed { get; }

        /// <summary>핑 (마우스 휠 클릭) — 이번 프레임 눌림.</summary>
        bool PingPressed { get; }

        /// <summary>이모트 휠 (T) — 이번 프레임 눌림.</summary>
        bool EmotePressed { get; }
    }
}
