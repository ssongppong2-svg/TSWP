// 근거: 맵 시스템.md / 방 시스템.md — 구조물은 맵의 일부이며 상호작용 가능,
//   "일부 구조물은 폭탄으로만 파괴할 수 있다", "건축가는 새로운 구조물을 설치할 수 있다".
// 실제 파괴 판정은 Combat.Structure(CombatEntity 파생)가 이 정의를 참조해 수행한다.
using UnityEngine;

namespace TSWP.Map
{
    /// <summary>
    /// 구조물 1종의 데이터 정의 (계약 §4 필드).
    /// 상호작용(레버/버튼/문)은 Player.IInteractable 구현 컴포넌트가,
    /// 파괴는 Combat 피해 파이프라인이 이 플래그를 읽어 처리한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Map/Structure Definition", fileName = "Structure_")]
    public class StructureDefinition : ScriptableObject
    {
        [Header("종류")]
        public StructureType structureType;
        public string displayName = "";

        [Header("파괴 규칙")]
        [Tooltip("파괴 가능 여부. false면 어떤 공격으로도 파괴 불가 (벽 등).")]
        public bool isDestructible = true;
        [Tooltip("폭탄으로만 파괴 가능 (일반 공격 파괴 불가 — 맵 시스템.md).")]
        public bool bombOnlyDestructible;
        [SerializeField] private float maxHp = 50f; // TODO(밸런스): 문서 미정 — 구조물 내구도

        [Header("상호작용")]
        [Tooltip("레버/버튼/문 등 E키 상호작용 가능 여부 (Player.IInteractable 연동).")]
        public bool isInteractable;

        [Header("건축가")]
        [Tooltip("건축가(jobId: architect)가 설치 가능한 구조물인지 — 구조물 스폰 API 공개 대상.")]
        public bool architectBuildable;

        [Header("비주얼")]
        public Sprite sprite; // 도트 시스템.md — 소품 16px 기준

        public float MaxHp => maxHp;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 폭탄 전용 파괴는 '파괴 가능'의 부분집합 — 모순 데이터 예방.
            if (bombOnlyDestructible && !isDestructible)
                isDestructible = true;
        }
#endif
    }
}
