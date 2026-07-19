// 근거: 온라인 시스템.md — 로비 → 인게임 → 결과 화면 → 뒷풀이 시간 흐름.
// 게임 단계는 Core.GameFlowState를 재사용한다 (ARCHITECTURE.md §5 — GamePhase 재정의 금지).
using UnityEngine;
using TSWP.Core;

namespace TSWP.Online
{
    /// <summary>
    /// 네트워크 세션과 게임 흐름의 연결. 실제 전송은 네트워크 도입 시 채운다.
    /// </summary>
    public class GameSessionManager : MonoBehaviour
    {
        public static GameSessionManager Instance { get; private set; }

        [Header("런 시드")]
        [Tooltip("호스트가 생성해 전파하는 시드 — 전 클라이언트가 동일한 맵을 재생성한다.")]
        [SerializeField] private int seed;

        public int Seed => seed;

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
        }

        private void OnDisable()
        {
            GameEvents.FlowStateChanged -= OnFlowStateChanged;
        }

        private void OnFlowStateChanged(GameFlowState state)
        {
            switch (state)
            {
                case GameFlowState.Starting:
                    BeginRun();
                    break;

                case GameFlowState.Results:
                    // TODO(NGO): 결과 데이터를 전 클라이언트에 동기화.
                    break;

                case GameFlowState.AfterParty:
                    // 뒷풀이 — 이동/음성/이모트 유지, 방장의 다시 플레이/로비 이동 선택 대기.
                    break;
            }
        }

        /// <summary>런 시작 — 시드를 확정하고 RunManager를 초기화한다.</summary>
        private void BeginRun()
        {
            // SYNC: 호스트 권위 — 호스트가 시드를 정하고 클라이언트에 전파한다.
            if (seed == 0)
                seed = System.Environment.TickCount;

            int playerCount = LobbyManager.Instance != null
                ? Mathf.Max(1, LobbyManager.Instance.PlayerCount)
                : 1;

            RunManager.Instance?.StartRun(playerCount, seed);
        }

        /// <summary>호스트가 전파한 시드를 클라이언트가 수신할 때 호출. // TODO(NGO): RPC 연결.</summary>
        public void ReceiveSeed(int hostSeed) => seed = hostSeed;
    }
}
