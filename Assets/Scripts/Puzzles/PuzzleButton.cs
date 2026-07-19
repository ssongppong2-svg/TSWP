// 근거: 퍼즐 시스템.md — 버튼 퍼즐: 여러 버튼을 동시에 눌러야 한다. 트롤: 버튼을 잘못 누르면 몬스터가 소환된다.
using UnityEngine;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 퍼즐 버튼. E키 상호작용으로 눌린 상태를 토글하며, 동시 누름 판정은 컨트롤러가 집계한다.
    /// </summary>
    public class PuzzleButton : PuzzleElement, IInteractable
    {
        [Header("버튼")]
        [Tooltip("이 버튼이 정답 조합에 포함되는가. false면 누르는 순간 오조작(트롤)이 된다.")]
        [SerializeField] private bool isCorrectButton = true;

        [Tooltip("한 번 누르면 유지되는가(래치). false면 밟고 있는 동안만 눌림 — 발판은 PressurePlate 사용.")]
        [SerializeField] private bool latching = true;

        [Header("동시 누름")]
        [Tooltip("눌림이 유지되는 시간(초). 동시 누름 판정 여유 — 0이면 무한 유지.")]
        [SerializeField, Min(0f)] private float holdSeconds = 2f; // TODO(밸런스): 문서 미정

        public bool IsPressed { get; private set; }

        private float _releaseTimer;

        public string PromptDescription => IsPressed ? "버튼 해제" : "버튼 누르기";

        public bool CanInteract(PlayerController user)
        {
            return owner == null || owner.State == PuzzleState.Active;
        }

        public void Interact(PlayerController user)
        {
            if (!CanInteract(user)) return;

            owner?.AddParticipant(user != null ? user.PlayerId : -1);

            if (!isCorrectButton)
            {
                // 오답 버튼 — 몬스터 소환 등 트롤 결과
                NotifyWrongAction();
                return;
            }

            if (latching && IsPressed)
            {
                SetPressed(false);
                return;
            }

            SetPressed(true);

            if (holdSeconds > 0f)
                _releaseTimer = holdSeconds;
        }

        private void Update()
        {
            if (!IsPressed || holdSeconds <= 0f) return;

            _releaseTimer -= Time.deltaTime;
            if (_releaseTimer <= 0f)
                SetPressed(false);
        }

        private void SetPressed(bool pressed)
        {
            if (IsPressed == pressed) return;
            IsPressed = pressed;
            NotifyStateChanged();
        }

        /// <summary>퍼즐 초기화 시 컨트롤러가 호출.</summary>
        public void ResetElement()
        {
            IsPressed = false;
            _releaseTimer = 0f;
        }
    }
}
