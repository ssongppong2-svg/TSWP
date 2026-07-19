// 근거: 상태이상 시스템.md — 상태이상별 데이터 정의.
// 문서에 구체 수치(지속시간/피해량/감소율)가 전혀 명시되지 않아 전부 SerializeField로 열어둔다.
// 상대 규칙만 명시됨: 중독 피해 < 화상 피해, 중독 지속시간 > 화상 지속시간 — 에셋 밸런싱 시 준수할 것.
using UnityEngine;

namespace TSWP.StatusEffects
{
    /// <summary>
    /// 상태이상 1종의 정의 데이터. 상태이상별로 SO 에셋 1개를 만들어 밸런싱 수치를
    /// 코드 수정 없이 인스펙터에서 조정한다. 발생원(직업/아이템/보스/환경)은 이 에셋을 참조해 부여한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/StatusEffects/Status Effect Data", fileName = "StatusEffectData")]
    public class StatusEffectData : ScriptableObject
    {
        [Header("식별")]
        [SerializeField] private StatusEffectType effectType;
        [SerializeField] private string displayNameKo; // 한글 표기명 (화상, 중독 등)

        [Header("지속시간")]
        [SerializeField] private float duration = 3f; // TODO(밸런스): 문서 미정

        [Header("틱 피해 (화상/중독)")]
        [SerializeField] private float tickDamage;     // TODO(밸런스): 문서 미정 (중독 < 화상 규칙만 존재)
        [SerializeField] private float tickInterval = 1f; // TODO(밸런스): 문서 미정

        [Header("이동 피해 (출혈)")]
        [Tooltip("출혈: 이동 거리 1유닛당 추가 피해. 정지 시 무피해.")]
        [SerializeField] private float moveDamagePerUnit; // TODO(밸런스): 문서 미정

        [Header("배율 (1 = 변화 없음)")]
        [SerializeField] private float moveSpeedMultiplier = 1f;   // 감전/둔화용 — TODO(밸런스): 문서 미정
        [SerializeField] private float attackSpeedMultiplier = 1f; // 감전용 — TODO(밸런스): 문서 미정
        [SerializeField] private float attackPowerMultiplier = 1f; // 약화용 — TODO(밸런스): 문서 미정
        [SerializeField] private float damageTakenMultiplier = 1f; // 취약용 — TODO(밸런스): 문서 미정

        [Header("행동 차단 플래그")]
        [SerializeField] private bool blocksMovement; // 속박/기절/빙결
        [SerializeField] private bool blocksAttack;   // 기절/빙결
        [SerializeField] private bool blocksSkill;    // 침묵/기절/빙결
        [SerializeField] private bool blocksHealing;  // 회복 불가

        [Header("해제/CC 규칙")]
        [SerializeField] private bool breaksOnDamage; // 빙결: 피해를 받으면 즉시 해제
        [SerializeField] private bool isCC;           // 군중 제어 여부
        [Tooltip("CC 우선순위. 기절>빙결>속박>둔화>기타. StatusEffectCcPriority 기본값 참고.")]
        [SerializeField] private int ccPriority;

        [Header("전이 (감전)")]
        [SerializeField] private bool canSpread;
        [SerializeField] private float spreadRadius = 2f;          // TODO(밸런스): 문서 미정
        [Range(0f, 1f)]
        [SerializeField] private float spreadChance = 0.5f;        // TODO(밸런스): 문서 미정 ("전이될 수 있다")

        [Header("협동/배신")]
        [Tooltip("설계 철학 ⑤: 일부 상태이상은 아군에게도 적용될 수 있다.")]
        [SerializeField] private bool canAffectAllies;

        [Header("UI")]
        [SerializeField] private Sprite icon; // 픽셀아트 아이콘 (HUD 상태이상 표시용)

        // ── 읽기 전용 접근자 ──────────────────────────────────────
        public StatusEffectType EffectType => effectType;
        public string DisplayNameKo => displayNameKo;
        public float Duration => duration;
        public float TickDamage => tickDamage;
        public float TickInterval => tickInterval;
        public float MoveDamagePerUnit => moveDamagePerUnit;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public float AttackSpeedMultiplier => attackSpeedMultiplier;
        public float AttackPowerMultiplier => attackPowerMultiplier;
        public float DamageTakenMultiplier => damageTakenMultiplier;
        public bool BlocksMovement => blocksMovement;
        public bool BlocksAttack => blocksAttack;
        public bool BlocksSkill => blocksSkill;
        public bool BlocksHealing => blocksHealing;
        public bool BreaksOnDamage => breaksOnDamage;
        public bool IsCC => isCC;
        public int CcPriority => ccPriority;
        public bool CanSpread => canSpread;
        public float SpreadRadius => spreadRadius;
        public float SpreadChance => spreadChance;
        public bool CanAffectAllies => canAffectAllies;
        public Sprite Icon => icon;

        /// <summary>즉발형(넉백/공중 띄우기) 여부 — 지속형 리스트에 넣지 않는다.</summary>
        public bool IsInstantPhysical =>
            effectType == StatusEffectType.Knockback || effectType == StatusEffectType.Launch;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // CC인데 우선순위가 비어 있으면 문서 서열 기본값을 채워 준다.
            if (isCC && ccPriority == 0)
            {
                ccPriority = StatusEffectCcPriority.GetDefault(effectType);
            }

            if (tickInterval <= 0f)
            {
                tickInterval = 1f; // 0 나눗셈 방지
            }

            // 넉백/공중 띄우기는 즉발형 — 지속형 데이터 에셋으로 만들면 경고.
            if (IsInstantPhysical)
            {
                Debug.LogWarning(
                    $"[StatusEffectData] {name}: {effectType}는 즉발형입니다. " +
                    "지속형 컨트롤러가 아닌 Combat.KnockbackInfo 경로로 처리됩니다.", this);
            }
        }
#endif
    }
}
