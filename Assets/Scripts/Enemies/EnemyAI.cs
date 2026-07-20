// 근거: 적 시스템.md — AI는 플레이어 거리/체력/시야/장애물/공격 가능 여부/아군 위치 6요소를 고려해 행동을 바꾼다.
//       난이도는 숫자(체력)가 아니라 행동·조합·패턴으로 만든다.
// 근거: 전투 시스템.md — "공격은 명확해야 한다 / 피격이 공정해야 한다" → 공격 전 예비 동작(telegraph)으로 예고한다.
// 근거: 적 시스템.md — 적도 환경(낙사)의 영향을 받지만, 스스로 낭떠러지로 걸어가면 전투가 성립하지 않는다.
// 유틸리티 AI: 매 결정 주기마다 컨텍스트를 수집하고 행동 후보를 스코어링해 최고점을 실행한다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;

namespace TSWP.Enemies
{
    /// <summary>적이 선택할 수 있는 행동.</summary>
    public enum EnemyAction
    {
        Idle,          // 대기 — 대상 없음
        Approach,      // 접근
        Retreat,       // 후퇴 — 저체력/근접 회피
        Attack,        // 공격
        UseAbility,    // 고유 능력 사용 (특수 적)
        Reposition,    // 우회 — 장애물로 시야가 막힘
        Telegraph,     // 공격 예비 동작 — 이동 정지 + 예고 연출 (전투 시스템.md '공정한 피격')
        KeepDistance,  // 사거리 유지 — 너무 붙었을 때 물러난다 (원거리 적의 정체성)
    }

    /// <summary>
    /// 유틸리티 AI. 컨텍스트 6요소 수집 → 행동 스코어링 → 실행(이동/예고/공격)까지 담당한다.
    /// 행동 성향 값은 전부 <see cref="EnemyAIProfile"/>(= EnemyData 소유)에서 읽으므로
    /// 새로운 적을 만들 때 이 컴포넌트도 프리팹도 건드릴 필요가 없다.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class EnemyAI : MonoBehaviour
    {
        // 주의: 감지 거리·후퇴 임계치·행동 점수 같은 '성격' 값은 이 컴포넌트에 두지 않는다.
        //   그 값들이 프리팹에 박히면 적 1종을 추가할 때마다 프리팹을 복제해야 한다.
        //   → 성격은 EnemyData.aiProfile(EnemyAIProfile)이 소유하고, 여기에는 '씬 환경' 값만 남긴다.
        //   (공용 몸통 프리팹 1개 + EnemyData N개 = 적 N종)

        [Header("레이어")]
        [Tooltip("시야 차단 판정 대상 — 지형/구조물 레이어.")]
        [SerializeField] private LayerMask obstacleMask;

        [Tooltip("주변 탐지에 포함할 레이어(플레이어/적 캐릭터). 비워 두면 모든 레이어를 훑어 버퍼가 지형·드롭으로 넘칠 수 있다.")]
        [SerializeField] private LayerMask characterMask;

        [Tooltip("발밑 지면 판정 레이어. 비우면 obstacleMask를 사용하고, 그것도 비어 있으면 낭떠러지 회피가 비활성된다.")]
        [SerializeField] private LayerMask groundMask;

        [Header("탐지 버퍼")]
        // 근거: 감사 §23 — 버퍼가 작으면 8인 + 적 + 지형이 몰릴 때 플레이어가 잘려 적이 Idle에 빠진다.
        [Tooltip("한 번의 주변 탐지에서 담을 최대 콜라이더 수. 8인 + 다수 적을 고려해 넉넉히 잡는다.")]
        [SerializeField, Min(8)] private int maxScanTargets = 64;

        [Header("낭떠러지 회피")]
        [Tooltip("진행 방향으로 이만큼 앞을 짚어 본다(월드 유닛). 이동 속도보다 약간 길게 잡는다.")]
        [SerializeField, Min(0.05f)] private float ledgeProbeForward = 0.6f; // TODO(밸런스): 문서 미정

        [Tooltip("발밑으로 이 깊이까지 땅이 없으면 낭떠러지로 본다.")]
        [SerializeField, Min(0.05f)] private float ledgeProbeDepth = 1.2f; // TODO(밸런스): 문서 미정

        [Tooltip("탐지선 시작 높이(자기 원점 기준). 콜라이더 안쪽에서 시작하지 않도록 살짝 올린다.")]
        [SerializeField] private float ledgeProbeOriginY = 0.1f;

        [Header("연출")]
        [Tooltip("이동/공격 방향으로 스프라이트를 좌우 반전한다 (Art.CharacterVisual과 동일 규칙).")]
        [SerializeField] private bool faceTarget = true;

        [Tooltip("원본 스프라이트가 오른쪽을 보고 있는가. 왼쪽을 보고 그렸다면 끈다.")]
        [SerializeField] private bool spriteFacesRight = true;

        /// <summary>
        /// 후퇴 시작 경계 = 유지 거리 × 이 비율. 접근 정지 경계(유지 거리)와 겹치지 않게 하는 히스테리시스 폭.
        /// </summary>
        private const float KeepDistanceInnerRatio = 0.7f;

        private EnemyController _controller;
        private CombatEntity _combat;
        private Rigidbody2D _body;
        private SpriteRenderer _renderer;
        private HitFlash _hitFlash;
        private Color _baseColor = Color.white;

        private readonly EnemyAIContext _context = new EnemyAIContext();
        private Collider2D[] _overlapBuffer;
        private ContactFilter2D _scanFilter;
        private bool _warnedTruncated;

        private float _decisionTimer;
        private float _attackCooldown;

        // ── 예비 동작 상태 ────────────────────────────────────────
        private float _telegraphTimer;
        private CombatEntity _telegraphTarget;
        private bool _telegraphing;
        private bool _tintApplied;

        public EnemyAction CurrentAction { get; private set; } = EnemyAction.Idle;
        public EnemyAIContext Context => _context;

        /// <summary>공격 예비 동작 중인가 (연출·디버그 조회용).</summary>
        public bool IsTelegraphing => _telegraphing;

        /// <summary>
        /// 예비 동작이 끝난 뒤 되돌아갈 '원래 색'을 갱신한다.
        /// EnemyController가 에셋 외형(bodyColor)을 입힌 뒤 호출한다 — 이걸 하지 않으면
        /// 예고가 끝날 때 프리팹 원본 색(보통 흰색)으로 되돌아가 적 색깔이 사라진다.
        /// </summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            if (_telegraphing) return;      // 예고 중이면 종료 시 이 색으로 복귀한다
            if (_renderer != null && !_tintApplied) _renderer.color = color;
        }

