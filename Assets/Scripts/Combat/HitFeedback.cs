// 근거: 게임 성경.md — "재미가 현실성보다 우선한다", 30초마다 반응이 나와야 한다.
// 근거: 전투 시스템.md — "공격은 명확해야 한다", "전투가 빠르고 시원한가"(테스트 체크리스트).
// 근거: UI 시스템.md — 접근성: 화면 흔들림 감소 / 플래시 효과 감소.
// 근거: 성능 감사 보고 §1·§6 — 히트스톱 무한 연장(전역 정지) 제거, 플래시/셰이크 탈코루틴화.
// 타격감 3요소: 히트스톱(순간 정지) + 화면 흔들림 + 피격 플래시.
// 피해 파이프라인(DamageSystem)이 이 서비스를 호출하므로, 개별 공격 코드가 연출을 신경 쓰지 않아도 된다.
using UnityEngine;
using UnityEngine.Rendering;

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
        [SerializeField, Min(0f)] private float hitStopDuration = 0.06f; // TODO(밸런스): 문서 미정

        [Tooltip("히트스톱 중 시간 배율. 0이면 완전 정지 — 8인 협동에서는 완전 정지가 위험해 0.05를 기본으로 둔다.")]
        [Range(0f, 1f)][SerializeField] private float hitStopTimeScale = 0.05f;

        [Tooltip("치명타 시 히트스톱 배수 — 더 강하게 멈춘다.")]
        [SerializeField, Min(1f)] private float criticalHitStopMultiplier = 2f;

        [Tooltip("히트스톱 사이 최소 간격(초). 활성 중에도 적용된다 — 연타로 화면이 계속 얼지 않게 한다.")]
        [SerializeField, Min(0f)] private float hitStopCooldown = 0.14f;

        [Tooltip("어떤 경로로도 이 시간을 넘겨 멈추지 않는다(안전 상한, 초).")]
        [SerializeField, Min(0.02f)] private float hitStopHardCap = 0.2f;

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

        // ── 히트스톱 상태 ─────────────────────────────────────────
        // 코루틴을 쓰지 않는다. 코루틴을 중간에 죽이면 timeScale 복구가 누락될 수 있다.
        private float _hitStopUntil;
        private float _hitStopReadyAt;
        private bool _hitStopActive;
        private float _savedTimeScale = 1f;

        // ── 화면 흔들림 상태 ──────────────────────────────────────
        // 카메라 추적 스크립트(LateUpdate)와 싸우지 않도록, 렌더 직전에 오프셋을 더하고
        // 렌더 직후에 되돌린다. 어떤 실행 순서에서도 잔여 오프셋이 남지 않는다.
        private Camera _camera;
        private float _shakeRemaining;
        private float _shakeTotal;
        private float _shakeStrength;
        private Vector3 _shakeOffset;
        private bool _shakeApplied;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

            // 씬 전환·비활성화 시 시간이 멈춘 채로 남지 않도록 반드시 복구한다.
            if (_hitStopActive)
            {
                _hitStopActive = false;
                Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
            }
            RevertShakeOffset();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// 피격 연출 실행. DamageSystem이 피해 적용 직후 호출한다.
        /// </summary>
        /// <param name="target">피격 대상</param>
        /// <param name="finalDamage">실제 적용된 피해량</param>
        /// <param name="isCritical">치명타 여부</param>
        /// <param name="allowHitStop">
        /// 히트스톱 허용 여부. 도트(화상·중독)·환경 지속피해처럼 '타격'이 아닌 피해는 false로 넘겨
        /// 전역 정지가 연쇄되지 않게 한다 (성능 감사 §1).
        /// </param>
        public void PlayHit(CombatEntity target, float finalDamage, bool isCritical, bool allowHitStop = true)
        {
            if (allowHitStop) PlayHitStop(isCritical);
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

        // NOTE(기획 확인 필요): 히트스톱은 본질적으로 '전역' 상태다. 8인 협동에서 남이 맞을 때마다
        //   내 화면이 서는 설계가 맞는지 확인이 필요하다. 확정 전까지는 쿨다운 + 상한으로 억제만 한다.
        //   (대안: 로컬 플레이어가 때렸거나 맞았을 때만 발동 — OwnerPlayerId 판정 추가)
        private void PlayHitStop(bool isCritical)
        {
            if (hitStopDuration <= 0f) return;

            float now = Time.unscaledTime;

            // 쿨다운은 '활성 여부와 무관하게' 검사한다.
            // (활성일 때 예외를 두면 타격이 이어지는 동안 정지가 끝없이 연장돼 게임이 실제로 멈춘다.)
            if (now < _hitStopReadyAt) return;

            // 이미 정지 중이면 길이를 연장하지 않는다 — 히트스톱 길이는 시작 시점에 고정.
            if (_hitStopActive) return;

            float duration = hitStopDuration * (isCritical ? criticalHitStopMultiplier : 1f);
            duration = Mathf.Min(duration, hitStopHardCap); // 안전 상한

            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            _hitStopUntil = now + duration;
            _hitStopReadyAt = _hitStopUntil + hitStopCooldown; // 시작 시점에 다음 허용 시각을 확정한다
            _hitStopActive = true;
            Time.timeScale = hitStopTimeScale;
        }

        private void Update()
        {
            TickHitStop();
            TickShake();
        }

        /// <summary>히트스톱 해제를 매 프레임 확인한다 — 어떤 경로로도 timeScale이 묶이지 않게 한다.</summary>
        private void TickHitStop()
        {
            if (!_hitStopActive) return;
            if (Time.unscaledTime < _hitStopUntil) return;

            _hitStopActive = false;
            Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
        }

        // ── 화면 흔들림 ───────────────────────────────────────────

        private void PlayShake(float damage)
        {
            if (shakeDuration <= 0f || shakeMagnitude <= 0f) return;

            // 접근성: '화면 흔들림 감소'가 켜져 있으면 생략한다 (UI 시스템.md 접근성 5종).
            var settings = UI.SettingsManager.Instance;
            if (settings != null && settings.Accessibility != null && settings.Accessibility.reduceScreenShake) return;

            float strength = Mathf.Clamp01(damage / shakeReferenceDamage);
            if (strength <= 0.01f) return;

            // 이미 흔들리는 중이면 더 강한 쪽을 채택한다 (겹쳐서 폭주하지 않게).
            if (_shakeRemaining > 0f && strength <= _shakeStrength)
            {
                _shakeRemaining = Mathf.Max(_shakeRemaining, shakeDuration * 0.5f);
                return;
            }

            _shakeStrength = strength;
            _shakeTotal = shakeDuration;
            _shakeRemaining = shakeDuration;
        }

        private void TickShake()
        {
            if (_shakeRemaining <= 0f) return;

            _shakeRemaining -= Time.unscaledDeltaTime; // 히트스톱 중에도 흔들려야 한다
            if (_shakeRemaining <= 0f)
            {
                _shakeRemaining = 0f;
                _shakeStrength = 0f;
                _shakeOffset = Vector3.zero;
                return;
            }

            float damping = _shakeTotal > 0f ? _shakeRemaining / _shakeTotal : 0f;
            Vector2 offset = Random.insideUnitCircle * (shakeMagnitude * _shakeStrength * damping);
            _shakeOffset = new Vector3(offset.x, offset.y, 0f);
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (_shakeRemaining <= 0f || _shakeApplied) return;
            if (cam == null || cam != ResolveCamera()) return;

            cam.transform.position += _shakeOffset;
            _shakeApplied = true;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!_shakeApplied || cam == null || cam != _camera) return;
            cam.transform.position -= _shakeOffset;
            _shakeApplied = false;
        }

        private void RevertShakeOffset()
        {
            if (!_shakeApplied) return;
            if (_camera != null) _camera.transform.position -= _shakeOffset;
            _shakeApplied = false;
        }

        /// <summary>메인 카메라 캐시. 씬 전환으로 파괴되면 다시 찾는다.</summary>
        private Camera ResolveCamera()
        {
            if (_camera == null) _camera = Camera.main;
            return _camera;
        }

        // ── 피격 플래시 ───────────────────────────────────────────

        private void PlayFlash(CombatEntity target)
        {
            if (target == null || flashDuration <= 0f) return;

            // 접근성: '플래시 효과 감소'가 켜져 있으면 생략한다.
            var settings = UI.SettingsManager.Instance;
            if (settings != null && settings.Accessibility != null && settings.Accessibility.reduceFlashEffects) return;

            // HitFlash가 렌더러/원본색을 캐시하므로 타격마다 계층 순회를 하지 않는다.
            if (!target.TryGetComponent(out HitFlash flash))
                flash = target.gameObject.AddComponent<HitFlash>();

            flash.Flash(flashColor, flashDuration);
        }
    }
}
