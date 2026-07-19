// 근거: 적 시스템.md — 특수 적: 고유 능력을 가진 적. 예시: 힐러, 자폭, 저격수, 소환사, 버퍼, 디버퍼.
// 특수 적은 일반 적과 함께 등장해 전투를 더욱 복잡하게 만든다.
using UnityEngine;

namespace TSWP.Enemies
{
    /// <summary>특수 적 고유 능력 6종 (문서 예시). 확장 가능 — 새 능력은 SpecialAbility 파생으로 추가.</summary>
    public enum SpecialAbilityType
    {
        Healer,       // 아군 적 치유
        SelfDestruct, // 자폭 — 접근 후 폭발 (폭발 판정 → 구조물 파괴 연계)
        Sniper,       // 저격 — 장거리 고피해 단발
        Summoner,     // 소환 — 추가 적 생성 (스폰 규칙 준수)
        Buffer,       // 버퍼 — 아군 적 강화
        Debuffer,     // 디버퍼 — 플레이어 약화 (상태이상 부여)
    }

    /// <summary>
    /// 특수 능력 실행 추상 SO. 구체 능력은 파일 1개 = 클래스 1개로 파생하고,
    /// [CreateAssetMenu(menuName = "TSWP/Enemies/Abilities/...")]는 파생 클래스에만 붙인다
    /// (추상 클래스는 에셋 생성 불가이므로 여기엔 붙이지 않는다).
    /// EnemyAI의 UseAbility 행동이 CanExecute 스코어링 → Execute 실행 순으로 호출한다.
    /// </summary>
    public abstract class SpecialAbility : ScriptableObject
    {
        [Tooltip("능력 분류 — AI 보정/UI 표기용.")]
        [SerializeField] private SpecialAbilityType abilityType;

        [Tooltip("재사용 대기시간(초). 난이도 패턴 속도 배율로 나눠 적용된다.")]
        [SerializeField, Min(0.1f)] private float cooldown = 8f; // TODO(밸런스): 문서 미정

        [Tooltip("발동 사거리(월드 유닛).")]
        [SerializeField, Min(0f)] private float range = 5f; // TODO(밸런스): 문서 미정

        public SpecialAbilityType AbilityType => abilityType;
        public float Cooldown => cooldown;
        public float Range => range;

        /// <summary>실행 가능 여부 (대상 유무/사거리 등). EnemyAI의 UseAbility 스코어링이 조회한다.</summary>
        public abstract bool CanExecute(EnemyController owner, EnemyAIContext context);

        /// <summary>능력 실행. // SYNC: 호스트 권위 — 발동 판정·효과 적용은 호스트 전용, 클라는 연출 복제.</summary>
        public abstract void Execute(EnemyController owner, EnemyAIContext context);

        // TODO(구현): 파생 클래스 예정 (각각 별도 파일 — 파일명 = 클래스명 1:1) —
        //   HealerAbility       : context.allyPositions 근처 아군 CombatEntity.Heal 호출
        //   SelfDestructAbility : 접근 후 DamageInfo.IsExplosive=true 범위 피해 + 자기 파괴 (구조물 파괴 연계)
        //   SniperAbility       : 조준선 표시(플레이어가 반응할 시간 제공) 후 고피해 단발 — 공략 방법 존재 원칙 ⑤
        //   SummonerAbility     : SpawnManager.TrySpawnNear 경유 소환 (플레이어 근처 갑툭튀 금지 규칙 준수)
        //   BufferAbility       : 아군 적에게 StatusEffects 버프 적용
        //   DebufferAbility     : 플레이어에게 StatusEffectData(둔화/약화 등) 부여 — CC 역할군 연계
    }
}