        /// <summary>
        /// 현재 적의 AI 성격. 데이터가 없거나(단독 배치) 프로파일이 비어 있어도 공용 기본값이 나오므로
        /// AI가 NullReference로 멈추지 않는다.
        /// </summary>
        public EnemyAIProfile Profile
        {
            get
            {
                var data = _controller != null ? _controller.Data : null;
                return data != null ? data.ResolveAIProfile() : EnemyAIProfile.Default;
            }
        }

        /// <summary>현재 기본 공격 사거리. 데이터가 없으면 근접 기본값.</summary>
        private float AttackRange
        {
            get
            {
                var data = _controller != null ? _controller.Data : null;
                return data != null && data.basicAttack != null ? data.basicAttack.range : 1.5f;
            }
        }

        private void Awake()
        {
            _controller = GetComponent<EnemyController>();
            _combat = GetComponent<CombatEntity>();
            _body = GetComponent<Rigidbody2D>();
            _renderer = ResolveBodyRenderer();
            _hitFlash = GetComponent<HitFlash>();
            if (_renderer != null) _baseColor = _renderer.color;

            _overlapBuffer = new Collider2D[Mathf.Max(8, maxScanTargets)];

            // 명시 초기화 — 기본값 struct는 layerMask가 0이라 아무것도 잡지 못하는 경우가 있다.
            _scanFilter = new ContactFilter2D();
            _scanFilter.useTriggers = false;
            if (characterMask.value != 0) _scanFilter.SetLayerMask(characterMask);
        }

        private void OnDisable()
        {
            // 예비 동작 중 비활성화되면 틴트가 남는다 — 반드시 원색으로 되돌린다.
            if (_telegraphing) CancelTelegraph();
        }

