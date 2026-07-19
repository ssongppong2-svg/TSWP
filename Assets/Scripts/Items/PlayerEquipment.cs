// 근거: 아이템 시스템.md — 장착 제한(최대 5개, GameRules.EquipSlotCount), 장착 즉시 효과 적용,
//       아이템 교체(슬롯 가득 시 기존 아이템 버리고 교체 — 버린 아이템은 바닥에 떨어져 다른 플레이어가 획득 가능),
//       중복(중첩 방식 아이템별 개별 설정), 소비 아이템(일회성, 사용 후 소멸).
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;

namespace TSWP.Items
{
    /// <summary>플레이어의 아이템 장착 컴포넌트. 장착 = StatCollection modifier 추가 + ItemEffect.OnEquip,
    /// 해제 = Source 기준 modifier 일괄 제거 + OnUnequip. UI 통지는 GameEvents 경유.</summary>
    public class PlayerEquipment : MonoBehaviour
    {
        // SYNC: 호스트 권위, 추후 NGO NetworkVariable — 장착 목록/playerId는 호스트가 확정하고 복제한다.
        [SerializeField] private int playerId = -1;

        [SerializeField] private DroppedItem droppedItemPrefab; // 버린 아이템 스폰용 프리팹

        /// <summary>장착 슬롯. 최대 GameRules.EquipSlotCount(5).</summary>
        private readonly List<ItemInstance> _slots = new();

        /// <summary>장착자의 스탯. Player 담당의 PlayerStats 조립 코드가 Initialize로 주입한다.</summary>
        private StatCollection _stats;

        public int PlayerId => playerId;
        public IReadOnlyList<ItemInstance> EquippedItems => _slots;
        public bool HasFreeSlot => _slots.Count < GameRules.EquipSlotCount;

        /// <summary>Player 조립 지점에서 호출 (PlayerStats가 보유한 StatCollection 주입).
        /// TODO: Player 담당의 스폰 파이프라인에서 호출 연결.</summary>
        public void Initialize(int ownerPlayerId, StatCollection stats)
        {
            playerId = ownerPlayerId;
            _stats = stats;

            // 드롭 매니저에 등록 — 픽업 판정(호스트 권위 단일 지점)이 playerId → 장비를 해석할 수 있도록.
            if (ItemDropManager.Instance != null)
                ItemDropManager.Instance.RegisterPlayer(this);
        }

        private void OnDestroy()
        {
            if (ItemDropManager.Instance != null)
                ItemDropManager.Instance.UnregisterPlayer(playerId);
        }

        // ── 장착 ──────────────────────────────────────────────────

        /// <summary>장착 시도. 성공 시 즉시 효과 적용 + GameEvents.RaiseItemAcquired.
        /// 실패(false) 사유: 슬롯 가득(교체 필요 — UI에서 SwapAndDrop 유도), 최대 소지/중첩 상한 도달.</summary>
        public bool Equip(ItemDefinition definition)
        {
            if (definition == null) return false;

            // SYNC: 호스트 권위 — 장착 확정은 추후 호스트 판정(ServerRpc) 경유로 이동.

            // 최대 소지 가능 검사 (0 = 무제한)
            if (definition.maxPossessCount > 0 && CountPossessed(definition.itemCode) >= definition.maxPossessCount)
                return false;

            // 중복 획득: 중복 장착 불가 아이템은 기존 인스턴스에 StackingBehavior대로 중첩.
            // NOTE(기획 확인 필요): allowDuplicateEquip=true인 아이템은 별도 슬롯에 독립 인스턴스로 장착한다고 해석함.
            ItemInstance existing = FindByCode(definition.itemCode);
            if (existing != null && !definition.allowDuplicateEquip)
                return TryStack(existing);

            if (!HasFreeSlot) return false;

            var instance = new ItemInstance(definition, playerId);
            _slots.Add(instance);

            // 장착 즉시 효과 적용 (아이템 시스템.md '장착 제한')
            ApplyStatModifiers(instance);
            if (definition.itemType != ItemType.Consumable)
                InvokeOnEquip(instance);
            // NOTE(기획 확인 필요): 소비 아이템은 장착 시가 아닌 '사용 시' 효과 발동으로 해석 (UseConsumable 참조).

            GameEvents.RaiseItemAcquired(playerId, definition.itemCode);
            return true;
        }

