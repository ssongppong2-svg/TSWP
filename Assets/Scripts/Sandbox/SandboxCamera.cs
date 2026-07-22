// 테스트 전용 — 플레이어를 부드럽게 따라가는 임시 카메라.
// 실제 카메라 연출(Pixel Perfect, 화면 흔들림 등)은 추후 별도 구현한다.
// 근거: 숨겨진 보정 설계 — 카메라 예측(룩어헤드). 이동 방향을 약간 앞서 보여주되
//       선행 오프셋 자체를 별도로 스무딩해 플레이어가 보정을 눈치채지 못하게 한다.
using UnityEngine;

namespace TSWP.Sandbox
{
    /// <summary>플레이어 추적 카메라. 이동 방향을 약간 앞서 보여주는 룩어헤드 예측 포함.</summary>
    public class SandboxCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;

        [Header("룩어헤드 예측 (숨겨진 보정)")]
        [Tooltip("수평 최대 선행 거리(유닛). 이동 방향으로 카메라가 앞서 보여주는 한계.")]
        [SerializeField, Min(0f)] private float lookAheadDistance = 1.3f;

        [Tooltip("선행 오프셋 자체의 스무딩 시간(초). 급반전 시 카메라가 홱 돌지 않게 한다.")]
        [SerializeField, Min(0.01f)] private float lookAheadSmoothTime = 0.45f;

        [Tooltip("수직 선행 비율(0~1). 점프·낙하의 상하 출렁임을 줄이기 위해 수직은 수평보다 약하게 선행한다.")]
        [SerializeField, Range(0f, 1f)] private float verticalLookAheadRatio = 0.35f;

        [Tooltip("이 속도(유닛/초) 이하의 미세 이동은 선행하지 않는다. 제자리 미세 조작 시 카메라 떨림 방지.")]
        [SerializeField, Min(0f)] private float lookAheadDeadZone = 0.5f;

        [Tooltip("속도 1유닛/초당 선행 거리(초 단위 게인). 데드존 초과 속도에 곱해 선행 거리를 만든다.")]
        [SerializeField, Min(0f)] private float lookAheadVelocityGain = 0.18f;

        [Tooltip("타깃이 한 프레임에 이 거리(유닛) 이상 이동하면 순간이동(방 이동/부활 텔레포트)으로 보고 선행을 리셋한다.")]
        [SerializeField, Min(1f)] private float teleportResetDistance = 8f;

