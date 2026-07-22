// 근거: UI 시스템.md — 보스전 상단에 기믹 진행 게이지를 표시하고, 화면 덮기 연출(거미줄 등)은 오버레이 계층에 그린다.
// ARCHITECTURE.md §3-5 — UI는 게임 로직을 직접 참조하지 않는다. 그래서 UI(BossHealthBar/BossUIModel)는
//   Bosses 타입을 모르고, 반대 방향(Bosses → UI)으로 값을 밀어 넣는 이 다리를 Bosses에 둔다.
// 이게 없으면 DangerGaugeGimmick/JumpPlatformGimmick/거미줄 패턴이 보내는 값을 아무도 받지 않아
//   기믹이 실제로 돌아도 화면에는 아무것도 보이지 않는다.
using UnityEngine;
using TSWP.UI;

namespace TSWP.Bosses
{
    /// <summary>
    /// BossGaugeChannel(보스 → UI 통지 허브)을 BossUIModel에 연결하는 다리.
    /// 씬 어디에든 하나만 놓으면 되고, UI가 없는 씬에서는 조용히 아무 일도 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossUiBridge : MonoBehaviour
    {
        [Header("화면 오버레이 색")]
        [Tooltip("거미줄 등 시야를 가리는 연출의 색. 보스별로 다르게 하고 싶으면 이 값을 바꾼다.")]
        [SerializeField] private Color webOverlayColor = new Color(0.85f, 0.85f, 0.9f, 1f);

        private BossHealthBar _cachedBar;

        private void OnEnable()
        {
            BossGaugeChannel.GaugeChanged += OnGaugeChanged;
            BossGaugeChannel.ScreenOverlayChanged += OnOverlayChanged;
        }

        private void OnDisable()
        {
            BossGaugeChannel.GaugeChanged -= OnGaugeChanged;
            BossGaugeChannel.ScreenOverlayChanged -= OnOverlayChanged;
        }

        // ── 채널 → 뷰모델 ─────────────────────────────────────────
        private void OnGaugeChanged(string bossId, float value01, bool visible)
        {
            var model = ResolveModel();
            if (model == null) return;

            // BossGaugeChannel이 문서화한 매핑 그대로 (BossGaugeChannel.GaugeChanged → SetGimmickGauge).
            model.SetGimmickGauge(value01, visible);
        }

        private void OnOverlayChanged(string overlayId, float intensity01)
        {
            var model = ResolveModel();
            if (model == null) return;
            model.SetScreenOverlay(intensity01, webOverlayColor);
        }

        /// <summary>
        /// 표시 대상 뷰모델. GameplayHud가 있으면 그 모델을(구독이 이미 걸려 있다),
        /// 없으면 씬의 BossHealthBar가 자체 생성한 모델을 쓴다. 둘 다 없으면 null(연출 생략).
        /// </summary>
        private BossUIModel ResolveModel()
        {
            var hud = GameplayHud.Instance;
            if (hud != null) return hud.Model.Boss;

            // Unity 6: FindObjectOfType는 제거됨 — FindFirstObjectByType 사용. 결과는 캐시한다.
            if (_cachedBar == null)
                _cachedBar = FindAnyObjectByType<BossHealthBar>();

            return _cachedBar != null ? _cachedBar.Model : null;
        }
    }
}