        /// <summary>중복 획득 시 StackingBehavior 분기 (아이템 시스템.md '중복').</summary>
        private bool TryStack(ItemInstance instance)
        {
            var def = instance.Definition;

            // 최대 중첩 제한 — 상한 도달 시 획득 불가
            if (def.stackingBehavior == StackingBehavior.MaxStackLimited && instance.StackCount >= def.maxStacks)
                return false;

            instance.AddStack();

            switch (def.stackingBehavior)
            {
                case StackingBehavior.EffectStack:
                case StackingBehavior.MaxStackLimited:
                    // 효과 중첩: 능력치 modifier 1세트 추가 + 효과 훅 재발동
                    // (해제 시 RemoveModifiersFromSource(instance)로 전 세트 일괄 제거되므로 안전)
                    ApplyStatModifiers(instance);
                    if (def.itemType != ItemType.Consumable)
                        InvokeOnEquip(instance);
                    break;

                case StackingBehavior.DurationIncrease:
                    // 지속시간 증가: ItemEffect가 ctx.Instance.StackCount로 지속시간을 산출한다. TODO: 효과 모듈 구현 시 반영
                    break;

                case StackingBehavior.DamageIncrease:
                    // 피해량 증가: OnAttack 훅에서 ctx.Instance.StackCount를 피해 배율로 반영한다. TODO: 효과 모듈 구현 시 반영
                    break;
            }

            GameEvents.RaiseItemAcquired(playerId, def.itemCode);
            return true;
        }

        // ── 교체/버리기 ───────────────────────────────────────────

        /// <summary>슬롯 가득 시 기존 아이템을 버리고 새 아이템으로 교체.
        /// 버린 아이템은 DroppedItem으로 바닥에 스폰되어 다른 플레이어가 획득할 수 있다.</summary>
        public bool SwapAndDrop(ItemDefinition newItem, int discardIndex)
        {
            if (newItem == null || discardIndex < 0 || discardIndex >= _slots.Count) return false;

            // SYNC: 호스트 권위 — 교체·드롭 스폰은 추후 호스트 판정 경유.
            ItemInstance discarded = _slots[discardIndex];
            RemoveInstance(discarded);

            GameEvents.RaiseItemDropped(playerId, discarded.Definition.itemCode);
            SpawnDroppedItem(discarded.Definition);
            // NOTE(기획 확인 필요): 중첩된 인스턴스를 버리면 스택 전체가 아이템 1개(정의 기준)로 드롭된다고 우선 처리.

            return Equip(newItem);
        }

        /// <summary>슬롯의 아이템을 그냥 버린다 (교체 없이). 바닥에 DroppedItem 스폰.</summary>
        public bool DropItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return false;

            ItemInstance discarded = _slots[slotIndex];
            RemoveInstance(discarded);

            GameEvents.RaiseItemDropped(playerId, discarded.Definition.itemCode);
            SpawnDroppedItem(discarded.Definition);
            return true;
        }

        // ── 소비 아이템 ───────────────────────────────────────────

        /// <summary>소비 아이템 사용 — 일회성, 사용 후 소멸 (아이템 시스템.md '소비 아이템').
        /// 효과 발동은 ItemEffect.OnEquip 훅을 '사용 시 1회 발동'으로 재사용한다.</summary>
        public bool UseConsumable(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Count) return false;

            ItemInstance instance = _slots[slotIndex];
            if (instance.Definition.itemType != ItemType.Consumable) return false;

            // 재사용 대기시간 검사 (ability.cooldown, 0이면 즉시 사용 가능)
            if (!instance.Cooldown.TryUse()) return false;

            var ctx = MakeContext(instance);
            foreach (ItemEffect effect in EnumerateEffects(instance.Definition))
                effect.OnEquip(ctx); // 사용 시 1회 발동 (예: 회복 물약, 폭탄, 버프 물약)

            GameEvents.RaiseStatCounter("consumable.used", 1); // 업적 카운터 집계용

