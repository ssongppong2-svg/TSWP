// 근거: 퍼즐 시스템.md — 버튼 퍼즐: 여러 버튼을 동시에 눌러야 한다. 트롤: 버튼을 잘못 누르면 몬스터가 소환된다.
// 프로토타입: 컨트롤러 없이 버튼 하나만 놓아도 눌렸다/해제됐다가 색과 눌림 오프셋으로 보여야 한다.
using UnityEngine;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 퍼즐 버튼. E키 상호작용으로 눌린 상태를 토글하며, 동시 누름 판정은 컨트롤러가 집계한다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))] // PlayerInteraction의 OverlapCircle 탐색 대상이 되려면 콜라이더가 필요하다
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

        /// <summary>정답 조합에 포함되는 버튼인가. 컨트롤러가 자동 수집 시 함정 버튼을 걸러내는 데 쓴다.</summary>
        public bool IsCorrectButton => isCorrectButton;

        /// <summary>눌림 유지 잔여 시간(초). 0 이하면 유지 제한 없음/해제됨.</summary>
        public float HoldRemaining => _releaseTimer;

        private float _releaseTimer;

        public string PromptDescription => IsPressed ? "버튼 해제" : "버튼 누르기";

        protected override void Awake()
        {
            // 눌리면 살짝 내려간다 — 인스펙터에서 값을 정했다면 그대로 존중한다.
            visual.SuggestMotion(new Vector3(0f, -0.12f, 0f), 0f);
            base.Awake();
        }

        public override string DebugStatus =>
            IsPressed
                ? (holdSeconds > 0f ? $"눌림 (해제까지 {_releaseTimer:0.0}s)" : "눌림 (유지)")
                : (isCorrectButton ? "해제" : "해제 / 함정 버튼");

        public bool CanInteract(PlayerController user)
        {
            // owner가 없으면 단독 버튼 — 항상 조작 가능해야 한다(프로토타입 검증).
            return owner == null || owner.State == PuzzleState.Active;
        }

        public void Interact(PlayerController user)
        {
            if (!CanInteract(user)) return;

            RegisterParticipant(user);

            if (!isCorrectButton)
            {
                // 오답 버튼 — 몬스터 소환 등 트롤 결과 (눌림 상태로는 만들지 않는다)
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

        protected override void Update()
        {
            base.Update(); // 시각 보간

            if (!IsPressed || holdSeconds <= 0f) return;

            _releaseTimer -= Time.deltaTime;
            if (_releaseTimer <= 0f)
                SetPressed(false);
        }

        private void SetPressed(bool pressed)
        {
            if (IsPressed == pressed) return;
            IsPressed = pressed;

            visual.SetActive(pressed); // 색 변화 + 눌림 오프셋
            Log(pressed ? "눌림" : "해제");

            NotifyStateChanged();
        }

        /// <summary>퍼즐 초기화 시 컨트롤러가 호출.</summary>
        public void ResetElement()
        {
            IsPressed = false;
            _releaseTimer = 0f;
            visual.ResetVisual();
        }
    }
}
