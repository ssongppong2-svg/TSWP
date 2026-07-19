// 근거: 보스 시스템.md — AI: 보스는 플레이어 위치, 체력, 행동, 퍼즐 진행 상황을 고려하여 행동한다.
//       같은 패턴만 반복하지 않는다 → 최근 패턴 이력을 함께 담아 선택기가 반복을 방지한다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Bosses
{
    /// <summary>
    /// 보스 AI 판단 입력값. 매 패턴 선택 직전에 BossController가 갱신한다.
    /// 보스 AI 고려 4요소(플레이어 위치/체력/행동/퍼즐 진행) + 반복 방지용 최근 패턴 이력.
    /// </summary>
    public sealed class BossAIContext
    {
        /// <summary>최근 플레이어 행동 1건 (예: 스킬 사용). GameEvents.SkillUsed 구독으로 수집한다.</summary>
        public struct PlayerActionRecord
        {
            public int PlayerId;
            public string ActionId;  // 스킬 ID 등
            public float Timestamp;  // Time.time 기준
        }

        /// <summary>최근 행동 보관 개수 상한.</summary>
        public const int MaxActionRecords = 8;

        /// <summary>'최근'으로 인정하는 행동의 유효 시간(초). TODO(밸런스): 문서 미정.</summary>
        public const float RecentActionWindowSeconds = 5f;

        // ── ① 플레이어 위치 ──────────────────────────────────────
        public readonly List<Vector2> PlayerPositions = new();
        public Vector2 BossPosition;

        // ── ② 체력 (보스 자신) ───────────────────────────────────
        public float BossHealthRatio = 1f;

        // ── ③ 플레이어 행동 ──────────────────────────────────────
        public readonly List<PlayerActionRecord> RecentPlayerActions = new();

        // ── ④ 퍼즐 진행 상황 ─────────────────────────────────────
        public bool PuzzleActive;
        public float PuzzleProgress; // 0~1

        // ── 반복 방지 — 최근 사용 패턴 이력 (BossPatternSelector가 소유, 여기엔 읽기 전용 공유) ──
        public IReadOnlyList<string> RecentPatternIds = Array.Empty<string>();

        /// <summary>가장 가까운 플레이어까지의 거리. 플레이어가 없으면 float.MaxValue.</summary>
        public float NearestPlayerDistance()
        {
            float best = float.MaxValue;
            for (int i = 0; i < PlayerPositions.Count; i++)
            {
                float d = Vector2.Distance(BossPosition, PlayerPositions[i]);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>플레이어 산개 반경 — 무게중심으로부터 가장 먼 플레이어 거리. 뭉침/산개 판정용.</summary>
        public float PlayerSpreadRadius()
        {
            int count = PlayerPositions.Count;
            if (count == 0) return 0f;

            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < count; i++) centroid += PlayerPositions[i];
            centroid /= count;

            float max = 0f;
            for (int i = 0; i < count; i++)
            {
                float d = Vector2.Distance(centroid, PlayerPositions[i]);
                if (d > max) max = d;
            }
            return max;
        }

        /// <summary>플레이어 행동 기록 (오래된 항목은 상한 초과 시 제거).</summary>
        public void RecordAction(int playerId, string actionId, float timestamp)
        {
            RecentPlayerActions.Add(new PlayerActionRecord { PlayerId = playerId, ActionId = actionId, Timestamp = timestamp });
            while (RecentPlayerActions.Count > MaxActionRecords)
                RecentPlayerActions.RemoveAt(0);
        }

        /// <summary>유효 시간 내에 해당 행동이 있었는지 판정 (PatternCondition.RecentPlayerActionMatches).</summary>
        public bool HasRecentAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            float now = Time.time;
            for (int i = RecentPlayerActions.Count - 1; i >= 0; i--)
            {
                var rec = RecentPlayerActions[i];
                if (now - rec.Timestamp > RecentActionWindowSeconds) break; // 시간순 저장 — 이후는 전부 만료
                if (rec.ActionId == actionId) return true;
            }
            return false;
        }
    }
}
