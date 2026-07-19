// 근거: 아이템 시스템.md — 성장 구조(단순 능력치보다 플레이 스타일 변화 우선), 유물(점프 2회/자동 부활/전 공격 화상),
//       위험 요소(조건부 패널티 포함)는 동일 파이프라인의 '음수 효과'로 구현한다.
// 스탯 수정만으로 표현 불가한 효과(투사체 분열, 자동 부활 등)를 다형 모듈로 표현하는 추상 SO.
using UnityEngine;
using TSWP.Combat;

namespace TSWP.Items
{
    /// <summary>효과 훅 호출 시 전달되는 문맥. 장착자와 아이템 인스턴스(중첩 수 참조)를 담는다.</summary>
    public struct ItemEffectContext
    {
        /// <summary>장착자의 장비 컴포넌트. Owner.gameObject로 장착자 오브젝트 접근.</summary>
        public PlayerEquipment Owner;

        /// <summary>대상 아이템 인스턴스. StackCount로 중첩 배율(효과 중첩/지속시간 증가/피해량 증가)을 산출한다.</summary>
        public ItemInstance Instance;
    }

    /// <summary>아이템 효과 추상 모듈. 장점 효과와 위험 요소(음수 효과)가 동일 훅 파이프라인을 공유한다.
    /// 추상 클래스이므로 CreateAssetMenu는 붙이지 않는다 — 구체 파생 클래스에
    /// [CreateAssetMenu(menuName = "TSWP/Items/Effects/...")]를 붙일 것.</summary>
    public abstract class ItemEffect : ScriptableObject
    {
        /// <summary>효과 설명 (UI 툴팁/디버그용).</summary>
        [TextArea]
        public string effectDescription;

        /// <summary>장착 즉시 호출. 소비 아이템의 경우 PlayerEquipment.UseConsumable에서 '사용 시 1회 발동' 훅으로 재사용된다.</summary>
        public virtual void OnEquip(in ItemEffectContext ctx) { }

        /// <summary>해제(버리기/교체) 시 호출. OnEquip에서 건 상태를 반드시 되돌린다.</summary>
        public virtual void OnUnequip(in ItemEffectContext ctx) { }

        /// <summary>장착자의 공격 확정 직전 호출. damage 수정 가능 — 예: 모든 공격에 화상 부여(유물), 피해량 증가 중첩.</summary>
        public virtual void OnAttack(in ItemEffectContext ctx, ref DamageInfo damage) { }

        /// <summary>치명타 발생 시 호출. 조건부 패널티(예: 치명타 미발생 시 공격력 -10%)는
        /// OnAttack에서 비치명타를 감지해 음수 modifier를 거는 식으로 같은 파이프라인에서 처리한다.</summary>
        public virtual void OnCrit(in ItemEffectContext ctx, ref DamageInfo damage) { }

        /// <summary>장착자가 적 처치 시 호출.</summary>
        public virtual void OnKill(in ItemEffectContext ctx, string enemyId) { }

        /// <summary>장착자 사망 시 호출. true 반환 시 사망 무효 — 예: 사망 시 1회 자동 부활 유물.
        /// 실제 부활 처리(공유 부활 미소모 여부 포함)는 Combat/Core 측 소관. // NOTE(기획 확인 필요)</summary>
        public virtual bool OnDeath(in ItemEffectContext ctx) => false;

        /// <summary>매 프레임 호출 (지속형 효과/버프 타이머용).</summary>
        public virtual void OnTick(in ItemEffectContext ctx, float deltaTime) { }
    }
}
