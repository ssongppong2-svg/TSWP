// 근거: 상태이상 시스템.md — 공통 규칙(지속시간/중첩 금지/동시 적용/면역),
// CC 우선순위(기절>빙결>속박>둔화>기타), 빙결 피격 해제, 출혈 이동 피해,
// 공포(반대 방향 강제 이동)/혼란(좌우 입력 반전), 감전 전이, 시너지(감전+물 등).
// 플레이어·몬스터·보스가 공유하는 컴포넌트. 이동/공격/스킬/회복 시스템은
// 상태이상을 직접 알 필요 없이 CanMove/CanAttack/CanUseSkill/CanBeHealed 질의만 사용한다.
using System;
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;

namespace TSWP.StatusEffects
{
    public class StatusEffectController : MonoBehaviour
    {
        [Header("데이터")]
        [Tooltip("시너지 규칙 조회용 데이터베이스. 비워두면 시너지 미동작.")]
        [SerializeField] private StatusEffectDatabase database;

        [Tooltip("기본 면역 목록 — 보스/엘리트 데이터(SO)가 스폰 시 주입하거나 프리팹에 직접 지정.")]
        [SerializeField] private List<StatusEffectType> initialImmunities = new List<StatusEffectType>();

        // 현재 적용 중인 지속형 상태이상. 즉발형(Knockback/Launch)은 여기 넣지 않는다 —
        // Combat.KnockbackInfo(DamageSystem 넉백 단계)가 물리 이동·낙하 연계·짧은 행동불가를 처리한다.
        // SYNC: 호스트 권위, 추후 NGO NetworkVariable
        private readonly List<StatusEffectInstance> activeEffects = new List<StatusEffectInstance>();

        // 면역 목록 (문서: 보스/엘리트/아이템/패시브 4종 주체가 부여).
        // TODO: 아이템 해제 시 해당 아이템의 면역만 제거하려면 ImmunitySource별 참조 카운트 필요 — 스켈레톤은 단순 집합.
        // SYNC: 호스트 권위, 추후 NGO NetworkVariable
        private readonly HashSet<StatusEffectType> immunities = new HashSet<StatusEffectType>();

        // 틱 피해 전달 대상 (같은 오브젝트의 CombatEntity 등)
        private IDamageable damageable;

        // ── UI(HUD) 통지용 로컬 이벤트 ────────────────────────────
        // GameEvents에는 상태이상 전용 이벤트가 없고 수정이 금지되어 있으므로(ARCHITECTURE.md §3-5의
        // 전역 통지 대상이 아닌 엔티티 단위 정보), HUD는 대상 엔티티의 이 이벤트를 직접 구독한다.
        public event Action<StatusEffectInstance> EffectApplied;
        public event Action<StatusEffectInstance> EffectRefreshed;
        public event Action<StatusEffectType> EffectRemoved;

        /// <summary>HUD 아이콘/남은 시간 표시용 읽기 전용 목록.</summary>
        public IReadOnlyList<StatusEffectInstance> ActiveEffects => activeEffects;

        private void Awake()
        {
            damageable = GetComponent<IDamageable>();
            for (int i = 0; i < initialImmunities.Count; i++)
            {
                immunities.Add(initialImmunities[i]);
            }
        }

        private void Update()
        {
            // 지속시간 감소 → 틱 피해(화상/중독) → 만료 제거.
            float dt = Time.deltaTime;
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                StatusEffectInstance inst = activeEffects[i];
                int ticks = inst.AdvanceTime(dt);
                if (ticks > 0)
                {
                    ApplyInternalDamage(inst.Data.TickDamage * ticks);
                }

                if (inst.IsExpired)
                {
                    // 화상 등: 시간이 지나면 자동 해제.
                    activeEffects.RemoveAt(i);
                    EffectRemoved?.Invoke(inst.Data.EffectType);
                }
            }
        }

        // ── 적용 / 해제 ───────────────────────────────────────────

