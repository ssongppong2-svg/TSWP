// 근거: 방 시스템.md — 보스 총 15종, 각 플레이에서 순서대로 공략(스테이지 15개 진행), 맵당 생물 군계 1개.
// 스테이지 1~15 ↔ 생물 군계/보스 매핑을 데이터(SO)로 분리 — 기획이 에셋만 편집해 순서를 조정한다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Map
{
    /// <summary>
    /// 스테이지 1~GameRules.TotalBossCount(15) 진행 계획.
    /// RunManager.CurrentStage → 이 SO 조회 → MapGenerator.Generate(biome) 흐름.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Map/Stage Plan", fileName = "StagePlan")]
    public class StagePlan : ScriptableObject
    {
        /// <summary>스테이지 1개의 계획 항목.</summary>
        [Serializable]
        public class StageEntry
        {
            [Range(1, GameRules.TotalBossCount)]
            public int stageIndex = 1;
            public BiomeType biome;
            [Tooltip("이 스테이지의 군계 콘텐츠 풀. 비어 있으면 biome enum만 사용.")]
            public BiomeDefinition biomeDefinition;
            [Tooltip("이 스테이지 보스 id (느슨한 참조 — Bosses 시스템의 BossData id).")]
            public string bossId = "";
        }

        [Tooltip("스테이지 순서대로 15개 — 15번째가 최종 보스 스테이지.")]
        public List<StageEntry> stages = new List<StageEntry>();

        [Header("맵 생성 튜닝 (전 스테이지 공통 기본값)")]
        public MapGenerationConfig generationConfig = new MapGenerationConfig();

        /// <summary>스테이지 항목 조회 (1~15). 없으면 null.</summary>
        public StageEntry GetEntry(int stageIndex)
        {
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] != null && stages[i].stageIndex == stageIndex)
                    return stages[i];
            }
            return null;
        }

        /// <summary>스테이지의 생물 군계. 항목이 없으면 Forest 폴백 + 경고 없이 진행(뼈대 단계).</summary>
        public BiomeType GetBiome(int stageIndex)
        {
            var entry = GetEntry(stageIndex);
            return entry != null ? entry.biome : BiomeType.Forest;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 보스 15종 = 스테이지 15개 (GameRules.TotalBossCount) — 에셋 구성 검증.
            if (stages.Count != 0 && stages.Count != GameRules.TotalBossCount)
                Debug.LogWarning($"[StagePlan] 스테이지 수 {stages.Count} ≠ {GameRules.TotalBossCount} — 방 시스템.md: 보스 15종 순서대로 공략", this);
        }
#endif
    }
}
