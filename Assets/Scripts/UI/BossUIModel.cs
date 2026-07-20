// 근거: UI 시스템.md — 보스전에서 화면 상단에 보스 이름 / 체력 / 분노 상태를 표시하고,
//   기믹 진행 상황이 있을 경우 별도의 게이지를 표시한다.
// 보스 로직은 Bosses.BossController가 소유하며, UI는 GameEvents 구독으로만 갱신된다.
using System;
using UnityEngine;
using TSWP.Core;

namespace TSWP.UI
{
    /// <summary>보스 상단 UI 뷰모델. 보스전 진입/종료 이벤트로 활성화된다.</summary>
    public sealed class BossUIModel
    {
        /// <summary>표시 여부 — BossAppeared에서 true, BossDefeated에서 false.</summary>
        public bool IsVisible;

        /// <summary>보스 식별자 (BossData의 bossId).</summary>
        public string BossId;

        /// <summary>보스 표시 이름. bossId만으로는 알 수 없으므로 뷰가 BossData 조회로 채운다.</summary>
        public string BossName;

        /// <summary>체력 비율 0~1 (GameEvents.BossHealthChanged가 비율로 전달).</summary>
        public float HpRatio = 1f;

        /// <summary>분노(광폭화) 상태 — 체력바 색/이펙트 변경 트리거.</summary>
        public bool IsEnraged;

        /// <summary>현재 전투 단계 (Bosses.BossFightPhase의 int 값).</summary>
        public int PhaseIndex;

        /// <summary>기믹 게이지가 존재하는 보스인지. false면 게이지 UI 자체를 숨긴다.</summary>
        public bool HasGimmickGauge;

        /// <summary>기믹 진행도 0~1. HasGimmickGauge가 true일 때만 표시.</summary>
        public float GimmickGauge;

        /// <summary>위험도(Danger Bar) 표시 여부. false면 위험도 바 자체를 숨긴다.</summary>
        public bool HasDangerBar;

        /// <summary>위험도 0~1. 보스 패턴(예: 돌진 예고)이 얼마나 임박했는지 — 1에 가까울수록 위험.</summary>
        public float DangerLevel;

        /// <summary>위험도 라벨 (예: "돌진"). 없으면 뷰가 라벨을 생략한다.</summary>
        public string DangerLabel;

        /// <summary>화면 덮기 연출 강도 0~1 (예: 거미줄이 시야를 가림). 0이면 뷰가 그리지 않는다.</summary>
        public float ScreenOverlayAmount;

        /// <summary>화면 덮기 색 (보스마다 다르다 — 코드가 아니라 호출자가 결정한다).</summary>
        public Color ScreenOverlayColor = new Color(0.85f, 0.85f, 0.9f, 1f);

        public event Action Changed;

        private bool _subscribed;

        public void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;
            GameEvents.BossAppeared += OnBossAppeared;
            GameEvents.BossHealthChanged += OnBossHealthChanged;
            GameEvents.BossPhaseChanged += OnBossPhaseChanged;
            GameEvents.BossEnraged += OnBossEnraged;
            GameEvents.BossDefeated += OnBossDefeated;
        }

        public void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;
            GameEvents.BossAppeared -= OnBossAppeared;
            GameEvents.BossHealthChanged -= OnBossHealthChanged;
            GameEvents.BossPhaseChanged -= OnBossPhaseChanged;
            GameEvents.BossEnraged -= OnBossEnraged;
            GameEvents.BossDefeated -= OnBossDefeated;
        }

        private void OnBossAppeared(string bossId)
        {
            BossId = bossId;
            BossName = bossId;   // TODO: BossData 조회로 표시 이름 치환 (이벤트 페이로드는 id만 전달)
            HpRatio = 1f;
            IsEnraged = false;
            PhaseIndex = 0;
            GimmickGauge = 0f;
            HasGimmickGauge = false;
            HasDangerBar = false;
            DangerLevel = 0f;
            DangerLabel = null;
            ScreenOverlayAmount = 0f;
            IsVisible = true;
            Changed?.Invoke();
        }

        private void OnBossHealthChanged(string bossId, float ratio)
        {
            if (bossId != BossId) return;
            HpRatio = Mathf.Clamp01(ratio);
            Changed?.Invoke();
        }

        private void OnBossPhaseChanged(string bossId, int phase)
        {
            if (bossId != BossId) return;
            PhaseIndex = phase;
            Changed?.Invoke();
        }

        private void OnBossEnraged(string bossId)
        {
            if (bossId != BossId) return;
            IsEnraged = true;
            Changed?.Invoke();
        }

        private void OnBossDefeated(string bossId)
        {
            if (bossId != BossId) return;
            IsVisible = false;
            // 전투가 끝났는데 위험도/화면 덮기 연출이 남으면 화면이 잠긴 것처럼 보인다 — 반드시 정리한다.
            HasDangerBar = false;
            DangerLevel = 0f;
            ScreenOverlayAmount = 0f;
            Changed?.Invoke();
        }

        /// <summary>기믹 게이지 갱신. 보스별 IGimmick 구현이 UI로 밀어 넣는 지점.
        /// TODO: 기믹 진행도 전용 GameEvents가 없으므로 현재는 직접 호출 (GameEvents 수정 금지).</summary>
        public void SetGimmickGauge(float value01, bool hasGauge = true)
        {
            HasGimmickGauge = hasGauge;
            GimmickGauge = Mathf.Clamp01(value01);
            Changed?.Invoke();
        }

        /// <summary>
        /// 위험도(Danger Bar) 갱신. 보스별 패턴/기믹 구현이 UI로 밀어 넣는 지점.
        /// 보스가 늘어도 UI 코드는 그대로다 — 새 보스는 이 API를 호출하기만 하면 된다.
        /// TODO: 위험도 전용 GameEvents가 없으므로 직접 호출 (GameEvents 수정 금지).
        /// </summary>
        public void SetDangerLevel(float value01, bool hasBar = true, string label = null)
        {
            HasDangerBar = hasBar;
            DangerLevel = Mathf.Clamp01(value01);
            if (label != null) DangerLabel = label;
            Changed?.Invoke();
        }

        /// <summary>
        /// 화면 덮기 연출 갱신 (예: 거미줄이 시야를 가리는 패턴). amount 0이면 사라진다.
        /// 색을 인자로 받으므로 보스별 연출이 늘어도 UI 코드는 바뀌지 않는다.
        /// </summary>
        public void SetScreenOverlay(float amount01, Color? color = null)
        {
            ScreenOverlayAmount = Mathf.Clamp01(amount01);
            if (color.HasValue) ScreenOverlayColor = color.Value;
            Changed?.Invoke();
        }

        /// <summary>보스전 이탈/리셋 시 정리 (전투 중단 시 잔상 연출 제거).</summary>
        public void Clear()
        {
            IsVisible = false;
            HasGimmickGauge = false;
            GimmickGauge = 0f;
            HasDangerBar = false;
            DangerLevel = 0f;
            DangerLabel = null;
            ScreenOverlayAmount = 0f;
            Changed?.Invoke();
        }
    }
}
