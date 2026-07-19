// 근거: 조작과 시스템.md — E 상호작용 대상 '팀원 부활'.
// 근거: 게임 시작과 선택, 직업, 플레이.md — 사망 시 즉시 부활 + 공유 부활 횟수(인원×3) 소모, 결과 화면 '가장 많은 구조' 항목.
// NOTE(기획 확인 필요): '사망 즉시 부활' 규칙 vs E키 '팀원 부활'·'구조' 통계가 문서 간 미해소 (스펙 설계 주의점 / ARCHITECTURE.md §3-7).
//   - 즉시부활 채택 시: CombatEntity.autoReviveOnDeath = true → 이 컴포넌트는 사실상 발동하지 않는다 (CanInteract 상시 false).
//   - 구조부활 채택 시: autoReviveOnDeath = false → 이 상호작용이 유일한 부활 경로가 된다.
//   양쪽 모두 부활 소모는 Core.SharedReviveSystem.TryConsume 공용 경로를 탄다 (ARCHITECTURE.md §5 — 부활 판정 한 곳).
// SYNC: 호스트 권위 — 부활 소모/실행은 호스트 판정 후 전파, 추후 NGO 연동.
using UnityEngine;
using TSWP.Core;
using TSWP.Combat;

namespace TSWP.Player
{
    /// <summary>
    /// 플레이어 오브젝트에 부착하는 '팀원 부활' 상호작용 스텁.
    /// 다른 플레이어가 E키(PlayerInteraction)로 이 플레이어를 부활시킨다.
    /// </summary>
    public class TeammateReviveInteractable : MonoBehaviour, IInteractable
    {
        [Tooltip("부활 대상 전투 유닛. 비워두면 같은 오브젝트에서 자동 탐색.")]
        [SerializeField] private CombatEntity targetEntity;

        [Tooltip("구조부활 채택 시 홀드(채널링) 시간. 0이면 즉시 실행.")]
        [SerializeField] private float reviveHoldSeconds = 0f; // TODO(밸런스): 문서 미정 — 즉시 vs 채널링

        private void Awake()
        {
            if (targetEntity == null)
                targetEntity = GetComponent<CombatEntity>();
        }

        // ── IInteractable ─────────────────────────────────────────
        public string PromptDescription
        {
            get
            {
                SharedReviveSystem revive = RunManager.Instance != null ? RunManager.Instance.ReviveSystem : null;
                int remaining = revive != null ? revive.Remaining : 0;
                return $"팀원 부활 (남은 부활 {remaining}회)"; // UI.InteractionPrompt 표시용
            }
        }

        public bool CanInteract(PlayerController user)
        {
            if (targetEntity == null || !targetEntity.IsDead) return false;      // 살아있으면 대상 아님
            if (user == null || user.gameObject == gameObject) return false;      // 자기 자신 부활 금지

            // 공유 부활 소진 시 불가 — 잔여 확인만 하고 실제 소모는 Interact에서 TryConsume 단일 경로.
            SharedReviveSystem revive = RunManager.Instance != null ? RunManager.Instance.ReviveSystem : null;
            return revive != null && revive.Remaining > 0;
        }

        public void Interact(PlayerController user)
        {
            if (!CanInteract(user)) return;

            if (reviveHoldSeconds > 0f)
            {
                // TODO: 홀드 부활(채널링) — 게이지 진행/이동 시 취소/UI 게이지 연동. 뼈대 단계에서는 즉시 실행으로 대체.
            }

            // 부활 소모는 공용 경로 — 소진 판정·ReviveCountChanged·업적 카운터("revive.count") 통지 포함.
            SharedReviveSystem revive = RunManager.Instance.ReviveSystem;
            if (!revive.TryConsume()) return;

            targetEntity.Revive(); // 부활 실행 (부활 체력/짧은 무적은 CombatEntity.Revive 소관)

            // '가장 많은 구조' 통계 — 구조자(user) 귀속 (Core.PlayerMatchStats.Rescues).
            if (user.PlayerId >= 0)
            {
                RunManager.Instance.GetStats(user.PlayerId).Rescues++;
                GameEvents.RaiseStatCounter("rescue.count", 1); // 업적 카운터 (구조 관련 업적용)
            }
        }
    }
}
