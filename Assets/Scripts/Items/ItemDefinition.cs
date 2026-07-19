// 근거: 아이템 시트.md — 시트 1장 = SO 에셋 1개. 시트의 전 섹션(기본 정보/장착 정보/능력/능력치 변화/
//       시너지/위험 요소/밸런스/시각 효과/개발 메모)을 필드로 보존한다. / 아이템 시스템.md — 위험 요소 필수.
// 밸런스 노트·개발 메모는 에디터 전용(#if UNITY_EDITOR)으로 빌드에서 제외한다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Items
{
    /// <summary>능력치 변화 1건. Core.StatCollection의 modifier 스택(가산/승산)으로 그대로 변환된다.
    /// 문서 예시가 % 위주(공격력 +50% 등)이므로 기본 모드는 승산, 값은 +50% = 0.5f.</summary>
    [Serializable]
    public class StatModifierEntry
    {
        public StatType stat;
        public StatModifierMode mode = StatModifierMode.Multiplicative;
        public float value;
    }

    /// <summary>아이템 시트 '능력' 섹션. 서술 필드는 기획 기록용이며 실제 로직은 effects의 ItemEffect 모듈이 담당.</summary>
    [Serializable]
    public class ItemAbility
    {
        [TextArea] public string baseEffect;       // 기본 효과
        [TextArea] public string additionalEffect; // 추가 효과
        public string triggerCondition;            // 발동 조건 (서술 — 로직은 ItemEffect)
        public float duration;                     // 지속 시간 (0 = 상시) // TODO(밸런스): 문서 미정
        public float cooldown;                     // 재사용 대기시간 (0 = 없음) // TODO(밸런스): 문서 미정
    }

    /// <summary>아이템 시트 '위험 요소' 섹션 1건. 모든 아이템 필수(1개 이상).
    /// 상시 패널티는 음수 StatModifierEntry, 조건부 패널티는 ItemEffect(음수 효과) — 동일 파이프라인.</summary>
    [Serializable]
    public class ItemRisk
    {
        [TextArea] public string description;                       // 위험 요소 서술
        public List<StatModifierEntry> statPenalties = new();       // 상시 패널티 (예: 최대 체력 -30% = -0.3f)
        public ItemEffect conditionalPenalty;                       // 조건부 패널티 모듈 (예: 치명타 미발생 시 공격력 -10%)
        public float allyDamageModifier;                            // 아군 피해 증가 배율 (예: +50% = 0.5f, 0 = 없음)
    }

    /// <summary>아이템 시트 '시너지' 섹션 (각 3칸). 추천/UI 표시용 데이터 —
    /// 직업 전용 아이템 금지 원칙에 따라 장착 제한 검사에 절대 사용하지 않는다.</summary>
    [Serializable]
    public class ItemSynergy
    {
        public List<string> synergyJobIds = new();         // 잘 어울리는 직업 (jobId 문자열 — 직업 enum 금지)
        public List<string> synergyItemCodes = new();      // 잘 어울리는 아이템 (itemCode)
        public List<string> synergyBossStrategies = new(); // 잘 어울리는 보스 공략 (서술 또는 bossId)
    }

    /// <summary>아이템 시트 '시각 효과' 섹션. 2D URP 프리팹 참조로 관리.</summary>
    [Serializable]
    public class ItemVisualData
    {
        public Sprite appearanceOverride;   // 착용 시 외형 변화 (스프라이트) — TODO: Art.CharacterVisual 연동
        public GameObject equipVisualPrefab; // 착용 부착물 프리팹
        public GameObject particlePrefab;    // 파티클
        public AudioClip sfx;                // 효과음
        [TextArea] public string specialPresentation; // 특수 연출 서술 — TODO: 연출 시스템 연결
    }

#if UNITY_EDITOR
    /// <summary>아이템 시트 '밸런스' 섹션 — 에디터 전용 메타데이터 (빌드 제외).</summary>
    [Serializable]
    public class ItemBalanceNote
    {
        [TextArea] public string earlyGame;        // 초반 평가
        [TextArea] public string midGame;          // 중반 평가
        [TextArea] public string lateGame;         // 후반 평가
        [TextArea] public string recommendedBuild; // 추천 빌드
    }

    /// <summary>아이템 시트 '개발 메모' 섹션 — 에디터 전용 메타데이터 (빌드 제외).</summary>
    [Serializable]
    public class ItemDevMemo
    {
        [TextArea] public string designIntent;    // 기획 의도
        [TextArea] public string testResults;     // 테스트 결과
        [TextArea] public string revisionHistory; // 수정 내역
    }
#endif

    /// <summary>아이템 정의 SO. 아이템 시트 1장 = 에셋 1개.</summary>
    [CreateAssetMenu(menuName = "TSWP/Items/Item Definition", fileName = "Item_")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("기본 정보")]
        public string itemCode;                 // 아이템 코드(ID) — GameEvents 페이로드로 사용
        public string itemName;                 // 아이템 이름
        public ItemRarity rarity = ItemRarity.Common;
        public ItemType itemType = ItemType.Accessory;
        public Sprite icon;
        [TextArea] public string description;   // 아이템 설명
        public string flavorText;               // 한 줄 설명 (Flavor Text)

        [Header("장착 정보")]
        public AcquisitionMethod acquisitionMethods = AcquisitionMethod.None; // 획득 방법 (복수 체크)
        [Min(0)] public int maxPossessCount;    // 최대 소지 가능 (0 = 무제한) // TODO(밸런스): 문서 미정
        public bool allowDuplicateEquip = true; // 중복 장착 여부 — false면 별도 슬롯 대신 기존 인스턴스에 중첩
        public StackingBehavior stackingBehavior = StackingBehavior.EffectStack; // 중복 시 적용 방식 (아이템별 개별 설정)
        [Min(1)] public int maxStacks = 1;      // MaxStackLimited일 때 상한 // TODO(밸런스): 문서 미정
        public bool isCursed;                   // 저주 아이템 — 강력한 효과 + 심각한 패널티, 빌드의 일부 가능

        [Header("능력")]
        public ItemAbility ability = new();
        public List<ItemEffect> effects = new(); // 다형 효과 모듈 — 플레이 스타일 변화 효과(투사체 분열, 점프 횟수 증가 등)

        [Header("능력치 변화 (시트 8축 — 기타는 아래 서술)")]
        public List<StatModifierEntry> statModifiers = new();
        [TextArea] public string otherStatNote; // 시트 '기타' 축 — 수치화 불가 효과 서술

        [Header("위험 요소 (필수 — 모든 아이템은 장점과 위험 요소를 가진다)")]
        public List<ItemRisk> risks = new();

        [Header("시너지 (추천용 — 장착 제한에 사용 금지)")]
        public ItemSynergy synergy = new();

        [Header("시각 효과")]
        public ItemVisualData visual = new();

#if UNITY_EDITOR
        [Header("밸런스 노트 (에디터 전용)")]
        public ItemBalanceNote balance = new();

        [Header("개발 메모 (에디터 전용)")]
        public ItemDevMemo devMemo = new();

        private void OnValidate()
        {
            // 위험 요소는 모든 아이템 필수 (아이템 시트.md '위험 요소 (필수)')
            if (risks == null || risks.Count == 0)
                Debug.LogWarning($"[ItemDefinition] '{name}': 위험 요소는 필수입니다 (1개 이상) — 아이템 시트.md", this);

            if (string.IsNullOrEmpty(itemCode))
                Debug.LogWarning($"[ItemDefinition] '{name}': itemCode가 비어 있습니다.", this);

            if (stackingBehavior == StackingBehavior.MaxStackLimited && maxStacks < 1)
                maxStacks = 1;
        }
#endif
    }
}
