// 근거: 아이템 시스템.md — 유물 예시 "사망 시 1회 자동 부활".
// 근거: 전투 시스템.md / ARCHITECTURE.md §5 — 부활·게임오버 판정은 Core.SharedReviveSystem 한 곳.
//   자동 부활은 '공유 부활 횟수를 소모하지 않는' 예외이므로, 여기서는 사망 무효(true)만 반환하고
//   실제 Revive() 호출은 PlayerEquipment.OnOwnerDied가 수행한다 (판정 지점 중복 금지).
// NOTE(기획 확인 필요): 자동 부활이 공유 부활 횟수를 소모하지 않는 것으로 해석했다.
//   소모하는 쪽으로 확정되면 PlayerEquipment 쪽 호출을 TryReviveShared로 바꾸면 된다.
using UnityEngine;
using TSWP.Art;

namespace TSWP.Items
{
    /// <summary>사망을 정해진 횟수만큼 무효화하는 유물 효과.</summary>
    [CreateAssetMenu(menuName = "TSWP/Items/Effects/Auto Revive", fileName = "Effect_AutoRevive")]
    public class AutoReviveEffect : ItemEffect
    {
        private const int SlotUsed = 0; // 이번 런에서 이미 쓴 횟수

        [Tooltip("장착 중 자동 부활이 가능한 횟수. 소진하면 아이템은 남지만 발동하지 않는다.")]
        [Min(1)] public int usesPerEquip = 1;

        [Tooltip("중첩 획득 시 사용 가능 횟수도 늘어난다.")]
        public bool scaleWithStack = true;

        [Tooltip("부활 직후 추가로 부여할 무적 시간(초). 0이면 CombatEntity 기본값만 적용.")]
        public float extraInvincibility = 1f; // TODO(밸런스): 문서 미정

        public override void OnEquip(in ItemEffectContext ctx)
        {
            // 새로 장착하면 사용 횟수를 초기화한다 (교체로 되찾은 아이템도 다시 쓸 수 있다).
            ctx.Instance?.SetState(this, SlotUsed, 0f);
        }

        public override bool OnDeath(in ItemEffectContext ctx)
        {
            if (ctx.Instance == null) return false;

            int used = Mathf.RoundToInt(ctx.Instance.GetState(this, SlotUsed));
            int max = Mathf.Max(1, usesPerEquip) *
                      (scaleWithStack ? Mathf.Max(1, ctx.Instance.StackCount) : 1);
            if (used >= max) return false;

            ctx.Instance.SetState(this, SlotUsed, used + 1);

            if (extraInvincibility > 0f && ctx.Owner != null && ctx.Owner.Entity != null)
            {
                // 상시 무적 API는 존재하지 않는다 — 유한 타이머만 사용 (전투 시스템.md '무적').
                ctx.Owner.Entity.SetInvincibleFor(extraInvincibility);
            }

            var spawner = VfxSpawner.Instance;
            if (spawner != null && ctx.Owner != null)
                spawner.Play(VfxId.Buff, ctx.Owner.transform.position);

            return true; // 사망 무효 — 호출자(PlayerEquipment)가 Revive()를 실행한다
        }
    }
}
