// 근거: 퍼즐 시스템.md — 발판 퍼즐: 발판을 유지해야 문이 열린다.
//       트롤: 발판에서 내려오면 문이 닫히고 모두 갇힌다 → 협동 강제 + 웃음 포인트.
// 프로토타입: 컨트롤러 없이 발판 하나만 놓아도 '밟으면 색이 바뀐다'가 성립해야 한다.
//   플레이어 판정은 CombatEntity가 아직 없는 프로토타입 캐릭터도 통과하도록 PlayerController를 우선 확인한다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 압력 발판. 플레이어(또는 상자/운반물/폭탄)가 올라가 있는 동안만 활성이며, 이탈 시 트롤 결과를 유발한다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PressurePlate : PuzzleElement
    {
        [Header("발판")]
        [Tooltip("활성에 필요한 점유 수. 2 이상이면 여러 명이 함께 올라가야 한다.")]
        [SerializeField, Min(1)] private int requiredOccupants = 1;

        [Tooltip("상자 등 오브젝트로도 누를 수 있는가 (플레이어가 자리를 비우기 위한 우회 해법).")]
        [SerializeField] private bool acceptsObjects = true;

        [Tooltip("한 번 활성화되면 유지되는가. false면 이탈 시 즉시 비활성(문이 닫힘).")]
        [SerializeField] private bool latching;

        [Header("프로토타입 편의")]
        [Tooltip("콜라이더를 자동으로 트리거로 만든다. 끄면 트리거가 아닌 콜라이더에서는 감지가 되지 않는다.")]
        [SerializeField] private bool forceTriggerCollider = true;

        private readonly HashSet<Collider2D> _occupants = new HashSet<Collider2D>();
        private readonly List<Collider2D> _pruneBuffer = new List<Collider2D>();

        public bool IsActive { get; private set; }
        public int OccupantCount => _occupants.Count;

        public override string DebugStatus =>
            $"{(IsActive ? "ON" : "OFF")} — 점유 {_occupants.Count}/{requiredOccupants}{(latching ? " (유지형)" : "")}";

        protected override void Awake()
        {
            // 발판이 눌리는 표현 — 인스펙터에서 지정했다면 그대로 둔다.
            visual.SuggestMotion(new Vector3(0f, -0.08f, 0f), 0f);
            base.Awake();

            if (!forceTriggerCollider) return;

            // 트리거가 아니면 OnTriggerEnter2D가 오지 않아 '밟아도 아무 일이 없다'가 된다.
            var colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].isTrigger = true;
        }

        protected override void Update()
        {
            base.Update(); // 시각 보간

            // 점유자가 파괴/비활성화되면 Exit이 오지 않는다 — 주기적으로 정리한다.
            if (_occupants.Count == 0) return;

            _pruneBuffer.Clear();
            foreach (var col in _occupants)
                if (col == null || !col.gameObject.activeInHierarchy) _pruneBuffer.Add(col);

            if (_pruneBuffer.Count == 0) return;

            for (int i = 0; i < _pruneBuffer.Count; i++) _occupants.Remove(_pruneBuffer[i]);
            Evaluate();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsValidOccupant(other)) return;
            if (!_occupants.Add(other)) return;

            RegisterParticipant(other.GetComponentInParent<PlayerController>());
            Log($"'{other.name}' 올라옴 ({_occupants.Count}/{requiredOccupants})");

            Evaluate();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!_occupants.Remove(other)) return;

            Log($"'{(other != null ? other.name : "?")}' 내려감 ({_occupants.Count}/{requiredOccupants})");

            // 발판에서 내려옴 — 유지형이 아니면 문이 닫히고 모두 갇힌다.
            if (!latching && IsActive && _occupants.Count < requiredOccupants)
                NotifyWrongAction();

            Evaluate();
        }

        /// <summary>
        /// 발판을 누를 수 있는 대상인가.
        /// CombatEntity가 없는 프로토타입 플레이어도 인정하고, 상자/운반물/폭탄도 우회 해법으로 인정한다.
        /// </summary>
        private bool IsValidOccupant(Collider2D other)
        {
            if (other == null) return false;
            if (other.transform == transform || other.transform.IsChildOf(transform)) return false;

            if (other.GetComponentInParent<PlayerController>() != null) return true;

            var entity = other.GetComponentInParent<CombatEntity>();
            if (entity != null && entity.Team == TeamType.Players) return true;

            if (!acceptsObjects) return false;

            // 상자/운반물/폭탄 등 퍼즐 오브젝트 (자기 자신은 위에서 제외했다)
            return other.GetComponentInParent<PuzzleElement>() != null;
        }

        private void Evaluate()
        {
            bool active = _occupants.Count >= requiredOccupants;

            if (latching && IsActive) active = true; // 유지형은 한 번 켜지면 꺼지지 않는다

            if (IsActive == active) return;
            IsActive = active;

            visual.SetActive(active);
            Log(active ? "활성 — 문이 열린다" : "비활성 — 문이 닫힌다");

            NotifyStateChanged();
        }

        public void ResetElement()
        {
            _occupants.Clear();
            IsActive = false;
            visual.ResetVisual();
        }
    }
}