        /// <summary>
        /// 본체 스프라이트를 찾는다. 체력바(EnemyHealthBar)가 만든 자식 바를 본체로 착각하면
        /// 예비 동작 색이 체력바에 칠해지므로 자기 오브젝트의 렌더러를 우선한다.
        /// </summary>
        private SpriteRenderer ResolveBodyRenderer()
        {
            if (TryGetComponent(out SpriteRenderer own)) return own;

            var candidates = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate == null) continue;
                // 체력바 루트("HealthBar") 밑에 있는 렌더러는 본체가 아니다.
                if (candidate.GetComponentInParent<EnemyHealthBar>() != null &&
                    candidate.transform.parent != null &&
                    candidate.transform.parent.name == "HealthBar") continue;
                return candidate;
            }
            return null;
        }

        private void Update()
        {
            if (_combat == null || _combat.IsDead)
            {
                if (_telegraphing) CancelTelegraph();
                return;
            }

            float dt = Time.deltaTime;
            if (_attackCooldown > 0f) _attackCooldown -= dt;

            // ── 예비 동작 중에는 재판단하지 않는다 ──
            // 예고한 공격은 반드시 발동해야 회피가 의미를 갖는다 (예고 후 취소는 학습을 방해한다).
            if (_telegraphing)
            {
                // 단, 기절/침묵 등으로 공격이 봉쇄되면 예고는 무효가 된다 —
                // CC를 걸었는데도 예고된 공격이 그대로 터지면 CC가 무의미해진다(공략 방법 존재 원칙).
                if (_controller.Status != null && !_controller.Status.CanAttack)
                {
                    CancelTelegraph();
                    return;
                }

                CurrentAction = EnemyAction.Telegraph;
                StopMoving();
                if (_telegraphTarget != null) FaceToward(_telegraphTarget.transform.position);

                _telegraphTimer -= dt;
                if (_telegraphTimer <= 0f) ReleaseTelegraph();
                return;
            }

            _decisionTimer -= dt;
            if (_decisionTimer > 0f) return;
            _decisionTimer = Profile.SafeDecisionInterval;

            BuildContext();
            CurrentAction = SelectAction();
            ExecuteAction(CurrentAction);
        }

        // ── ① 컨텍스트 수집 (문서의 AI 고려 6요소) ───────────────────
        private void BuildContext()
        {
            _context.Reset();

            var profile = Profile;
            Vector2 self = transform.position;

            // ② 자신의 체력
            _context.selfHealthRatio = _combat.MaxHp > 0f ? _combat.CurrentHp / _combat.MaxHp : 0f;

            // ① 최근접 생존 플레이어 + ⑥ 아군 적 위치
            int hitCount = Physics2D.OverlapCircle(
                self, Mathf.Max(profile.detectionRange, profile.allyScanRadius), _scanFilter, _overlapBuffer);

            // 버퍼가 꽉 찼다면 결과가 잘렸다는 뜻 — 플레이어가 잘려 나가면 적이 Idle에 빠진다.
            if (hitCount >= _overlapBuffer.Length && !_warnedTruncated)
            {
                _warnedTruncated = true;
                Debug.LogWarning($"[EnemyAI] '{name}': 주변 탐지 버퍼({_overlapBuffer.Length})가 가득 찼습니다. " +
                                 "characterMask를 캐릭터 레이어로 좁히거나 maxScanTargets를 늘리세요.", this);
            }

            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                // 콜라이더가 자식에 붙은 구성(몸통/발)에서도 본체를 찾도록 부모까지 훑는다
                // — 프로젝트 규약(EnvironmentHazard/PlayerInteraction)과 동일.
                var collider = _overlapBuffer[i];
                var other = collider != null ? collider.GetComponentInParent<CombatEntity>() : null;
                if (other == null || other == _combat || other.IsDead) continue;

                float distance = Vector2.Distance(self, other.transform.position);

                if (other.Team == TeamType.Players)
                {
                    if (distance <= profile.detectionRange && distance < bestDistance)
                    {
                        bestDistance = distance;
                        _context.targetPlayer = other;
                    }
                }
                else if (other.Team == TeamType.Enemies && distance <= profile.allyScanRadius)
                {
                    _context.allyPositions.Add(other.transform.position);
                }
            }

            if (_context.targetPlayer == null) return;

            _context.distanceToPlayer = bestDistance;

            // ③ 시야 / ④ 장애물
            Vector2 targetPos = _context.targetPlayer.transform.position;
            RaycastHit2D blocker = Physics2D.Linecast(self, targetPos, obstacleMask);
            _context.hasObstacleBetween = blocker.collider != null;
            _context.hasLineOfSight = !_context.hasObstacleBetween;

            // ⑤ 공격 가능 여부 — 쿨타임 + 사거리 + 시야 + 상태이상 미차단
            bool notBlocked = _controller.Status == null || _controller.Status.CanAttack;

            _context.canAttack = _attackCooldown <= 0f
                                 && bestDistance <= AttackRange
                                 && _context.hasLineOfSight
                                 && notBlocked;
        }

        // ── ② 행동 스코어링 ───────────────────────────────────────
        private EnemyAction SelectAction()
        {
            if (_context.targetPlayer == null) return EnemyAction.Idle;

            EnemyAction best = EnemyAction.Idle;
            float bestScore = float.MinValue;

            Evaluate(EnemyAction.Attack, ScoreAttack(), ref best, ref bestScore);
            Evaluate(EnemyAction.UseAbility, ScoreAbility(), ref best, ref bestScore);
            Evaluate(EnemyAction.Retreat, ScoreRetreat(), ref best, ref bestScore);
            Evaluate(EnemyAction.KeepDistance, ScoreKeepDistance(), ref best, ref bestScore);
            Evaluate(EnemyAction.Reposition, ScoreReposition(), ref best, ref bestScore);
            Evaluate(EnemyAction.Approach, ScoreApproach(), ref best, ref bestScore);

            return best;
        }

        private static void Evaluate(EnemyAction action, float score, ref EnemyAction best, ref float bestScore)
        {
            if (score <= bestScore) return;
            bestScore = score;
            best = action;
        }

        private float ScoreAttack() => _context.canAttack ? Profile.attackScore : float.MinValue;

        private float ScoreAbility()
        {
            var ability = _controller.Data != null ? _controller.Data.specialAbility : null;
            if (ability == null) return float.MinValue;
            if (!ability.CanExecute(_controller, _context)) return float.MinValue;
            return Profile.abilityScore; // 고유 능력은 일반 공격보다 우선순위가 낮다 (쿨타임 자원)
        }

        private float ScoreRetreat()
        {
            var profile = Profile;

            // retreatHealthRatio == 0 → 절대 후퇴하지 않는 우직한 추격형 (예: Spider)
            if (profile.NeverRetreats) return float.MinValue;

            // 저체력일수록 후퇴 선호
            if (_context.selfHealthRatio > profile.retreatHealthRatio) return float.MinValue;
            return profile.retreatScore + (profile.retreatHealthRatio - _context.selfHealthRatio) * 50f;
        }

        /// <summary>
        /// 사거리 유지 — 유지 거리보다 안쪽으로 들어온 대상에게서 물러난다.
        /// 이게 없으면 원거리 적이 플레이어에게 붙어 버려 '원거리'라는 정체성이 사라진다.
        /// </summary>
        private float ScoreKeepDistance()
        {
            var profile = Profile;
            if (!profile.KeepsDistance) return float.MinValue;
            if (float.IsPositiveInfinity(_context.distanceToPlayer)) return float.MinValue;

            // 접근을 멈추는 경계와 물러나기 시작하는 경계를 겹치지 않게 띄운다(히스테리시스).
            // 같은 값이면 경계선에서 접근/후퇴를 매 판단마다 번갈아 골라 제자리 떨림이 생긴다.
            float hold = profile.GetHoldDistance(AttackRange) * KeepDistanceInnerRatio;
            if (_context.distanceToPlayer >= hold) return float.MinValue;

            return profile.keepDistanceScore;
        }

        private float ScoreReposition()
        {
            // 시야가 막혀 있으면 우회
            return _context.hasObstacleBetween ? Profile.repositionScore : float.MinValue;
        }

        private float ScoreApproach()
        {
            var profile = Profile;

            // 항상 가능한 기본 행동 — 멀수록 선호도 상승
            if (float.IsPositiveInfinity(_context.distanceToPlayer)) return float.MinValue;

            // 유지 거리 안쪽이면 더 접근하지 않는다 (원거리 적이 붙는 것을 막는다).
            if (profile.KeepsDistance && _context.distanceToPlayer <= profile.GetHoldDistance(AttackRange))
                return float.MinValue;

            return profile.approachScore + Mathf.Min(_context.distanceToPlayer, profile.detectionRange);
        }

        // ── ③ 행동 실행 ───────────────────────────────────────────
        private void ExecuteAction(EnemyAction action)
        {
            switch (action)
            {
                case EnemyAction.Attack:
                    BeginAttack();
                    break;

                case EnemyAction.UseAbility:
                    _controller.Data.specialAbility.Execute(_controller, _context);
                    break;

                case EnemyAction.Approach:
                    MoveToward(_context.targetPlayer.transform.position);
                    break;

                case EnemyAction.Retreat:
                case EnemyAction.KeepDistance:
                    // 물러나면서도 대상을 바라본다 — 다음 공격 타이밍을 노리는 자세.
                    MoveAwayFrom(_context.targetPlayer.transform.position);
                    FaceToward(_context.targetPlayer.transform.position);
                    break;

                case EnemyAction.Reposition:
                    // TODO: 경로 탐색 기반 우회 — 현재는 접근으로 폴백.
                    MoveToward(_context.targetPlayer.transform.position);
                    break;

                case EnemyAction.Idle:
                default:
                    // 유지 거리 안에서 대기하는 경우도 여기로 온다 — 멈춰도 대상은 계속 바라본다.
                    StopMoving();
                    if (_context.targetPlayer != null) FaceToward(_context.targetPlayer.transform.position);
                    break;
            }
        }

        // ── 예비 동작 (telegraph) ─────────────────────────────────
        // 근거: 전투 시스템.md — 예고 없는 공격은 피할 수 없어 불공정하다.
        //   예고 중 적은 멈추고 색이 변하며(선택) 이펙트를 재생한다. 시간이 지나면 공격은 반드시 발동한다.

        /// <summary>공격 진입점 — 예비 동작 시간이 있으면 예고부터, 없으면 즉시 발동.</summary>
        private void BeginAttack()
        {
            var data = _controller.Data;
            var target = _context.targetPlayer;
            if (data == null || target == null) return;

            // 패턴 속도 배율이 높은 난이도일수록 예고가 짧아진다 (허용된 3종 배율 중 '패턴 속도').
            float speed = Mathf.Max(0.1f, _controller.PatternSpeedMultiplier);
            float telegraph = data.basicAttack.telegraphDuration / speed;

            if (telegraph <= 0f)
            {
                PerformAttack(target);
                return;
            }

            _telegraphing = true;
            _telegraphTimer = telegraph;
            _telegraphTarget = target;
            CurrentAction = EnemyAction.Telegraph;
            StopMoving();
            FaceToward(target.transform.position);

            // 흰색이면 색을 바꾸지 않는다 (이펙트만으로 예고하는 적)
            if (data.basicAttack.telegraphColor != Color.white)
            {
                _tintApplied = true;
                ApplyTelegraphTint(data.basicAttack.telegraphColor);
            }

            if (!string.IsNullOrEmpty(data.basicAttack.telegraphVfxId))
            {
                // 연출 매니저가 없으면 조용히 생략된다 — 게임 로직은 영향을 받지 않는다.
                var spawner = Art.VfxSpawner.Instance;
                if (spawner != null) spawner.Play(data.basicAttack.telegraphVfxId, transform.position);
            }
        }

        /// <summary>예고 종료 → 공격 발동. 대상이 사라졌으면 발동만 생략하고 쿨타임은 소모한다.</summary>
        private void ReleaseTelegraph()
        {
            var target = _telegraphTarget;
            EndTelegraphVisual();

            var data = _controller.Data;
            if (data == null) return;

            if (target != null && !target.IsDead)
            {
                PerformAttack(target);
            }
            else
            {
                // 헛스윙 — 쿨타임은 정상적으로 소모해 예고 직후 연타를 막는다.
                float speed = Mathf.Max(0.1f, _controller.PatternSpeedMultiplier);
                _attackCooldown = data.basicAttack.cooldown / speed;
            }
        }

        /// <summary>사망/비활성화로 예고가 중단될 때 — 색만 되돌리고 공격은 발동하지 않는다.</summary>
        private void CancelTelegraph() => EndTelegraphVisual();

        private void EndTelegraphVisual()
        {
            _telegraphing = false;
            _telegraphTimer = 0f;
            _telegraphTarget = null;

            if (!_tintApplied) return;
            _tintApplied = false;
            ApplyTelegraphTint(_baseColor);
        }

        /// <summary>
        /// 틴트 적용. HitFlash가 있으면 그쪽 '기본 색'을 갱신해 피격 플래시와 색 소유권이 충돌하지 않게 한다
        /// (플래시 중이면 플래시가 끝난 뒤 이 색으로 복귀한다 — 흰색 고착 버그 재발 방지).
        /// </summary>
        private void ApplyTelegraphTint(Color color)
        {
            if (_renderer == null) return;

            // HitFlash는 첫 피격 때 HitFeedback이 붙이므로 Awake 시점에는 없을 수 있다 — 매번 다시 확인한다.
            if (_hitFlash == null) TryGetComponent(out _hitFlash);

            if (_hitFlash != null) _hitFlash.SetBaseColor(color);
            else _renderer.color = color;
        }

        private void PerformAttack(CombatEntity target)
        {
            var data = _controller.Data;
            if (data == null || target == null) return;

            EnemyAttack attack = data.basicAttack;
            Vector2 direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
            FaceToward(target.transform.position);

            // 공격 이펙트
            if (!string.IsNullOrEmpty(attack.attackVfxId))
            {
                Vector3 origin = transform.position + (Vector3)(direction * attack.muzzleForward);
                Art.VfxSpawner.Instance?.Play(attack.attackVfxId, origin, flipX: direction.x < 0f);
            }

            var statusEffects = attack.statusEffects != null && attack.statusEffects.Count > 0
                ? new List<StatusEffects.StatusEffectData>(attack.statusEffects)
                : null;

            KnockbackInfo? knockback = null;
            if (attack.applyKnockback)
            {
                KnockbackInfo kb = attack.knockback;
                kb.Direction = direction;
                knockback = kb;
            }

            float damage = attack.damage * _controller.AttackMultiplier;

            if (attack.isRanged && attack.projectilePrefab != null)
            {
                FireProjectile(attack, direction, damage, statusEffects, knockback);
            }
            else
            {
                var info = new DamageInfo
                {
                    BaseDamage = damage,
                    IsExplosive = attack.isExplosive,
                    Source = _combat,
                    StatusEffects = statusEffects,
                    Knockback = knockback,
                };
                DamageSystem.Apply(target, in info);
            }

            // 패턴 속도 배율이 높을수록 쿨타임이 짧아진다 (난이도 3종 배율 중 하나)
            float speed = Mathf.Max(0.1f, _controller.PatternSpeedMultiplier);
            _attackCooldown = attack.cooldown / speed;
        }

        /// <summary>
        /// 투사체 발사 — 총구는 자기 콜라이더 밖에서 생성한다.
        /// 8인 난전에서 발사마다 Instantiate/Destroy가 일어나지 않도록 ProjectilePool을 경유한다
        /// (풀이 씬에 없으면 풀이 스스로 만들어지고, 종료 중이면 Instantiate로 폴백한다).
        /// </summary>
        private void FireProjectile(EnemyAttack attack, Vector2 direction, float damage,
                                    List<StatusEffects.StatusEffectData> statusEffects,
                                    KnockbackInfo? knockback)
        {
            Vector3 muzzle = transform.position + (Vector3)(direction * attack.muzzleForward);

            var projectile = ProjectilePool.Spawn(attack.projectilePrefab, muzzle, Quaternion.identity);
            if (projectile == null) return;

            projectile.SetSpeed(attack.projectileSpeed);
            projectile.SetObstacleMask(obstacleMask);
            projectile.Launch(_combat, direction, damage, statusEffects, knockback, attack.isExplosive);
        }

        /// <summary>
        /// 대상 쪽으로 스프라이트를 돌린다. 연출용이므로 렌더러가 없으면 조용히 생략한다
        /// (연출 부재가 게임 로직을 실패시키지 않는다).
        /// </summary>
        private void FaceToward(Vector2 worldPosition)
        {
            if (!faceTarget || _renderer == null) return;

            float dx = worldPosition.x - transform.position.x;
            if (Mathf.Abs(dx) < 0.01f) return; // 수직 정렬 시 좌우가 떨리는 것을 막는다

            bool lookingRight = dx > 0f;
            _renderer.flipX = spriteFacesRight ? !lookingRight : lookingRight;
        }

        private void MoveToward(Vector2 destination)
        {
            if (!CanMove()) { StopMoving(); return; }

            Vector2 direction = (destination - (Vector2)transform.position).normalized;
            ApplyVelocity(direction);
        }

        private void MoveAwayFrom(Vector2 threat)
        {
            if (!CanMove()) { StopMoving(); return; }

            Vector2 direction = ((Vector2)transform.position - threat).normalized;
            ApplyVelocity(direction);
        }

        private bool CanMove() => _controller.Status == null || _controller.Status.CanMove;

        private void ApplyVelocity(Vector2 direction)
        {
            if (_body == null) return;

            // Mathf.Sign(0) == 1 이라 수직 정렬 시 항상 오른쪽으로 새는 것을 막는다.
            if (Mathf.Abs(direction.x) < 0.01f) { StopMoving(); return; }
            float sign = direction.x > 0f ? 1f : -1f;

            // 낭떠러지 회피 — 스스로 걸어서 떨어지지 않는다. (넉백/밀림은 이 경로를 타지 않으므로
            // 플레이어가 밀어서 떨어뜨리는 공략은 그대로 가능하다 — 적 시스템.md '환경의 영향')
            if (ShouldAvoidLedge(sign)) { StopMoving(); return; }

            var data = _controller.Data;
            float speed = data != null ? data.moveSpeed : 2f;
            if (_controller.Status != null)
                speed *= _controller.Status.GetMoveSpeedMultiplier(); // 감전/둔화 반영

            // 2D 횡스크롤 — 수평 이동만 적용하고 중력은 유지한다.
            _body.linearVelocity = new Vector2(sign * speed, _body.linearVelocity.y);

            FaceToward((Vector2)transform.position + new Vector2(sign, 0f));
        }

        /// <summary>진행 방향 앞의 발밑에 땅이 없으면 true (= 그쪽으로 걸어가면 안 된다).</summary>
        private bool ShouldAvoidLedge(float sign)
        {
            var data = _controller.Data;
            if (data == null || !data.avoidLedges || data.canTraverseGaps) return false;

            // 지면 레이어가 하나도 지정되지 않으면 판정 자체를 포기한다.
            // (마스크 0으로 레이캐스트하면 항상 '땅 없음'이 되어 적이 영영 움직이지 않는다.)
            int mask = groundMask.value != 0 ? groundMask.value : obstacleMask.value;
            if (mask == 0) return false;

            Vector2 origin = (Vector2)transform.position + new Vector2(sign * ledgeProbeForward, ledgeProbeOriginY);

            // 공중(점프/낙하/넉백 중)에서는 판정하지 않는다 — 착지 후 다시 검사한다.
            Vector2 feet = (Vector2)transform.position + new Vector2(0f, ledgeProbeOriginY);
            bool grounded = Physics2D.Raycast(feet, Vector2.down, ledgeProbeDepth, mask).collider != null;
            if (!grounded) return false;

            return Physics2D.Raycast(origin, Vector2.down, ledgeProbeDepth, mask).collider == null;
        }

        private void StopMoving()
        {
            if (_body == null) return;
            _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
        }

#if UNITY_EDITOR
        /// <summary>낭떠러지 탐지선 시각화 — 씬 뷰에서 probe 길이를 눈으로 맞춘다.</summary>
        private void OnDrawGizmosSelected()
        {
            Vector2 basePoint = (Vector2)transform.position + new Vector2(0f, ledgeProbeOriginY);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(basePoint, basePoint + Vector2.down * ledgeProbeDepth);

            Gizmos.color = Color.yellow;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 probe = basePoint + new Vector2(s * ledgeProbeForward, 0f);
                Gizmos.DrawLine(probe, probe + Vector2.down * ledgeProbeDepth);
            }
        }
#endif
    }
}
