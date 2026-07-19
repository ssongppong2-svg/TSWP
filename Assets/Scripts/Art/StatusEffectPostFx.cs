// 근거: 상태이상 시스템.md — 혼란(좌우 입력 반전), 공포(강제 이동), 감전, 중독 등은 "명확한 효과"를 가져야 하고
//       플레이어가 자기 상태를 즉시 알아채야 한다 (UI 시스템.md — 2초 안에 이해).
// 색수차(Chromatic Aberration)는 화면 전체를 흔들어 "지금 내가 정상이 아니다"를 아이콘보다 강하게 전달한다.
// 로컬 플레이어에게만 적용한다 — 남의 상태이상으로 내 화면이 깨지면 안 된다.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TSWP.StatusEffects;

namespace TSWP.Art
{
    /// <summary>
    /// 상태이상에 따라 화면 후처리(색수차·비네트·채도)를 적용한다.
    /// 로컬 플레이어의 StatusEffectController를 구독하며, 없으면 아무것도 하지 않는다.
    /// </summary>
    [RequireComponent(typeof(Volume))]
    public class StatusEffectPostFx : MonoBehaviour
    {
        [System.Serializable]
        public class EffectVisual
        {
            public StatusEffectType effect = StatusEffectType.Confusion;

            [Tooltip("색수차 강도 0~1. 화면 가장자리 색이 어긋난다.")]
            [Range(0f, 1f)] public float chromaticAberration = 0.6f;

            [Tooltip("비네트 강도 0~1. 화면 가장자리가 어두워진다.")]
            [Range(0f, 1f)] public float vignette = 0.25f;

            [Tooltip("채도 변화 -100~100. 음수면 색이 빠진다.")]
            [Range(-100f, 100f)] public float saturation = 0f;

            [Tooltip("비네트 색상 — 중독은 초록, 화상은 주황처럼 상태별로 구분한다.")]
            public Color vignetteColor = Color.black;

            [Tooltip("맥동 속도(회/초). 0이면 고정 강도.")]
            [Min(0f)] public float pulseSpeed = 2f;
        }

        [Header("대상")]
        [Tooltip("로컬 플레이어의 StatusEffectController. 비우면 자동 탐색한다.")]
        [SerializeField] private StatusEffectController target;

        [Header("상태이상별 연출")]
        [Tooltip("문서의 상태이상 16종 중 화면 연출이 필요한 것만 등록한다.")]
        [SerializeField]
        private List<EffectVisual> visuals = new List<EffectVisual>
        {
            // 혼란 — 좌우가 반대로 되는 상태. 색수차를 강하게 걸어 즉시 인지시킨다.
            new EffectVisual { effect = StatusEffectType.Confusion, chromaticAberration = 0.8f, vignette = 0.2f, pulseSpeed = 3f },
            // 공포 — 강제로 반대 방향 이동. 어두운 비네트로 압박감을 준다.
            new EffectVisual { effect = StatusEffectType.Fear, chromaticAberration = 0.45f, vignette = 0.45f, saturation = -35f, pulseSpeed = 1.5f },
            // 감전 — 이동/공격 속도 저하. 빠른 맥동으로 지직거림을 표현한다.
            new EffectVisual { effect = StatusEffectType.Shock, chromaticAberration = 0.7f, vignette = 0.15f, pulseSpeed = 8f },
            // 중독 — 초록 비네트 + 채도 저하.
            new EffectVisual { effect = StatusEffectType.Poison, chromaticAberration = 0.25f, vignette = 0.35f, saturation = -25f, pulseSpeed = 1f },
            // 빙결 — 행동 불가. 채도를 낮추고 옅은 색수차.
            new EffectVisual { effect = StatusEffectType.Freeze, chromaticAberration = 0.3f, vignette = 0.3f, saturation = -50f, pulseSpeed = 0f },
        };

        [Header("전환")]
        [Tooltip("효과가 켜지고 꺼질 때의 보간 속도. 높을수록 즉각적이다.")]
        [SerializeField, Min(0.1f)] private float blendSpeed = 6f;

        // ── 피격 섬광 ──
        // 캐릭터 플래시만으로는 화면 중앙을 보고 있을 때 자기가 맞은 걸 놓친다.
        // 화면 가장자리를 붉게 번쩍여 시선이 어디 있든 피격을 알아채게 한다.
        // 비네트를 상태이상과 공유하므로 한 컴포넌트가 함께 소유한다(두 곳에서 쓰면 서로 덮어쓴다).
        [Header("피격 섬광")]
        [Tooltip("로컬 플레이어의 CombatEntity. 비우면 자동 탐색한다.")]
        [SerializeField] private TSWP.Combat.CombatEntity damageTarget;

        [Range(0f, 1f)][SerializeField] private float hitFlashIntensity = 0.55f;
        [SerializeField] private Color hitFlashColor = new Color(0.75f, 0.05f, 0.1f);
        [SerializeField, Min(0.1f)] private float hitFadeSpeed = 4f;

