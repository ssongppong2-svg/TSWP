// 근거: 조작과 시스템.md — E키 상호작용 대상 8종 중 ④ 문(Map 구조물).
//   방 시스템.md — 전투 방은 클리어 전 출구가 봉쇄되고, 클리어하면 문이 열려 다음 방으로 이동한다.
// 이동 판정 자체는 RoomManager.TryMoveToRoom(단일 지점)이 소유한다 — 이 컴포넌트는 '요청'만 한다.
// SYNC: 호스트 권위 — 추후 클라이언트는 이동 요청만 보내고 결과를 수신한다.
using System;
using UnityEngine;
using TSWP.Player;

namespace TSWP.Map
{
    /// <summary>
    /// 방 출구(문) 1개. 잠김/열림 상태를 가지며 열린 뒤 E키 상호작용 또는 접촉으로 다음 방으로 이동시킨다.
    /// 연출 오브젝트(lockedVisual/openVisual)는 없어도 로직이 동작한다 (null이면 조용히 생략).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomDoor : MonoBehaviour, IInteractable
    {
        [Header("목적지")]
        [Tooltip("이동할 방 id를 직접 지정한다. -1이면 RoomFlowManager가 그래프 연결 순서대로 배정한다.")]
        [SerializeField] private int explicitTargetRoomId = -1;

        [Header("개방 방식")]
        [Tooltip("체크하면 접촉(트리거)만으로 이동한다. 끄면 E키 상호작용이 필요하다.")]
        [SerializeField] private bool travelOnTouch = false;

        [Tooltip("방 클리어와 무관하게 항상 열려 있는 문 (휴식/보상 방의 출구 등).")]
        [SerializeField] private bool alwaysOpen = false;

        [Header("프롬프트 문구 (UI.InteractionPrompt 표시용)")]
        [SerializeField] private string openPrompt = "다음 방으로";
        [SerializeField] private string lockedPrompt = "잠긴 문 — 방을 클리어해야 합니다";

        [Header("연출/차단 (선택 — 비어 있어도 동작)")]
        [Tooltip("잠김 상태에서 켜지는 오브젝트.")]
        [SerializeField] private GameObject lockedVisual;
        [Tooltip("열림 상태에서 켜지는 오브젝트.")]
        [SerializeField] private GameObject openVisual;
        [Tooltip("잠김 상태에서 통행을 물리적으로 막는 콜라이더 (트리거 아님). 열리면 비활성화된다.")]
        [SerializeField] private Collider2D blocker;

        [Header("프로토타입 색 표시")]
        [Tooltip("lockedVisual/openVisual을 둘 다 비워 둔 문에 한해, 스프라이트 색으로 잠김/열림을 표시한다. " +
                 "연출 오브젝트를 지정한 문은 건드리지 않는다.")]
        [SerializeField] private bool tintSpriteByState = true;
        [SerializeField] private Color lockedColor = new Color(0.55f, 0.17f, 0.17f, 1f); // 잠김 — 붉은색
        [SerializeField] private Color openColor = new Color(0.22f, 0.72f, 0.40f, 1f);   // 열림 — 초록색

        private SpriteRenderer[] _renderers;
        private bool _visualInitialized;

        [Header("연타/중복 이동 방지")]
        [SerializeField, Min(0f)] private float travelCooldown = 0.5f; // TODO(밸런스): 문서 미정

        private float _nextTravelAllowedAt;

        /// <summary>현재 열림 여부.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>이동 목적지 방 id (-1 = 미배정 → 이동 불가).</summary>
        public int TargetRoomId { get; private set; } = -1;

        /// <summary>인스펙터에서 목적지를 수동 지정했는지 — 자동 배정이 덮어쓰지 않아야 한다.</summary>
        public bool HasExplicitTarget => explicitTargetRoomId >= 0;

        /// <summary>이동 성공 통지 (연출/사운드 훅).</summary>
        public event Action<RoomDoor> Traveled;

        public string PromptDescription => IsOpen ? openPrompt : lockedPrompt;

        private void Awake()
        {
            TargetRoomId = explicitTargetRoomId;
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            ApplyVisualState();
            _visualInitialized = true;
        }

        // ── 배선 API (RoomFlowManager / RoomInstance가 호출) ────────
        /// <summary>목적지 방 id 배정. explicitTargetRoomId가 지정된 문은 덮어쓰지 않는다.</summary>
        public void AssignTarget(int roomId)
        {
            if (explicitTargetRoomId >= 0) return; // 수동 배정 우선
            TargetRoomId = roomId;
        }

        /// <summary>목적지를 강제로 덮어쓴다 (에디터 도구/특수 케이스용).</summary>
        public void ForceTarget(int roomId) => TargetRoomId = roomId;

        /// <summary>잠금/개방 설정. alwaysOpen 문은 항상 열린 상태를 유지한다.</summary>
        public void SetOpen(bool open)
        {
            bool next = alwaysOpen || open;
            // 첫 호출은 값이 같아도 반드시 반영한다 (Awake 전에 SetOpen이 오는 순서 대비).
            if (_visualInitialized && IsOpen == next) return;
            IsOpen = next;
            _visualInitialized = true;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (alwaysOpen) IsOpen = true;
            if (lockedVisual != null) lockedVisual.SetActive(!IsOpen);
            if (openVisual != null) openVisual.SetActive(IsOpen);
            if (blocker != null) blocker.enabled = !IsOpen;

            // 연출 오브젝트가 아예 없는 문은 색으로라도 상태가 보여야 한다
            // (잠긴 문은 CanInteract가 false라 UI 프롬프트조차 뜨지 않는다).
            if (!tintSpriteByState || lockedVisual != null || openVisual != null) return;
            if (_renderers == null) _renderers = GetComponentsInChildren<SpriteRenderer>(true);

            Color color = IsOpen ? openColor : lockedColor;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var sr = _renderers[i];
                if (sr != null) sr.color = color;
            }
        }

        // ── IInteractable ─────────────────────────────────────────
        public bool CanInteract(PlayerController user)
            => IsOpen && TargetRoomId >= 0 && !travelOnTouch && Time.time >= _nextTravelAllowedAt;

        public void Interact(PlayerController user) => Travel();

        // ── 접촉 이동 ─────────────────────────────────────────────
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!travelOnTouch || !IsOpen) return;
            // 문 콜라이더는 자식 구성일 수 있으므로 부모 방향으로 찾는다.
            if (other.GetComponentInParent<PlayerController>() == null) return;
            Travel();
        }

        // ── 이동 요청 ─────────────────────────────────────────────
        /// <summary>이동 요청. 실제 판정은 RoomFlowManager → RoomManager.TryMoveToRoom이 수행한다.</summary>
        public bool Travel()
        {
            if (!IsOpen || TargetRoomId < 0) return false;
            if (Time.time < _nextTravelAllowedAt) return false;
            _nextTravelAllowedAt = Time.time + travelCooldown;

            bool moved = false;
            var flow = RoomFlowManager.Instance;
            if (flow != null) moved = flow.RequestMove(TargetRoomId);
            else
            {
                // RoomFlowManager 없이 RoomManager만 있는 최소 구성도 지원한다.
                var rooms = RoomManager.Instance;
                if (rooms != null) moved = rooms.TryMoveToRoom(TargetRoomId);
            }

            if (moved) Traveled?.Invoke(this);
            return moved;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = IsOpen || alwaysOpen ? Color.green : Color.red;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 1.6f, 0f));
        }
#endif
    }
}
