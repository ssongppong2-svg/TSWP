// 근거: 게임 성경.md — "재미가 현실성보다 우선한다", 30초마다 반응이 나와야 한다.
// 근거: 전투 시스템.md — "공격은 명확해야 한다", "전투가 빠르고 시원한가"(테스트 체크리스트).
// 타격감 3요소: 히트스톱(순간 정지) + 화면 흔들림 + 피격 플래시.
// 피해 파이프라인(DamageSystem)이 이 서비스를 호출하므로, 개별 공격 코드가 연출을 신경 쓰지 않아도 된다.
using System.Collections;
using UnityEngine;

namespace TSWP.Combat
{
    /// <summary>
    /// 전역 타격 연출 서비스. 씬에 1개 배치한다(없어도 게임은 정상 동작 — 연출만 생략).
    /// </summary>
    public class HitFeedback : MonoBehaviour
    {
        public static HitFeedback Instance { get; private set; }

        [Header("히트스톱")]
        [Tooltip("타격 순간 시간을 멈춰 무게감을 준다. 0이면 사용 안 함.")]
        [SerializeField, Min(0f)] private float hitStopDuration = 0.07f; // TODO(밸런스): 문서 미정

        [Tooltip("히트스톱 중 시간 배율. 0이면 완전 정지.")]
        [Range(0f, 1f)][SerializeField] private float hitStopTimeScale = 0f;

        [Tooltip("치명타 시 히트스톱 배수 — 더 강하게 멈춘다.")]
        [SerializeField, Min(1f)] private float criticalHitStopMultiplier = 2f;

        [Header("화면 흔들림")]
        [SerializeField, Min(0f)] private float shakeDuration = 0.18f;   // TODO(밸런스): 문서 미정
        [SerializeField, Min(0f)] private float shakeMagnitude = 0.22f;

        [Tooltip("피해량이 이 값일 때 흔들림이 최대가 된다 (피해에 비례해 강해진다).")]
        [SerializeField, Min(1f)] private float shakeReferenceDamage = 25f;

        [Header("피격 플래시")]
        [Tooltip("피격 시 스프라이트가 이 색으로 번쩍인다.")]
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField, Min(0f)] private float flashDuration = 0.11f;

        [Header("타격 이펙트")]
        [Tooltip("일반 타격에 재생할 이펙트 id (Art.VfxId).")]
        [SerializeField] private string hitVfxId = Art.VfxId.HitNeutral;

        [Tooltip("치명타에 재생할 이펙트 id.")]
        [SerializeField] private string criticalVfxId = Art.VfxId.HitCritical;

        [Tooltip("히트스톱 사이 최소 간격(초). 연타나 다중 타격이 화면을 계속 얼리지 않게 한다.")]
        [SerializeField, Min(0f)] private float hitStopCooldown = 0.12f;

        private Coroutine _shakeRoutine;
        private Transform _cameraTransform;
        private Vector3 _cameraBasePosition;

        // 히트스톱은 코루틴을 갈아끼우지 않고 '해제 예정 시각'으로 관리한다.
        // 코루틴을 중간에 죽이면 timeScale 복구가 누락될 수 있고, 여러 대상이 동시에 맞을 때
        // 정지가 연쇄되어 FixedUpdate가 계속 멈춘다(= 조작이 씹히는 원인).
        private float _hitStopUntil;
        private float _hitStopReadyAt;
        private bool _hitStopActive;
        private float _savedTimeScale = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// 피격 연출 실행. DamageSystem이 피해 적용 직후 호출한다.
        /// </summary>
        /// <param name="target">피격 대상</param>
        /// <param name="finalDamage">실제 적용된 피해량</param>
        /// <param name="isCritical">치명타 여부</param>
        public void PlayHit(CombatEntity target, float finalDamage, bool isCritical)
        {
            PlayHitStop(isCritical);
            PlayShake(finalDamage);
            PlayFlash(target);
            PlayVfx(target, isCritical);
        }

        /// <summary>타격 지점에 이펙트를 재생한다. VfxSpawner가 없으면 조용히 생략된다.</summary>
        private void PlayVfx(CombatEntity target, bool isCritical)
        {
            if (target == null) return;

            var spawner = Art.VfxSpawner.Instance;
            if (spawner == null) return;

            string id = isCritical ? criticalVfxId : hitVfxId;
            if (string.IsNullOrEmpty(id)) return;

            spawner.Play(id, target.transform.position);
        }