        private Vector3 _velocity;             // 본체 추적 SmoothDamp 속도 버퍼
        private Rigidbody2D _targetBody;       // 속도 추정용 캐시 — SetTarget 시 갱신 (매 프레임 GetComponent 금지)
        private Vector3 _prevTargetPosition;   // 위치 델타 폴백·순간이동 감지용 이전 프레임 위치
        private bool _hasPrevPosition;         // 타깃 교체·첫 프레임 직후 델타 오염 방지
        private Vector2 _lookAhead;            // 스무딩된 현재 선행 오프셋
        private Vector2 _lookAheadVelocity;    // 선행 오프셋 SmoothDamp 속도 버퍼
        private Vector3 _trackPosition;        // 셰이크 제외 추적 위치 — SmoothDamp의 current에 셰이크가 되먹임되지 않게 별도 보관
        private bool _hasTrackPosition;        // 첫 프레임·인트로 종료 직후 현재 위치로 재기준
        private bool _introWasPlaying;         // 인트로 종료 전이 감지 — 복귀 시 추적 상태 리셋용

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            // 속도 추정용 Rigidbody2D 캐싱. 없으면 위치 델타 폴백 경로를 쓴다.
            _targetBody = newTarget != null ? newTarget.GetComponent<Rigidbody2D>() : null;
            _hasPrevPosition = false;      // 교체 직후 이전 타깃과의 델타로 속도가 폭주하는 것 방지
            ResetLookAhead();
        }

        private void LateUpdate()
        {
            if (target == null) return;    // 타깃 부재 시 조용히 대기

            // 맵 인트로(시네마틱) 중에는 카메라 위치를 만지지 않는다 — 인트로 팬이 위치를 소유한다.
            // (이 가드가 없으면 코루틴 팬(Update 후) → 추적 덮어쓰기(LateUpdate)가 매 프레임 반복돼
            //  팬이 플레이어 쪽으로 끌리며 흔들리고, 누적된 _velocity로 종료 순간 카메라가 러치한다.
            //  PlayerController가 IsPlaying으로 입력을 잠그는 것과 같은 가드다.)
            if (TSWP.Map.MapIntroManager.Instance != null && TSWP.Map.MapIntroManager.Instance.IsPlaying)
            {
                _introWasPlaying = true;
                return;
            }

            if (_introWasPlaying)
            {
                // 인트로 종료 — 팬이 남긴 현재 위치에서 스냅 없이 부드럽게 복귀하도록 추적 상태를 전부 리셋한다.
                _introWasPlaying = false;
                _velocity = Vector3.zero;
                _hasPrevPosition = false;
                _hasTrackPosition = false;
                ResetLookAhead();
            }

            Vector3 targetPos = target.position;
            UpdateLookAhead(targetPos);

            Vector3 desired = targetPos + offset + (Vector3)_lookAhead;

            // 셰이크 제외 위치를 별도로 스무딩한다 — 셰이크 오프셋이 SmoothDamp의 current에 섞이면
            // 흔들림이 추적에 되먹임되어 감쇠가 어긋난다.
            if (!_hasTrackPosition)
            {
                _trackPosition = transform.position;
                _hasTrackPosition = true;
            }
            _trackPosition = Vector3.SmoothDamp(_trackPosition, desired, ref _velocity, smoothTime);

            // 화면 흔들림은 위치 소유자인 추적 카메라가 마지막에 더한다
            // (Art.CameraShake 계약 — 흔들림은 오프셋 제공자, 카메라를 직접 만지지 않는다).
            transform.position = _trackPosition + TSWP.Art.CameraShake.Offset;
        }

        // 선행 오프셋 갱신 — 속도 추정 → 데드존/클램프 → 별도 SmoothDamp.
        private void UpdateLookAhead(Vector3 targetPos)
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;          // 일시정지 프레임 — 0 나누기(NaN) 방지, 이번 프레임은 건너뛴다

            if (!_hasPrevPosition)
            {
                // 첫 프레임(또는 타깃 교체 직후): 델타 기준점만 확보하고 선행 없이 지나간다.
                _prevTargetPosition = targetPos;
                _hasPrevPosition = true;
                return;
            }

            Vector2 frameDelta = targetPos - _prevTargetPosition;
            _prevTargetPosition = targetPos;

            // 순간이동 대응: 방 이동/부활 텔레포트로 한 프레임에 크게 이동하면 델타 기반 속도가 폭주한다.
            // 텔레포트 직후에는 Rigidbody2D의 linearVelocity도 신뢰하지 않고 동일하게 선행을 리셋한다.
            if (frameDelta.sqrMagnitude >= teleportResetDistance * teleportResetDistance)
            {
                ResetLookAhead();
                return;
            }

            // 속도 추정: Rigidbody2D가 있으면 linearVelocity(Unity 6), 없으면 위치 델타/deltaTime 폴백.
            Vector2 estimatedVelocity = _targetBody != null ? _targetBody.linearVelocity : frameDelta / dt;

            // 데드존 이하의 미세 이동은 선행 목표 0 — 떨림 방지.
            // 데드존 초과분에만 게인을 곱해 경계에서 오프셋이 툭 튀지 않고 연속적으로 차오르게 한다.
            Vector2 lookTarget = Vector2.zero;
            float speed = estimatedVelocity.magnitude;
            if (speed > lookAheadDeadZone)
            {
                Vector2 raw = estimatedVelocity / speed * ((speed - lookAheadDeadZone) * lookAheadVelocityGain);
                float verticalMax = lookAheadDistance * verticalLookAheadRatio;
                lookTarget.x = Mathf.Clamp(raw.x, -lookAheadDistance, lookAheadDistance);
                lookTarget.y = Mathf.Clamp(raw.y * verticalLookAheadRatio, -verticalMax, verticalMax);
            }

            // 선행 오프셋 자체를 별도 SmoothDamp — 급반전 시 카메라가 홱 돌지 않는다.
            _lookAhead = Vector2.SmoothDamp(_lookAhead, lookTarget, ref _lookAheadVelocity, lookAheadSmoothTime);
        }

        // 선행 상태 초기화 — 순간이동·타깃 교체 시 호출.
        private void ResetLookAhead()
        {
            _lookAhead = Vector2.zero;
            _lookAheadVelocity = Vector2.zero;
        }
    }
}
