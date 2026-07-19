// 근거: 아이템 시스템.md — 보스 보상(드롭 아이템은 모든 플레이어가 획득 가능, 먼저 집는 플레이어가 소유자),
//       아이템 교체(버린 아이템은 바닥에 떨어지며 다른 플레이어가 획득 가능). 아이템 경쟁도 게임의 일부.
using UnityEngine;

namespace TSWP.Items
{
    /// <summary>월드에 떨어진 아이템. 보스 공용 드롭·시작 아이템·버린 아이템 모두 이 오브젝트로 표현한다.
    /// 소유권 없음 — 선착순 선점(먼저 집는 플레이어가 소유자).</summary>
    public class DroppedItem : MonoBehaviour
    {
        [SerializeField] private ItemDefinition item;

        /// <summary>모든 플레이어 획득 가능 (보스 공용 드롭 등). false는 예약 드롭용 확장 여지.</summary>
        [SerializeField] private bool sharedPickup = true;

        /// <summary>선점 완료 플래그. // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        private bool _claimed;

        public ItemDefinition Item => item;
        public bool SharedPickup => sharedPickup;
        public bool IsClaimed => _claimed;

        /// <summary>스폰 직후 초기화 (ItemDropManager/PlayerEquipment가 호출).</summary>
        public void Initialize(ItemDefinition definition)
        {
            item = definition;
            _claimed = false;
            // TODO(시각): definition.icon을 SpriteRenderer에 반영 + 희귀도별 색상(Art.RarityColorConfig 참조는 표시 계층에서).
        }

        /// <summary>픽업 판정 단일 지점 — 선착순 선점. 이 메서드 외의 경로로 아이템을 넘기지 말 것.
        /// // SYNC: 호스트 권위 판정 예정 — 동시 픽업 레이스는 호스트가 최초 요청 1건만 승인하고 나머지는 기각한다.</summary>
        public void Pickup(int playerId)
        {
            if (_claimed || item == null) return;

            _claimed = true; // 선점 — 후속 요청 차단

            bool equipped = ItemDropManager.Instance != null
                && ItemDropManager.Instance.ResolvePickup(this, playerId);

            if (!equipped)
            {
                // 장착 실패 (슬롯 가득/중첩 상한/소지 상한) — 선점 해제.
                // 슬롯 가득의 경우 UI가 교체 흐름(PlayerEquipment.SwapAndDrop)을 유도한다.
                _claimed = false;
                return;
            }

            Destroy(gameObject);
        }

        // TODO(상호작용): 트리거 접촉 + E키 픽업 연결.
        //   Player 폴더의 IInteractable은 Player 담당 소유이므로 여기서 구현하지 않는다 (순환 조정 회피).
        //   Player 담당이 IInteractable 어댑터(또는 PlayerInteraction)에서 이 컴포넌트를 탐지해
        //   Pickup(playerId)만 호출하면 된다 — 본 클래스는 이 API 하나만 노출한다.
    }
}
