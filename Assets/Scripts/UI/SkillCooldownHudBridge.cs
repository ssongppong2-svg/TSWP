// 근거: UI 시스템.md — 스킬 UI는 아이콘과 남은 쿨타임을 표시하고, 사용 불가 시 회색 처리한다.
// 쿨타임 진행도는 Core.CooldownTimer가 소유하며 전용 GameEvents가 없다(GameEvents 수정 금지).
//   따라서 HudModel.UpdateSkillCooldown(문서에 명시된 푸시 지점)으로 이 브리지가 값을 밀어 넣는다.
// 확장성: 스킬 슬롯 수를 코드에 적지 않는다 — 오브젝트에 붙은 Jobs.SkillCaster 개수가 곧 슬롯 수다.
//   직업이 늘어 스킬이 2개 이상이 되면 SkillCaster를 하나 더 붙이는 것만으로 슬롯이 늘어난다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Jobs;
using TSWP.StatusEffects;

namespace TSWP.UI
{
    /// <summary>
    /// 로컬 플레이어의 SkillCaster(들) → HudModel.SkillCooldowns 단방향 브리지.
    /// 플레이어 오브젝트(또는 그 하위)에 붙인다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillCooldownHudBridge : MonoBehaviour
    {
        [Tooltip("비우면 이 오브젝트(및 하위)의 SkillCaster를 모두 사용한다. 개수 제한 없음.")]
        [SerializeField] private List<SkillCaster> casters = new List<SkillCaster>();

        [Tooltip("침묵 등으로 사용이 막혔는지 판정한다. 비우면 자동 탐색, 없으면 항상 사용 가능으로 본다.")]
        [SerializeField] private StatusEffectController statusController;

        [Tooltip("비우면 GameplayHud.Instance를 사용한다.")]
        [SerializeField] private GameplayHud hud;

        [Tooltip("스킬 아이콘. skillId 순서가 아니라 casters 순서로 대응한다. 비워도 무방(아이콘 없이 표시).")]
        [SerializeField] private List<Sprite> skillIcons = new List<Sprite>();

        /// <summary>이 값보다 작은 변화는 HUD로 밀어 넣지 않는다 (매 프레임 Changed 발행 방지).</summary>
        [SerializeField, Min(0f)] private float pushThreshold = 0.05f; // TODO(밸런스): 문서 미정

        private readonly List<float> _lastPushed = new List<float>();

        private void Awake()
        {
            if (casters.Count == 0)
                GetComponentsInChildren<SkillCaster>(true, casters);

            if (statusController == null)
                statusController = GetComponentInChildren<StatusEffectController>(true);

            SyncPushCache();
        }

        /// <summary>런타임에 SkillCaster가 추가돼도 인덱스가 어긋나지 않게 맞춘다.</summary>
        private void SyncPushCache()
        {
            while (_lastPushed.Count < casters.Count) _lastPushed.Add(float.NaN);
        }

        private void Update()
        {
            var model = Model;
            if (model == null) return;

            SyncPushCache();

            bool canUseSkill = statusController == null || statusController.CanUseSkill;

            for (int i = 0; i < casters.Count; i++)
            {
                var caster = casters[i];
                if (caster == null) continue;

                var skill = caster.Skill;              // 직업 조립 전에는 null일 수 있다
                var cooldown = caster.Cooldown;
                if (skill == null || cooldown == null) continue;

                float remaining = cooldown.Remaining;

                // 값이 사실상 그대로면 밀어 넣지 않는다 — HudModel.Changed 폭주를 막는다.
                float last = _lastPushed[i];
                bool becameReady = remaining <= 0f && last > 0f;
                if (!becameReady && !float.IsNaN(last) && Mathf.Abs(last - remaining) < pushThreshold)
                    continue;

                _lastPushed[i] = remaining;
                model.UpdateSkillCooldown(skill.SkillId, remaining, cooldown.Duration, canUseSkill);

                // 아이콘은 목록에 있을 때만 채운다 (없어도 슬롯은 정상 표시된다).
                if (i < skillIcons.Count && skillIcons[i] != null)
                    AssignIcon(model, skill.SkillId, skillIcons[i]);
            }
        }

        private static void AssignIcon(HudModel model, string skillId, Sprite icon)
        {
            var list = model.SkillCooldowns;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].SkillId != skillId) continue;
                if (list[i].Icon != icon) list[i].Icon = icon;
                return;
            }
        }

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
