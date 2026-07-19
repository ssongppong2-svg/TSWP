// 근거: 상태이상 시스템.md — 시너지: 감전+물→범위 감전, 화상+기름→폭발, 빙결+폭탄→얼음 파편 생성.
// "(추가 예정)" 명시 — 결과를 코드에 하드코딩하지 않고 규칙 SO 에셋을 추가하는 것만으로
// 새 시너지를 확장할 수 있는 데이터 주도 구조로 만든다.
using UnityEngine;

namespace TSWP.StatusEffects
{
    /// <summary>
    /// 시너지 촉매 (환경 요소). 환경 오브젝트(물 타일/기름 장판/폭탄 — Map·Puzzles 소유)가
    /// 이 값을 노출하고, 접촉/폭발 시 대상의 StatusEffectController.TryTriggerSynergy(catalyst)를 호출한다.
    /// 새 촉매는 여기 멤버 추가 + 규칙 에셋 추가로 확장한다 (추가 예정 대응).
    /// </summary>
    public enum SynergyCatalyst
    {
        Water, // 물 — 감전과 결합 시 범위 감전
        Oil,   // 기름 — 화상과 결합 시 폭발
        Bomb,  // 폭탄 — 빙결과 결합 시 얼음 파편
    }

    /// <summary>
    /// 시너지 규칙 1건: 유발 상태이상(trigger) + 촉매(catalyst) → 결과(result 파라미터).
    /// 결과는 종류별 분기 없이 "범위 피해 + 범위 상태이상 + 결과 프리팹" 파라미터 조합만으로 표현한다.
    /// 예) 감전+물: areaEffect=감전 데이터 / 화상+기름: areaDamage>0, isExplosive=true / 빙결+폭탄: resultPrefab=얼음 파편.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/StatusEffects/Status Synergy Rule", fileName = "StatusSynergyRule")]
    public class StatusSynergyRule : ScriptableObject
    {
        [Header("조건")]
        [SerializeField] private StatusEffectType triggerEffect; // 시너지 유발 상태이상 (감전/화상/빙결)
        [SerializeField] private SynergyCatalyst catalyst;       // 결합 환경 요소 (물/기름/폭탄)

        [Header("결과 설명")]
        [SerializeField] private string resultDescriptionKo;     // "범위 감전", "폭발", "얼음 파편 생성" 등 (툴팁/디버그용)

        [Header("결과 — 범위 피해")]
        [SerializeField] private float areaRadius = 3f;          // TODO(밸런스): 문서 미정
        [SerializeField] private float areaDamage;               // TODO(밸런스): 문서 미정 (0이면 피해 없음)
        [Tooltip("폭발 판정 여부 — 구조물은 폭발 공격만 파괴 가능 (화상+기름→폭발).")]
        [SerializeField] private bool isExplosive;

        [Header("결과 — 범위 상태이상")]
        [Tooltip("범위 내 대상에게 부여할 상태이상 (감전+물→범위 감전). null이면 없음.")]
        [SerializeField] private StatusEffectData areaEffect;

        [Header("결과 — 프리팹 스폰")]
        [Tooltip("발동 위치에 스폰할 결과 오브젝트 (빙결+폭탄→얼음 파편). null이면 없음.")]
        [SerializeField] private GameObject resultPrefab;

        [Header("후처리")]
        [Tooltip("발동 시 유발 상태이상을 소모(해제)할지 여부. NOTE(기획 확인 필요): 문서 미정.")]
        [SerializeField] private bool consumesTriggerEffect = true;

        // ── 읽기 전용 접근자 ──────────────────────────────────────
        public StatusEffectType TriggerEffect => triggerEffect;
        public SynergyCatalyst Catalyst => catalyst;
        public string ResultDescriptionKo => resultDescriptionKo;
        public float AreaRadius => areaRadius;
        public float AreaDamage => areaDamage;
        public bool IsExplosive => isExplosive;
        public StatusEffectData AreaEffect => areaEffect;
        public GameObject ResultPrefab => resultPrefab;
        public bool ConsumesTriggerEffect => consumesTriggerEffect;

        /// <summary>보유 상태이상 + 접촉 촉매 조합이 이 규칙과 일치하는지.</summary>
        public bool Matches(StatusEffectType trigger, SynergyCatalyst contactCatalyst)
        {
            return triggerEffect == trigger && catalyst == contactCatalyst;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 결과가 전혀 정의되지 않은 빈 규칙 경고.
            if (areaDamage <= 0f && areaEffect == null && resultPrefab == null)
            {
                Debug.LogWarning($"[StatusSynergyRule] {name}: 결과(범위 피해/상태이상/프리팹)가 하나도 없습니다.", this);
            }
        }
#endif
    }
}
