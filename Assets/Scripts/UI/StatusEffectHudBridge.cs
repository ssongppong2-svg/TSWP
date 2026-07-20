// 근거: UI 시스템.md — HUD에 상태이상 아이콘과 남은 시간을 표시한다.
// Core.GameEvents에는 상태이상 이벤트가 없고 GameEvents.cs는 수정 금지다. 상태이상은 전역 통지가 아니라
//   '엔티티 단위' 정보이므로, StatusEffects.StatusEffectController가 노출하는 로컬 이벤트를
//   이 브리지가 구독해 HudModel로 밀어 넣는다 (Jobs → HudModel.UpdateSkillCooldown과 동일한 푸시 패턴).
// 상태이상 판정·해제는 전부 StatusEffectController가 소유한다 — 여기서는 표시값만 복사한다(중복 구현 금지).
using UnityEngine;
using TSWP.StatusEffects;

namespace TSWP.UI
{
    /// <summary>
    /// 로컬 플레이어의 StatusEffectController → HudModel.StatusEffects 단방향 브리지.
    /// 플레이어 오브젝트(또는 그 하위)에 붙인다. HUD가 없어도 게임 로직은 영향을 받지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StatusEffectHudBridge : MonoBehaviour
    {
        [Tooltip("비우면 이 오브젝트(및 하위)에서 자동으로 찾는다.")]
        [SerializeField] private StatusEffectController source;

        [Tooltip("비우면 GameplayHud.Instance를 사용한다.")]
        [SerializeField] private GameplayHud hud;

        private bool _subscribed;

        private void Awake()
        {
            if (source == null) source = GetComponentInChildren<StatusEffectController>(true);
        }

        private void OnEnable()
        {
            if (source == null || _subscribed) return;
            _subscribed = true;

            source.EffectApplied += OnEffectApplied;
            source.EffectRefreshed += OnEffectApplied;   // 갱신도 같은 처리 (중첩 금지 규칙: 지속시간만 갱신)
            source.EffectRemoved += OnEffectRemoved;

            // 이미 걸려 있던 상태이상을 초기 반영 (HUD가 늦게 켜져도 표시가 비지 않도록).
            var effects = source.ActiveEffects;
            for (int i = 0; i < effects.Count; i++) OnEffectApplied(effects[i]);
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;

            source.EffectApplied -= OnEffectApplied;
            source.EffectRefreshed -= OnEffectApplied;
            source.EffectRemoved -= OnEffectRemoved;

            Model?.ClearStatusEffects();
        }

        private void Update()
        {
            // 남은 시간만 복사한다. 목록 구조 변경은 이벤트가 담당하므로 여기서는 값만 갱신하고
            // Changed를 발행하지 않는다 (매 프레임 이벤트 폭주 방지 — ARCHITECTURE.md §3-8).
            var model = Model;
            if (model == null || source == null) return;

            var effects = source.ActiveEffects;
            for (int i = 0; i < effects.Count; i++)
            {
                var instance = effects[i];
                if (instance?.Data == null) continue;
                model.UpdateStatusEffectRemaining(instance.Data.EffectType, instance.RemainingDuration);
            }
        }

        private void OnEffectApplied(StatusEffectInstance instance)
        {
            var model = Model;
            if (model == null || instance?.Data == null) return;

            StatusEffectData data = instance.Data;
            model.ApplyStatusEffect(
                data.EffectType,
                data.DisplayNameKo,
                data.Icon,
                instance.RemainingDuration,
                Mathf.Max(data.Duration, instance.RemainingDuration),
                data.IsCC);
        }

        private void OnEffectRemoved(StatusEffectType type) => Model?.RemoveStatusEffect(type);

        private HudModel Model
        {
            get
            {
                if (hud == null) hud = GameplayHud.Instance;
                return hud != null ? hud.Model : null;
            }
        }
    }
}
