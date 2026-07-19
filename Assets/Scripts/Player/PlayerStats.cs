// 근거: 직업 시스템.md — 성장은 레벨이 아닌 아이템 (스탯 = base + 아이템 modifier 스택).
// 근거: 전투 시스템.md — 기본 치명타 확률 0% (GameRules.BaseCritChance), 치명타는 아이템·버프로만 획득.
// 근거: 조작과 시스템.md — 모든 플레이어는 동일한 이동 능력 (base 이동속도는 직업 공통).
// 플레이어 스탯 조립 지점 — Items.PlayerEquipment가 Stats.AddModifier / RemoveModifiersFromSource로 아이템 효과를 적용한다.
// SYNC: 호스트 권위, 추후 NGO NetworkVariable — 스탯 변경(아이템 장착/해제)은 호스트 판정 후 동기화.
using UnityEngine;
using TSWP.Core;
using TSWP.Combat;

namespace TSWP.Player
{
    /// <summary>
    /// Core.StatCollection 보유·초기화 컴포넌트.
    /// MaxHealth 변경을 CombatEntity에 전파하고, 체력 HUD 갱신은 GameEvents.RaisePlayerHealthChanged 경유.
    /// </summary>
    [RequireComponent(typeof(CombatEntity))]
    public class PlayerStats : MonoBehaviour
    {
        [Header("기본 스탯 — 직업 문서에 수치 미정, 디자이너 튜닝용")]
        [SerializeField] private float baseMaxHealth = 100f;       // TODO(밸런스): 문서 미정
        [SerializeField] private float baseAttackPower = 10f;      // TODO(밸런스): 문서 미정 (직업별 값은 Jobs 프로파일 소관)
        [SerializeField] private float baseDefense = 0f;           // TODO(밸런스): 문서 미정
        [SerializeField] private float baseMoveSpeed = 5f;         // TODO(밸런스): 문서 미정 — 전 직업 동일 (조작과 시스템.md)
        [SerializeField] private float baseAttackSpeed = 1f;       // TODO(밸런스): 문서 미정
        [SerializeField] private float baseRange = 1f;             // TODO(밸런스): 문서 미정
        [SerializeField] private float baseCooldownReduction = 0f; // TODO(밸런스): 문서 미정
        // 치명타 base는 GameRules.BaseCritChance(0%)로 고정 — 문서 명시 수치라 직렬화 필드를 두지 않는다.

        private CombatEntity _entity;

        /// <summary>스탯 컨테이너 — 아이템/버프 시스템이 modifier를 추가/제거한다.</summary>
        public StatCollection Stats { get; } = new StatCollection();

        public int PlayerId => _entity != null ? _entity.OwnerPlayerId : -1;

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();

            // base 값 일괄 초기화 — 이후 변경은 modifier(가산/승산)로만.
            Stats.SetBase(StatType.MaxHealth, baseMaxHealth);
            Stats.SetBase(StatType.AttackPower, baseAttackPower);
            Stats.SetBase(StatType.Defense, baseDefense);
            Stats.SetBase(StatType.MoveSpeed, baseMoveSpeed);
            Stats.SetBase(StatType.AttackSpeed, baseAttackSpeed);
            Stats.SetBase(StatType.CritChance, GameRules.BaseCritChance); // 기본 0% 고정
            Stats.SetBase(StatType.Range, baseRange);
            Stats.SetBase(StatType.CooldownReduction, baseCooldownReduction);
        }

        private void OnEnable() => Stats.Changed += OnStatChanged;
        private void OnDisable() => Stats.Changed -= OnStatChanged;

        private void Start()
        {
            // CombatEntity 최대 체력 연동 (SetMaxHp 내부에서 HUD 통지 발생).
            _entity.SetMaxHp(Stats.GetValue(StatType.MaxHealth));

            // 초기 HUD 동기화 보장 — 게임플레이 → UI 통지는 GameEvents 경유 (ARCHITECTURE.md §3-5).
            if (PlayerId >= 0)
                GameEvents.RaisePlayerHealthChanged(PlayerId, _entity.CurrentHp, _entity.MaxHp);
        }

        /// <summary>최종 스탯 조회 편의 메서드 — PlayerController(MoveSpeed)·Jobs(AttackPower 등)가 사용.</summary>
        public float GetValue(StatType stat) => Stats.GetValue(stat);

        /// <summary>스탯 변경 반영 — 아이템 장착/해제(modifier 변동) 시 StatCollection이 발행.</summary>
        private void OnStatChanged(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHealth:
                    // 최대 체력 즉시 반영 — 현재 체력 비율 유지 (SetMaxHp가 GameEvents.RaisePlayerHealthChanged 발행).
                    if (_entity != null)
                        _entity.SetMaxHp(Stats.GetValue(StatType.MaxHealth), keepRatio: true);
                    break;

                case StatType.MoveSpeed:
                    // PlayerController가 매 물리 프레임 GetValue로 조회 — 별도 푸시 불필요.
                    break;

                case StatType.CooldownReduction:
                    // TODO: Jobs.SkillCaster의 CooldownTimer.SetDuration 연동 (SkillCaster 측 TODO와 대응).
                    break;

                // AttackPower/AttackSpeed/CritChance/Range는 사용 시점(공격 실행)에 조회 — Jobs 연동 TODO.
            }
        }
    }
}
