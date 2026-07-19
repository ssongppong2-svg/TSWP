// 근거: 퍼즐 시스템.md — 발판 퍼즐: 발판을 유지해야 문이 열린다.
//       트롤: 발판에서 내려오면 문이 닫히고 모두 갇힌다 → 협동 강제 + 웃음 포인트.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 압력 발판. 플레이어(또는 상자)가 올라가 있는 동안만 활성이며, 이탈 시 트롤 결과를 유발한다.
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

        private readonly HashSet<Collider2D> _occupants = new HashSet<Collider2D>();

        public bool IsActive { get; private set; }
        public int OccupantCount => _occupants.Count;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsValidOccupant(other)) return;

            _occupants.Add(other);

            var entity = other.GetComponent<CombatEntity>();
            if (entity != null && entity.Team == TeamType.Players)
                owner?.AddParticipant(entity.OwnerPlayerId);

            Evaluate();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!_occupants.Remove(other)) return;

            // 발판에서 내려옴 — 유지형이 아니면 문이 닫히고 모두 갇힌다.
            if (!latching && IsActive && _occupants.Count < requiredOccupants)
                NotifyWrongAction();

            Evaluate();
        }

        private bool IsValidOccupant(Collider2D other)
        {
            if (other == null) return false;

            var entity = other.GetComponent<CombatEntity>();
            if (entity != null && entity.Team == TeamType.Players) return true;

            return acceptsObjects && other.GetComponent<PushableBox>() != null;
        }

        private void Evaluate()
        {
            bool active = _occupants.Count >= requiredOccupants;

            if (latching && IsActive) active = true; // 유지형은 한 번 켜지면 꺼지지 않는다

            if (IsActive == active) return;
            IsActive = active;
            NotifyStateChanged();
        }

        public void ResetElement()
        {
            _occupants.Clear();
            IsActive = false;
        }
    }
}
