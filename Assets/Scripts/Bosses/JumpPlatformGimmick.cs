// 근거: 보스 시스템.md — 기믹 예시 '위치 이동'/'약점 노출'. 난이도가 바뀌어도 기믹은 불변이다.
// 보스 01 해치 퀸의 '점프 발판(Jump Platform)' 구현.
//   발판이 일정 시간만 열리고, 그 동안 위로 올라가면 보스 약점을 때릴 수 있다
//   → 누군가는 올라가고 누군가는 아래에서 버티는 역할 분담이 자연스럽게 생긴다.
// 발판·약점 오브젝트는 전부 인스펙터 참조다 — 새 보스는 같은 컴포넌트에 다른 오브젝트만 꽂으면 된다.
using System;
using UnityEngine;

namespace TSWP.Bosses
{
    /// <summary>
    /// 일정 시간 동안 점프 발판(과 선택적으로 약점)을 열어 두는 기믹.
    /// 열린 시간의 잔여 비율을 게이지로 UI에 밀어 넣는다.
    /// SYNC: 개방 시각/종료 시각은 호스트 권위.
    /// </summary>
    public sealed class JumpPlatformGimmick : MonoBehaviour, IGimmick
    {
        [Header("식별")]
        [SerializeField] private string gimmickId = "gimmick.jumpplatform";

        [Tooltip("기믹 분류 — 발판을 타고 올라가 약점을 치는 흐름이므로 기본은 WeakPointExpose.")]
        [SerializeField] private GimmickType gimmickType = GimmickType.WeakPointExpose;

        [Header("발판")]
        [Tooltip("기믹 작동 중에만 켜지는 발판 오브젝트들. 씬/보스 프리팹에 미리 배치하고 꺼 둔다.")]
        [SerializeField] private GameObject[] platformObjects = Array.Empty<GameObject>();

        [Tooltip("기믹 작동 중에만 노출되는 약점(선택). 없으면 발판만 열린다.")]
        [SerializeField] private BossWeakPoint weakPoint;

        [Header("시간")]
        [Tooltip("발판이 열려 있는 시간(초).")]
        [SerializeField, Min(0.1f)] private float openDuration = 8f; // TODO(밸런스): 문서 미정

        [Tooltip("발판이 열리기 전 예고 시간(초) — 갑자기 열리면 반응할 수 없다.")]
        [SerializeField, Min(0f)] private float warmupDuration = 1f; // TODO(밸런스): 문서 미정

        [Header("연출")]
        [SerializeField] private string openVfxId;
        [SerializeField] private string closeVfxId;

        [Tooltip("잔여 시간을 보스 게이지 UI로 표시할지.")]
        [SerializeField] private bool pushGaugeToUi = true;

        private BossController _owner;
        private float _timer;
        private bool _running;
        private bool _opened;

        // ── IGimmick ──────────────────────────────────────────────
        public string GimmickId => gimmickId;
        public GimmickType Type => gimmickType;
        public bool IsRunning => _running;
        public event Action<IGimmick> Completed;

        private void Awake()
        {
            // 시작 상태는 항상 닫힘. 씬에 켜진 채로 저장돼 있어도 여기서 정리된다.
            SetPlatformsActive(false);
        }

        public void Activate(BossController owner)
        {
            if (_running) return;

            _owner = owner;
            _running = true;
            _opened = false;
            _timer = 0f;

            PushGauge(0f, true);
        }

        public void Interrupt()
        {
            if (!_running) return;
            Close(silent: true);
        }

        private void OnDisable()
        {
            if (_running) Interrupt();
        }

        private void Update()
        {
            if (!_running) return;

            _timer += Time.deltaTime;

            if (!_opened)
            {
                // 예고 구간 — 아직 발판은 닫혀 있다.
                if (_timer < warmupDuration)
                {
                    PushGauge(warmupDuration <= 0f ? 0f : _timer / warmupDuration, true);
                    return;
                }
                Open();
                return;
            }

            float openElapsed = _timer - warmupDuration;
            if (openElapsed >= openDuration)
            {
                Close(silent: false);
                return;
            }

            // 남은 시간 비율 — 플레이어가 '언제 닫히는지' 볼 수 있어야 공정하다.
            PushGauge(1f - openElapsed / openDuration, true);
        }

        private void Open()
        {
            _opened = true;
            SetPlatformsActive(true);

            if (!string.IsNullOrEmpty(openVfxId))
                Art.VfxSpawner.Instance?.Play(openVfxId, transform.position);

            PushGauge(1f, true);
        }

        private void Close(bool silent)
        {
            _running = false;
            _opened = false;
            SetPlatformsActive(false);

            if (!silent && !string.IsNullOrEmpty(closeVfxId))
                Art.VfxSpawner.Instance?.Play(closeVfxId, transform.position);

            PushGauge(0f, false); // 게이지 UI를 반드시 숨긴다

            if (!silent)
                Completed?.Invoke(this);
        }

        private void SetPlatformsActive(bool value)
        {
            for (int i = 0; i < platformObjects.Length; i++)
                if (platformObjects[i] != null) platformObjects[i].SetActive(value);

            if (weakPoint != null)
                weakPoint.gameObject.SetActive(value);
        }

        private void PushGauge(float value01, bool visible)
        {
            if (!pushGaugeToUi) return;
            string bossId = _owner != null && _owner.Data != null ? _owner.Data.BossId : gimmickId;
            BossGaugeChannel.RaiseGauge(bossId, value01, visible);
        }
    }
}
