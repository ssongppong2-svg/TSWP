// 근거: 조작과 시스템.md — 키 바인딩: 이동 A/D, 점프 Space, 달리기 LShift(홀드), 기본 공격 좌클릭, 스킬 Q, 상호작용 E, 핑 휠클릭, 이모트 T.
// UnityEngine.Input(레거시) 기반 기본 구현 — 뼈대 단계 규칙 (ARCHITECTURE.md §1).
// TODO: Input System 교체 — 설정의 '키 변경(리바인딩)'·'컨트롤러 지원'·'마우스 감도' 요구 충족을 위해 필수.
using UnityEngine;

namespace TSWP.Player
{
    /// <summary>
    /// 레거시 Input 구현. 키는 문서 확정값을 하드코딩한다 (리바인딩은 Input System 교체 시 지원).
    /// PlayerController가 기본 생성하며, 테스트/네트워크 원격 플레이어는 다른 IPlayerInput 구현으로 교체한다.
    /// </summary>
    public sealed class LegacyPlayerInput : IPlayerInput
    {
        public float MoveAxis
        {
            get
            {
                // 문서가 A/D로 확정 — 방향키 포함 GetAxis("Horizontal") 대신 명시 키를 읽는다.
                float axis = 0f;
                if (Input.GetKey(KeyCode.A)) axis -= 1f;
                if (Input.GetKey(KeyCode.D)) axis += 1f;
                return axis;
            }
        }

        public bool JumpPressed => Input.GetKeyDown(KeyCode.Space);

        /// <summary>가변 점프 높이용 — 떼는 순간 상승이 잘린다.</summary>
        public bool JumpHeld => Input.GetKey(KeyCode.Space);

        public bool RunHeld => Input.GetKey(KeyCode.LeftShift);

        /// <summary>
        /// 대쉬 = 마우스 우클릭(1). 좌클릭 공격과 대칭이라 손가락 이동 없이 즉시 쓸 수 있다.
        /// NOTE(문서 갱신 필요): 조작과 시스템.md에 대쉬 항목 추가 필요.
        /// </summary>
        public bool DashPressed => Input.GetMouseButtonDown(1);

        // TODO(조작감): 홀드 연사(GetMouseButton) 지원 여부 문서 미정 — 우선 단발 에지 입력 (공격 간격은 Jobs.BasicAttacker가 관리).
        public bool AttackPressed => Input.GetMouseButtonDown(0);

        public bool SkillPressed => Input.GetKeyDown(KeyCode.Q);

        public bool InteractPressed => Input.GetKeyDown(KeyCode.E);

        /// <summary>마우스 휠 클릭 = 가운데 버튼(2).</summary>
        public bool PingPressed => Input.GetMouseButtonDown(2);

        public bool EmotePressed => Input.GetKeyDown(KeyCode.T);
    }
}
