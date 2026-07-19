// 근거: 직업 시스템.md — 직업 공통 스키마 (기본 공격 / 액티브 스킬(Q) / 패시브 / 고유 플레이 스타일 / 장점 / 위험 요소).
// 직업 식별은 jobId 문자열 — enum 금지, 데이터 주도 (ARCHITECTURE.md §4·§5).
// 알려진 jobId (팔레트 시스템.md): warrior, bomber, doctor, shieldbearer, archer, mage, architect, psycho
// 성장은 레벨이 아닌 아이템(Core.StatCollection modifier 스택) — 레벨/경험치 필드를 두지 않는다 (스펙 unityNotes ⑥).
using UnityEngine;

namespace TSWP.Jobs
{
    /// <summary>팀 기여 능력 종류 — 각 직업은 최소 1개 보유해야 한다 (문서: 팀 플레이).</summary>
    public enum TeamPlayType
    {
        Heal,               // 회복
        MovementAssist,     // 이동 보조
        EnemyControl,       // 적 제어
        StructurePlacement, // 구조물 설치
        Buff,               // 버프
        Debuff,             // 디버프
    }

    /// <summary>
    /// 직업당 1개 에셋 (데이터 주도 — 직업별 프리팹 분기 대신 단일 플레이어 프리팹 + 데이터 주입).
    /// 검증 규칙(팀 기여 ≥1, 활약 보스 ≥1, 위험 요소 ≥1, 6항목 체크리스트)은 JobAdditionChecklist가 담당.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Jobs/Job Definition", fileName = "Job_")]
    public class JobDefinition : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("직업 식별자 문자열 (예: warrior, bomber, doctor, shieldbearer, archer, mage, architect, psycho). enum 금지 — 데이터 주도.")]
        [SerializeField] private string jobId;
        [Tooltip("직업 이름 (예: 폭탄마, 의사, 용사).")]
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [Tooltip("직업 대표색 폴백 — 실제 표시 색은 Art.JobColorConfig가 jobId 문자열 키로 보관한다 (ARCHITECTURE.md §4).")]
        [SerializeField] private Color jobColor = Color.white;

        [Header("난이도 — 5단계 별점 (★1 쉬움 ~ ★5 어려움)")]
        [Range(1, 5)]
        [SerializeField] private int difficulty = 1;

        [Header("역할군 — 참고용 태그 (역할을 강제하지 않는다 — 게임플레이 로직 의존 금지)")]
        [SerializeField] private JobRole[] roles = System.Array.Empty<JobRole>();

        [Header("구성 요소 — 모든 직업 공통")]
        [SerializeField] private BasicAttackProfile basicAttack = new BasicAttackProfile();
        [SerializeField] private ActiveSkillDefinition activeSkill;
        [SerializeField] private PassiveDefinition passive;

        [Header("플레이 스타일 / 장점 / 위험 요소")]
        [TextArea]
        [SerializeField] private string playStyleDescription;
        [SerializeField] private string[] strengths = System.Array.Empty<string>();
        [Tooltip("최소 1개 — 모든 직업은 명확한 위험 요소를 가진다 (예: 폭탄마 — 아군도 피해를 입을 수 있다).")]
        [SerializeField] private string[] risks = System.Array.Empty<string>();

        [Header("협동 / 보스 상성 / 트롤 — JobAdditionChecklist 검증 대상")]
        [Tooltip("최소 1개 — 팀을 도울 수 있는 능력 (회복/이동 보조/적 제어/구조물 설치/버프/디버프).")]
        [SerializeField] private TeamPlayType[] teamPlayAbilities = System.Array.Empty<TeamPlayType>();
        [Tooltip("최소 1개 — 이 직업이 가장 활약하는 보스 bossId (모든 직업은 최소 한 보스에서 활약한다).")]
        [SerializeField] private string[] favoredBossIds = System.Array.Empty<string>();
        [Tooltip("웃긴 상황을 만드는 트롤 요소 (예: 폭탄으로 팀원을 날림). 고의적 게임 방해 장려 수준 금지.")]
        [SerializeField] private string[] trollElements = System.Array.Empty<string>();
        [Tooltip("아이템 시너지 서술 — 신규 직업 체크리스트 ⑥(아이템과 시너지가 존재한다) 검증용.")]
        [SerializeField] private string[] itemSynergyNotes = System.Array.Empty<string>();

        public string JobId => jobId;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public Color JobColor => jobColor;
        public int Difficulty => difficulty;
        public JobRole[] Roles => roles;
        public BasicAttackProfile BasicAttack => basicAttack;
        public ActiveSkillDefinition ActiveSkill => activeSkill;
        public PassiveDefinition Passive => passive;
        public string PlayStyleDescription => playStyleDescription;
        public string[] Strengths => strengths;
        public string[] Risks => risks;
        public TeamPlayType[] TeamPlayAbilities => teamPlayAbilities;
        public string[] FavoredBossIds => favoredBossIds;
        public string[] TrollElements => trollElements;
        public string[] ItemSynergyNotes => itemSynergyNotes;

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 난이도는 5단계 별점(1~5)만 허용 — Range 어트리뷰트 외에 코드 경로 변경도 방어.
            difficulty = Mathf.Clamp(difficulty, 1, 5);

            // 나머지 데이터 무결성(위험 요소 ≥1, 팀 기여 ≥1, 활약 보스 ≥1, 6항목 체크리스트)은
            // JobAdditionChecklist 메뉴 검증에서 일괄 확인한다 (OnValidate 로그 스팸 방지).
        }
#endif
    }
}