            instance.RemoveStack();
            if (instance.StackCount <= 0)
            {
                _stats?.RemoveModifiersFromSource(instance); // 안전 제거 (소비 아이템이 상시 modifier를 가졌을 경우)
                _slots.Remove(instance);
            }
            return true;
        }

        // ── 전투 훅 중계 (Combat/Player 측이 호출) ────────────────

        /// <summary>장착자의 공격 확정 직전 호출 — 전 장착 아이템의 OnAttack 훅 전파. damage 수정 가능.</summary>
        public void NotifyAttack(ref DamageInfo damage)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var ctx = MakeContext(_slots[i]);
                foreach (ItemEffect effect in EnumerateEffects(_slots[i].Definition))
                    effect.OnAttack(ctx, ref damage);
            }
        }

        /// <summary>치명타 발생 시 호출 — OnCrit 훅 전파.</summary>
        public void NotifyCrit(ref DamageInfo damage)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var ctx = MakeContext(_slots[i]);
                foreach (ItemEffect effect in EnumerateEffects(_slots[i].Definition))
                    effect.OnCrit(ctx, ref damage);
            }
        }

        /// <summary>적 처치 시 호출 — OnKill 훅 전파.</summary>
        public void NotifyKill(string enemyId)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                var ctx = MakeContext(_slots[i]);
                foreach (ItemEffect effect in EnumerateEffects(_slots[i].Definition))
                    effect.OnKill(ctx, enemyId);
            }
        }

        /// <summary>장착자 사망 시 호출. true 반환 = 어떤 효과가 사망을 무효화함 (자동 부활 유물).
        /// 호출자(Combat 측)는 true면 사망 처리를 중단한다.</summary>
        public bool NotifyOwnerDeath()
        {
            bool prevented = false;
            for (int i = 0; i < _slots.Count; i++)
            {
                var ctx = MakeContext(_slots[i]);
                foreach (ItemEffect effect in EnumerateEffects(_slots[i].Definition))
                {
                    if (effect.OnDeath(ctx))
                        prevented = true;
                }
            }
            return prevented;
        }

        /// <summary>장착 아이템 위험 요소의 아군 피해 증가 배율 합 (예: 폭발 범위 +100% ↔ 아군 피해 +50%).
        /// DamageSystem/공격 코드가 아군 피해 산정 시 참조한다.</summary>
        public float GetAllyDamageModifier()
        {
            float total = 0f;
            for (int i = 0; i < _slots.Count; i++)
            {
                var risks = _slots[i].Definition.risks;
                if (risks == null) continue;
                for (int r = 0; r < risks.Count; r++)
                    total += risks[r].allyDamageModifier; // NOTE(기획 확인 필요): 중첩 수 반영 여부 미정
            }
            return total;
        }

        // ── 내부 처리 ─────────────────────────────────────────────

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _slots.Count; i++)
            {
                ItemInstance instance = _slots[i];
                instance.Cooldown.Tick(dt);

                var ctx = MakeContext(instance);
                foreach (ItemEffect effect in EnumerateEffects(instance.Definition))
                    effect.OnTick(ctx, dt);
            }
        }

        /// <summary>능력치 변화 + 위험 요소 상시 패널티를 modifier 스택에 적용. Source = 인스턴스.</summary>
        private void ApplyStatModifiers(ItemInstance instance)
        {
            if (_stats == null) return; // TODO: Player 조립에서 Initialize 호출 필수 — 미주입 시 스탯 미반영

            var def = instance.Definition;

            if (def.statModifiers != null)
            {
                for (int i = 0; i < def.statModifiers.Count; i++)
                {
                    StatModifierEntry e = def.statModifiers[i];
                    _stats.AddModifier(new StatModifier(e.stat, e.mode, e.value, instance));
                }
            }

            // 위험 요소 상시 패널티 — 음수 효과도 동일 파이프라인 (아이템 시스템.md '위험 요소')
            if (def.risks != null)
            {
                for (int r = 0; r < def.risks.Count; r++)
                {
                    var penalties = def.risks[r].statPenalties;
                    if (penalties == null) continue;
                    for (int p = 0; p < penalties.Count; p++)
                    {
                        StatModifierEntry e = penalties[p];
                        _stats.AddModifier(new StatModifier(e.stat, e.mode, e.value, instance));
                    }
                }
            }
        }

        private void InvokeOnEquip(ItemInstance instance)
        {
            var ctx = MakeContext(instance);
            foreach (ItemEffect effect in EnumerateEffects(instance.Definition))
                effect.OnEquip(ctx);
        }

        /// <summary>해제 공통 처리: modifier 일괄 제거 + OnUnequip 훅.</summary>
        private void RemoveInstance(ItemInstance instance)
        {
            _stats?.RemoveModifiersFromSource(instance);

            if (instance.Definition.itemType != ItemType.Consumable)
            {
                var ctx = MakeContext(instance);
                foreach (ItemEffect effect in EnumerateEffects(instance.Definition))
                    effect.OnUnequip(ctx);
            }

            _slots.Remove(instance);
        }

        private void SpawnDroppedItem(ItemDefinition definition)
        {
            // SYNC: 호스트 권위 — 드롭 오브젝트 스폰은 호스트만 수행, 클라이언트는 복제 수신.
            if (droppedItemPrefab == null)
            {
                Debug.LogWarning($"[PlayerEquipment] droppedItemPrefab 미할당 — '{definition.itemCode}' 드롭 스폰 생략", this);
                return;
            }
            DroppedItem drop = Instantiate(droppedItemPrefab, transform.position, Quaternion.identity);
            drop.Initialize(definition);
        }

        private ItemEffectContext MakeContext(ItemInstance instance)
            => new ItemEffectContext { Owner = this, Instance = instance };

        /// <summary>장점 효과 + 위험 요소의 조건부 패널티 효과를 하나의 파이프라인으로 열거.</summary>
        private static IEnumerable<ItemEffect> EnumerateEffects(ItemDefinition def)
        {
            if (def.effects != null)
            {
                for (int i = 0; i < def.effects.Count; i++)
                {
                    if (def.effects[i] != null)
                        yield return def.effects[i];
                }
            }
            if (def.risks != null)
            {
                for (int i = 0; i < def.risks.Count; i++)
                {
                    if (def.risks[i].conditionalPenalty != null)
                        yield return def.risks[i].conditionalPenalty;
                }
            }
        }

        private ItemInstance FindByCode(string itemCode)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Definition.itemCode == itemCode)
                    return _slots[i];
            }
            return null;
        }

        /// <summary>동일 코드 아이템의 총 소지 수 (중첩 포함) — 최대 소지 가능 검사용.</summary>
        private int CountPossessed(string itemCode)
        {
            int count = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Definition.itemCode == itemCode)
                    count += _slots[i].StackCount;
            }
            return count;
        }
    }
}
