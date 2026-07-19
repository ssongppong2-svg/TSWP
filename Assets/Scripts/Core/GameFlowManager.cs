// 근거: 게임 시작과 선택, 직업, 플레이.md — 메인 메뉴→로비→튜토리얼→탐험/보스→결과→뒷풀이 전체 흐름.
// 상태 전환은 호스트 권위로 구동하고 전 클라이언트에 동기화한다.
// SYNC: 호스트 권위, 추후 NGO NetworkVariable
using UnityEngine;

namespace TSWP.Core
{
    public sealed class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        [SerializeField] private GameFlowState _initialState = GameFlowState.MainMenu;

        public GameFlowState State { get; private set; }
        public Difficulty SelectedDifficulty { get; private set; } = Difficulty.Human;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            State = _initialState;
        }

        public void ChangeState(GameFlowState next)
        {
            if (State == next) return;
            State = next;
            GameEvents.RaiseFlowStateChanged(next);
        }

        /// <summary>난이도는 방장만 선택할 수 있다. 호출부에서 방장 검증 후 호출.</summary>
        public void SetDifficulty(Difficulty difficulty)
        {
            SelectedDifficulty = difficulty;
            GameEvents.RaiseDifficultyChanged(difficulty);
        }

        // ── 흐름 전환 헬퍼 (문서의 순서 그대로) ─────────────────────
        public void EnterLobby() => ChangeState(GameFlowState.Lobby);
        public void StartGame() => ChangeState(GameFlowState.Starting);
        public void BeginTutorial() => ChangeState(GameFlowState.Tutorial);           // 스킵 가능 오버레이
        public void DropStartItems() => ChangeState(GameFlowState.StartItemDrop);
        public void BeginExploration() => ChangeState(GameFlowState.Exploration);
        public void BeginBossFight() => ChangeState(GameFlowState.BossFight);
        public void ShowResults() => ChangeState(GameFlowState.Results);
        public void BeginAfterParty() => ChangeState(GameFlowState.AfterParty);       // 뒷풀이 — 방장 선택 대기

        /// <summary>뒷풀이 후 방장 선택: 다시 플레이 또는 로비 이동.</summary>
        public void HostChoseReplay() => ChangeState(GameFlowState.Starting);
        public void HostChoseReturnToLobby() => ChangeState(GameFlowState.Lobby);
    }
}
