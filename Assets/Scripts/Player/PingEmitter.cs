// 근거: 조작과 시스템.md — 마우스 휠 클릭 핑: 현재 위치 또는 대상을 팀원에게 표시 (위험/이동/아이템/집합/도움 요청 5종).
// 핑 타입은 Core.PingType 한 곳에만 정의 (ARCHITECTURE.md §5) — 통지는 GameEvents.RaisePing 경유,
// 월드 마커/미니맵 표시는 UI가 PingRaised 구독으로 처리한다.
// SYNC: 호스트 권위 — 추후 Online.PingBroadcaster가 네트워크 전파 담당 (이 컴포넌트는 로컬 발동만).
using UnityEngine;
using TSWP.Core;

namespace TSWP.Player
{
    /// <summary>휠 클릭 → 핑 발동. 핑 종류 선택 UI(핑 휠 등)는 UI 폴더 소관 — SetPingType으로 주입받는다.</summary>
    [RequireComponent(typeof(PlayerController))]
    public class PingEmitter : MonoBehaviour
    {
        [Tooltip("현재 선택된 핑 종류 — 핑 선택 UI(UI 폴더 소관)가 SetPingType으로 변경한다.")]
        [SerializeField] private PingType selectedType = PingType.Move; // NOTE(기획 확인 필요): 기본 핑 종류 문서 미정

        [SerializeField] private float pingCooldown = 1f; // TODO(밸런스): 문서 미정 — 핑 스팸 방지 간격

        private PlayerController _controller;
        private CooldownTimer _cooldown;

        public PingType SelectedType => selectedType;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _cooldown = new CooldownTimer(pingCooldown);
        }

        private void Update()
        {
            _cooldown.Tick(Time.deltaTime);

            IPlayerInput input = _controller.InputSource;
            if (input != null && input.PingPressed)
                EmitPing(selectedType, GetPingWorldPosition());
        }

        /// <summary>핑 종류 변경 — 핑 선택 UI가 호출.</summary>
        public void SetPingType(PingType type) => selectedType = type;

        /// <summary>핑 발동. 성공 시 true (쿨타임 중이면 무시 — 스팸 방지).</summary>
        public bool EmitPing(PingType type, Vector2 worldPos)
        {
            if (!_cooldown.TryUse()) return false;

            GameEvents.RaisePing(_controller.PlayerId, type, worldPos); // UI 마커 + RunManager 통계(PingsUsed) 집계
            GameEvents.RaiseStatCounter("ping.used", 1);                // 업적 카운터 (GameEvents 예시 키)
            return true;
        }

        /// <summary>
        /// 핑 위치 — 마우스가 가리키는 월드 지점, 카메라 부재 시 플레이어 위치("현재 위치" 규칙 폴백).
        /// TODO: '대상' 핑 — 적/아이템 콜라이더에 스냅하는 대상 지정은 UI/탐색 로직 확장 시.
        /// </summary>
        private Vector2 GetPingWorldPosition()
        {
            var cam = Camera.main;
            if (cam != null)
                return cam.ScreenToWorldPoint(Input.mousePosition); // TODO: Input System 교체 시 입력 계층으로 이동
            return transform.position;
        }
    }
}
