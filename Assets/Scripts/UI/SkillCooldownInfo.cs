// 근거: UI 시스템.md — 스킬 UI: 스킬 아이콘과 남은 쿨타임 표시, 사용 불가능 시 회색 표시.
// 뷰모델 전용 데이터. 실제 쿨타임 진행은 Jobs.SkillCaster의 Core.CooldownTimer가 소유한다.
using System;
using UnityEngine;

namespace TSWP.UI
{
    /// <summary>스킬 슬롯 1칸의 표시 정보.</summary>
    [Serializable]
    public sealed class SkillCooldownInfo
    {
        /// <summary>Jobs.ActiveSkillDefinition의 skillId 문자열 (직업 enum 금지 원칙과 동일하게 문자열 키).</summary>
        public string SkillId;
        public Sprite Icon;

        /// <summary>남은 쿨타임(초). 0이면 사용 가능.</summary>
        public float RemainingCooldown;

        /// <summary>전체 쿨타임(초) — 원형 게이지 비율 계산용.</summary>
        public float TotalCooldown;

        /// <summary>false면 아이콘을 회색으로 표시한다 (쿨타임 중 / 침묵 상태 등).</summary>
        public bool IsUsable = true;

        /// <summary>게이지 채움 비율 0~1 (1 = 사용 가능).</summary>
        public float FillRatio =>
            TotalCooldown <= 0f ? 1f : Mathf.Clamp01(1f - (RemainingCooldown / TotalCooldown));

        public void SetCooldown(float remaining, float total)
        {
            RemainingCooldown = Mathf.Max(0f, remaining);
            TotalCooldown = Mathf.Max(0f, total);
            IsUsable = RemainingCooldown <= 0f;
        }
    }
}
