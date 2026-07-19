// 근거: 적 시스템.md — 적 조합: 같은 적만 계속 등장하지 않는다. 다양한 역할군을 조합하여 새로운 전투 경험을
//       만든다 (예: 방패병+저격수, 힐러+탱커, 자폭병+돌격병). 난이도 증가: 게임이 진행될수록
//       적의 종류 증가/조합 다양화/패턴 강화 — 단순 체력 증가만으로 난이도를 올리지 않는다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Enemies
{
    /// <summary>
    /// 조우(전투) 1건의 적 구성 SO. 방(Room) 전투 배치의 데이터 단위 —
    /// SpawnManager.SpawnEncounter가 이 구성을 받아 스폰 규칙에 따라 생성한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Enemies/Encounter Composition", fileName = "Encounter_")]
    public class EncounterComposition : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public EnemyData enemy;
            [Min(1)] public int count = 1;
        }

        [Header("등장 적 구성")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        [Header("역할군 조합 메타데이터 (설계 의도)")]
        [Tooltip("이 조우가 노리는 역할 조합 (예: Tank | Ranged = 방패병+저격수). 실제 구성과 대조 검증된다.")]
        [SerializeField] private EnemyRole intendedRoleMix = EnemyRole.None;

        [Tooltip("조합 설계 메모 (예: '방패병이 전면을 막는 동안 저격수가 후방 견제').")]
        [SerializeField, TextArea] private string designNote;

        [Header("진행도")]
        [Tooltip("게임 진행도 단계 — 높을수록 종류 증가/조합 다양화/패턴 강화 구간에서 선택된다.")]
        [SerializeField, Min(0)] private int progressionTier;

        public IReadOnlyList<Entry> Entries => entries;
        public EnemyRole IntendedRoleMix => intendedRoleMix;
        public int ProgressionTier => progressionTier;

        /// <summary>구성에 실제 포함된 역할군 합집합 — 조합 다양성 검증/조우 선택 필터에 사용.</summary>
        public EnemyRole GetActualRoleMix()
        {
            EnemyRole mix = EnemyRole.None;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].enemy != null)
                    mix |= entries[i].enemy.roles;
            }
            return mix;
        }

        /// <summary>총 스폰 수.</summary>
        public int GetTotalCount()
        {
            int total = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].enemy != null)
                    total += entries[i].count;
            }
            return total;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entries == null || entries.Count == 0)
            {
                Debug.LogWarning($"[EncounterComposition] '{name}': 등장 적 구성이 비어 있습니다.", this);
                return;
            }

            // "같은 적만 계속 등장하지 않는다" — 단일 종 구성 경고 (역할군 조합 권장)
            EnemyData first = null;
            bool allSame = true;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null || entries[i].enemy == null)
                {
                    Debug.LogWarning($"[EncounterComposition] '{name}': entries[{i}]의 적이 비어 있습니다.", this);
                    continue;
                }
                if (first == null) first = entries[i].enemy;
                else if (entries[i].enemy != first) allSame = false;
            }
            if (first != null && allSame && GetTotalCount() > 1)
                Debug.LogWarning($"[EncounterComposition] '{name}': 단일 종 구성 — 다양한 역할군 조합을 권장합니다 (적 시스템.md 적 조합).", this);

            // 설계 의도 역할이 실제 구성에 빠져 있으면 경고
            if (intendedRoleMix != EnemyRole.None)
            {
                EnemyRole actual = GetActualRoleMix();
                if ((actual & intendedRoleMix) != intendedRoleMix)
                    Debug.LogWarning($"[EncounterComposition] '{name}': 의도한 역할 조합({intendedRoleMix}) 일부가 실제 구성({actual})에 없습니다.", this);
            }
        }
#endif
    }
}
