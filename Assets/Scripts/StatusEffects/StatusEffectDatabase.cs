// 근거: 상태이상 시스템.md — 상태이상 16종 정의 + 시너지 규칙("추가 예정" → 데이터 주도 목록).
// 전체 효과/시너지 에셋을 한곳에 모아 조회하는 카탈로그. 컨트롤러·발생원(직업/아이템/보스/환경)이 참조한다.
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.StatusEffects
{
    /// <summary>
    /// 상태이상 데이터베이스. 프로젝트에 에셋 1개를 두고
    /// StatusEffectController(시너지 조회)와 각 발생원 시스템(타입→데이터 조회)이 공유한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/StatusEffects/Status Effect Database", fileName = "StatusEffectDatabase")]
    public class StatusEffectDatabase : ScriptableObject
    {
        [Tooltip("전체 상태이상 정의 목록 (지속형 14종 권장 — 넉백/공중 띄우기는 즉발형이라 Combat.KnockbackInfo가 처리).")]
        [SerializeField] private List<StatusEffectData> effects = new List<StatusEffectData>();

        [Tooltip("시너지 규칙 목록 — 새 시너지는 규칙 에셋을 만들어 여기 등록만 하면 된다 (하드코딩 금지).")]
        [SerializeField] private List<StatusSynergyRule> synergyRules = new List<StatusSynergyRule>();

        public IReadOnlyList<StatusEffectData> Effects => effects;
        public IReadOnlyList<StatusSynergyRule> SynergyRules => synergyRules;

        /// <summary>타입으로 정의 데이터 조회. 없으면 null. (16종 소규모라 선형 탐색로 충분)</summary>
        public StatusEffectData GetData(StatusEffectType type)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] != null && effects[i].EffectType == type)
                {
                    return effects[i];
                }
            }
            return null;
        }

        /// <summary>유발 상태이상 + 촉매 조합과 일치하는 첫 규칙 조회. 없으면 null.</summary>
        public StatusSynergyRule FindSynergy(StatusEffectType trigger, SynergyCatalyst catalyst)
        {
            for (int i = 0; i < synergyRules.Count; i++)
            {
                if (synergyRules[i] != null && synergyRules[i].Matches(trigger, catalyst))
                {
                    return synergyRules[i];
                }
            }
            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 같은 타입 중복 등록 경고 (동종 판정은 타입 기준이므로 데이터가 갈라지면 안 됨).
            var seen = new HashSet<StatusEffectType>();
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i] == null)
                {
                    continue;
                }
                if (!seen.Add(effects[i].EffectType))
                {
                    Debug.LogWarning($"[StatusEffectDatabase] {name}: {effects[i].EffectType} 타입이 중복 등록되었습니다.", this);
                }
            }
        }
#endif
    }
}
