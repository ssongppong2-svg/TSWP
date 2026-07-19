// 근거: 전투 시스템.md — 타격감 3요소 중 '화면 흔들림'. "전투가 빠르고 시원한가"(테스트 체크리스트).
// 근거: UI 시스템.md — 접근성 설정에 '화면 흔들림 감소'가 있어야 한다.
//
// 설계 의도 — 이 컴포넌트는 카메라를 '움직이지 않는다'. 오프셋만 계산해 노출한다.
// 흔들림이 카메라 transform을 직접 만지면 추적 카메라(매 프레임 위치를 덮어씀)와 서로를 지운다.
// 실제로 HitFeedback은 흔들기 전 위치를 저장했다가 되돌리는 방식이라,
// 저장 시점과 추적 갱신 시점이 어긋나면 카메라가 튀거나 원래 자리로 못 돌아온다.
// → 흔들림은 '오프셋 제공자'로 두고, 위치를 소유한 쪽(추적 카메라)이 마지막에 더한다.
//
//   Vector3 desired = target.position + offset;
//   transform.position = Vector3.SmoothDamp(...) + CameraShake.Offset;   // ← 추적 계산 '후'에 더한다
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>
    /// 화면 흔들림 오프셋 제공자. 씬에 1개 배치한다(없어도 게임은 정상 동작 — 연출만 생략).
    /// 카메라 위치를 소유한 컴포넌트가 <see cref="Offset"/>을 자기 계산 결과에 더해 쓴다.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("강도")]
        [Tooltip("강도 1.0일 때의 최대 흔들림 크기(유닛).")]
        [SerializeField, Min(0f)] private float maxMagnitude = 0.22f; // TODO(밸런스): 문서 미정

        [Tooltip("기본 지속 시간(초). Shake 호출에서 개별 지정할 수 있다.")]
        [SerializeField, Min(0f)] private float defaultDuration = 0.18f; // TODO(밸런스): 문서 미정

        [Tooltip("피해량이 이 값일 때 흔들림이 최대가 된다.")]
        [SerializeField, Min(1f)] private float referenceDamage = 25f; // TODO(밸런스): 문서 미정

        [Header("느낌")]
        [Tooltip("노이즈 주파수(회/초). 높을수록 잘게 떨린다. 픽셀아트에서는 20~30이 무난하다.")]
        [SerializeField, Min(1f)] private float frequency = 24f;

        [Tooltip("감쇠 곡선의 지수. 1이면 선형, 2면 처음에 세게 흔들리고 빠르게 잦아든다.")]
        [SerializeField, Min(0.1f)] private float damping = 2f;

        [Header("접근성")]
        [Tooltip("켜면 흔들림을 전혀 만들지 않는다(오프셋 항상 0).")]
        [SerializeField] private bool reduceScreenShake;
        // TODO(접근성): UI.AccessibilitySettings.reduceScreenShake와 연동해 런타임에 이 값을 갱신한다.

        private float _strength;      // 0~1
        private float _remaining;
        private float _duration;

        // Perlin 노이즈 시드 — 인스턴스마다 다른 패턴을 쓰면 x/y가 같은 방향으로 움직이지 않는다.
        private float _seedX;
        private float _seedY;

        /// <summary>현재 프레임의 흔들림 오프셋. 흔들리지 않는 동안은 Vector3.zero.</summary>
        public Vector3 CurrentOffset { get; private set; }

        /// <summary>
        /// 인스턴스가 없어도 안전하게 쓸 수 있는 정적 접근자.
        /// 추적 카메라는 <c>transform.position = 계산결과 + CameraShake.Offset;</c> 형태로 쓰면 된다.
        /// </summary>
        public static Vector3 Offset => Instance != null ? Instance.CurrentOffset : Vector3.zero;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _seedX = Random.value * 100f;
            _seedY = Random.value * 100f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 흔들림 시작. 이미 흔들리는 중이면 '더 센 쪽'을 채택한다 —
        /// 누적하면 다수의 적이 동시에 맞을 때 화면이 통제 불능이 된다.
        /// </summary>
        /// <param name="strength">0~1. 1이 최대 강도.</param>
        /// <param name="duration">지속 시간(초). 0 이하면 기본값 사용.</param>
        public void Shake(float strength, float duration = 0f)
        {
            if (reduceScreenShake) return;

            strength = Mathf.Clamp01(strength);
            if (strength <= 0.01f) return;

            float length = duration > 0f ? duration : defaultDuration;
            if (length <= 0f) return;

            // 남은 시간과 강도 모두 '더 강한 쪽'으로 갱신한다.
            if (strength >= _strength)
            {
                _strength = strength;
                _duration = length;
                _remaining = length;
            }
            else if (length > _remaining)
            {
                _remaining = length;
                _duration = Mathf.Max(_duration, length);
            }
        }

        /// <summary>피해량 기준 흔들림. 피해가 클수록 세게 흔들린다.</summary>
        public void ShakeFromDamage(float damage, float duration = 0f)
        {
            Shake(damage / Mathf.Max(1f, referenceDamage), duration);
        }

        /// <summary>흔들림 즉시 중단(컷신 전환 등).</summary>
        public void StopShake()
        {
            _remaining = 0f;
            _strength = 0f;
            CurrentOffset = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (_remaining <= 0f)
            {
                if (CurrentOffset != Vector3.zero) CurrentOffset = Vector3.zero;
                return;
            }

            // 히트스톱(timeScale 0) 중에도 흔들려야 타격감이 산다 → unscaled.
            _remaining -= Time.unscaledDeltaTime;

            if (_remaining <= 0f)
            {
                StopShake();
                return;
            }

            float t = _duration > 0f ? _remaining / _duration : 0f;
            float falloff = Mathf.Pow(Mathf.Clamp01(t), damping);
            float magnitude = maxMagnitude * _strength * falloff;

            // 프레임마다 난수를 쓰면 지직거리기만 한다. Perlin 노이즈는 '흔들리는' 느낌을 준다.
            float time = Time.unscaledTime * frequency;
            float x = Mathf.PerlinNoise(_seedX, time) * 2f - 1f;
            float y = Mathf.PerlinNoise(_seedY, time) * 2f - 1f;

            CurrentOffset = new Vector3(x * magnitude, y * magnitude, 0f);
        }

        private void OnDisable()
        {
            // 비활성화된 채 오프셋이 남으면 카메라가 어긋난 자리에 굳는다.
            StopShake();
        }
    }
}
