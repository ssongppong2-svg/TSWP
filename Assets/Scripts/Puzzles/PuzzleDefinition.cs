// 근거: 퍼즐 시스템.md — 협동 규칙(최소 2명) / 직업 활용 + 대체 해법 필수 / 시간 제한 / 보상 / 실패 불이익 / 리커버리 / 실패 연출 / 난이도 3단계.
// 근거: 게임 시작과 선택, 직업, 플레이.md — 난이도 '슈퍼 겁쟁이'는 퍼즐 제한시간이 증가한다 (Core.Difficulty 참조).
// 퍼즐 하나의 정적 데이터. 코드 수정 없이 퍼즐을 추가할 수 있도록 유형·인원·시간·보상·불이익·리커버리를 전부 데이터로 둔다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;
using TSWP.Enemies;
using TSWP.Items;

namespace TSWP.Puzzles
{
    /// <summary>퍼즐 보상 1건. 타입에 따라 사용하는 필드가 다르다.</summary>
    [Serializable]
    public class PuzzleRewardEntry
    {
        [Tooltip("보상 종류 (아이템/골드/비밀방/이벤트/지름길/숨겨진 상자).")]
        public PuzzleRewardType type = PuzzleRewardType.Item;

        [Tooltip("Item/HiddenChest 보상에서 지급할 아이템 (Items.ItemDefinition).")]
        public ItemDefinition item;

        [Tooltip("Gold 보상 수량, 또는 아이템 개수. TODO(밸런스): 문서 미정.")]
        public int amount = 1;

        [Tooltip("SecretRoom/Shortcut 보상이 여는 대상 방 ID (Map.RoomNode). -1이면 런타임에서 지정.")]
        public int targetRoomId = -1;

        [Tooltip("Event 보상 식별자 (이벤트 시스템 키).")]
        public string eventId;
    }

    /// <summary>
    /// 퍼즐 정적 데이터.
    /// 제작 체크리스트(협동 요소 / 직업 활약 / 실패해도 진행 가능 / 직관성)는 OnValidate로 최대한 자동 검증한다.
    /// </summary>
    [CreateAssetMenu(fileName = "PuzzleDefinition", menuName = "TSWP/Puzzles/Puzzle Definition")]
    public class PuzzleDefinition : ScriptableObject
    {
        // ── 식별 ──────────────────────────────────────────────────
        [Header("식별")]
        [SerializeField] private string puzzleId = "puzzle.new";
        [SerializeField] private string displayName = "이름 없는 퍼즐";
        [SerializeField] private PuzzleType puzzleType = PuzzleType.Button;

        [Tooltip("등장 구간 — 초반(기본 조작 학습)/중반(협동·역할 분담)/후반(보스 기믹+환경+시간 제한).")]
        [SerializeField] private DifficultyPhase difficultyPhase = DifficultyPhase.Early;

        [TextArea(2, 4)]
        [Tooltip("설명 없이도 이해 가능해야 한다(설계 철학 ②) — 이 문구는 기획/디버그용 메모다.")]
        [SerializeField] private string designerNote;

        // ── 협동 규칙 ─────────────────────────────────────────────
        [Header("협동 규칙")]
        [Tooltip("최소 협동 인원. 문서: 모든 퍼즐은 최소 2명 이상의 협동을 기본으로 설계한다.")]
        [SerializeField] private int minPlayers = 2;

        [Tooltip("혼자서도 시간이 오래 걸릴 뿐 해결 가능한 퍼즐인지 (일부만 true). true여도 minPlayers는 설계 기준값으로 유지한다.")]
        [SerializeField] private bool soloSolvable;

        [Tooltip("인원이 많을수록 쉬워지는 정도 — 참가 인원당 제한시간 가산 비율. TODO(밸런스): 문서 미정(정성 서술만).")]
        [SerializeField] private float timeBonusPerExtraPlayer = 0.05f;

        // ── 시간 제한 ─────────────────────────────────────────────
        [Header("시간 제한")]
        [SerializeField] private bool hasTimeLimit;

        [Tooltip("기본 제한시간(초). TODO(밸런스): 문서 미정 — 퍼즐별로 설정.")]
        [SerializeField] private float timeLimitSeconds = 60f;

        [Header("난이도별 제한시간 배율 (Core.Difficulty 순서: SuperCoward/Human/God/Meme)")]
        [Tooltip("슈퍼 겁쟁이 — 문서: 퍼즐 제한시간 '증가' 이므로 1보다 커야 한다. TODO(밸런스): 구체 배율 문서 미정.")]
        [SerializeField] private float superCowardTimeMultiplier = 1.5f;
        [Tooltip("인간 — 기준 난이도(1.0 고정 권장).")]
        [SerializeField] private float humanTimeMultiplier = 1f;
        [Tooltip("신 — 문서에 퍼즐 시간 감소 명시 없음. TODO(밸런스): 문서 미정 — 기본은 기준값 유지.")]
        [SerializeField] private float godTimeMultiplier = 1f;
        [Tooltip("밈 — 무작위 특수 규칙. TODO(밸런스): 문서 미정.")]
        [SerializeField] private float memeTimeMultiplier = 1f;

