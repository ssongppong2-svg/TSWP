// 근거: 아이템 시스템.md — 중복 획득 기본 허용 + 중첩 방식 아이템별 개별 설정 → 정의(SO)와 런타임 인스턴스 분리.
//       보스 보상: 먼저 집는 플레이어가 소유자가 된다 (선착순 선점).
using TSWP.Core;

namespace TSWP.Items
{
    /// <summary>장착된 아이템의 런타임 인스턴스. StatCollection modifier의 Source 키로도 사용된다
    /// (해제 시 RemoveModifiersFromSource(this)로 일괄 제거).</summary>
    public sealed class ItemInstance
    {
        /// <summary>원본 정의 SO 참조.</summary>
        public ItemDefinition Definition { get; }

        /// <summary>현재 중첩 수. StackingBehavior에 따라 효과 배율/지속시간/피해량 산출에 쓰인다.</summary>
        public int StackCount { get; private set; }

        /// <summary>소유자 — 먼저 집은 플레이어. // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        public int OwnerPlayerId { get; }

        /// <summary>능력 재사용 대기시간 (소비/발동형 아이템용). Definition.ability.cooldown으로 초기화.</summary>
        public CooldownTimer Cooldown { get; }

        public ItemInstance(ItemDefinition definition, int ownerPlayerId)
        {
            Definition = definition;
            OwnerPlayerId = ownerPlayerId;
            StackCount = 1;
            Cooldown = new CooldownTimer(definition != null && definition.ability != null ? definition.ability.cooldown : 0f);
        }

        /// <summary>중첩 1 증가. 상한 검사(MaxStackLimited)는 PlayerEquipment가 수행한다.</summary>
        public void AddStack() => StackCount++;

        /// <summary>중첩 1 소모 (소비 아이템 사용). 0 이하가 되면 PlayerEquipment가 슬롯에서 제거한다.</summary>
        public void RemoveStack()
        {
            if (StackCount > 0) StackCount--;
        }
    }
}
