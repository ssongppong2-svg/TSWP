// 근거: 보스 시스템.md — 난이도: 보스는 난이도에 따라 체력/공격력/패턴 속도 '일부 수치만' 변경한다.
// 핵심 기믹은 절대 변경하지 않는다 — 따라서 이 클래스에는 3종 배율 외의 필드를 추가하지 않는다.
using UnityEngine;

namespace TSWP.Bosses
{
    /// <summary>
    /// 난이도별 보스 수치 배율. 체력/공격력/패턴 속도 3종만 존재한다.
    /// 기믹·퍼즐·패턴 구성을 바꾸는 필드는 금지 (난이도가 바뀌어도 공략법은 동일해야 한다).
    /// </summary>
    [System.Serializable]
    public sealed class DifficultyScaling
    {
        [Tooltip("체력 배율.")]
        [SerializeField] private float hpMultiplier = 1f;      // TODO(밸런스): 문서 미정 — 난이도별 구체 수치 없음

        [Tooltip("공격력 배율.")]
        [SerializeField] private float attackMultiplier = 1f;  // TODO(밸런스): 문서 미정

        [Tooltip("패턴 속도 배율 (1보다 크면 패턴 간격·시전이 빨라진다).")]
        [SerializeField] private float patternSpeedMultiplier = 1f; // TODO(밸런스): 문서 미정

        public float HpMultiplier => hpMultiplier;
        public float AttackMultiplier => attackMultiplier;
        public float PatternSpeedMultiplier => patternSpeedMultiplier;
    }
}
