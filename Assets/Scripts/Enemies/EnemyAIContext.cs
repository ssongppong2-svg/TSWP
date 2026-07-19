// 근거: 적 시스템.md — AI는 아래 요소를 고려한다: 플레이어 거리 / 체력 / 시야 / 장애물 / 공격 가능 여부 / 아군 적의 위치.
// 문서의 AI 고려 6요소를 그대로 필드화 — EnemyAI 유틸리티 스코어링의 입력값.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;

namespace TSWP.Enemies
{
    /// <summary>
    /// 적 AI 판단 컨텍스트. 매 결정 주기마다 EnemyAI가 갱신해 재사용한다 (프레임당 할당 최소화).
    /// SpecialAbility 파생 클래스도 이 컨텍스트를 입력으로 받는다.
    /// </summary>
    public sealed class EnemyAIContext
    {
        // ── ① 플레이어 거리 ───────────────────────────────────────
        /// <summary>최근접 생존 플레이어 (편의 참조 — 거리 판단의 기준 대상).</summary>
        public CombatEntity targetPlayer;
        /// <summary>대상 플레이어까지의 거리. 대상 없으면 +Infinity.</summary>
        public float distanceToPlayer = float.PositiveInfinity;

        // ── ② (자신의) 체력 ───────────────────────────────────────
        /// <summary>자기 체력 비율 0~1 — 저체력 후퇴 판단.</summary>
        public float selfHealthRatio = 1f;

        // ── ③ 시야 ────────────────────────────────────────────────
        /// <summary>대상까지 시야가 트여 있는가 (장애물 레이캐스트 결과).</summary>
        public bool hasLineOfSight;

        // ── ④ 장애물 ──────────────────────────────────────────────
        /// <summary>대상과의 직선상에 장애물이 있는가 — 우회(Reposition) 판단.</summary>
        public bool hasObstacleBetween;

        // ── ⑤ 공격 가능 여부 ──────────────────────────────────────
        /// <summary>공격 가능한가 (쿨타임 완료 + 사거리 안 + 상태이상 미차단 + 시야 확보).</summary>
        public bool canAttack;

        // ── ⑥ 아군 적의 위치 ──────────────────────────────────────
        /// <summary>주변 아군 적 위치 목록 — 힐러 대상 탐색/후퇴 방향/진형 판단.</summary>
        public readonly List<Vector2> allyPositions = new List<Vector2>();

        /// <summary>결정 주기 시작 시 초기화 — 인스턴스를 재사용한다.</summary>
        public void Reset()
        {
            targetPlayer = null;
            distanceToPlayer = float.PositiveInfinity;
            selfHealthRatio = 1f;
            hasLineOfSight = false;
            hasObstacleBetween = false;
            canAttack = false;
            allyPositions.Clear();
        }
    }
}
