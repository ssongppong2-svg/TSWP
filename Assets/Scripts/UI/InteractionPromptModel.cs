// 근거: UI 시스템.md — 상호작용 UI: 상호작용 가능한 오브젝트 근처에서 사용 키와 설명을 표시.
//   예시: [E] 문 열기 / [E] 레버 당기기 / [E] 아이템 줍기.
// 설명 문구는 Player.IInteractable.PromptDescription이 제공한다 (UI는 문구를 만들지 않는다).
using System;

namespace TSWP.UI
{
    /// <summary>상호작용 프롬프트 뷰모델 (World Space 캔버스).</summary>
    public sealed class InteractionPromptModel
    {
        /// <summary>기본 상호작용 키 표기. 조작과 시스템.md 기준 E키.
        /// TODO: Input System 도입 후 리바인딩된 키 표기로 대체 (Player.IPlayerInput 추상화 뒤).</summary>
        public const string DefaultKeyLabel = "E";

        public bool IsVisible;

        /// <summary>표시할 키 라벨 (예: "E").</summary>
        public string KeyLabel = DefaultKeyLabel;

        /// <summary>설명 (예: "문 열기"). IInteractable.PromptDescription 값.</summary>
        public string Description;

        public event Action Changed;

        /// <summary>근접한 상호작용 대상이 생겼을 때 호출 (Player.PlayerInteraction → UI 단방향).</summary>
        public void Show(string description, string keyLabel = DefaultKeyLabel)
        {
            IsVisible = true;
            Description = description;
            KeyLabel = keyLabel;
            Changed?.Invoke();
        }

        public void Hide()
        {
            if (!IsVisible) return;
            IsVisible = false;
            Description = null;
            Changed?.Invoke();
        }

        /// <summary>"[E] 문 열기" 형식 완성 문구.</summary>
        public string GetPromptText() => $"[{KeyLabel}] {Description}";
    }
}
