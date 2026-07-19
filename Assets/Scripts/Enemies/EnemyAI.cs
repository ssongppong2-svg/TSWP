// 근거: 적 시스템.md — AI는 플레이어 거리/체력/시야/장애물/공격 가능 여부/아군 위치 6요소를 고려해 행동을 바꾼다.
//       난이도는 숫자(체력)가 아니라 행동·조합·패턴으로 만든다.
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
        Idle,        // 대기 — 대상 없음
        Approach,    // 접근
        Retreat,     // 후퇴 — 저체력/근접 회피
        Attack,      // 공격
        UseAbility,  // 고유 능력 사용 (특수 적)
        Reposition,  // 우회 — 장애물로 시야가 막힘
    }

    /// <summary>
    /// 유틸리티 AI 스켈레톤. 실제 이동/공격 실행은 TODO(연출·물리)로 두되 판단 흐름은 완성한다.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("판단 주기")]
        [Tooltip("AI 재판단 간격(초). 매 프레임 판단을 피해 8인 멀티에서 부하를 줄인다.")]
        [SerializeField, Min(0.02f)] private float decisionInterval = 0.2f; // TODO(밸런스): 문서 미정

        [Header("감지")]
        [SerializeField, Min(0f)] private float detectionRange = 12f;   // TODO(밸런스): 문서 미정
        [SerializeField, Min(0f)] private float allyScanRadius = 8f;    // TODO(밸런스): 문서 미정
        [SerializeField, Min(0f)] private float retreatHealthRatio = 0.25f; // TODO(밸런스): 문서 미정

        [Header("레이어")]
        [Tooltip("시야 차단 판정 대상 — 지형/구조물 레이어.")]
        [SerializeField] private LayerMask obstacleMask;

        private EnemyController _controller;
        private CombatEntity _combat;
        private Rigidbody2D _body;

        private readonly EnemyAIContext _context = new EnemyAIContext();
        private readonly Collider2D[] _overlapBuffer = new Collider2D[16];

        private float _decisionTimer;
        private float _attackCooldown;

        public EnemyAction CurrentAction { get; private set; } = EnemyAction.Idle;
        public EnemyAIContext Context => _context;

        private void Awake()
        {
            _controller = GetComponent<EnemyController>();
            _combat = GetComponent<CombatEntity>();
            _body = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (_combat == null || _combat.IsDead) return;

            float dt = Time.deltaTime;
            if (_attackCooldown > 0f) _attackCooldown -= dt;

            _decisionTimer -= dt;
            if (_decisionTimer > 0f) return;
            _decisionTimer = decisionInterval;

            BuildContext();
            CurrentAction = SelectAction();
            ExecuteAction(CurrentAction);
        }

        // ── ① 컨텍스트 수집 (문서의 AI 고려 6요소) ───────────────────
        private void BuildContext()
        {
            _context.Reset();

            Vector2 self = transform.position;

            // ② 자신의 체력
            _context.selfHealthRatio = _combat.MaxHp > 0f ? _combat.CurrentHp / _combat.MaxHp : 0f;

            // ① 최근접 생존 플레이어 + ⑥ 아군 적 위치
            int hitCount = Physics2D.OverlapCircleNonAlloc(self, Mathf.Max(detectionRange, allyScanRadius), _overlapBuffer);
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                var other = _overlapBuffer[i] != null ? _overlapBuffer[i].GetComponent<CombatEntity>() : null;
                if (other == null || other == _combat || other.IsDead) continue;

                float distance = Vector2.Distance(self, other.transform.position);

                if (other.Team == TeamType.Players)
                {
                    if (distance <= detectionRange && distance < bestDistance)
                    {
                        bestDistance = distance;
                        _context.targetPlayer = other;
                    }
                }
                else if (other.Team == TeamType.Enemies && distance <= allyScanRadius)
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
            var data = _controller.Data;
            float attackRange = data != null ? data.basicAttack.range : 1.5f;
            bool notBlocked = _controller.Status == null || _controller.Status.CanAttack;

            _context.canAttack = _attackCooldown <= 0f
                                 && bestDistance <= attackRange
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

        private float ScoreAttack() => _context.canAttack ? 100f : float.MinValue;

        private float ScoreAbility()
        {
            var ability = _controller.Data != null ? _controller.Data.specialAbility : null;
            if (ability == null) return float.MinValue;
            if (!ability.CanExecute(_controller, _context)) return float.MinValue;
            return 90f; // 고유 능력은 일반 공격보다 우선순위가 낮다 (쿨타임 자원)
        }

        private float ScoreRetreat()
        {
            // 저체력일수록, 대상이 가까울수록 후퇴 선호
            if (_context.selfHealthRatio > retreatHealthRatio) return float.MinValue;
            return 80f + (retreatHealthRatio - _context.selfHealthRatio) * 50f;
        }

        private float ScoreReposition()
        {
            // 시야가 막혀 있으면 우회
            return _context.hasObstacleBetween ? 70f : float.MinValue;
        }

        private float ScoreApproach()
        {
            // 항상 가능한 기본 행동 — 멀수록 선호도 상승
            if (float.IsPositiveInfinity(_context.distanceToPlayer)) return float.MinValue;
            return 10f + Mathf.Min(_context.distanceToPlayer, detectionRange);
        }

        // ── ③ 행동 실행 ───────────────────────────────────────────
        private void ExecuteAction(EnemyAction action)
        {
            switch (action)
            {
                case EnemyAction.Attack:
                    PerformAttack();
                    break;

                case EnemyAction.UseAbility:
                    _controller.Data.specialAbility.Execute(_controller, _context);
                    break;

                case EnemyAction.Approach:
                    MoveToward(_context.targetPlayer.transform.position);
                    break;

                case EnemyAction.Retreat:
                    MoveAwayFrom(_context.targetPlayer.transform.position);
                    break;

                case EnemyAction.Reposition:
                    // TODO: 경로 탐색 기반 우회 — 현재는 접근으로 폴백.
                    MoveToward(_context.targetPlayer.transform.position);
                    break;

                case EnemyAction.Idle:
                default:
                    StopMoving();
                    break;
            }
        }

        private void PerformAttack()
        {
            var data = _controller.Data;
            var target = _context.targetPlayer;
            if (data == null || target == null) return;

            EnemyAttack attack = data.basicAttack;

            var info = new DamageInfo
            {
                BaseDamage = attack.damage * _controller.AttackMultiplier,
                IsExplosive = attack.isExplosive,
                Source = _combat,
                StatusEffects = attack.statusEffects != null && attack.statusEffects.Count > 0
                    ? new List<StatusEffects.StatusEffectData>(attack.statusEffects)
                    : null,
            };

            if (attack.applyKnockback)
            {
                KnockbackInfo kb = attack.knockback;
                kb.Direction = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                info.Knockback = kb;
            }

            DamageSystem.Apply(target, in info);

            // 패턴 속도 배율이 높을수록 쿨타임이 짧아진다 (난이도 3종 배율 중 하나)
            float speed = Mathf.Max(0.1f, _controller.PatternSpeedMultiplier);
            _attackCooldown = attack.cooldown / speed;
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
            var data = _controller.Data;
            float speed = data != null ? data.moveSpeed : 2f;
            if (_controller.Status != null)
                speed *= _controller.Status.GetMoveSpeedMultiplier(); // 감전/둔화 반영

            if (_body == null) return;
            // 2D 횡스크롤 — 수평 이동만 적용하고 중력은 유지한다.
            _body.linearVelocity = new Vector2(direction.x * speed, _body.linearVelocity.y);
        }

        private void StopMoving()
        {
            if (_body == null) return;
            _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
        }
    }
}
