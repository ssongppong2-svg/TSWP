// 근거: 아이템 시스템.md — 유물 예시 "점프를 2번 할 수 있게 된다" (플레이 방식을 바꾸는 성장).
// 근거: 게임 시작과 선택, 직업, 플레이.md(밈 모드 중력 반전) — 위쪽 방향은 PlayerController.IsGravityInverted를 따른다.
// 구현 메모: Player.PlayerController는 공중 점프 API를 제공하지 않으므로(수정 금지 폴더),
//   장착 중에만 강체 속도를 직접 세워 추가 점프를 만든다. 컨트롤러가 코요테 타임으로
//   이미 점프를 처리한 프레임과 겹치지 않도록 '공중에 뜬 지 minAirborneTime 이상'을 요구한다.
using UnityEngine;
using TSWP.Art;
using TSWP.Player;

namespace TSWP.Items
{
    /// <summary>공중 추가 점프를 부여하는 유물 효과. 착지하면 횟수가 회복된다.</summary>
    [CreateAssetMenu(menuName = "TSWP/Items/Effects/Double Jump", fileName = "Effect_DoubleJump")]
    public class DoubleJumpEffect : ItemEffect
    {
        // ItemInstance 상태 슬롯 — 효과 SO는 전 플레이어가 공유하므로 상태를 여기 두면 안 된다.
        private const int SlotJumpsLeft = 0;   // 남은 공중 점프 횟수
        private const int SlotAirTime = 1;     // 공중에 뜬 시간(초)

        [Tooltip("추가로 얻는 공중 점프 횟수 (1 = 2단 점프).")]
        [Min(1)] public int extraJumps = 1;

        [Tooltip("추가 점프의 상승 속도. PlayerController의 jumpSpeed와 맞춰 둔다.")]
        public float jumpSpeed = 12f; // TODO(밸런스): 문서 미정 — 컨트롤러 기본값과 동일하게 시작

        [Tooltip("이 시간 이상 공중에 떠 있어야 추가 점프가 발동한다(코요테 점프와 겹침 방지).")]
        public float minAirborneTime = 0.15f;

        [Tooltip("중첩 획득 시 점프 횟수도 함께 늘어난다.")]
        public bool scaleWithStack = true;

        public override void OnEquip(in ItemEffectContext ctx)
        {
            ctx.Instance?.SetState(this, SlotJumpsLeft, MaxJumps(ctx));
            ctx.Instance?.SetState(this, SlotAirTime, 0f);
        }

        public override void OnTick(in ItemEffectContext ctx, float deltaTime)
        {
            PlayerEquipment owner = ctx.Owner;
            if (owner == null || ctx.Instance == null) return;

            PlayerController controller = owner.Controller;
            Rigidbody2D body = owner.Body;
            if (controller == null || body == null) return; // 플레이어가 아닌 장착자 — 조용히 생략

            if (controller.IsGrounded)
            {
                // 착지 — 횟수 회복, 체공 시간 초기화.
                ctx.Instance.SetState(this, SlotJumpsLeft, MaxJumps(ctx));
                ctx.Instance.SetState(this, SlotAirTime, 0f);
                return;
            }

            float airTime = ctx.Instance.GetState(this, SlotAirTime) + deltaTime;
            ctx.Instance.SetState(this, SlotAirTime, airTime);

            if (airTime < minAirborneTime) return;
            if (!controller.CanControl) return; // 기절/빙결/사망 중에는 발동하지 않는다

            IPlayerInput input = controller.InputSource;
            if (input == null || !input.JumpPressed) return;

            int jumpsLeft = Mathf.RoundToInt(ctx.Instance.GetState(this, SlotJumpsLeft));
            if (jumpsLeft <= 0) return;

            ctx.Instance.SetState(this, SlotJumpsLeft, jumpsLeft - 1);

            // 중력 반전(밈 모드) 상태에서는 '위'가 아래쪽이다.
            float upSign = controller.IsGravityInverted ? -1f : 1f;
            Vector2 velocity = body.linearVelocity; // Unity 6: rigidbody.velocity 제거됨
            velocity.y = jumpSpeed * upSign;
            body.linearVelocity = velocity;

            // 연출 — VfxSpawner가 없으면 조용히 생략된다.
            var spawner = VfxSpawner.Instance;
            if (spawner != null) spawner.Play(VfxId.JumpDust, owner.transform.position);
        }

        private int MaxJumps(in ItemEffectContext ctx)
        {
            int stacks = scaleWithStack && ctx.Instance != null ? Mathf.Max(1, ctx.Instance.StackCount) : 1;
            return Mathf.Max(1, extraJumps) * stacks;
        }
    }
}
