// 근거: 퍼즐 시스템.md — 퍼즐은 진행 방해가 아니라 협동으로 해결하는 게임플레이다.
//       실패해도 게임 진행이 막혀서는 안 되며, 즉시 게임 오버는 금지다. 실패한 퍼즐은 반드시 재도전 가능해야 한다.
// 공통 FSM: Idle → Active → (Solved | Failed) ; Failed → Recovering → Active.
// GameOver로 가는 전이는 이 클래스에 존재하지 않는다 (구조적으로 불가능하게 둔다).
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Core;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 모든 퍼즐의 공통 기반. 유형별 판정은 파생 클래스가 EvaluateSolved()로 구현한다.
    /// </summary>
    public abstract class PuzzleController : MonoBehaviour
    {
        [Header("정의")]
        [SerializeField] protected PuzzleDefinition definition;

        [Header("연출/리커버리")]
        [SerializeField] private PuzzleFeedbackService feedbackService;

        /// <summary>현재 상태. 외부에서는 읽기 전용.</summary>
        public PuzzleState State { get; private set; } = PuzzleState.Idle;

        public PuzzleDefinition Definition => definition;

        /// <summary>제한시간 잔여(초). hasTimeLimit이 false면 의미 없음.</summary>
        public float TimerRemaining { get; private set; }

        /// <summary>현재 퍼즐에 참여 중인 플레이어 수 — 최소 인원 판정과 제한시간 보정에 사용.</summary>
        public int ParticipantCount => _participants.Count;

        /// <summary>지금까지의 재도전 횟수.</summary>
        public int RetryCount { get; private set; }

        // ── 이벤트 (UI/보스/방이 구독) ─────────────────────────────
        public event Action<PuzzleController> Solved;
        public event Action<PuzzleController> Failed;
        public event Action<PuzzleController> Recovered;
        public event Action<PuzzleState> StateChanged;

        private readonly HashSet<int> _participants = new HashSet<int>();
        private RecoveryHandler _recovery;

        protected virtual void Awake()
        {
            _recovery = new RecoveryHandler(this);
        }

        protected virtual void Update()
        {
            if (State != PuzzleState.Active) return;
            if (definition == null || !definition.HasTimeLimit) return;

            TimerRemaining -= Time.deltaTime;
            if (TimerRemaining <= 0f)
            {
                TimerRemaining = 0f;
                // 시간 초과도 실패 처리 — 단, 게임 오버가 아니라 불이익 + 재도전이다.
                Fail();
            }
        }

        // ── 참여 관리 ─────────────────────────────────────────────

        /// <summary>퍼즐 구역에 들어오거나 요소를 조작한 플레이어를 등록한다.</summary>
        public void AddParticipant(int playerId)
        {
            if (playerId < 0) return;
            _participants.Add(playerId);
        }

        public void RemoveParticipant(int playerId) => _participants.Remove(playerId);

        /// <summary>
        /// 최소 인원 충족 여부. 문서: 모든 퍼즐은 최소 2명 협동이 기본이나,
        /// 일부 퍼즐은 혼자서도 해결 가능하다(시간이 더 걸릴 뿐).
        /// </summary>
        public bool HasEnoughParticipants()
        {
            if (definition == null) return true;
            if (definition.SoloSolvable) return _participants.Count >= 1;
            return _participants.Count >= definition.MinPlayers;
        }

        // ── FSM 전이 ──────────────────────────────────────────────

        /// <summary>퍼즐 시작. Idle 또는 Recovering 상태에서만 진입한다.</summary>
        public virtual void Begin()
        {
            if (State == PuzzleState.Active || State == PuzzleState.Solved) return;

            if (definition != null && definition.HasTimeLimit)
            {
                Difficulty difficulty = GameFlowManager.Instance != null
                    ? GameFlowManager.Instance.SelectedDifficulty
                    : Difficulty.Human;
                TimerRemaining = definition.GetEffectiveTimeLimit(difficulty, _participants.Count);
            }

            OnBegin();
            SetState(PuzzleState.Active);
        }

        /// <summary>해결 처리. 보상 지급은 상위(방/보스)가 이벤트를 구독해 수행한다.</summary>
        public void Solve()
        {
            if (State != PuzzleState.Active) return;

            OnSolve();
            SetState(PuzzleState.Solved);

            string id = definition != null ? definition.PuzzleId : name;
            GameEvents.RaisePuzzleSolved(id);
            GameEvents.RaiseStatCounter("puzzle.solved", 1);
            Solved?.Invoke(this);
        }

        /// <summary>
        /// 실패 처리 — 불이익을 적용하고 리커버리로 넘긴다.
        /// 게임 오버로 가지 않는다 (문서 명시: 즉시 게임 오버 금지).
        /// </summary>
        public void Fail()
        {
            if (State != PuzzleState.Active) return;

            OnFail();
            SetState(PuzzleState.Failed);

            string id = definition != null ? definition.PuzzleId : name;
            GameEvents.RaisePuzzleFailed(id);
            Failed?.Invoke(this);

            // 실패 연출 — 같은 실수를 반복하지 않도록 원인을 즉시 알린다.
            if (feedbackService != null && definition != null)
                feedbackService.Play(definition.FailureFeedbacks, transform.position);

            ApplyFailurePenalties();
            BeginRecovery();
        }

        /// <summary>리커버리 시작 — 어떤 경로로든 재도전이 보장되어야 한다(소프트락 금지 불변식).</summary>
        private void BeginRecovery()
        {
            SetState(PuzzleState.Recovering);
            _recovery?.Begin(definition, this);
        }

        /// <summary>RecoveryHandler가 재도전 준비를 마쳤을 때 호출한다.</summary>
        internal void CompleteRecovery()
        {
            if (State != PuzzleState.Recovering) return;

            RetryCount++;
            ResetPuzzle();
            Recovered?.Invoke(this);

            // 재도전 횟수 제한이 있어도 진행을 막지 않는다 — 우회 경로가 열릴 뿐이다.
            if (definition != null && definition.MaxRetryCount > 0 && RetryCount >= definition.MaxRetryCount)
            {
                // TODO: 우회 경로(OpenAlternatePath) 개방을 방 시스템에 통지 — 소프트락 방지 최후 수단.
                Debug.Log($"[PuzzleController] '{name}' 재도전 한도 도달 — 우회 경로를 열어야 합니다.", this);
                SetState(PuzzleState.Idle);
                return;
            }

            Begin();
        }

        private void SetState(PuzzleState next)
        {
            if (State == next) return;
            State = next;
            StateChanged?.Invoke(next);
        }

        // ── 실패 불이익 ───────────────────────────────────────────
        private void ApplyFailurePenalties()
        {
            if (definition == null) return;

            var penalties = definition.FailurePenalties;
            for (int i = 0; i < penalties.Count; i++)
                ApplyPenalty(penalties[i]);
        }

        /// <summary>불이익 1건 적용. 파생 클래스가 유형별로 확장할 수 있다.</summary>
        protected virtual void ApplyPenalty(PuzzleFailurePenalty penalty)
        {
            switch (penalty)
            {
                case PuzzleFailurePenalty.SpawnEnemies:
                case PuzzleFailurePenalty.RevealHiddenEnemy:
                    // TODO: Enemies.SpawnManager로 definition.PenaltyEncounter 스폰 요청.
                    break;

                case PuzzleFailurePenalty.ActivateTrap:
                case PuzzleFailurePenalty.CollapseBridge:
                case PuzzleFailurePenalty.LockDoor:
                    // TODO: 맵 구조물(함정/다리/문) 상태 변경 — Map 시스템 연동.
                    break;

                case PuzzleFailurePenalty.DamageHealth:
                    // TODO: 참여 플레이어에게 definition.PenaltyDamage 적용 (Combat.DamageSystem 경유).
                    break;

                case PuzzleFailurePenalty.ReduceReward:
                case PuzzleFailurePenalty.LoseReward:
                    // 보상 감소는 지급 시점에 RewardReductionRatio로 반영한다.
                    break;

                case PuzzleFailurePenalty.ResetPuzzle:
                    ResetPuzzle();
                    break;
            }
        }

        // ── 파생 클래스 훅 ────────────────────────────────────────

        /// <summary>퍼즐 요소를 초기 상태로 되돌린다 (재도전 준비).</summary>
        public abstract void ResetPuzzle();

        /// <summary>현재 요소 상태가 정답인지 판정한다. 파생 클래스가 요소 이벤트 수신 후 호출한다.</summary>
        protected abstract bool EvaluateSolved();

        protected virtual void OnBegin() { }
        protected virtual void OnSolve() { }
        protected virtual void OnFail() { }

        /// <summary>요소 상태가 바뀔 때 파생 클래스가 호출 — 정답이면 자동으로 Solve로 간다.</summary>
        protected void CheckSolved()
        {
            if (State != PuzzleState.Active) return;
            if (!HasEnoughParticipants()) return;
            if (EvaluateSolved()) Solve();
        }
    }
}
