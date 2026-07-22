// 근거: 전투 시스템.md — "공격은 명확해야 한다 / 피격이 공정해야 한다".
//   "피했는데 맞았다"는 불공정 체감의 최대 원인 — 적의 공격 판정은 보이는 이펙트보다 약간 작게,
//   플레이어 피격 판정은 스프라이트보다 약간 작게 잡아 회피 성공을 후하게 인정한다.
// 원칙(숨은 보정): 플레이어가 눈치채지 못할 만큼 자연스럽게 — 실력을 대체하지 않고 회피 의도를 보조한다.
//   반드시 적→플레이어 방향에만 적용한다. 플레이어의 공격(적 타격 판정)은 절대 축소하지 않으며,
//   적↔적(혼란 등), 아군사격(플레이어→플레이어, DamageSystem 50% 규칙)도 기존 판정 그대로다.
using UnityEngine;
using TSWP.Core;

namespace TSWP.Combat
{
    /// <summary>
    /// 숨은 관용 판정의 단일 창구. 모든 직업·적·보스 15종이 이 헬퍼를 재사용한다
    /// (관용 수치가 파일마다 흩어지면 튜닝이 불가능해진다 — 값은 여기 한 곳에만 둔다).
    /// 모든 메서드의 불변식: 피해자가 플레이어 진영이 아니면 원래 판정 그대로(항상 통과).
    /// </summary>
    public static class HitForgiveness
    {
        // 문서에 없는 게임필 튜닝값 — 하드코딩 대신 문서화된 public static 필드로 노출한다.
        // (씬 컴포넌트가 아니라 static class이므로 [SerializeField] 대신 이 방식을 쓴다.)

        /// <summary>
        /// 적/보스의 근접·광역 판정 반경을 플레이어 상대로 축소하는 배율. 1이면 비활성.
        /// 값 근거: 8% 축소는 이펙트 가장자리 '스침'만 걸러내는 수준 — 플레이어가 "판정이 후하다"고
        /// 눈치채기 시작하는 0.85 미만보다 훨씬 보수적으로 잡는다(숨은 보정 원칙).
        /// </summary>
        public static float hitboxScaleVsPlayer = 0.92f; // TODO(밸런스): 추후 GameFeelConfig SO로 승격

        /// <summary>
        /// 플레이어 피격 인정에 필요한 최소 관통 깊이(월드 유닛). 0이면 비활성.
        /// 값 근거: PPU 16 기준 1픽셀 ≈ 0.0625 — 스프라이트 가장자리 1픽셀 스침만 무시하는 수준.
        /// 이보다 크면 투사체가 몸을 관통하는 것처럼 보여 오히려 판정 불신이 생긴다.
        /// </summary>
        public static float playerHurtboxMargin = 0.06f; // TODO(밸런스): 추후 GameFeelConfig SO로 승격

        /// <summary>
        /// 근접 임팩트 판정 여유(월드 유닛) — 넉백 반동·프레임 간 미세 이동 같은 물리 지터로
        /// 정당한 명중이 헛스윙 처리되는 것을 막는다.
        /// 개시 거리(ShrinkVsPlayers) &lt; 명중 거리(ShrinkVsPlayers + 여유)가 항상 성립해야
        /// 경계에 선 플레이어에게 영원히 헛스윙하는 구간이 생기지 않는다 — 0 미만 금지.
        /// </summary>
        public static float meleeImpactSlack = 0.1f; // TODO(밸런스): 추후 GameFeelConfig SO로 승격

        /// <summary>
        /// 피해자가 플레이어 진영인가. 팀 판정은 레이어가 아닌 CombatEntity.Team 비교(ARCHITECTURE.md §3-6).
        /// 자식 콜라이더(몸통/발)에 붙은 경우도 부모에서 본체를 찾는다 — 프로젝트 규약
        /// (EnvironmentHazard/PlayerInteraction/BossCombatUtil)과 동일. null 안전.
        /// </summary>
        public static bool IsPlayerVictim(Component victim)
        {
            if (victim == null) return false;

            var entity = victim as CombatEntity;
            if (entity == null) entity = victim.GetComponentInParent<CombatEntity>();
            return entity != null && entity.Team == TeamType.Players;
        }

        /// <summary>
        /// 적→플레이어 '데미지용' 판정 반경 축소. 감지/조준/거리측정 등 비데미지 질의에 쓰면
        /// AI 행동 거리가 어긋나므로 반드시 데미지 판정에만 쓸 것.
        /// </summary>
        public static float ShrinkVsPlayers(float radius) => radius * hitboxScaleVsPlayer;

        /// <summary>
        /// 근접 임팩트 순간의 명중 재확인 — 예고(telegraph) 중 회피에 성공했는지 판정한다.
        /// 피해자가 플레이어가 아니면 항상 true(관용은 적→플레이어 전용 — 적↔적 전투 영향 없음).
        /// </summary>
        public static bool MeleeConnects(Vector2 attackerPos, Component victim, float attackRange)
        {
            if (!IsPlayerVictim(victim)) return true; // null 포함 — 관용 미적용 경로는 원래 판정 그대로

            float allowed = ShrinkVsPlayers(attackRange) + meleeImpactSlack;
            return Vector2.Distance(attackerPos, victim.transform.position) <= allowed;
        }

        /// <summary>
        /// 콜라이더 기반 판정(투사체 등)의 관통 깊이 확인 — 가장자리 스침을 무시한다.
        /// 피해자가 플레이어가 아니면 항상 true. 판정 불가(콜라이더 없음/무효)여도 true —
        /// 관용은 어디까지나 보너스이며, 연출·판정 부재가 게임 로직(명중)을 실패시키지 않는다.
        /// </summary>
        public static bool DeepEnough(Collider2D attackCollider, Collider2D victimCollider)
        {
            if (!IsPlayerVictim(victimCollider)) return true;
            if (attackCollider == null) return true;
            if (playerHurtboxMargin <= 0f) return true;

            ColliderDistance2D d = Physics2D.Distance(attackCollider, victimCollider);
            if (!d.isValid) return true;

            // Physics2D.Distance는 겹침일 때 distance가 음수(= -관통 깊이)다.
            // d.distance <= -margin ⇔ 관통 깊이 >= margin.
            return d.distance <= -playerHurtboxMargin;
        }
    }
}
