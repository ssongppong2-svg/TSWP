// 근거: 전투 시스템.md — 팀/아군 판정은 레이어가 아닌 TeamType 비교 (아군사격 상시 존재), 피해는 DamageSystem 단일 경로.
// 근거: ARCHITECTURE.md §3-6 — 레이어 기반 팀 필터 금지.
// 직업 스킬 구현체들이 공통으로 쓰는 범위 탐색/대상 판정 유틸.
// Unity 6 제거 API 회피: OverlapCircleNonAlloc 대신 ContactFilter2D + List 오버로드를 쓴다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;

namespace TSWP.Jobs
{
    /// <summary>스킬 구현체 공용 범위 질의 헬퍼. 상태를 갖지 않는 정적 유틸.</summary>
    public static class SkillTargeting
    {
        // 매 발동마다 List를 새로 만들면 8인 난전에서 GC가 튄다 — 공용 버퍼를 재사용한다.
        // (게임 로직은 단일 스레드이므로 정적 버퍼로 충분하다.)
        private static readonly List<Collider2D> _overlapBuffer = new List<Collider2D>(32);
        private static ContactFilter2D _filter;
        private static bool _filterReady;

        private static ContactFilter2D NoFilter
        {
            get
            {
                if (!_filterReady)
                {
                    // 팀 필터를 레이어로 하지 않으므로 레이어 마스크를 쓰지 않는다.
                    // 트리거도 포함해야 상자/장치 같은 트리거 콜라이더가 스킬에 맞는다.
                    _filter = new ContactFilter2D { useTriggers = true };
                    _filter.useLayerMask = false;
                    _filter.useDepth = false;
                    _filterReady = true;
                }
                return _filter;
            }
        }

        /// <summary>
        /// 원형 범위 안의 CombatEntity를 results에 채운다 (기존 내용은 지운다).
        /// 결과 리스트를 <b>호출측이 소유</b>하는 이유: 순회 도중 DamageSystem.Apply가 사망 이벤트를 부르고,
        /// 그 콜백이 다시 범위 질의를 하면 공용 버퍼가 갈려 순회가 깨진다.
        /// </summary>
        /// <param name="excludeSelf">이 엔티티는 결과에서 제외한다 (자기 자신 자동 피격 방지). null 허용.</param>
        public static void OverlapEntities(Vector2 center, float radius, CombatEntity excludeSelf,
                                           List<CombatEntity> results)
        {
            if (results == null) return;
            results.Clear();
            if (radius <= 0f) return;

            _overlapBuffer.Clear();
            int count = Physics2D.OverlapCircle(center, radius, NoFilter, _overlapBuffer);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _overlapBuffer[i];
                if (col == null) continue;

                // 자식 콜라이더에 붙어 있어도 본체 엔티티를 찾도록 부모까지 올라간다.
                CombatEntity entity = col.GetComponentInParent<CombatEntity>();
                if (entity == null || entity.IsDead) continue;
                if (excludeSelf != null && entity == excludeSelf) continue;

                // 콜라이더 여러 개짜리 유닛이 두 번 맞지 않게 중복 제거.
                if (results.Contains(entity)) continue;
                results.Add(entity);
            }
        }

        /// <summary>대상이 시전자 기준 전방 부채꼴(halfAngle 도) 안에 있는지.</summary>
        public static bool IsInCone(Vector2 origin, Vector2 forward, Vector2 targetPosition, float halfAngleDegrees)
        {
            if (halfAngleDegrees >= 180f) return true;

            Vector2 to = targetPosition - origin;
            if (to.sqrMagnitude <= 0.0001f) return true; // 겹쳐 있으면 무조건 포함

            return Vector2.Angle(forward, to) <= halfAngleDegrees;
        }

        /// <summary>아군 판정 — 레이어가 아닌 TeamType 비교 (자기 자신은 아군으로 보지 않는다).</summary>
        public static bool IsAlly(CombatEntity source, CombatEntity target)
        {
            if (source == null || target == null) return false;
            if (source == target) return false;
            return source.Team == target.Team;
        }

        /// <summary>대상에서 바깥으로 밀어내는 넉백 정보 (폭발 등 방사형 스킬용).</summary>
        public static KnockbackInfo RadialKnockback(Vector2 center, Vector2 targetPosition, float force, float upward)
        {
            Vector2 dir = targetPosition - center;
            if (dir.sqrMagnitude <= 0.0001f) dir = Vector2.up;
            dir = dir.normalized;
            dir.y += upward; // 살짝 띄워야 날아가는 게 보인다

            return new KnockbackInfo
            {
                Direction = dir.normalized,
                Force = force,
            };
        }

        // ── 프로토타입 표시용 스프라이트 ─────────────────────────────
        // 아직 도트 에셋이 없어 스킬 투척체가 눈에 보이지 않는다.
        // 흰 사각형 스프라이트를 런타임에 1회 만들어 색만 바꿔 쓴다 (Art 에셋 도입 시 교체).
        private static Sprite _squareSprite;

        /// <summary>1x1 흰색 사각형 스프라이트 (PPU 16 기준 16px). 프로토타입 전용.</summary>
        public static Sprite SquareSprite
        {
            get
            {
                if (_squareSprite != null) return _squareSprite;

                var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, // 픽셀아트 — 보간 금지 (도트 시스템.md)
                    wrapMode = TextureWrapMode.Clamp,
                    name = "TSWP_ProtoSquare",
                };
                var pixels = new Color32[16 * 16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(pixels);
                tex.Apply();

                _squareSprite = Sprite.Create(tex, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.5f), 16f);
                _squareSprite.name = "TSWP_ProtoSquare";
                return _squareSprite;
            }
        }
    }
}
