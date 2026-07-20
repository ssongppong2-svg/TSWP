// 근거: 보스 시스템.md — 모든 보스는 협동 퍼즐을 반드시 포함한다 (혼자 해결하기 어렵게 설계).
// 퍼즐 시스템.md — 퍼즐 FSM/판정은 Puzzles.PuzzleController가 소유한다.
//
// 의존 방향 정리(중요): ICoopPuzzle.Begin(BossController)를 Puzzles가 구현하면 Puzzles → Bosses 역참조가 생긴다.
// 그래서 어댑터를 Bosses/에 둔다. Puzzles는 보스를 전혀 모르고, Bosses가 PuzzleController를 '감싼다'.
//   → 보스 2번의 협동 퍼즐이 레버든 발판이든 폭탄 릴레이든 이 어댑터 하나로 전부 연결된다(코드 수정 0).
using System;
using UnityEngine;
using TSWP.Puzzles;

namespace TSWP.Bosses
{
    /// <summary>
    /// Puzzles.PuzzleController를 보스전 협동 퍼즐(ICoopPuzzle)로 노출하는 어댑터.
    /// 보스 방의 퍼즐 오브젝트에 이 컴포넌트를 얹고 대상 PuzzleController를 꽂으면 된다.
    /// </summary>
    public sealed class CoopPuzzleAdapter : MonoBehaviour, ICoopPuzzle
    {
        [Header("식별")]
        [SerializeField] private string puzzleId = "boss.puzzle.coop";

        [Tooltip("협동 퍼즐 분류 5종 (동시 레버/발판 유지/폭탄 전달/팀원 보호/다중 위치 공격).")]
        [SerializeField] private CoopPuzzleType puzzleType = CoopPuzzleType.SimultaneousLevers;

        [Tooltip("최소 참여 인원. 비워두면 PuzzleDefinition.MinPlayers를 따른다(0 이하일 때).")]
        [SerializeField] private int minPlayers = 0;

        [Header("대상 퍼즐")]
        [Tooltip("감쌀 퍼즐 컨트롤러. ButtonPuzzleController/레버 퍼즐 등 어떤 파생이든 된다.")]
        [SerializeField] private PuzzleController puzzle;

        [Tooltip("퍼즐 시작 시 켜고, 중단/해결 시 끌 오브젝트들 (퍼즐 구조물·안내 표식 등).")]
        [SerializeField] private GameObject[] activateOnBegin = Array.Empty<GameObject>();

        [Tooltip("퍼즐이 해결된 뒤에도 구조물을 남겨 둘지. false면 해결 시 끈다.")]
        [SerializeField] private bool keepObjectsAfterSolved = true;

        [Header("진행도 (거친 단계 표현)")]
        // PuzzleController는 공통 '진행도'를 노출하지 않는다(유형마다 정의가 다르기 때문).
        // 보스 AI는 진행도를 '방해 패턴 트리거' 용도로만 쓰므로 단계값으로 충분하다.
        [Tooltip("퍼즐이 진행 중일 때 보스 AI에 보고할 진행도(0~1).")]
        [SerializeField, Range(0f, 1f)] private float activeProgress = 0.5f;

        private BossController _owner;
        private bool _subscribed;

        /// <summary>이 퍼즐을 시작시킨 보스 (연출/보상 연동 확장 지점). Begin 전에는 null.</summary>
        public BossController Owner => _owner;

        // ── ICoopPuzzle ───────────────────────────────────────────
        public string PuzzleId => puzzleId;
        public CoopPuzzleType Type => puzzleType;

        public int MinPlayers
        {
            get
            {
                if (minPlayers > 0) return minPlayers;
                if (puzzle != null && puzzle.Definition != null) return puzzle.Definition.MinPlayers;
                return 2; // 문서 기본값 — 협동 퍼즐은 최소 2명
            }
        }

        public bool IsSolved => puzzle != null && puzzle.State == PuzzleState.Solved;

        public float Progress
        {
            get
            {
                if (puzzle == null) return 0f;
                if (puzzle.State == PuzzleState.Solved) return 1f;
                if (puzzle.State == PuzzleState.Active || puzzle.State == PuzzleState.Recovering)
                    return activeProgress;
                return 0f;
            }
        }

        public event Action<ICoopPuzzle> Solved;
        public event Action<ICoopPuzzle, float> ProgressChanged;

        public void Begin(BossController owner)
        {
            _owner = owner;

            if (puzzle == null)
            {
                Debug.LogWarning($"[CoopPuzzleAdapter] '{name}': 대상 PuzzleController가 비어 있어 시작할 수 없습니다.", this);
                return;
            }

            SetObjectsActive(true);
            Subscribe();
            puzzle.Begin();
            RaiseProgress();
        }

        public void Cancel()
        {
            Unsubscribe();
            if (!IsSolved) SetObjectsActive(false);
            RaiseProgress();
        }

        private void OnDisable() => Unsubscribe();

        // ── 퍼즐 이벤트 중계 ──────────────────────────────────────
        private void Subscribe()
        {
            if (_subscribed || puzzle == null) return;
            _subscribed = true;
            puzzle.Solved += OnPuzzleSolved;
            puzzle.StateChanged += OnPuzzleStateChanged;
        }

        private void Unsubscribe()
        {
            if (!_subscribed || puzzle == null) return;
            _subscribed = false;
            puzzle.Solved -= OnPuzzleSolved;
            puzzle.StateChanged -= OnPuzzleStateChanged;
        }

        private void OnPuzzleSolved(PuzzleController _)
        {
            if (!keepObjectsAfterSolved) SetObjectsActive(false);
            RaiseProgress();
            Solved?.Invoke(this); // BossController가 받아 PatternChange 단계로 전이
        }

        // 실패 → 리커버리 → 재시작도 상태 변화로만 통지된다. 퍼즐 실패가 보스전을 막지 않는다
        // (퍼즐 시스템.md: 실패해도 진행이 막히면 안 된다 — 보스는 계속 패턴을 돌린다).
        private void OnPuzzleStateChanged(PuzzleState _) => RaiseProgress();

        private void RaiseProgress() => ProgressChanged?.Invoke(this, Progress);

        private void SetObjectsActive(bool value)
        {
            for (int i = 0; i < activateOnBegin.Length; i++)
                if (activateOnBegin[i] != null) activateOnBegin[i].SetActive(value);
        }
    }
}
