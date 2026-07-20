// 근거: 보스 시스템.md — 모든 보스는 최소 5개의 행동 패턴을 가진다 / 패턴은 예고 후 발동한다.
// ARCHITECTURE.md §4 — BossPattern은 ScriptableObject로 고정. 그 계약을 지키면서 '실행 로직'을
// 데이터로 갈아끼우기 위해, BossPattern이 이 Behaviour SO를 참조하는 구조(전략 패턴)를 쓴다.
//
// 확장 규칙: 새 보스/새 패턴을 추가할 때 BossController는 절대 수정하지 않는다.
//   ① BossPatternBehaviour를 상속한 SO 클래스를 하나 만들고
//   ② CreateRunner()에서 자기 전용 Runner를 반환하고
//   ③ 애셋을 만들어 BossPattern.behaviour에 꽂는다.
using UnityEngine;

namespace TSWP.Bosses
{
    /// <summary>
    /// 패턴 실행 전략(데이터 절반). 튜닝 수치는 파생 SO가 [SerializeField]로 갖고,
    /// 실행 중 상태는 CreateRunner()가 만든 BossPatternRunner 인스턴스가 갖는다.
    /// </summary>
    public abstract class BossPatternBehaviour : ScriptableObject
    {
        [Header("예고 (모든 패턴 공통 — 예고 없는 패턴은 금지)")]
        [Tooltip("발동 전 예고 시간(초). 난이도·광폭화 속도 배율로 나눠 적용된다.")]
        [SerializeField, Min(0f)] private float telegraphSeconds = 0.6f; // TODO(밸런스): 문서 미정

        [Tooltip("예고 시 재생할 이펙트 id (Art.VfxId). 비우면 재생하지 않는다.")]
        [SerializeField] private string telegraphVfxId;

        public float TelegraphSeconds => telegraphSeconds;
        public string TelegraphVfxId => telegraphVfxId;

        /// <summary>이 패턴 1회 실행분을 담당할 Runner 인스턴스를 만든다.
        /// SO는 여러 보스가 공유하므로 여기서 절대 자기 필드를 변경하지 말 것.</summary>
        public abstract BossPatternRunner CreateRunner();
    }
}
