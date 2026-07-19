// 테스트 전용 — 플레이어를 부드럽게 따라가는 임시 카메라.
// 실제 카메라 연출(Pixel Perfect, 화면 흔들림 등)은 추후 별도 구현한다.
using UnityEngine;

namespace TSWP.Sandbox
{
    /// <summary>플레이어 추적 카메라.</summary>
    public class SandboxCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, -10f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.15f;

        private Vector3 _velocity;

        public void SetTarget(Transform newTarget) => target = newTarget;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }
    }
}