        /// <summary>
        /// 상태이상 적용 단일 진입점. 면역이면 false.
        /// 같은 종류 재적용 = 지속시간 갱신(중첩 금지), 다른 종류 = 동시 적용.
        /// 아군 대상 적용 가능 여부(canAffectAllies) 필터는 부여 측(DamageSystem)이 팀 비교 후 결정한다.
        /// // SYNC: 호스트 권위, 추후 NGO NetworkVariable (적용/해제는 이 진입점을 서버 권위 이벤트로 동기화)
        /// </summary>
        public bool ApplyEffect(StatusEffectData data, GameObject source)
        {
            if (data == null)
            {
                return false;
            }

            // 즉발형(넉백/공중 띄우기)은 지속형 리스트로 관리하지 않는다 — Combat.KnockbackInfo 경로.
            if (data.IsInstantPhysical)
            {
                Debug.LogWarning($"[StatusEffectController] {data.EffectType}는 즉발형 — Combat.KnockbackInfo로 처리하세요.", this);
                return false;
            }

            // 면역 체크 (보스/엘리트/아이템/패시브가 부여한 면역).
            if (immunities.Contains(data.EffectType))
            {
                return false;
            }

            StatusEffectInstance existing = FindInstance(data.EffectType);
            if (existing != null)
            {
                // 공통 규칙: 같은 상태이상은 스택 없이 지속시간만 갱신.
                existing.Refresh(data.Duration);
                EffectRefreshed?.Invoke(existing);
                return true;
            }

            var instance = new StatusEffectInstance(data, source);
            activeEffects.Add(instance);
            EffectApplied?.Invoke(instance);

            // 감전 전이: 적용 시점에 1회 전파 시도. NOTE(기획 확인 필요): 전이 시점(적용 시/주기적) 문서 미정.
            if (data.CanSpread)
            {
                TrySpread(data, source);
            }

            return true;
        }