        // ── 직업/능력 활용 ────────────────────────────────────────
        [Header("직업 활용 (능력 인터페이스 기준 — 직업 enum 금지)")]
        [Tooltip("이 퍼즐에서 활약하는 능력. Player의 IPlatformBuilder/IWallBreaker/ITrapBlocker/IRangedActivator/IPoisonSupport에 대응. 최소 1개 권장(제작 체크리스트).")]
        [SerializeField] private PuzzleAbility featuredAbilities = PuzzleAbility.None;

        [Tooltip("활약 직업 jobId 문자열 (예: architect, bomber, shieldbearer, archer, doctor). 표시/튜토리얼 힌트용 — 판정에는 쓰지 않는다.")]
        [SerializeField] private List<string> featuredJobIds = new List<string>();

        [Tooltip("특정 직업이 없어도 해결 가능한 대체 해법이 존재하는가. 문서상 항상 true여야 한다 (OnValidate가 강제).")]
        [SerializeField] private bool alternativeSolutionRequired = true;

        [TextArea(2, 4)]
        [Tooltip("대체 해법 서술 (예: 건축가 없으면 상자를 밀어 발판 대용). 비어 있으면 설계 미완으로 경고.")]
        [SerializeField] private string alternativeSolutionNote;

        // ── 환경 ──────────────────────────────────────────────────
        [Header("환경 활용")]
        [SerializeField] private List<EnvironmentElementType> environmentElements = new List<EnvironmentElementType>();

        // ── 보상 ──────────────────────────────────────────────────
        [Header("보상")]
        [SerializeField] private List<PuzzleRewardEntry> rewards = new List<PuzzleRewardEntry>();

        // ── 실패 ──────────────────────────────────────────────────
        [Header("실패 불이익 (진행 차단 금지 — 즉시 게임 오버 값 자체가 없다)")]
        [SerializeField] private List<PuzzleFailurePenalty> failurePenalties = new List<PuzzleFailurePenalty>();

        [Tooltip("SpawnEnemies/RevealHiddenEnemy 불이익에 사용할 적 조합 (Enemies.EncounterComposition).")]
        [SerializeField] private EncounterComposition penaltyEncounter;

        [Tooltip("DamageHealth 불이익 피해량. TODO(밸런스): 문서 미정 — '실패가 너무 가혹하지 않은가' 체크리스트 대상.")]
        [SerializeField] private float penaltyDamage = 10f;

        [Tooltip("ReduceReward 불이익의 보상 감소 비율(0~1). TODO(밸런스): 문서 미정.")]
        [Range(0f, 1f)]
        [SerializeField] private float rewardReductionRatio = 0.5f;

        [Header("실패 연출 (실패 원인 즉시 이해 — 트롤 원칙 ④)")]
        [SerializeField] private List<FailureFeedbackType> failureFeedbacks = new List<FailureFeedbackType>
        {
            FailureFeedbackType.ScreenShake,
            FailureFeedbackType.SoundEffect,
            FailureFeedbackType.WarningIcon,
        };

        // ── 리커버리 ──────────────────────────────────────────────
        [Header("리커버리 (소프트락 금지 — 반드시 재도전 가능)")]
        [SerializeField] private RecoveryMethod recoveryMethod = RecoveryMethod.ResetButtons;

        [Tooltip("WaitTimer 방식의 대기 시간(초). 다른 방식에서도 연출 여유 시간으로 쓰인다. TODO(밸런스): 문서 미정.")]
        [SerializeField] private float recoveryWaitSeconds = 5f;

        [Tooltip("재도전 허용 횟수. 0 이하 = 무제한. 문서: '실패한 퍼즐은 다시 도전할 수 있어야 한다' → 무제한이 기본.")]
        [SerializeField] private int maxRetryCount = 0;

        // ── 조회 프로퍼티 ─────────────────────────────────────────
        public string PuzzleId => puzzleId;
        public string DisplayName => displayName;
        public PuzzleType Type => puzzleType;
        public DifficultyPhase Phase => difficultyPhase;

        public int MinPlayers => minPlayers;
        public bool SoloSolvable => soloSolvable;
        public float TimeBonusPerExtraPlayer => timeBonusPerExtraPlayer;

        public bool HasTimeLimit => hasTimeLimit;
        public float TimeLimitSeconds => timeLimitSeconds;

        public PuzzleAbility FeaturedAbilities => featuredAbilities;
        public IReadOnlyList<string> FeaturedJobIds => featuredJobIds;
        public bool AlternativeSolutionRequired => alternativeSolutionRequired;
        public string AlternativeSolutionNote => alternativeSolutionNote;

        public IReadOnlyList<EnvironmentElementType> EnvironmentElements => environmentElements;
        public IReadOnlyList<PuzzleRewardEntry> Rewards => rewards;
        public IReadOnlyList<PuzzleFailurePenalty> FailurePenalties => failurePenalties;
        public EncounterComposition PenaltyEncounter => penaltyEncounter;
        public float PenaltyDamage => penaltyDamage;
        public float RewardReductionRatio => rewardReductionRatio;
        public IReadOnlyList<FailureFeedbackType> FailureFeedbacks => failureFeedbacks;

