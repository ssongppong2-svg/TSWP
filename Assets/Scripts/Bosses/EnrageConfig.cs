// 근거: 보스 시스템.md — 광폭화: 공격 속도 증가 / 이동 속도 증가 / 신규 패턴 추가 / 기존 패턴 강화.
// "체력만 증가시키지 않는다" — 체력 배율 필드는 이 클래스에 절대 두지 않는다 (문서 명시, 스펙 notes).
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Bosses
{
    /// <summary>
    /// 광폭화 설정. 일부 보스만 보유한다 (BossData.hasEnrage로 사용 여부 결정).
    /// 광폭화가 없는 보스는 '패턴 변화'가 그 역할을 대신한다 (제작 체크리스트: 광폭화 또는 패턴 변화).
    /// </summary>
    [System.Serializable]
    public sealed class EnrageConfig
    {
        [Header("진입 조건")]
        [Tooltip("보스 체력 비율이 이 값 이하로 내려가면 광폭화 진입.")]
        [SerializeField] private float triggerHealthRatio = 0.3f; // TODO(밸런스): 문서 미정 — 진입 조건 수치 미명시

        [Header("속도 배율 (체력 배율 금지)")]
        [SerializeField] private float attackSpeedMultiplier = 1.25f; // TODO(밸런스): 문서 미정
        [SerializeField] private float moveSpeedMultiplier = 1.25f;   // TODO(밸런스): 문서 미정

        [Header("패턴")]
        [Tooltip("광폭화 시 추가되는 신규 패턴 (isEnrageOnly=true인 패턴만 등록).")]
        [SerializeField] private List<BossPattern> newPatterns = new();

        [Tooltip("enhancedInEnrage=true인 기존 패턴에 곱해지는 피해 배율.")]
        [SerializeField] private float enhancedPatternDamageMultiplier = 1.2f; // TODO(밸런스): 문서 미정

        [Tooltip("enhancedInEnrage=true인 기존 패턴에 곱해지는 속도 배율.")]
        [SerializeField] private float enhancedPatternSpeedMultiplier = 1.2f;  // TODO(밸런스): 문서 미정

        public float TriggerHealthRatio => triggerHealthRatio;
        public float AttackSpeedMultiplier => attackSpeedMultiplier;
        public float MoveSpeedMultiplier => moveSpeedMultiplier;
        public IReadOnlyList<BossPattern> NewPatterns => newPatterns;
        public float EnhancedPatternDamageMultiplier => enhancedPatternDamageMultiplier;
        public float EnhancedPatternSpeedMultiplier => enhancedPatternSpeedMultiplier;

        // 주의: hpMultiplier 등 체력 관련 필드를 추가하지 말 것.
        // 보스 난이도는 체력이 아니라 패턴과 기믹으로 결정한다 (보스 시스템.md '체력 설계'/'광폭화').
    }
}