        [Tooltip("체력이 이 비율 이하면 옅은 붉은 비네트를 상시 유지한다 (위험 경고).")]
        [Range(0f, 1f)][SerializeField] private float lowHealthRatio = 0.3f;
        [Range(0f, 1f)][SerializeField] private float lowHealthIntensity = 0.2f;

        private Volume _volume;
        private ChromaticAberration _chromatic;
        private Vignette _vignette;
        private ColorAdjustments _colorAdjustments;

        private float _currentChromatic;
        private float _currentVignette;
        private float _currentSaturation;
        private float _hitFlash;

        private void Awake()
        {
            _volume = GetComponent<Volume>();

            // 이 볼륨만의 프로파일을 만들어 전역 설정을 오염시키지 않는다.
            if (_volume.profile == null)
                _volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();

            _chromatic = GetOrAdd<ChromaticAberration>();
            _vignette = GetOrAdd<Vignette>();
            _colorAdjustments = GetOrAdd<ColorAdjustments>();

            _chromatic.intensity.overrideState = true;
            _vignette.intensity.overrideState = true;
            _vignette.color.overrideState = true;
            _colorAdjustments.saturation.overrideState = true;
        }

        private void Start()
        {
            var player = FindFirstObjectByType<TSWP.Player.PlayerController>();

            // 로컬 플레이어 탐색 — 여러 명이면 첫 번째(로컬)만 대상으로 한다.
            if (target == null && player != null)
                target = player.GetComponent<StatusEffectController>();

            if (damageTarget == null && player != null)
                damageTarget = player.GetComponent<TSWP.Combat.CombatEntity>();

            SubscribeDamage();
        }

        private void OnDisable()
        {
            if (damageTarget != null) damageTarget.Damaged -= OnDamaged;
        }

        private void SubscribeDamage()
        {
            if (damageTarget == null) return;
            damageTarget.Damaged -= OnDamaged; // 중복 구독 방지
            damageTarget.Damaged += OnDamaged;
        }

        private void OnDamaged(TSWP.Combat.DamageInfo info) => _hitFlash = hitFlashIntensity;

        private T GetOrAdd<T>() where T : VolumeComponent
        {
            if (_volume.profile.TryGet(out T component)) return component;
            return _volume.profile.Add<T>(true);
        }

        private void Update()
        {
            // 현재 걸린 상태이상 중 가장 강한 연출을 고른다 (동시 적용 시 중첩하지 않고 최댓값 사용).
            float targetChromatic = 0f;
            float targetVignette = 0f;
            float targetSaturation = 0f;
            Color targetColor = Color.black;

            if (target != null)
            {
                for (int i = 0; i < visuals.Count; i++)
                {
                    var v = visuals[i];
                    if (!target.HasEffect(v.effect)) continue;

                    float pulse = v.pulseSpeed > 0f
                        ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * v.pulseSpeed * Mathf.PI * 2f)
                        : 1f;

                    float chromatic = v.chromaticAberration * pulse;
                    if (chromatic > targetChromatic) targetChromatic = chromatic;

                    if (v.vignette > targetVignette)
                    {
                        targetVignette = v.vignette * pulse;
                        targetColor = v.vignetteColor;
                    }

                    if (Mathf.Abs(v.saturation) > Mathf.Abs(targetSaturation))
                        targetSaturation = v.saturation;
                }
            }

            // 급격한 전환은 눈에 거슬리므로 보간한다.
            float t = Time.unscaledDeltaTime * blendSpeed;
            _currentChromatic = Mathf.Lerp(_currentChromatic, targetChromatic, t);
            _currentVignette = Mathf.Lerp(_currentVignette, targetVignette, t);
            _currentSaturation = Mathf.Lerp(_currentSaturation, targetSaturation, t);

            // ── 피격 섬광 / 저체력 경고 ──
            float healthFloor = 0f;
            if (damageTarget != null && damageTarget.MaxHp > 0f && !damageTarget.IsDead)
            {
                float ratio = damageTarget.CurrentHp / damageTarget.MaxHp;
                if (ratio <= lowHealthRatio) healthFloor = lowHealthIntensity;
            }

            _hitFlash = Mathf.Max(healthFloor,
                                  Mathf.Lerp(_hitFlash, healthFloor, Time.unscaledDeltaTime * hitFadeSpeed));

            // 비네트는 상태이상과 피격이 공유한다 — 더 강한 쪽을 쓰고, 색도 그쪽을 따른다.
            float finalVignette = _currentVignette;
            Color finalColor = targetColor;
            if (_hitFlash > finalVignette)
            {
                finalVignette = _hitFlash;
                finalColor = hitFlashColor;
            }

            _chromatic.intensity.value = _currentChromatic;
            _vignette.intensity.value = finalVignette;
            _vignette.color.value = finalColor;
            _colorAdjustments.saturation.value = _currentSaturation;
        }
    }
}
