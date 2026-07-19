// 근거: 직업 시스템.md — 기본 공격은 제한 없이 사용할 수 있다 (자원/횟수 제한 없음, 공격 속도 간격만 존재).
// 근거: 상태이상 시스템.md — 기절/빙결 중 공격 불가 (StatusEffectController.CanAttack 질의만 사용),
//       약화(공격력 배율)·감전(공격 속도 배율)은 공격 측에서 곱한다.
// 근거: 전투 시스템.md — 피해 = 기본 공격력 + 아이템 + 스킬 + 기타 (DamageInfo 합산), 아군 판정은 DamageSystem 소관.
using UnityEngine;
using TSWP.Combat;
using TSWP.StatusEffects;

namespace TSWP.Jobs
{
    /// <summary>
    /// 직업별 기본 공격 실행 컴포넌트. 입력 계층(PlayerController)이 공격 입력 시 TryAttack을 호출한다.
    /// 팀 필터를 하드코딩하지 않는다 — 아군사격이 상시 존재하며 50% 규칙은 DamageSystem이 적용한다.
    /// </summary>
    public class BasicAttacker : MonoBehaviour
    {
        [Tooltip("직업 조립 시 JobSelectionManager.ApplyJobTo가 SetProfile로 주입한다. 테스트용 직접 지정 가능.")]
        [SerializeField] private BasicAttackProfile profile = new BasicAttackProfile();

        // 공격 간격 잔여 시간 (쿨타임이 아닌 공격 속도 파생값 — 기본 공격은 '제한 없음').
        private float _attackIntervalRemaining;
        private CombatEntity _entity;
        private StatusEffectController _statusController;

        public BasicAttackProfile Profile => profile;

        /// <summary>공격 가능 여부 (간격/상태이상/사망 종합) — UI 표시용.</summary>
        public bool CanAttackNow =>
            profile != null
            && _attackIntervalRemaining <= 0f
            && (_entity == null || !_entity.IsDead)
            && (_statusController == null || _statusController.CanAttack);

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _statusController = GetComponent<StatusEffectController>();
        }

        private void Update()
        {
            if (_attackIntervalRemaining > 0f)
            {
                _attackIntervalRemaining -= Time.deltaTime;
            }
        }

        /// <summary>직업 조립 시 프로파일 주입.</summary>
        /// <summary>
        /// 공격 이펙트를 조준 방향 앞쪽에 재생한다.
        /// VfxSpawner가 씬에 없으면 조용히 생략된다 (연출 부재로 공격이 실패하지 않는다).
        /// </summary>
        private void PlayAttackVfx(Vector2 direction)
        {
            if (profile == null || string.IsNullOrEmpty(profile.AttackVfxId)) return;

            var spawner = TSWP.Art.VfxSpawner.Instance;
            if (spawner == null) return;

            Vector3 origin = transform.position + (Vector3)(direction * (profile.Range * profile.VfxForwardRatio));
            spawner.Play(profile.AttackVfxId, origin, flipX: direction.x < 0f);
        }

        public void SetProfile(BasicAttackProfile newProfile)
        {
            profile = newProfile;
            _attackIntervalRemaining = 0f;
        }

        /// <summary>
        /// 기본 공격 입력 진입점. 실행했으면 true.
        /// 자원/횟수 게이트는 의도적으로 없다 (문서: 기본 공격은 제한 없이 사용) — 간격·상태이상만 검사.
        /// </summary>
        public bool TryAttack(Vector2 aimDirection)
        {
            if (profile == null)
            {
                return false;
            }

            if (_entity != null && _entity.IsDead)
            {
                return false;
            }

            // 상태이상 게이트 — 기절/빙결이 차단 (속박·침묵·혼란 중에는 공격 가능).
            if (_statusController != null && !_statusController.CanAttack)
            {
                return false;
            }

            if (_attackIntervalRemaining > 0f)
            {
                return false;
            }

            _attackIntervalRemaining = GetAttackInterval();
            Vector2 direction = aimDirection.sqrMagnitude > 0f ? aimDirection.normalized : Vector2.right;

            PlayAttackVfx(direction);
            PerformAttack(direction);
            return true;
        }

