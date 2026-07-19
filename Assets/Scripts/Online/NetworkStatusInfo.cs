// 근거: 온라인 시스템.md / UI 시스템.md — 핑(지연시간)과 네트워크 상태를 표시한다(설정에서 켜고 끌 수 있다).
using UnityEngine;

namespace TSWP.Online
{
    /// <summary>
    /// 네트워크 상태 측정값. UI가 이 값을 읽어 표시한다.
    /// </summary>
    public class NetworkStatusInfo : MonoBehaviour
    {
        public static NetworkStatusInfo Instance { get; private set; }

        /// <summary>왕복 지연시간(ms). // TODO(NGO): 실제 RTT 측정으로 교체.</summary>
        public int PingMs { get; private set; }

        /// <summary>측정 프레임레이트.</summary>
        public int Fps { get; private set; }

        public ConnectionState State { get; private set; } = ConnectionState.Connected;

        [Header("측정")]
        [SerializeField, Min(0.1f)] private float sampleInterval = 0.5f;

        private float _timer;
        private int _frameCount;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            _frameCount++;
            _timer += Time.unscaledDeltaTime;

            if (_timer < sampleInterval) return;

            Fps = Mathf.RoundToInt(_frameCount / _timer);
            _frameCount = 0;
            _timer = 0f;
        }

        public void SetPing(int pingMs) => PingMs = pingMs;

        public void SetState(ConnectionState state) => State = state;
    }
}
