// 근거: 아이템 시스템.md — 유물 예시 "모든 공격에 화상 부여". 아이템은 스탯 증감을 넘어
//       플레이 스타일을 바꾸는 효과를 가진다.
// 근거: 전투 시스템.md — 피격 추가 효과(상태이상)는 DamageInfo.StatusEffects로 실려 가고,
//       실제 부여는 DamageSystem이 StatusEffectController에 위임한다 (부여 로직 중복 금지).
// 근거: 상태이상 시스템.md — 일부 상태이상은 아군에게도 적용될 수 있다(canAffectAllies) → 트롤/위험 요소.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.StatusEffects;

namespace TSWP.Items
{
    /// <summary>
    /// 장착자의 공격에 상태이상을 얹는 효과. 부여 판정은 하지 않고
    /// DamageInfo에 '실어 보내기'만 한다 — 면역/중첩 규칙은 StatusEffectController 한 곳이 소유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Items/Effects/On Hit Status", fileName = "Effect_OnHit_")]
    public class OnHitStatusEffect : ItemEffect
    {
        [Tooltip("공격에 얹을 상태이상 정의 에셋 (Settings/StatusEffects/*.asset).")]
        public StatusEffectData statusToApply;

        [Range(0f, 1f)]
        [Tooltip("발동 확률. 1이면 '모든 공격에' 부여한다.")]
        public float applyChance = 1f; // TODO(밸런스): 문서 미정

        [Tooltip("공격 1회당 더해줄 아이템 피해 성분. 중첩 수만큼 곱해진다 (StackingBehavior.DamageIncrease와 호응).")]
        public float bonusDamagePerStack; // TODO(밸런스): 문서 미정

        public override void OnAttack(in ItemEffectContext ctx, ref DamageInfo damage)
        {
            int stacks = ctx.Instance != null ? Mathf.Max(1, ctx.Instance.StackCount) : 1;

            if (bonusDamagePerStack != 0f)
                damage.ItemBonus += bonusDamagePerStack * stacks;

            if (statusToApply == null) return;

            // NOTE: 드롭 추첨과 달리 전투 난수는 결정성 요구가 없어 UnityEngine.Random을 쓴다.
            //   추후 네트워크 동기화 시 호스트 판정으로 옮긴다. // SYNC: 호스트 권위
            if (applyChance < 1f && Random.value > applyChance) return;

            damage.StatusEffects ??= new List<StatusEffectData>();

            // 같은 공격에 동일 상태이상을 두 번 싣지 않는다 (중첩 금지 규칙과 충돌 방지).
            for (int i = 0; i < damage.StatusEffects.Count; i++)
            {
                if (damage.StatusEffects[i] == statusToApply) return;
            }

            damage.StatusEffects.Add(statusToApply);
        }
    }
}