        /// <summary>해제 수단 대응 (테스트 체크리스트: "해제 방법이 존재하는가") — 아이템/스킬 정화 등이 호출.</summary>
        public void RemoveEffect(StatusEffectType type)
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i].Data.EffectType == type)
                {
                    activeEffects.RemoveAt(i);
                    EffectRemoved?.Invoke(type);
                    return;
                }
            }
        }

        /// <summary>사망/부활 시 전체 해제. CombatEntity.Died 구독 측에서 호출한다.</summary>
        public void ClearAllEffects()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                StatusEffectType type = activeEffects[i].Data.EffectType;
                activeEffects.RemoveAt(i);
                EffectRemoved?.Invoke(type);
            }
        }

        public bool HasEffect(StatusEffectType type) => FindInstance(type) != null;

        // ── 면역 ─────────────────────────────────────────────────

        /// <summary>면역 부여 (주체: 보스/엘리트/아이템/패시브 — ImmunitySource는 로깅/추적용).</summary>
        public void AddImmunity(StatusEffectType type, ImmunitySource source)
        {
            immunities.Add(type);
            // TODO: source별 참조 카운트로 확장 (아이템 2개가 같은 면역을 줄 때 하나만 해제해도 유지되도록).
        }

        public void RemoveImmunity(StatusEffectType type)
        {
            immunities.Remove(type);
        }

        public bool IsImmune(StatusEffectType type) => immunities.Contains(type);

        // ── 행동 가능 질의 (이동/공격/스킬/회복 시스템은 이것만 참조) ──

        /// <summary>이동 가능 여부 — 속박/기절/빙결이 차단.</summary>
        public bool CanMove => !AnyBlocks(static d => d.BlocksMovement);

        /// <summary>공격 가능 여부 — 기절/빙결이 차단 (속박·침묵·혼란 중에는 공격 가능).</summary>
        public bool CanAttack => !AnyBlocks(static d => d.BlocksAttack);

        /// <summary>직업 스킬(Q) 가능 여부 — 침묵/기절/빙결이 차단.</summary>
        public bool CanUseSkill => !AnyBlocks(static d => d.BlocksSkill);

        /// <summary>회복 수혜 가능 여부 — 회복 불가가 차단. 회복 부여 측(닥터 등)이 확인.</summary>
        public bool CanBeHealed => !AnyBlocks(static d => d.BlocksHealing);

        /// <summary>
        /// 현재 적용 중인 CC 중 최상위 우선순위(기절>빙결>속박>둔화>기타) 타입.
        /// 애니메이션/HUD 대표 표시용. 없으면 null.
        /// NOTE: 행동 차단 질의(Can*)는 플래그 합집합으로 판정한다 — 상위 CC의 차단 범위가
        /// 하위 CC를 포함하므로(기절 ⊃ 빙결 ⊃ 속박) 결과는 우선순위 판정과 동일하다.
        /// </summary>
        public StatusEffectType? GetHighestPriorityCC()
        {
            StatusEffectType? best = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                StatusEffectData d = activeEffects[i].Data;
                if (d.IsCC && d.CcPriority > bestPriority)
                {
                    bestPriority = d.CcPriority;
                    best = d.EffectType;
                }
            }
            return best;
        }

        // ── 스탯 배율 질의 (전투/이동 시스템이 곱연산으로 반영) ────

        /// <summary>이동속도 배율 (감전/둔화). PlayerController·EnemyAI가 이동 속도에 곱한다.</summary>
        public float GetMoveSpeedMultiplier() => ProductOf(static d => d.MoveSpeedMultiplier);

        /// <summary>공격속도 배율 (감전).</summary>
        public float GetAttackSpeedMultiplier() => ProductOf(static d => d.AttackSpeedMultiplier);

        /// <summary>공격력 배율 (약화). 공격 측 피해 계산에서 곱한다.</summary>
        public float GetAttackPowerMultiplier() => ProductOf(static d => d.AttackPowerMultiplier);

        /// <summary>받는 피해 배율 (취약). DamageSystem이 최종 피해에 곱한다.</summary>
        public float GetDamageTakenMultiplier() => ProductOf(static d => d.DamageTakenMultiplier);

        // ── 입력 변조 (공포/혼란) ─────────────────────────────────

        /// <summary>
        /// 이동 입력 변조 지점 — PlayerController가 원 입력을 이 메서드에 통과시킨 뒤 사용한다.
        /// 공포: 입력 무시 수준의 강제 반전 이동(전체 부호 반전). 혼란: 좌우(x축) 입력만 반전 (공격 입력은 통과).
        /// 이동 차단 CC 중에는 0 벡터를 돌려준다 (호출 측 CanMove 이중 확인과 무관하게 안전).
        /// </summary>
        public Vector2 MoveInputModifier(Vector2 input)
        {
            if (!CanMove)
            {
                return Vector2.zero;
            }

            // 공포가 혼란보다 우선 — 둘 다 적용하면 x축이 이중 반전되어 원상복구되므로 공포만 적용.
            if (HasEffect(StatusEffectType.Fear))
            {
                return -input; // 반대 방향 강제 이동
            }

            if (HasEffect(StatusEffectType.Confusion))
            {
                return new Vector2(-input.x, input.y); // 좌우 입력만 반전
            }

            return input;
        }

        // ── 외부 훅 ───────────────────────────────────────────────

        /// <summary>
        /// 피격 훅 — DamageSystem이 피해 확정 후 호출. 빙결(breaksOnDamage) 즉시 해제.
        /// NOTE(기획 확인 필요): 문서는 "해제될 수 있다" — 확률 해제 여부 미정, 우선 확정 해제로 구현.
        /// </summary>
        public void OnDamageTaken()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i].Data.BreaksOnDamage)
                {
                    StatusEffectType type = activeEffects[i].Data.EffectType;
                    activeEffects.RemoveAt(i);
                    EffectRemoved?.Invoke(type);
                }
            }
        }

        /// <summary>
        /// 이동 훅 — 이동 시스템이 실제 이동 거리(월드 유닛)를 전달. 출혈: 움직일수록 추가 피해.
        /// 가만히 있으면(distance 0) 피해 없음.
        /// </summary>
        public void OnMoved(float distance)
        {
            float totalDamage = 0f;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                totalDamage += activeEffects[i].AccumulateMoveDamage(distance);
            }

            if (totalDamage > 0f)
            {
                ApplyInternalDamage(totalDamage);
            }
        }

        // ── 시너지 (데이터 주도 — StatusSynergyRule SO 매칭) ──────

        /// <summary>
        /// 환경 촉매 접촉 시 호출 — 물 타일/기름 장판/폭탄 폭발 측(Map·Puzzles·Combat)이
        /// 자신의 촉매 종류를 전달한다. 보유 상태이상과 매칭되는 규칙이 있으면 결과를 실행한다.
        /// 예: 감전+물→범위 감전, 화상+기름→폭발, 빙결+폭탄→얼음 파편.
        /// </summary>
        public bool TryTriggerSynergy(SynergyCatalyst catalyst)
        {
            if (database == null)
            {
                return false;
            }

            bool triggered = false;
            // 실행 중 리스트 변형(RemoveEffect) 가능성이 있어 역순 순회.
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                StatusSynergyRule rule = database.FindSynergy(activeEffects[i].Data.EffectType, catalyst);
                if (rule != null)
                {
                    ExecuteSynergy(rule);
                    triggered = true;
                }
            }
            return triggered;
        }

        /// <summary>
        /// 시너지 결과 실행 — 규칙 SO의 파라미터만으로 동작 (결과 종류별 하드코딩 금지).
        /// 범위 피해 → 범위 상태이상 부여 → 결과 프리팹 스폰 순서.
        /// </summary>
        private void ExecuteSynergy(StatusSynergyRule rule)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, rule.AreaRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                // 범위 피해 (화상+기름→폭발 등). isExplosive면 구조물 파괴 판정과 연계된다.
                if (rule.AreaDamage > 0f && hits[i].TryGetComponent(out IDamageable target))
                {
                    var info = new DamageInfo
                    {
                        BaseDamage = rule.AreaDamage,
                        IsExplosive = rule.IsExplosive,
                        // NOTE: 발생원 CombatEntity 귀속(통계/아군 판정)은 Combat 연동 시 채운다.
                        Source = null,
                    };
                    target.TakeDamage(in info);
                }

                // 범위 상태이상 부여 (감전+물→범위 감전 등).
                if (rule.AreaEffect != null && hits[i].TryGetComponent(out StatusEffectController other))
                {
                    other.ApplyEffect(rule.AreaEffect, gameObject);
                }
            }

            // 결과 프리팹 스폰 (빙결+폭탄→얼음 파편 등).
            if (rule.ResultPrefab != null)
            {
                Instantiate(rule.ResultPrefab, transform.position, Quaternion.identity);
            }

            // TODO(연출): 시너지 발동 이펙트/사운드 — 픽셀아트 VFX 연동.

            if (rule.ConsumesTriggerEffect)
            {
                RemoveEffect(rule.TriggerEffect);
            }
        }

        // ── 내부 구현 ─────────────────────────────────────────────

        private StatusEffectInstance FindInstance(StatusEffectType type)
        {
            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (activeEffects[i].Data.EffectType == type)
                {
                    return activeEffects[i];
                }
            }
            return null;
        }

        private bool AnyBlocks(Func<StatusEffectData, bool> predicate)
        {
            for (int i = 0; i < activeEffects.Count; i++)
            {
                if (predicate(activeEffects[i].Data))
                {
                    return true;
                }
            }
            return false;
        }

        private float ProductOf(Func<StatusEffectData, float> selector)
        {
            float product = 1f;
            for (int i = 0; i < activeEffects.Count; i++)
            {
                product *= selector(activeEffects[i].Data);
            }
            return product;
        }

        /// <summary>
        /// 틱/출혈 피해 전달. 같은 오브젝트의 IDamageable(CombatEntity)로 흘려보낸다.
        /// Source null = 환경형 취급 (아군 50% 배율 미적용).
        /// NOTE: 발생원 플레이어 귀속 통계(트롤 점수 등)는 instance.Source에서 CombatEntity를 찾아 채우는 것이 TODO.
        /// 빙결 중 틱 피해는 DamageSystem의 OnDamageTaken 호출을 거쳐 빙결을 해제할 수 있다 (문서 규칙과 일치).
        /// </summary>
        private void ApplyInternalDamage(float amount)
        {
            if (damageable == null || amount <= 0f)
            {
                return;
            }

            var info = new DamageInfo { BaseDamage = amount, Source = null };
            damageable.TakeDamage(in info);
        }

        /// <summary>
        /// 감전 전이 — 주변에서 전파 대상을 찾아 확률 적용.
        /// NOTE: 아군 전이 여부는 data.CanAffectAllies + TeamType 비교로 걸러야 한다 — Combat 연동 시 TODO.
        /// </summary>
        private void TrySpread(StatusEffectData data, GameObject source)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, data.SpreadRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].gameObject == gameObject)
                {
                    continue; // 자기 자신 제외
                }

                if (!hits[i].TryGetComponent(out StatusEffectController other))
                {
                    continue;
                }

                // TODO: TeamType 비교로 "다른 적" 필터링 (CombatEntity.Team — Combat 작성 후 연동).
                if (UnityEngine.Random.value <= data.SpreadChance)
                {
                    // 재전이 연쇄 방지: 이미 보유 중이면 Refresh만 되고 전이는 신규 적용 시에만 발동한다.
                    other.ApplyEffect(data, source);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 전이/시너지 반경 시각화는 데이터 에셋 선택 시 확인 — 여기서는 위치만 표시.
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.25f);
        }
#endif
    }
}