        public RecoveryMethod Recovery => recoveryMethod;
        public float RecoveryWaitSeconds => recoveryWaitSeconds;
        public int MaxRetryCount => maxRetryCount;

        /// <summary>
        /// 난이도·참가 인원을 반영한 실제 제한시간(초).
        /// 슈퍼 겁쟁이는 제한시간이 늘어나고, 인원이 많을수록 조금 더 여유를 준다("인원이 많을수록 더 쉽게 해결").
        /// hasTimeLimit이 false면 0을 반환한다 (호출측은 0 이하를 '무제한'으로 취급).
        /// </summary>
        public float GetEffectiveTimeLimit(Difficulty difficulty, int participantCount)
        {
            if (!hasTimeLimit) return 0f;

            float multiplier;
            switch (difficulty)
            {
                case Difficulty.SuperCoward: multiplier = superCowardTimeMultiplier; break;
                case Difficulty.God: multiplier = godTimeMultiplier; break;
                case Difficulty.Meme: multiplier = memeTimeMultiplier; break;
                default: multiplier = humanTimeMultiplier; break;
            }

            int extra = Mathf.Max(0, participantCount - minPlayers);
            multiplier += extra * timeBonusPerExtraPlayer;

            return Mathf.Max(1f, timeLimitSeconds * multiplier);
        }

        /// <summary>이 퍼즐이 해당 능력을 활용하도록 설계되었는가 (힌트/튜토리얼 표시용).</summary>
        public bool UsesAbility(PuzzleAbility ability) => (featuredAbilities & ability) != 0;

#if UNITY_EDITOR
        // 제작 체크리스트 자동 검증 — 설계 원칙 위반을 빌드 전에 잡는다 (스펙 unityNotes ⑩).
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(puzzleId))
                Debug.LogWarning($"[PuzzleDefinition] {name}: puzzleId가 비어 있다 (GameEvents 집계 키로 사용).", this);

            // 협동 요소 — 최소 2명 설계 기준
            minPlayers = Mathf.Max(1, minPlayers);
            if (minPlayers < 2)
                Debug.LogWarning($"[PuzzleDefinition] {name}: 모든 퍼즐은 최소 2명 협동이 기본이다 (minPlayers < 2). soloSolvable은 '혼자도 가능'이지 설계 기준 완화가 아니다.", this);

            // 대체 해법 필수 — 문서상 항상 true
            if (!alternativeSolutionRequired)
            {
                alternativeSolutionRequired = true;
                Debug.LogWarning($"[PuzzleDefinition] {name}: 특정 직업이 없어도 해결 가능해야 한다 — alternativeSolutionRequired를 true로 강제했다.", this);
            }
            if (featuredAbilities != PuzzleAbility.None && string.IsNullOrWhiteSpace(alternativeSolutionNote))
                Debug.LogWarning($"[PuzzleDefinition] {name}: 활약 능력이 지정됐는데 대체 해법 서술이 비어 있다.", this);

            // 직업 최소 1개 활약
            if (featuredAbilities == PuzzleAbility.None && featuredJobIds.Count == 0)
                Debug.LogWarning($"[PuzzleDefinition] {name}: 최소 하나 이상의 직업이 활약해야 한다 (제작 체크리스트).", this);

            // 실패해도 게임이 계속 진행되는가 — 리커버리 대기 시간이 음수면 재도전 불가로 읽힐 수 있다
            recoveryWaitSeconds = Mathf.Max(0f, recoveryWaitSeconds);
            if (maxRetryCount < 0) maxRetryCount = 0;

            // 시간 제한 검증
            timeLimitSeconds = Mathf.Max(1f, timeLimitSeconds);
            superCowardTimeMultiplier = Mathf.Max(0.1f, superCowardTimeMultiplier);
            if (superCowardTimeMultiplier < 1f)
                Debug.LogWarning($"[PuzzleDefinition] {name}: 슈퍼 겁쟁이는 퍼즐 제한시간이 '증가'해야 한다 (배율 < 1).", this);

            // 불이익 데이터 정합성
            bool needsEncounter = failurePenalties.Contains(PuzzleFailurePenalty.SpawnEnemies)
                                  || failurePenalties.Contains(PuzzleFailurePenalty.RevealHiddenEnemy);
            if (needsEncounter && penaltyEncounter == null)
                Debug.LogWarning($"[PuzzleDefinition] {name}: 적 소환 불이익이 있는데 penaltyEncounter가 비어 있다.", this);

            if (failureFeedbacks.Count == 0)
                Debug.LogWarning($"[PuzzleDefinition] {name}: 실패 연출이 하나도 없다 — 같은 실수를 반복하지 않도록 피드백을 제공해야 한다(트롤 원칙 ④).", this);
        }
#endif
    }
}
