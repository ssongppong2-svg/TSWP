// 근거: 조작과 시스템.md — T키 이모트 휠, 기본 7종(😀 😂 😭 👍 👎 💀 🖕), 추후 해금 이모트 추가 예정.
// 근거: 게임 시작과 선택, 직업, 플레이.md — 로비/뒷풀이에서도 이모트 사용 가능 (인게임 공용).
// 이 컴포넌트는 '휠 열림 상태'와 이모트 확정만 소유한다 — 라디얼 휠 렌더링/선택 UI는 UI 폴더 소관.
// 발동 통지는 GameEvents.RaiseEmoteUsed 경유 (오버헤드 말풍선/네트워크 전파는 구독 측 소관).
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Player
{
    /// <summary>T키 → 이모트 휠 열림 상태 토글. UI 휠이 SelectEmote로 확정한다.</summary>
    [RequireComponent(typeof(PlayerController))]
    public class EmoteWheel : MonoBehaviour
    {
        [Tooltip("휠에 표시할 이모트 목록 — 기본 7종 + 해금 이모트(Meta 업적 보상). Core.EmoteData 참조만 (재정의 금지).")]
        [SerializeField] private List<EmoteData> availableEmotes = new List<EmoteData>();

        private PlayerController _controller;

        /// <summary>휠 열림 상태 (UI 라디얼 휠 표시 여부).</summary>
        public bool IsOpen { get; private set; }

        /// <summary>휠 구성용 이모트 목록 — UI가 읽어 라디얼로 배치. 해금 필터는 UI/Meta 소관.</summary>
        public IReadOnlyList<EmoteData> AvailableEmotes => availableEmotes;

        /// <summary>열림/닫힘 통지 — UI 라디얼 휠이 구독.</summary>
        public event Action<bool> OpenStateChanged;

        private void Awake() => _controller = GetComponent<PlayerController>();

        private void Update()
        {
            IPlayerInput input = _controller.InputSource;
            if (input != null && input.EmotePressed)
            {
                // NOTE(기획 확인 필요): T 토글 방식 vs 홀드 중 열림·떼면 선택 방식 — 문서 미정, 우선 토글.
                SetOpen(!IsOpen);
            }
        }

        /// <summary>휠 열림 상태 변경 (UI가 ESC 취소 등으로도 호출 가능).</summary>
        public void SetOpen(bool open)
        {
            if (IsOpen == open) return;
            IsOpen = open;
            OpenStateChanged?.Invoke(open);
        }

        /// <summary>
        /// UI 휠에서 이모트 확정 시 호출. 발동 후 휠을 닫는다.
        /// TODO: 해금 여부 검증 — Meta(보유 이모트 목록/업적 보상) 연동 시 isUnlockable 이모트 필터.
        /// </summary>
        public bool SelectEmote(string emoteId)
        {
            if (!IsOpen || string.IsNullOrEmpty(emoteId)) return false;

            GameEvents.RaiseEmoteUsed(_controller.PlayerId, emoteId); // 말풍선 연출/업적은 구독 측 소관
            GameEvents.RaiseStatCounter("emote.used", 1);
            SetOpen(false);
            // TODO(연출): 캐릭터 머리 위 이모트 스프라이트 표시 — Art/UI 연동.
            return true;
        }
    }
}
