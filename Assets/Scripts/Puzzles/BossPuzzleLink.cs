// 근거: 보스 시스템.md / 퍼즐 시스템.md — 모든 보스는 최소 1개의 협동 퍼즐을 가진다.
//       퍼즐을 해결하면 보스에게 큰 피해를 주거나 다음 페이즈로 진행한다.
using System;
using UnityEngine;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 보스와 협동 퍼즐의 연결 데이터. BossData가 이 구조를 보유한다.
    /// </summary>
    [Serializable]
    public class BossPuzzleLink
    {
        [Tooltip("대상 보스 식별자 (BossData.bossId와 일치해야 한다).")]
        public string bossId;

        [Tooltip("이 보스전에서 사용할 협동 퍼즐 정의.")]
        public PuzzleDefinition puzzle;

        [Tooltip("퍼즐 성공 시 효과.")]
        public BossPuzzleSuccessEffect successEffect = BossPuzzleSuccessEffect.BigDamageToBoss;

        [Tooltip("BigDamageToBoss일 때 보스 최대 체력 대비 피해 비율.")]
        [Range(0f, 1f)] public float bigDamageRatio = 0.2f; // TODO(밸런스): 문서 미정

        [Tooltip("AdvancePhase일 때 진행할 페이즈 인덱스. -1이면 다음 페이즈.")]
        public int targetPhaseIndex = -1;
    }
}
