// 근거: 업적 시스템.md — 카운트형 업적(예: 대쉬 N회)은 counterKey 집계로 판정한다.
// PlayerController는 대쉬 시 이벤트를 발행하지 않고 IsDashing 프로퍼티만 노출한다(조작과 시스템.md).
// Player 폴더를 수정하지 않고 업적을 붙이기 위해, 상승 엣지를 감시해 counterKey를 발행하는 얇은 관찰자를 둔다.
// TODO: PlayerController에 대쉬 이벤트가 생기면 이 컴포넌트는 삭제하고 이벤트 구독으로 교체한다.
using UnityEngine;
using TSWP.Core;
using TSWP.Player;

namespace TSWP.Meta
{
    /// <summary>
    /// 플레이어의 대쉬 시작을 감지해 GameEvents.RaiseStatCounter("dash.count", 1)을 발행한다.
    /// 플레이어 오브젝트에 붙인다. PlayerController가 없으면 조용히 아무 일도 하지 않는다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class DashStatReporter : MonoBehaviour
    {
        /// <summary>발행하는 카운터 키 — AchievementData.counterKey에 이 값을 그대로 넣는다.</summary>
        public const string DashCounterKey = "dash.count";

        [Tooltip("착지/점프 등 다른 지표도 필요해지면 이 컴포넌트를 확장한다.")]
        [SerializeField] private bool reportDash = true;

        private PlayerController _controller;
        private bool _wasDashing;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (!reportDash || _controller == null) return;

            bool dashing = _controller.IsDashing;

            // 상승 엣지에서만 1회 — 대쉬가 여러 프레임 지속되므로 매 프레임 세면 안 된다.
            if (dashing && !_wasDashing)
                GameEvents.RaiseStatCounter(DashCounterKey, 1);

            _wasDashing = dashing;
        }
    }
}
