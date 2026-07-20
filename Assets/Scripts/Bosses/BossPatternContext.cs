// 근거: 보스 시스템.md — 패턴은 '공정하지만 방심하면 치명적'이어야 한다(예고 필수) /
//       난이도는 체력·공격력·패턴 속도 3종만 조정 / 광폭화는 공속·이속·신규 패턴·기존 패턴 강화.
// 이 클래스는 '패턴 실행에 필요한 모든 것'을 한 번에 넘기는 운반체다.
// BossPatternRunner(전략 구현)가 BossController의 내부를 직접 알지 않도록 격리한다 —
// 보스 2번을 추가할 때 새 Runner는 이 컨텍스트만 보면 되고 BossController는 수정하지 않는다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;

namespace TSWP.Bosses
{
    /// <summary>
    /// 패턴 1회 실행분의 실행 컨텍스트. BossController가 소유하고 재사용한다(GC 방지).
    /// Runner는 이 객체를 저장하지 말고 Tick 인자로 받은 것만 사용한다.
    /// </summary>
    public sealed class BossPatternContext
    {
        /// <summary>소유 보스 컨트롤러 (페이즈 조회/기믹 접근용).</summary>
        public BossController Owner { get; private set; }

        /// <summary>보스 전투 유닛 — DamageInfo.Source로 사용한다 (아군 판정은 TeamType 비교).</summary>
        public CombatEntity Boss { get; private set; }

        public Transform BossTransform { get; private set; }

        /// <summary>보스 물리 몸체. 없을 수 있다(정적 보스) — Runner는 반드시 null 체크할 것.</summary>
        public Rigidbody2D BossBody { get; private set; }

        /// <summary>보스 AI 4요소 컨텍스트 (플레이어 위치/체력/행동/퍼즐 진행).</summary>
        public BossAIContext Ai { get; private set; }

        /// <summary>지금 실행 중인 패턴 SO.</summary>
        public BossPattern Pattern { get; private set; }

        /// <summary>최종 피해량 — 난이도 공격 배율 × 광폭화 강화 배율이 이미 반영된 값.</summary>
        public float Damage { get; private set; }

        /// <summary>속도 배율(1보다 크면 빠름). 시전·지속 시간은 Scale()로 나눠 쓴다.</summary>
        public float SpeedMultiplier { get; private set; } = 1f;

        public bool IsEnraged { get; private set; }

        public Vector2 BossPosition => BossTransform != null ? (Vector2)BossTransform.position : Vector2.zero;

        /// <summary>현재 난이도 — 소환물 스폰 등에 필요. GameFlowManager가 없으면 Human.</summary>
        public Difficulty Difficulty =>
            GameFlowManager.Instance != null ? GameFlowManager.Instance.SelectedDifficulty : Difficulty.Human;

        // ── BossController 전용 초기화 ────────────────────────────
        internal void Bind(BossController owner, CombatEntity boss, Rigidbody2D body, BossAIContext ai)
        {
            Owner = owner;
            Boss = boss;
            BossTransform = boss != null ? boss.transform : null;
            BossBody = body;
            Ai = ai;
        }

        internal void SetExecution(BossPattern pattern, float damage, float speedMultiplier, bool isEnraged)
        {
            Pattern = pattern;
            Damage = damage;
            SpeedMultiplier = Mathf.Max(0.01f, speedMultiplier);
            IsEnraged = isEnraged;
        }

        // ── Runner 편의 API ───────────────────────────────────────

        /// <summary>속도 배율을 반영한 실제 소요 시간. 배율이 클수록 짧아진다.</summary>
        public float Scale(float seconds) => seconds / SpeedMultiplier;

        /// <summary>가장 가까운 살아있는 플레이어의 위치. 플레이어가 하나도 없으면 false.</summary>
        public bool TryGetNearestPlayerPosition(out Vector2 position)
        {
            position = BossPosition;
            if (Ai == null || Ai.PlayerPositions.Count == 0) return false;

            float best = float.MaxValue;
            Vector2 origin = BossPosition;
            for (int i = 0; i < Ai.PlayerPositions.Count; i++)
            {
                float d = Vector2.SqrMagnitude(Ai.PlayerPositions[i] - origin);
                if (d >= best) continue;
                best = d;
                position = Ai.PlayerPositions[i];
            }
            return best < float.MaxValue;
        }

        /// <summary>플레이어 위치 목록 (읽기 전용). 거미줄 설치 지점 등에 사용.</summary>
        public IReadOnlyList<Vector2> PlayerPositions =>
            Ai != null ? (IReadOnlyList<Vector2>)Ai.PlayerPositions : System.Array.Empty<Vector2>();

        /// <summary>연출 재생 — VfxSpawner가 씬에 없으면 조용히 생략된다(로직은 연출에 의존하지 않는다).</summary>
        public void PlayVfx(string vfxId, Vector2 position)
        {
            if (string.IsNullOrEmpty(vfxId)) return;
            Art.VfxSpawner.Instance?.Play(vfxId, position);
        }
    }
}