        /// <summary>
        /// DamageInfo 구성 헬퍼 — 기본 공격 피해 성분을 채운다.
        /// 약화(Weak) 공격력 배율은 공격 측이 곱한다 (StatusEffectController 계약).
        /// </summary>
        public DamageInfo BuildDamageInfo()
        {
            float baseDamage = profile != null ? profile.Damage : 0f;
            if (_statusController != null)
            {
                baseDamage *= _statusController.GetAttackPowerMultiplier();
            }

            return new DamageInfo
            {
                BaseDamage = baseDamage,
                ItemBonus = 0f,  // TODO: 아이템 modifier(Core.StatType.AttackPower) 반영 — Player.PlayerStats 연동 시
                SkillBonus = 0f, // 기본 공격에는 스킬 성분 없음
                MiscBonus = 0f,
                IsCritical = false, // TODO: 치명타 굴림 — 기본 0% (GameRules.BaseCritChance), 아이템/버프 연동 시
                Source = _entity,   // 아군 판정(TeamType 비교)·통계 귀속
            };
        }

        // ── 내부 구현 ─────────────────────────────────────────────

        /// <summary>공격 간격 = 1 / (공격 속도 × 상태이상 배율). 감전이 공격 속도를 낮춘다.</summary>
        private float GetAttackInterval()
        {
            float attacksPerSecond = profile.AttackSpeed;
            if (_statusController != null)
            {
                attacksPerSecond *= _statusController.GetAttackSpeedMultiplier();
            }
            // TODO: Core.StatType.AttackSpeed 스탯(아이템) 반영 — Player.PlayerStats 연동 시.
            return attacksPerSecond > 0f ? 1f / attacksPerSecond : 1f;
        }

        /// <summary>공격 방식별 실행. 구조·호출 흐름만 갖추고 물리/연출 세부는 TODO.</summary>
        private void PerformAttack(Vector2 direction)
        {
            switch (profile.AttackType)
            {
                case BasicAttackType.Melee:
                    PerformMelee(direction);
                    break;

                case BasicAttackType.Projectile:
                    // TODO: 투사체 스폰 (archer 화살/mage 탄환) — 프리팹·풀링·비행 물리는 연출 세부.
                    //       투사체 명중 시 BuildDamageInfo()를 IDamageable.TakeDamage로 전달한다.
                    break;

                case BasicAttackType.Throw:
                    // TODO: 투척체 스폰 (bomber 폭탄) — 포물선 궤적, 착탄 시 IsExplosive=true 피해
                    //       (구조물은 폭발 공격만 파괴 가능 — 전투 시스템 연계).
                    break;

                case BasicAttackType.Injection:
                    PerformInjection(direction);
                    break;
            }
            // TODO(연출): 공격 모션/이펙트/사운드 — Art.CharacterVisual 12FPS 애니메이션 연동.
        }

        /// <summary>근접 타격 — 전방 반원 범위 판정. 팀 필터 없음 (아군사격은 DamageSystem이 50% 규칙 처리).</summary>
        private void PerformMelee(Vector2 direction)
        {
            float radius = profile.Range * 0.5f;
            Vector2 origin = (Vector2)transform.position + direction * radius;
            Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].gameObject == gameObject)
                {
                    continue; // 자기 자신 제외
                }

                if (hits[i].TryGetComponent(out IDamageable target))
                {
                    DamageInfo info = BuildDamageInfo();
                    target.TakeDamage(in info);
                }
            }
        }

        /// <summary>
        /// 주사(doctor) — 사거리 내 최근접 유닛에게 회복을 놓는다.
        /// 팀 필터를 두지 않아 "회복 주사를 적에게 맞춤" 트롤이 성립한다 (문서: 트롤 요소).
        /// NOTE(기획 확인 필요): 주사의 적 대상 효과(회복 vs 피해)는 개별 직업 문서 미정 — 우선 대상 무관 회복으로 구현.
        /// </summary>
        private void PerformInjection(Vector2 direction)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, profile.Range);
            CombatEntity nearest = null;
            float nearestSqr = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].gameObject == gameObject)
                {
                    continue;
                }

                if (!hits[i].TryGetComponent(out CombatEntity candidate) || candidate.IsDead)
                {
                    continue;
                }

                float sqr = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = candidate;
                }
            }

            if (nearest != null)
            {
                int healerId = _entity != null ? _entity.OwnerPlayerId : -1;
                nearest.Heal(profile.Damage, healerId); // 회복량 = 프로파일 damage 값 재사용. TODO(밸런스): 문서 미정
            }
        }
    }
}
