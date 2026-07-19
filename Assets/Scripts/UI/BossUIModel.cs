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
    }
}
