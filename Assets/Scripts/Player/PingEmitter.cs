// 근거: 조작과 시스템.md — 마우스 휠 클릭 핑: 현재 위치 또는 대상을 팀원에게 표시 (위험/이동/아이템/집합/도움 요청 5종).
// 핑 타입은 Core.PingType 한 곳에만 정의 (ARCHITECTURE.md §5) — 통지는 GameEvents.RaisePing 경유,
// 월드 마커 표시는 PingMarkerView, 미니맵 표시는 UI가 PingRaised 구독으로 처리한다.
// SYNC: 호스트 권위 — 씬에 Online.PingBroadcaster가 있으면 그쪽으로 위임한다(마커 목록/도배 방지/네트워크 전파 소유자).
//   브로드캐스터가 GameEvents를 대신 발행하므로 이 컴포넌트는 같은 이벤트를 두 번 쏘지 않는다.
using UnityEngine;
using TSWP.Core;

namespace TSWP.Player
{
    /// <summary>휠 클릭 → 핑 발동. 핑 종류는 숫자키(기본 1~5)로 바꾼다 — 프로토타입 조작.</summary>
    [RequireComponent(typeof(PlayerController))]
    public class PingEmitter : MonoBehaviour
    {
        [Tooltip("현재 선택된 핑 종류 — 숫자키 또는 핑 선택 UI가 SetPingType으로 변경한다.")]
        [SerializeField] private PingType selectedType = PingType.Move; // NOTE(기획 확인 필요): 기본 핑 종류 문서 미정

        [SerializeField] private float pingCooldown = 1f; // TODO(밸런스): 문서 미정 — 핑 스팸 방지 간격

        [Header("핑 종류 선택 (프로토타입)")]
        [Tooltip("숫자키로 핑 종류를 바꾼다. 라디얼 핑 휠이 생기면 끈다.")]
        [SerializeField] private bool enableQuickTypeKeys = true;

        [Tooltip("PingType 5종에 순서대로 대응하는 키 (Danger/Move/Item/Rally/Help). Sandbox 디버그 키와 겹치면 여기서 바꾼다.")]
        [SerializeField]
        private KeyCode[] quickTypeKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
        };

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

            // 이모트 휠이 열려 있으면 숫자키는 이모트 선택용이므로 핑 종류 변경을 양보한다.
            if (enableQuickTypeKeys && !IsEmoteWheelOpen()) PollQuickTypeKeys();

            IPlayerInput input = _controller.InputSource;
            if (input != null && input.PingPressed)
                EmitPing(selectedType, GetPingWorldPosition());
        }

        /// <summary>숫자키 → 핑 종류 변경. 범위를 벗어난 키 개수는 무시한다.</summary>
        private void PollQuickTypeKeys()
        {
            if (quickTypeKeys == null) return;

            int count = Mathf.Min(quickTypeKeys.Length, PingTypeCount);
            for (int i = 0; i < count; i++)
            {
                if (quickTypeKeys[i] == KeyCode.None) continue;
                if (!Input.GetKeyDown(quickTypeKeys[i])) continue;
                SetPingType((PingType)i);
                return;
            }
        }

        /// <summary>같은 오브젝트의 이모트 휠 열림 여부 (없으면 false).</summary>
        private bool IsEmoteWheelOpen()
        {
            if (_emoteWheel == null) _emoteWheel = GetComponent<EmoteWheel>();
            return _emoteWheel != null && _emoteWheel.IsOpen;
        }

        private EmoteWheel _emoteWheel;

        /// <summary>PingType 5종(위험/이동/아이템/집합/도움) 개수 — Core 정의에서 1회만 읽는다(하드코딩 금지).</summary>
        private static readonly int PingTypeCount = System.Enum.GetValues(typeof(PingType)).Length;

        /// <summary>핑 종류 변경 — 숫자키/핑 선택 UI가 호출.</summary>
        public void SetPingType(PingType type) => selectedType = type;

        /// <summary>핑 발동. 성공 시 true (쿨타임 중이면 무시 — 스팸 방지).</summary>
        public bool EmitPing(PingType type, Vector2 worldPos)
        {
            if (!_cooldown.TryUse()) return false;

            // 마커 수명/도배 방지/네트워크 전파를 소유하는 브로드캐스터가 있으면 그쪽 단일 경로로 보낸다.
            // 브로드캐스터가 GameEvents.RaisePing + StatCounter를 대신 발행하므로 여기서 중복 발행하지 않는다.
            var broadcaster = Online.PingBroadcaster.Instance;
            if (broadcaster != null)
                return broadcaster.SendPing((ulong)(uint)_controller.PlayerId, type, worldPos);

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
