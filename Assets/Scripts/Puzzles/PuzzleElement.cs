// 근거: 퍼즐 시스템.md — 퍼즐 요소(버튼/레버/발판/상자/운반물/폭탄)는 각각 '정답 동작'과 '오답(트롤) 동작'을 가진다.
//       트롤 철학: 실수와 장난은 웃음이 되어야 하며, 고의적 방해를 유도해서는 안 된다.
// 요소는 컨트롤러를 직접 알지 않고 C# event로 상태 변화만 통지한다 (결합도 최소화).
// 프로토타입 원칙: 부모 PuzzleController가 없어도 요소 하나만 놓고 조작할 수 있어야 한다 (owner는 항상 null 가드).
using System;
using UnityEngine;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 퍼즐 구성 요소의 공통 기반. 파생 클래스가 상호작용 방식을 구현한다.
    /// </summary>
    public abstract class PuzzleElement : MonoBehaviour
    {
        [Header("소속")]
        [Tooltip("이 요소가 속한 퍼즐. 비우면 부모에서 자동 탐색한다. 끝내 없으면 요소 단독으로 동작한다(프로토타입).")]
        [SerializeField] protected PuzzleController owner;

        [Header("트롤 결과")]
        [Tooltip("오조작 시 발생할 결과. 비워두면 오조작에 결과가 없다.")]
        [SerializeField] protected TrollOutcome trollOutcome;

        [Header("시각 피드백 (프로토타입 — 스프라이트가 없으면 흰 사각형을 자동 생성한다)")]
        [SerializeField] protected PuzzleElementVisual visual = new PuzzleElementVisual();

        /// <summary>요소 상태가 변했을 때 발행 — 컨트롤러가 구독해 정답 판정을 수행한다.</summary>
        public event Action<PuzzleElement> StateChanged;

        /// <summary>오조작(트롤)이 발생했을 때 발행.</summary>
        public event Action<PuzzleElement, TrollOutcome> WrongAction;

        public PuzzleController Owner => owner;

        /// <summary>개발 HUD(PuzzleDebugHud)에 표시할 한 줄 상태. 파생 클래스가 덮어쓴다.</summary>
        public virtual string DebugStatus => "-";

        // 운반/투척 중 콜라이더를 잠시 통과 가능하게 바꾸기 위한 캐시.
        private Collider2D[] _colliders;
        private bool[] _colliderTriggerDefaults;

        protected virtual void Awake()
        {
            if (owner == null)
                owner = GetComponentInParent<PuzzleController>();

            CacheColliders();
            visual.Bind(this);
        }

        /// <summary>시각 보간 갱신. 파생 클래스가 Update를 쓸 때는 반드시 base.Update()를 호출한다.</summary>
        protected virtual void Update()
        {
            visual.Tick(Time.deltaTime);
        }

        /// <summary>상태 변화 통지 — 파생 클래스가 상태를 바꾼 뒤 호출한다.</summary>
        protected void NotifyStateChanged() => StateChanged?.Invoke(this);

        /// <summary>
        /// 오조작 통지. 결과(몬스터 소환/함정 개방/다리 붕괴 등)는 TrollOutcome 데이터가 정의한다.
        /// 웃음을 만드는 것이 목적이므로 진행을 완전히 막는 결과는 넣지 않는다.
        /// 컨트롤러가 없어도 섬광과 로그로 '뭔가 잘못됐다'는 것이 즉시 보여야 한다.
        /// </summary>
        protected void NotifyWrongAction()
        {
            visual.Flash();

            string what = trollOutcome != null
                ? $"{trollOutcome.wrongAction} → {trollOutcome.consequence}"
                : "결과 데이터 없음";
            PuzzleLog.Record(this, $"트롤! {name} — {what}");

            WrongAction?.Invoke(this, trollOutcome);
            GameEventsTrollCounter();
        }

        /// <summary>정상 상태 변화 로그 — 파생 클래스가 사람이 읽을 문구를 넘긴다.</summary>
        protected void Log(string message) => PuzzleLog.Record(this, $"{name}: {message}");

        private void GameEventsTrollCounter()
        {
            // 트롤 업적 카운터 — 실수도 기록되어 결과 화면의 '가장 많은 트롤(?)'에 반영된다.
            Core.GameEvents.RaiseStatCounter("puzzle.troll", 1);
        }

        // ── 참여자 등록 ───────────────────────────────────────────

        /// <summary>
        /// 조작한 플레이어를 소속 퍼즐에 참여자로 등록한다(컨트롤러가 없으면 무시).
        /// CombatEntity가 아직 없는 프로토타입 플레이어는 PlayerId가 -1이므로 인스턴스 ID로 대체한다.
        /// </summary>
        protected void RegisterParticipant(PlayerController user)
        {
            if (owner == null) return;
            owner.AddParticipant(ResolveParticipantId(user));
        }

        /// <summary>
        /// 참여자 식별자. CombatEntity가 붙어 있으면 그 PlayerId를 쓴다.
        /// 없을 때의 폴백은 인스턴스 고유값이 필요한데, GetInstanceID는 Unity 6에서 폐기됐고
        /// GetEntityId는 버전에 따라 없을 수 있어 둘 다 쓰지 않는다.
        /// 대신 이름 해시로 구분한다 — 프로토타입에서 서로 다른 오브젝트를 구별하는 데 충분하고
        /// 엔진 API 변경에 영향받지 않는다.
        /// </summary>
        protected static int ResolveParticipantId(PlayerController user)
        {
            if (user == null) return -1;

            int id = user.PlayerId;
            if (id >= 0) return id;

            // NOTE: 이름이 같은 플레이어가 둘이면 같은 참여자로 취급된다.
            //   멀티플레이 도입 시 PlayerId가 항상 유효해지므로 이 경로는 사라진다.
            return Mathf.Abs(user.name.GetHashCode());
        }

        // ── 콜라이더 통과 처리 (운반/투척) ────────────────────────

        protected void CacheColliders()
        {
            if (_colliders != null) return;

            _colliders = GetComponentsInChildren<Collider2D>(true);
            _colliderTriggerDefaults = new bool[_colliders.Length];
            for (int i = 0; i < _colliders.Length; i++)
                _colliderTriggerDefaults[i] = _colliders[i] != null && _colliders[i].isTrigger;
        }

        /// <summary>
        /// 들려 있는 동안 플레이어를 밀지 않도록 콜라이더를 트리거로 바꾼다.
        /// 콜라이더를 끄면 PlayerInteraction의 탐색(OverlapCircle)에서 사라져 '내려놓기'가 불가능해지므로 끄지 않는다.
        /// </summary>
        protected void SetCollidersPassable(bool passable)
        {
            CacheColliders();
            if (_colliders == null) return;

            for (int i = 0; i < _colliders.Length; i++)
            {
                var col = _colliders[i];
                if (col == null) continue;
                col.isTrigger = passable || _colliderTriggerDefaults[i];
            }
        }
    }
}
