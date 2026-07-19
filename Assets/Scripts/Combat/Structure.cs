// 근거: 전투 시스템.md — 구조물은 일반 공격으로 파괴되지 않는다. 폭발 공격만 파괴 가능.
//   예외: 건축가가 설치한 일부 구조물 (폭발이 아니어도 파괴 가능).
// 구조물도 동일 전투 규칙을 따르므로 CombatEntity 파생 — 파괴 판정만 추가한다.
// SYNC: 호스트 권위, 추후 NGO NetworkVariable — 파괴 상태.
using UnityEngine;

namespace TSWP.Combat
{
    /// <summary>
    /// 파괴 가능 구조물의 전투 측면 (나무 상자·폭탄 상자·문 등).
    /// 배치/상호작용 데이터는 Map.StructureDefinition 소관 — 이 클래스는 피해 규칙만 담당.
    /// </summary>
    public class Structure : CombatEntity
    {
        [Header("파괴 규칙")]
        [Tooltip("true(기본): 폭발 공격(DamageInfo.IsExplosive)만 이 구조물을 파괴할 수 있다.")]
        [SerializeField] private bool bombOnlyDestructible = true;

        [Tooltip("건축가(architect)가 설치한 예외 구조물 — 폭발이 아니어도 파괴 가능 (전투 시스템.md 예외 조항). " +
                 "건축가 구조물 전부가 아니라 '일부'만 이 플래그를 켠다.")]
        [SerializeField] private bool architectBuilt;

        public bool BombOnlyDestructible => bombOnlyDestructible;
        public bool ArchitectBuilt => architectBuilt;

        protected override void Awake()
        {
            base.Awake();
            // 구조물은 밀려나지 않는다 — 넉백 비대상.
            IsKnockbackImmune = true;
        }

        /// <summary>
        /// 이 공격으로 피해를 입을 수 있는가. DamageSystem 파이프라인 3단계(구조물 폭발 판정)에서 호출.
        /// 폭발 공격이거나, 애초에 일반 공격 파괴 허용이거나, 건축가 설치 예외면 통과.
        /// </summary>
        public bool CanBeDamagedBy(in DamageInfo info)
        {
            if (info.IsExplosive) return true;        // 폭발 공격은 항상 유효
            if (!bombOnlyDestructible) return true;   // 일반 공격 파괴 허용 구조물
            if (architectBuilt) return true;          // 건축가 설치 예외
            return false;
        }

        /// <summary>건축가 스킬이 런타임 설치 시 호출 — 예외 플래그 부여.</summary>
        public void MarkArchitectBuilt(bool allowNormalDestruction)
        {
            architectBuilt = allowNormalDestruction;
            // TODO(직업): 건축가(jobId: "architect") 설치 스킬에서 어떤 구조물이 예외인지는 직업 문서 확정 대기.
        }

        // TODO(연출): 파괴 시 잔해 스프라이트/사운드 — Died 이벤트(CombatEntity)를 Map/Art 측이 구독해 처리.
    }
}
