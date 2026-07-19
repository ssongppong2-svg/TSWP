// 근거: 조작과 시스템.md — E키 상호작용 대상 8종:
//   ① 아이템 획득(Items.DroppedItem)  ② 레버(Map/Puzzles)  ③ 버튼(Map/Puzzles)  ④ 문(Map 구조물)
//   ⑤ 상점(Shop)  ⑥ NPC  ⑦ 퍼즐 장치(Puzzles.PuzzleElement)  ⑧ 팀원 부활(Player.TeammateReviveInteractable)
// 시그니처는 ARCHITECTURE.md §4 고정 — 구현체는 각 소유 시스템(Items/Map/Puzzles/Player)이 작성한다.
namespace TSWP.Player
{
    public interface IInteractable
    {
        /// <summary>상호작용 프롬프트 문구 (UI.InteractionPrompt 표시용, 예: "레버 당기기", "팀원 부활").</summary>
        string PromptDescription { get; }

        /// <summary>
        /// 이 플레이어가 지금 상호작용 가능한지 (거리 외 조건: 잠긴 문, 부활 횟수 소진, 골드 부족 등).
        /// 거리 판정은 호출 측(PlayerInteraction의 OverlapCircle)이 담당한다.
        /// </summary>
        bool CanInteract(PlayerController user);

        /// <summary>
        /// 상호작용 실행.
        /// // SYNC: 호스트 권위 — 선착순 판정 대상(드롭 아이템 등)은 추후 호스트 확정 후 실행으로 변경.
        /// </summary>
        void Interact(PlayerController user);
    }
}