        // ── 히트스톱 ──────────────────────────────────────────────

        private void PlayHitStop(bool isCritical)
        {
            if (hitStopDuration <= 0f) return;

            float now = Time.unscaledTime;

            // 직전 정지가 끝난 지 얼마 안 됐으면 건너뛴다 — 연속 정지로 조작이 먹통이 되는 것을 막는다.
            if (!_hitStopActive && now < _hitStopReadyAt) return;

            float duration = hitStopDuration * (isCritical ? criticalHitStopMultiplier : 1f);
            float end = now + duration;

            // 이미 정지 중이면 길이를 '연장'만 한다 (겹쳐서 누적되지 않게).
            if (_hitStopActive)
            {
                if (end > _hitStopUntil) _hitStopUntil = end;
                return;
            }

            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            _hitStopUntil = end;
            _hitStopActive = true;
            Time.timeScale = hitStopTimeScale;
        }

        /// <summary>
        /// 히트스톱 해제를 매 프레임 확인한다. 코루틴 대신 Update에서 처리해
        /// 어떤 경로로도 timeScale이 0에 묶이지 않게 한다.
        /// </summary>
        private void Update()
        {
            if (!_hitStopActive) return;
            if (Time.unscaledTime < _hitStopUntil) return;

            _hitStopActive = false;
            Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
            _hitStopReadyAt = Time.unscaledTime + hitStopCooldown;
        }

        private void OnDisable()
        {
            // 씬 전환·비활성화 시 시간이 멈춘 채로 남지 않도록 반드시 복구한다.
            if (!_hitStopActive) return;

            _hitStopActive = false;
            Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
        }

        // ── 화면 흔들림 ───────────────────────────────────────────

        private void PlayShake(float damage)
        {
            if (shakeDuration <= 0f || shakeMagnitude <= 0f) return;

            // 접근성: '화면 흔들림 감소' 설정이 켜져 있으면 생략해야 한다.
            // TODO(접근성): UI.SettingsManager.Accessibility.reduceScreenShake 연동.

            var cam = Camera.main;
            if (cam == null) return;

            float strength = Mathf.Clamp01(damage / shakeReferenceDamage);
            if (strength <= 0.01f) return;

            if (_shakeRoutine != null)
            {
                StopCoroutine(_shakeRoutine);
                if (_cameraTransform != null) _cameraTransform.localPosition = _cameraBasePosition;
            }

            _cameraTransform = cam.transform;
            _shakeRoutine = StartCoroutine(ShakeRoutine(strength));
        }

        private IEnumerator ShakeRoutine(float strength)
        {
            // 카메라가 추적 중일 수 있으므로 매 프레임의 위치를 기준으로 오프셋만 더한다.
            float elapsed = 0f;
            float magnitude = shakeMagnitude * strength;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.unscaledDeltaTime; // 히트스톱 중에도 흔들려야 한다

                float damping = 1f - (elapsed / shakeDuration);
                _cameraBasePosition = _cameraTransform.localPosition;

                Vector2 offset = Random.insideUnitCircle * (magnitude * damping);
                _cameraTransform.localPosition = _cameraBasePosition + (Vector3)offset;

                yield return null;

                // 다음 프레임 기준 위치를 되돌려 추적 카메라와 충돌하지 않게 한다.
                _cameraTransform.localPosition = _cameraBasePosition;
            }

            _shakeRoutine = null;
        }

        // ── 피격 플래시 ───────────────────────────────────────────

        private void PlayFlash(CombatEntity target)
        {
            if (target == null || flashDuration <= 0f) return;

            var renderer = target.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null) return;

            StartCoroutine(FlashRoutine(renderer));
        }

        private IEnumerator FlashRoutine(SpriteRenderer renderer)
        {
            Color original = renderer.color;
            renderer.color = flashColor;

            yield return new WaitForSecondsRealtime(flashDuration);

            // 그 사이 파괴됐을 수 있다
            if (renderer != null) renderer.color = original;
        }
    }
}
