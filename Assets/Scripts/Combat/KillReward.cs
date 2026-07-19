// 근거: 전투 시스템.md — 적 처치 시 경험치·골드·아이템 3종을 획득한다.
// 아이템은 전투의 핵심 성장 요소 (레벨보다 아이템 영향력이 큼) — 실물 드롭 스폰은 Items.ItemDropManager 소관.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Combat
{
    /// <summary>
    /// 처치 보상 데이터. Enemies.EnemyData 등 SO에 직렬화 필드로 내장해 사용하고,
    /// 사망 확정 시(EnemyController가 CombatEntity.Died 구독) Grant를 호출한다.
    /// </summary>
    [Serializable]
    public class KillReward
    {
        [Tooltip("처치 경험치.")]
        public int Exp;   // TODO(밸런스): 문서 미정 — 적별 경험치 수치 미기재

        [Tooltip("처치 골드.")]
        public int Gold;  // TODO(밸런스): 문서 미정 — 적별 골드 수치 미기재

        [Tooltip("드롭 후보 아이템 코드 목록. 실제 스폰/확률 굴림은 Items.ItemDropManager(LootTable) 소관.")]
        public List<string> ItemCodes = new List<string>();

        /// <summary>
        /// 보상 지급 + GameEvents 발행 헬퍼. EnemyKilled 발행 책임은 이 헬퍼 단일 경로로 통일한다
        /// (호출측에서 중복 Raise 금지). // SYNC: 호스트 권위 — 보상 판정은 호스트에서만 실행.
        /// </summary>
        /// <param name="killerPlayerId">처치한 플레이어 ID. 환경/적 상호 처치 등 플레이어가 아니면 -1.</param>
        /// <param name="enemyId">처치된 적 식별자 (통계·업적 카운터 키).</param>
        public void Grant(int killerPlayerId, string enemyId)
        {
            if (killerPlayerId < 0)
                return; // 플레이어 처치가 아니면 보상·통계 없음 (환경사·자멸 등)

            GameEvents.RaiseEnemyKilled(killerPlayerId, enemyId); // 결과 화면 '가장 많은 처치' 집계
            if (Exp > 0) GameEvents.RaiseExpGained(killerPlayerId, Exp);
            if (Gold > 0) GameEvents.RaiseGoldGained(killerPlayerId, Gold);

            // TODO(아이템): ItemCodes의 실물 드롭 스폰은 Items.ItemDropManager에 위임 —
            //   드롭된 아이템은 자유 경쟁 선착순 픽업이며, 획득 통지(ItemAcquired)는 픽업 시점에 DroppedItem이 발행한다.
        }
    }
}
