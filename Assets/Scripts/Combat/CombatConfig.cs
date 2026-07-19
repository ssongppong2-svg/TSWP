// 근거: 전투 시스템.md — 문서에 미기재된 전투 튜닝 수치(치명타 배율·넉백 세기 등)를 SO로 노출한다.
// 문서에 명시된 수치(아군 피해 50%, 기본 치명타 0% 등)는 Core.GameRules 상수를 그대로 쓴다 — 여기 중복 금지.
// 스트리머 테스트 중 튜닝 용이성을 위해 하드코딩 대신 SO 필드로 관리 (스펙 unityNotes ③).
using UnityEngine;

namespace TSWP.Combat
{
    /// <summary>
    /// 전투 튜닝 설정. 부트스트랩에서 DamageSystem.Config에 주입해 사용한다.
    /// 미주입 시 각 사용 지점의 폴백 기본값으로 동작 (뼈대 단계 안전장치).
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Combat/CombatConfig", fileName = "CombatConfig")]
    public class CombatConfig : ScriptableObject
    {
        [Header("치명타")]
        [Tooltip("치명타 피해 배율. 기본 치명타 확률은 GameRules.BaseCritChance(0%) — 아이템·버프로만 획득.")]
        public float critDamageMultiplier = 2f; // TODO(밸런스): 문서 미정 — 치명타 배율 미기재

        [Header("넉백")]
        [Tooltip("공격에 넉백 세기가 지정되지 않았을 때 쓰는 기본 세기 (Rigidbody2D 임펄스).")]
        public float defaultKnockbackForce = 5f; // TODO(밸런스): 문서 미정

        [Tooltip("넉백 시 기본 경직 시간(초).")]
        public float defaultKnockbackStunDuration = 0.2f; // TODO(밸런스): 문서 미정

        [Header("부활")]
        [Tooltip("즉시 부활 시 회복되는 최대 체력 비율.")]
        [Range(0f, 1f)]
        public float reviveHpRatio = 1f; // TODO(밸런스): 문서 미정

        [Tooltip("부활 직후 짧은 무적 시간(초). 유한 타이머 — 상시 무적 아님 (전투 시스템.md '무적').")]
        public float reviveInvincibleDuration = 1.5f; // TODO(밸런스): 문서 미정

        [Header("환경 피해")]
        [Tooltip("해저드 개별 지정이 없을 때의 기본 틱 간격(초).")]
        public float defaultHazardTickInterval = 1f; // TODO(밸런스): 문서 미정
    }
}
