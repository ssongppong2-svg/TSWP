// 근거: 보스 시스템.md — 모든 보스는 최소 1개의 핵심 기믹을 가진다 / 30초 안에 반응할 상황을 만든다(30초 법칙).
// UI 시스템.md — 기믹 진행 상황이 있는 보스는 별도 게이지를 표시한다.
// 보스 01 해치 퀸의 'UI Danger Bar' 구현. 게이지가 차오르면 방 전체 폭발이 일어난다 →
// 플레이어는 게이지를 보고 '흩어질지 계속 딜할지'를 판단하게 된다.
// 보스 전용이 아니다 — '차오르면 무언가 터지는 게이지'라는 일반 기믹이다.
using System;
using UnityEngine;
using TSWP.Combat;
using TSWP.StatusEffects;
using System.Collections.Generic;

namespace TSWP.Bosses
{
    /// <summary>
    /// 시간에 따라 차오르는 위험도 게이지 기믹. 가득 차면 설정된 폭발을 일으키고 0으로 초기화한다.
    /// 게이지 값은 BossGaugeChannel로 UI에 밀어 넣는다 (UI는 게임 로직을 직접 참조하지 않는다).
    /// SYNC: 게이지 값과 폭발 판정은 호스트 권위.
    /// </summary>
    public sealed class DangerGaugeGimmick : MonoBehaviour, IGimmick
    {
        [Header("식별")]
        [SerializeField] private string gimmickId = "gimmick.dangerbar";

        [Tooltip("기믹 분류 — 게이지가 차기 전에 흩어져야 하므로 기본은 Reposition(위치 이동).")]
        [SerializeField] private GimmickType gimmickType = GimmickType.Reposition;

        [Header("게이지 충전")]
        [Tooltip("초당 기본 충전량(0~1 기준).")]
        [SerializeField, Min(0f)] private float fillPerSecond = 0.1f; // TODO(밸런스): 문서 미정

        [Tooltip("보스 근처에 플레이어가 있으면 추가로 차는 양(초당). 근접 딜에 위험 부담을 준다.")]
        [SerializeField, Min(0f)] private float fillPerSecondPerNearbyPlayer = 0.03f; // TODO(밸런스): 문서 미정

        [Tooltip("'보스 근처' 판정 반경.")]
        [SerializeField, Min(0.1f)] private float proximityRadius = 4f; // TODO(밸런스): 문서 미정

        [Header("폭발 (게이지가 가득 찼을 때)")]
        [Tooltip("폭발 반경. 이 밖으로 나가면 안전하다 — 회피 가능해야 공정하다.")]
        [SerializeField, Min(0.1f)] private float burstRadius = 6f; // TODO(밸런스): 문서 미정

        [Tooltip("폭발 피해량.")]
        [SerializeField, Min(0f)] private float burstDamage = 25f; // TODO(밸런스): 문서 미정

        [SerializeField, Min(0f)] private float burstKnockbackForce = 10f; // TODO(밸런스): 문서 미정
        [SerializeField, Min(0f)] private float burstStunDuration = 0.3f;  // TODO(밸런스): 문서 미정

        [Tooltip("폭발 시 부여할 상태이상. 비워도 된다.")]
        [SerializeField] private List<StatusEffectData> burstStatusEffects = new List<StatusEffectData>();

        [Tooltip("폭발을 폭발 판정으로 처리할지 (구조물 파괴 가능 여부에 영향).")]
        [SerializeField] private bool burstIsExplosive = true;

        [SerializeField] private string burstVfxId;

        [Header("반복")]
        [Tooltip("폭발 후 다시 차오를지. false면 1회만 터지고 기믹이 끝난다.")]
        [SerializeField] private bool repeat = true;

        [Tooltip("최대 반복 횟수. 0이면 무제한.")]
        [SerializeField, Min(0)] private int maxCycles = 0;

        [Tooltip("폭발 직후 게이지가 멈춰 있는 시간(초) — 연속 폭발로 도망칠 틈이 없어지는 것을 막는다.")]
        [SerializeField, Min(0f)] private float cooldownAfterBurst = 1.5f; // TODO(밸런스): 문서 미정

        private BossController _owner;
        private readonly List<CombatEntity> _buffer = new List<CombatEntity>(8);

        private float _gauge;
        private float _cooldownTimer;
        private int _cycles;
        private bool _running;

        // ── IGimmick ──────────────────────────────────────────────
        public string GimmickId => gimmickId;
        public GimmickType Type => gimmickType;
        public bool IsRunning => _running;
        public event Action<IGimmick> Completed;

        /// <summary>현재 게이지 0~1 (UI/디버그 조회용).</summary>
        public float Gauge => _gauge;

        public void Activate(BossController owner)
        {
            _owner = owner;
            _running = true;
            _gauge = 0f;
            _cooldownTimer = 0f;
            _cycles = 0;
            PushGauge(true);
        }

        public void Interrupt()
        {
            if (!_running) return;
            _running = false;
            _gauge = 0f;
            PushGauge(false); // 게이지 UI를 반드시 숨긴다 — 빼먹으면 보스 사후에도 게이지가 남는다
        }

        private void OnDisable()
        {
            // 오브젝트가 꺼질 때도 UI를 정리한다.
            if (_running) Interrupt();
        }

        private void Update()
        {
            if (!_running) return;

            float dt = Time.deltaTime;

            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= dt;
                return;
            }

            _gauge += CurrentFillRate() * dt;

            if (_gauge >= 1f)
            {
                _gauge = 1f;
                PushGauge(true);
                Burst();
                return;
            }

            PushGauge(true);
        }

        /// <summary>기본 충전량 + 근처 플레이어 수 보너스.</summary>
        private float CurrentFillRate()
        {
            BossCombatUtil.CollectPlayers(transform.position, proximityRadius, _buffer);
            return fillPerSecond + fillPerSecondPerNearbyPlayer * _buffer.Count;
        }

        private void Burst()
        {
            CombatEntity source = _owner != null ? _owner.Entity : null;

            BossCombatUtil.ApplyAreaHit(
                source, transform.position, burstRadius, burstDamage,
                burstKnockbackForce, burstStunDuration,
                burstStatusEffects.Count > 0 ? burstStatusEffects : null,
                burstIsExplosive);

            if (!string.IsNullOrEmpty(burstVfxId))
                Art.VfxSpawner.Instance?.Play(burstVfxId, transform.position);

            _cycles++;
            Completed?.Invoke(this); // 폭발은 '반응할 상황' — BossController가 30초 법칙 비트로 잡는다

            bool exhausted = !repeat || (maxCycles > 0 && _cycles >= maxCycles);
            if (exhausted)
            {
                _running = false;
                _gauge = 0f;
                PushGauge(false);
                return;
            }

            _gauge = 0f;
            _cooldownTimer = cooldownAfterBurst;
            PushGauge(true);
        }

        private void PushGauge(bool visible)
        {
            string bossId = _owner != null && _owner.Data != null ? _owner.Data.BossId : gimmickId;
            BossGaugeChannel.RaiseGauge(bossId, _gauge, visible);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, proximityRadius);
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, burstRadius);
        }
#endif
    }
}
