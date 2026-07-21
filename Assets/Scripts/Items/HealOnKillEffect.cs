// 근거: 아이템 시스템.md — 아이템은 플레이 스타일을 바꾼다(공격적 운영 보상형 효과).
//       모든 아이템은 장점과 위험 요소를 동시에 가진다 → 이 효과는 최대 체력 감소 등 패널티와 함께 쓴다.
// 회복은 Combat.CombatEntity.Heal 단일 경로를 탄다 (HealBlock 상태이상 검사·회복량 통계가 그 안에 있다).
using UnityEngine;

namespace TSWP.Items
{
    /// <summary>적 처치 시 장착자를 회복시키는 효과 (흡혈형 빌드의 축).</summary>
    [CreateAssetMenu(menuName = "TSWP/Items/Effects/Heal On Kill", fileName = "Effect_HealOnKill")]
    public class HealOnKillEffect : ItemEffect
    {
        [Tooltip("처치당 고정 회복량.")]
        public float flatHeal = 8f; // TODO(밸런스): 문서 미정

        [Tooltip("처치당 최대 체력 비례 회복량 (0.02 = 2%).")]
        [Range(0f, 1f)] public float maxHealthRatioHeal; // TODO(밸런스): 문서 미정

        [Tooltip("중첩 획득 시 회복량도 함께 늘어난다.")]
        public bool scaleWithStack = true;

        public override void OnKill(in ItemEffectContext ctx, string enemyId)
        {
            var owner = ctx.Owner;
            if (owner == null || owner.Entity == null || owner.Entity.IsDead) return;

            int stacks = scaleWithStack && ctx.Instance != null ? Mathf.Max(1, ctx.Instance.StackCount) : 1;
            float amount = (flatHeal + owner.Entity.MaxHp * maxHealthRatioHeal) * stacks;
            if (amount <= 0f) return;

            // healerPlayerId를 넘겨 결과 화면 '가장 많은 회복' 집계에 잡히게 한다.
            // 회복 숫자(초록)는 CombatEntity.Heal 안에서 표시되므로 화면에서 바로 확인된다.
            owner.Entity.Heal(amount, owner.PlayerId);
        }
    }
}
