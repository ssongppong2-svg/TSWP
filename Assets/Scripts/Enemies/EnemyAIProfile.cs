// 근거: 적 시스템.md — 적 1종 = SO 에셋 1개(데이터-로직 분리). "난이도는 숫자가 아니라 행동·조합·패턴으로 만든다".
//       → '어떻게 행동하는가'는 적의 정체성이므로 프리팹이 아니라 EnemyData가 소유해야 한다.
// 근거: ARCHITECTURE.md §3-2 — 문서 미정 수치는 필드로 개방한다.
//
// 설계 메모(확장성):
//   이 프로파일이 없으면 감지거리·후퇴 임계치 같은 '성격' 값이 EnemyAI 컴포넌트(=프리팹)에 박혀서
//   적 1종을 추가할 때마다 프리팹을 복제해야 한다. 프로파일을 EnemyData로 올리면
//   프리팹 1개(몸통) + EnemyData N개 = 적 N종이 성립한다. 레이어/탐지 버퍼 같은
//   '씬 환경' 값만 EnemyAI 컴포넌트에 남긴다.
using System;
using UnityEngine;

namespace TSWP.Enemies
{
    /// <summary>
    /// 적의 AI 성격(감지/판단/거리 유지). EnemyData에 내장되며 EnemyAI가 매 판단마다 조회한다.
    /// 값 변경은 에셋 편집만으로 끝나고 코드/프리팹 수정이 필요 없다.
    /// </summary>
    [Serializable]
    public sealed class EnemyAIProfile
    {
        [Header("판단 주기")]
        [Tooltip("AI 재판단 간격(초). 매 프레임 판단을 피해 8인 멀티에서 부하를 줄인다.")]
        [Min(0.02f)] public float decisionInterval = 0.2f; // TODO(밸런스): 문서 미정

        [Header("감지")]
        [Tooltip("플레이어를 인지하는 최대 거리. 원거리 적은 사거리보다 넉넉히 잡는다.")]
        [Min(0f)] public float detectionRange = 12f; // TODO(밸런스): 문서 미정

        [Tooltip("주변 아군 적을 파악하는 반경 (AI 6요소 중 '아군 위치').")]
        [Min(0f)] public float allyScanRadius = 8f; // TODO(밸런스): 문서 미정

        [Header("행동 성향")]
        [Tooltip("체력이 이 비율 이하로 떨어지면 후퇴를 고려한다. 0이면 절대 후퇴하지 않는다(우직한 추격형).")]
        [Range(0f, 1f)] public float retreatHealthRatio = 0.25f; // TODO(밸런스): 문서 미정

        [Tooltip("공격 사거리의 이 비율 안쪽으로는 더 접근하지 않는다(사거리 유지). " +
                 "0이면 사거리와 무관하게 계속 밀고 들어간다. 원거리 적은 반드시 0보다 크게 둘 것.")]
        [Range(0f, 1f)] public float holdDistanceRatio = 0.85f; // TODO(밸런스): 문서 미정

        [Header("행동 우선순위 (유틸리티 점수 — 높을수록 우선)")]
        // 점수를 데이터로 열어 두면 '겁 많은 적/저돌적인 적'을 코드 수정 없이 만들 수 있다.
        [Tooltip("공격 가능할 때의 점수.")]
        public float attackScore = 100f;

        [Tooltip("고유 능력 사용 점수 — 보통 공격보다 낮게 두어 쿨타임 자원을 아낀다.")]
        public float abilityScore = 90f;

        [Tooltip("후퇴 기본 점수 (저체력일수록 추가 가산된다).")]
        public float retreatScore = 80f;

        [Tooltip("시야가 막혔을 때 우회 점수.")]
        public float repositionScore = 70f;

        [Tooltip("접근 기본 점수 (대상이 멀수록 거리만큼 가산된다).")]
        public float approachScore = 10f;

        /// <summary>후퇴 판정을 아예 사용하지 않는 성향인가 (추격 전용 적).</summary>
        public bool NeverRetreats => retreatHealthRatio <= 0f;

        /// <summary>사거리 유지를 사용하는가.</summary>
        public bool KeepsDistance => holdDistanceRatio > 0f;
    }
}
