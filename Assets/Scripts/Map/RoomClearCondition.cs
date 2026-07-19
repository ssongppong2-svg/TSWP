// 근거: 방 시스템.md — 전투 방(일반/엘리트/보스)은 적 처치로, 퍼즐/기믹 연습 방은 목표 달성으로 진행.
//   퍼즐 실패는 페널티 없음 — 실패해도 방 출구가 열려야 한다 (소프트락 금지, 스펙 unityNotes ⑧).
using System;
using UnityEngine;

namespace TSWP.Map
{
    /// <summary>방 클리어 조건 2종 + 없음.</summary>
    public enum RoomClearType
    {
        None,              // 조건 없음 — 진입 즉시 클리어 (시작/휴식/상점/이벤트/비밀방)
        KillAllEnemies,    // 전멸형 — 등록된 적 전원 처치 (일반 전투/엘리트/보스)
        ObjectiveComplete, // 목표형 — 지정 목표 달성 통지 (퍼즐/보스 기믹 연습)
    }

    /// <summary>
    /// 방 1개의 클리어 조건 상태. RoomManager가 방 진입 시 구성하고 런타임 카운트를 갱신한다.
    /// SYNC: 호스트 권위, 추후 NGO NetworkVariable — 클리어 판정은 호스트 단일 지점.
    /// </summary>
    [Serializable]
    public sealed class RoomClearCondition
    {
        public RoomClearType clearType = RoomClearType.None;

        [Tooltip("목표형일 때 달성해야 하는 목표 id (퍼즐 id, 기믹 연습 id 등).")]
        public string objectiveId = "";

        /// <summary>전멸형 런타임 카운트 — SpawnManager 등록으로 증가, 사망으로 감소.</summary>
        [NonSerialized] public int RemainingEnemies;

        /// <summary>전멸형에서 스폰 등록이 끝났는지 — 첫 적이 등록 도중 죽어 조기 클리어되는 것 방지.</summary>
        [NonSerialized] public bool SpawnFinished;

        /// <summary>목표형 달성 플래그.</summary>
        [NonSerialized] public bool ObjectiveDone;

        /// <summary>현재 상태 기준 충족 여부.</summary>
        public bool IsSatisfied
        {
            get
            {
                switch (clearType)
                {
                    case RoomClearType.None: return true;
                    case RoomClearType.KillAllEnemies: return SpawnFinished && RemainingEnemies <= 0;
                    case RoomClearType.ObjectiveComplete: return ObjectiveDone;
                    default: return false;
                }
            }
        }
    }
}
