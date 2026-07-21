// 근거: 아이템 시스템.md — 능력치 증감(체력/공격력/이동속도 등)은 아이템 성장의 기본 축이며,
//       위험 요소(음수 효과)도 같은 파이프라인을 탄다.
// ItemDefinition.statModifiers가 '상시 스탯 변화'를 담당한다면, 이 모듈은 그 변화를
// '효과 모듈'로 붙일 수 있게 한다 — 특히 ItemRisk.conditionalPenalty 칸에 넣어
// 위험 요소를 하나의 재사용 가능한 에셋으로 관리할 때 쓴다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Items
{
    /// <summary>
    /// 장착 시 StatCollection에 modifier를 걸고, 해제 시 되돌리는 효과.
    /// modifier의 Source는 ItemInstance이므로 PlayerEquipment의 해제 경로
    /// (RemoveModifiersFromSource)와 자동으로 맞물린다 — 중첩 장착도 안전하다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Items/Effects/Stat Modifier", fileName = "Effect_Stat_")]
    public class StatModifierEffect : ItemEffect
    {
        [Tooltip("장착 중 적용할 능력치 변화 목록. 음수 값이면 그대로 패널티가 된다.")]
        public List<StatModifierEntry> modifiers = new();

        public override void OnEquip(in ItemEffectContext ctx)
        {
            StatCollection stats = ctx.Owner != null ? ctx.Owner.Stats : null;
            if (stats == null || modifiers == null) return;

            // 중첩 획득 시 PlayerEquipment가 OnEquip을 다시 부르므로,
            // 여기서 StackCount를 곱하면 이중 계산이 된다 — 1회분만 건다.
            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifierEntry entry = modifiers[i];
                if (entry == null) continue;
                stats.AddModifier(new StatModifier(entry.stat, entry.mode, entry.value, ctx.Instance));
            }
        }

        public override void OnUnequip(in ItemEffectContext ctx)
        {
            // PlayerEquipment.RemoveInstance가 이미 같은 Source를 정리하지만,
            // 이 모듈만 따로 떼어 쓰는 경로를 대비해 방어적으로 한 번 더 호출한다(중복 호출 무해).
            // `?.`는 Unity의 오버로드된 ==를 우회하므로 명시적 null 비교를 쓴다.
            if (ctx.Owner == null) return;
            StatCollection stats = ctx.Owner.Stats;
            if (stats == null) return;
            stats.RemoveModifiersFromSource(ctx.Instance);
        }
    }
}
