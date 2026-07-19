// 근거: 팔레트 시스템.md — 직업별 대표 색상: 용사=파랑, 폭탄마=주황, 의사=초록, 방패병=회색,
//       궁수=갈색, 마법사=보라, 건축가/정신병자 포함.
// 직업 enum은 만들지 않는다 (ARCHITECTURE.md §5 — jobId 문자열 주도).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>jobId → 대표 색상 매핑. HUD 직업 아이콘, 파티 패널, 머리 위 표시가 사용한다.</summary>
    [CreateAssetMenu(menuName = "TSWP/Art/Job Colors", fileName = "JobColorConfig")]
    public class JobColorConfig : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("JobDefinition.jobId와 일치해야 한다. 예: warrior, bomber, doctor, shieldbearer, archer, mage, architect, psycho")]
            public string jobId;
            public Color color = Color.white;
        }

        [SerializeField]
        private List<Entry> entries = new List<Entry>
        {
            new Entry { jobId = "warrior" },      // 용사 — 파랑
            new Entry { jobId = "bomber" },       // 폭탄마 — 주황
            new Entry { jobId = "doctor" },       // 의사 — 초록
            new Entry { jobId = "shieldbearer" }, // 방패병 — 회색
            new Entry { jobId = "archer" },       // 궁수 — 갈색
            new Entry { jobId = "mage" },         // 마법사 — 보라
            new Entry { jobId = "architect" },    // 건축가
            new Entry { jobId = "psycho" },       // 정신병자
        };

        [SerializeField]
        [Tooltip("등록되지 않은 jobId에 사용할 색.")]
        private Color fallbackColor = Color.white;

        public Color Get(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) return fallbackColor;

            for (int i = 0; i < entries.Count; i++)
                if (entries[i].jobId == jobId) return entries[i].color;

            return fallbackColor;
        }
    }
}
