// 근거: 직업 시스템.md — 기본 공격: 제한 없이 사용할 수 있고, 직업마다 공격 방식/사거리/공격 속도/피해량이 다르다.
// 자원/횟수 제한이 없으므로 코스트 필드를 두지 않는다 (스펙 BasicAttackProfile notes).
using UnityEngine;

namespace TSWP.Jobs
{
    /// <summary>기본 공격 방식 — 직업마다 다르다. 신규 직업 추가 시 확장.</summary>
    public enum BasicAttackType
    {
        Melee,      // 근접 타격 (예: warrior 검격, shieldbearer 방패 타격)
        Projectile, // 투사체 발사 (예: archer 화살, mage 마법 탄환)
        Throw,      // 투척 (예: bomber 폭탄 투척 — 포물선/폭발 판정)
        Injection,  // 주사 (예: doctor — 대상 회복. 적에게 맞추면 적이 회복되는 트롤 요소)
    }

    /// <summary>직업별 기본 공격 프로파일. JobDefinition에 내장되는 직렬화 클래스 (ARCHITECTURE.md §4).</summary>
    [System.Serializable]
    public class BasicAttackProfile
    {
        [Tooltip("공격 방식 — 직업마다 다르다.")]
        [SerializeField] private BasicAttackType attackType = BasicAttackType.Melee;

        [Tooltip("사거리 (월드 유닛).")]
        [SerializeField] private float range = 1f;       // TODO(밸런스): 문서 미정 — 개별 직업 데이터에서 정의

        [Tooltip("초당 공격 횟수.")]
        [SerializeField] private float attackSpeed = 1f; // TODO(밸런스): 문서 미정 — 개별 직업 데이터에서 정의

        [Tooltip("1회 피해량 (DamageInfo.BaseDamage 성분).")]
        [SerializeField] private float damage = 10f;     // TODO(밸런스): 문서 미정 — 개별 직업 데이터에서 정의

        public BasicAttackType AttackType => attackType;
        public float Range => range;
        public float AttackSpeed => attackSpeed;
        public float Damage => damage;
    }
}
