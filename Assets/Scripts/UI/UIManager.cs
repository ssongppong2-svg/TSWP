// 근거: UI 시스템.md — UI 5원칙: 필요한 정보만 / 전투 방해 없이 / 2초 안에 이해 / 스트리머 친화 / 크기·투명도 조절 가능.
// 캔버스 3계층 구조:
//   ① Screen Space HUD  — 체력/스킬/아이템/부활 횟수/미니맵/파티/보스/알림
//   ② World Space       — 머리 위 닉네임·체력바·이모트·마이크 아이콘, 상호작용 프롬프트, 월드 핑 마커
//   ③ Overlay           — 결과 화면, 설정, 이모트 휠 등 전체 화면 패널
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.UI
{
    /// <summary>UI 패널 식별자.</summary>
    public enum UIPanelId
    {
        Hud,
        Minimap,
        PartyPanel,
        BossBar,
        EmoteWheel,
        Results,
        Settings,
        Lobby,
    }

    /// <summary>
    /// 패널 등록/토글과 게임 흐름에 따른 표시 전환을 담당한다.
    /// 뷰 구현은 각 패널 컴포넌트가 담당하고, 여기서는 표시 여부만 관리한다.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("캔버스 3계층")]
        [SerializeField] private Canvas hudCanvas;      // ① Screen Space
        [SerializeField] private Canvas worldCanvas;    // ② World Space
        [SerializeField] private Canvas overlayCanvas;  // ③ Overlay

        private readonly Dictionary<UIPanelId, GameObject> _panels = new();

        public Canvas HudCanvas => hudCanvas;
        public Canvas WorldCanvas => worldCanvas;
        public Canvas OverlayCanvas => overlayCanvas;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            GameEvents.FlowStateChanged += OnFlowStateChanged;

            if (SettingsManager.Instance != null)
                SettingsManager.Instance.SettingsChanged += ApplySettings;
        }

        private void OnDisable()
        {
            GameEvents.FlowStateChanged -= OnFlowStateChanged;

            if (SettingsManager.Instance != null)
                SettingsManager.Instance.SettingsChanged -= ApplySettings;
        }

        // ── 패널 관리 ─────────────────────────────────────────────

        public void RegisterPanel(UIPanelId id, GameObject panel)
        {
            if (panel == null) return;
            _panels[id] = panel;
        }

        public void UnregisterPanel(UIPanelId id) => _panels.Remove(id);

        public void SetPanelVisible(UIPanelId id, bool visible)
        {
            if (_panels.TryGetValue(id, out var panel) && panel != null)
                panel.SetActive(visible);
        }

        public bool IsPanelVisible(UIPanelId id)
            => _panels.TryGetValue(id, out var panel) && panel != null && panel.activeSelf;

        public void TogglePanel(UIPanelId id) => SetPanelVisible(id, !IsPanelVisible(id));

        // ── 흐름 연동 ─────────────────────────────────────────────

        private void OnFlowStateChanged(GameFlowState state)
        {
            bool inGame = state == GameFlowState.Exploration
                          || state == GameFlowState.BossFight
                          || state == GameFlowState.Tutorial
                          || state == GameFlowState.StartItemDrop;

            SetPanelVisible(UIPanelId.Hud, inGame);
            SetPanelVisible(UIPanelId.Minimap, inGame);
            SetPanelVisible(UIPanelId.PartyPanel, inGame);
            SetPanelVisible(UIPanelId.BossBar, state == GameFlowState.BossFight);
            SetPanelVisible(UIPanelId.Results, state == GameFlowState.Results);
            SetPanelVisible(UIPanelId.Lobby, state == GameFlowState.Lobby);

            // 뒷풀이에서는 이동·이모트·음성이 가능하므로 HUD 일부를 남긴다 (결과 확인/스크린샷 촬영).
            if (state == GameFlowState.AfterParty)
                SetPanelVisible(UIPanelId.Hud, true);
        }

        // ── 설정 반영 ─────────────────────────────────────────────

        /// <summary>UI 크기/투명도/HUD 표시 여부를 캔버스에 반영한다.</summary>
        private void ApplySettings()
        {
            var settings = SettingsManager.Instance?.Ui;
            if (settings == null) return;

            if (hudCanvas != null)
            {
                hudCanvas.gameObject.SetActive(settings.hudEnabled);

                // TODO(UI): CanvasScaler.scaleFactor = uiScale, CanvasGroup.alpha = uiOpacity 반영.
            }

            SetPanelVisible(UIPanelId.Minimap, settings.showMinimap && settings.hudEnabled);
        }
    }
}
