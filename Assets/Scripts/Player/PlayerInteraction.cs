// 근거: 조작과 시스템.md — E키 상호작용 (아이템/레버/버튼/문/상점/NPC/퍼즐 장치/팀원 부활 8종).
// 근접 탐색(Physics2D.OverlapCircle) → 최근접 IInteractable 실행. 프롬프트 정보는 UI가 이 컴포넌트를 조회/구독한다
// (엔티티 단위 정보라 GameEvents 전역 허브 대상이 아님 — StatusEffectController의 로컬 이벤트 패턴과 동일).
using System;
using UnityEngine;
using TSWP.Combat;

namespace TSWP.Player
{
    /// <summary>
    /// E키 상호작용 처리. 매 프레임 최근접 대상을 갱신해 프롬프트를 노출하고, 입력 시 실행한다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private float interactRadius = 1.5f; // TODO(밸런스): 문서 미정 — 상호작용 탐색 반경
        [SerializeField] private LayerMask interactMask = ~0; // TODO(레벨): 상호작용 전용 레이어 확정 시 지정

        private PlayerController _controller;
        private CombatEntity _entity;
        private IInteractable _currentTarget;

        // 매 프레임 탐색이라 할당이 없어야 한다 (감사 #34: OverlapCircleAll의 프레임당 배열 할당).
        // Unity 6에서 제거된 OverlapCircleNonAlloc 대신 ContactFilter2D 오버로드 + 재사용 버퍼를 쓴다.
        private readonly Collider2D[] _hitBuffer = new Collider2D[16];
        private ContactFilter2D _filter;

        /// <summary>대상 변경 통지 — UI.InteractionPrompt("E: ...")가 구독한다. null = 대상 없음.</summary>
        public event Action<IInteractable> TargetChanged;

        /// <summary>현재 최근접 상호작용 대상 (없으면 null).</summary>
        public IInteractable CurrentTarget => _currentTarget;

        /// <summary>현재 프롬프트 문구 (없으면 null). UI 폴링용.</summary>
        public string CurrentPrompt => _currentTarget?.PromptDescription;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _entity = GetComponent<CombatEntity>();
        }

        private void Update()
        {
            RefreshTarget();

            IPlayerInput input = _controller.InputSource;
            if (input != null && input.InteractPressed)
                TryInteract();
        }

        /// <summary>E 입력 처리 진입점. 실행했으면 true.</summary>
        public bool TryInteract()
        {
            if (_currentTarget == null) return false;
            if (!_currentTarget.CanInteract(_controller)) return false;

            _currentTarget.Interact(_controller);
            // SYNC: 호스트 권위 — 선착순 대상(드롭 아이템 등)은 추후 '호스트 확정 → 결과 전파' 흐름으로 변경.
            return true;
        }

        /// <summary>최근접 대상 갱신. 사망 중에는 대상 없음 (죽은 채 상호작용 금지).</summary>
        private void RefreshTarget()
        {
            IInteractable nearest = (_entity == null || !_entity.IsDead) ? FindNearestInteractable() : null;

            if (!ReferenceEquals(nearest, _currentTarget))
            {
                _currentTarget = nearest;
                TargetChanged?.Invoke(_currentTarget);
            }
        }

        /// <summary>반경 내 상호작용 가능한 최근접 대상 탐색. 조건 불충족(CanInteract=false) 대상은 프롬프트에서 제외.</summary>
        private IInteractable FindNearestInteractable()
        {
            _filter.SetLayerMask(interactMask);
            _filter.useLayerMask = true;
            _filter.useTriggers = true; // 상호작용 대상은 대부분 트리거 콜라이더다

            int count = Physics2D.OverlapCircle(transform.position, interactRadius, _filter, _hitBuffer);

            IInteractable nearest = null;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var hit = _hitBuffer[i];
                if (hit == null) continue;
                if (hit.gameObject == gameObject) continue;          // 자기 자신 제외 (자기 부활 금지 등)
                if (hit.transform.IsChildOf(transform)) continue;    // 자기 자식 콜라이더도 제외

                // 콜라이더가 자식 오브젝트에 있는 구성 대응 — 부모 방향 탐색.
                IInteractable candidate = hit.GetComponentInParent<IInteractable>();
                if (candidate == null) continue;
                if (!candidate.CanInteract(_controller)) continue;

                float sqr = ((Vector2)hit.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = candidate;
                }
            }
            return nearest;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
#endif
    }
}
