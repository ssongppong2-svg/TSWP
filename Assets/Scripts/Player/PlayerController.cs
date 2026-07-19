// 근거: 조작과 시스템.md — A/D 이동, Space 점프, LShift 달리기(스태미나 없음, 이동속도 증가만), 좌클릭 기본 공격, Q 스킬.
// 근거: 게임 시작과 선택, 직업, 플레이.md — 밈 모드 '중력이 반전된다' → Rigidbody2D.gravityScale 부호 반전 대응.
// 근거: 상태이상 시스템.md — 이동은 StatusEffectController의 CanMove / MoveInputModifier(공포·혼란) / 이동속도 배율만 질의,
//       이동 거리는 OnMoved 훅으로 전달 (출혈: 움직일수록 추가 피해).
// 모든 직업 공통 컴포넌트 — 직업 차이는 Jobs(BasicAttacker/SkillCaster) 데이터 주입으로만 표현 (문서: 모든 직업 동일 조작 체계).
// SYNC: 호스트 권위, 추후 NGO NetworkVariable — 위치/속도/중력 반전 상태 (원격 플레이어는 IPlayerInput을 네트워크 구현으로 교체).
using UnityEngine;
using TSWP.Core;
using TSWP.Combat;
using TSWP.Jobs;
using TSWP.StatusEffects;

namespace TSWP.Player
{
    /// <summary>
    /// Rigidbody2D 기반 횡스크롤 이동 컨트롤러.
    /// 입력(Update) → 물리 적용(FixedUpdate) 분리. 전투 입력은 Jobs 컴포넌트로 위임한다.
    /// E/휠클릭/T 입력은 각 담당 컴포넌트(PlayerInteraction/PingEmitter/EmoteWheel)가 InputSource를 직접 폴링한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("이동")]
        [SerializeField] private float fallbackMoveSpeed = 5f;   // TODO(밸런스): 문서 미정 — PlayerStats 부재 시 임시 이동속도
        [SerializeField] private float runSpeedMultiplier = 1.5f; // TODO(밸런스): 문서 미정 — 달리기는 '이동속도 증가'만 확정

        [Header("가속 (조작감)")]
        [Tooltip("지상 가속도. 높을수록 즉각 반응하고, 낮을수록 묵직하다.")]
        [SerializeField] private float groundAcceleration = 90f;  // TODO(밸런스): 플레이 테스트로 조정
        [Tooltip("지상 감속도. 높을수록 칼같이 멈춘다(미끄러짐 감소).")]
        [SerializeField] private float groundDeceleration = 110f;
        [Tooltip("공중 가속도 — 지상보다 낮게 두면 공중 제어가 무겁게 느껴진다.")]
        [SerializeField] private float airAcceleration = 55f;
        [SerializeField] private float airDeceleration = 35f;

        [Header("점프 (조작감)")]
        [SerializeField] private float jumpSpeed = 12f;           // TODO(밸런스): 문서 미정 — 점프 초기 속도
        [Tooltip("발판에서 떨어진 뒤에도 점프를 받아주는 유예 시간(초). 낭떠러지 점프 실패를 줄인다.")]
        [SerializeField] private float coyoteTime = 0.1f;
        [Tooltip("착지 직전에 누른 점프를 기억하는 시간(초). 착지 순간 자동 발동한다.")]
        [SerializeField] private float jumpBufferTime = 0.12f;
        [Tooltip("점프 키를 일찍 뗐을 때 상승 속도에 곱하는 값. 낮을수록 짧은 점프가 확실히 낮아진다.")]
        [Range(0f, 1f)][SerializeField] private float jumpCutMultiplier = 0.45f;
        [Tooltip("하강 시 중력 배수. 1보다 크면 붕 뜨는 느낌이 줄고 낙하가 경쾌해진다.")]
        [SerializeField] private float fallGravityMultiplier = 1.9f;
        [Tooltip("점프 정점 부근에서 중력을 약간 줄여 체공 제어를 돕는다(1 = 사용 안 함).")]
        [SerializeField] private float apexGravityMultiplier = 0.85f;
        [Tooltip("정점 판정 기준 — 수직 속도 절댓값이 이 값보다 작으면 정점으로 본다.")]
        [SerializeField] private float apexThreshold = 2.5f;

        [Header("대쉬")]
        // NOTE(문서 갱신 필요): 조작과 시스템.md v1.1에 없는 신규 조작 (우클릭).
        [Tooltip("대쉬 속도.")]
        [SerializeField] private float dashSpeed = 18f;           // TODO(밸런스): 문서 미정
        [Tooltip("대쉬 지속 시간(초).")]
        [SerializeField] private float dashDuration = 0.16f;
        [Tooltip("대쉬 재사용 대기시간(초).")]
        [SerializeField] private float dashCooldown = 0.6f;
        [Tooltip("대쉬 중 무적 시간(초). 0이면 무적 없음.")]
        // NOTE(기획 확인 필요): 게임 성경.md "모든 강력한 능력에는 반드시 위험이 따른다" —
        //   무적 대쉬는 강력하므로 기본 0으로 두고 밸런스 논의 후 결정한다.
        [SerializeField] private float dashInvincibility = 0f;
        [Tooltip("대쉬 중 중력 무시 — 켜면 수평으로 곧게 날아간다.")]
        [SerializeField] private bool dashIgnoresGravity = true;
        [Tooltip("공중에서 사용 가능한 대쉬 횟수. 착지하면 회복된다.")]
        [SerializeField] private int airDashCount = 1;

        [Header("접지 판정")]
        [SerializeField] private LayerMask groundMask = ~0;       // TODO(레벨): 지형 전용 레이어 확정 시 지정 (자기 콜라이더 제외 필요)
        [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.5f); // 발밑 오프셋 (중력 반전 시 y 부호 반전)
        [SerializeField] private float groundCheckRadius = 0.1f;

        private Rigidbody2D _body;
        private PlayerStats _stats;
        private CombatEntity _entity;
        private StatusEffectController _statusController;
        private BasicAttacker _attacker;     // 좌클릭 → 직업별 기본 공격 (Jobs가 프로파일 주입)
        private SkillCaster _skillCaster;    // Q → 직업 스킬 (계약 §4 — 쿨타임/침묵 게이트는 SkillCaster 소관)

        private IPlayerInput _input;
        private float _moveAxis;
        private bool _runHeld;
        private bool _jumpHeld;
        private int _facingSign = 1;         // +1 오른쪽 / -1 왼쪽 (Art.CharacterVisual flipX 연동 예정)
        private bool _gravityInverted;       // 밈 규칙 '중력 반전' 상태 // SYNC: 호스트 권위
        private float _baseGravityScale = 1f;
        private Vector2 _lastBodyPosition;

        // 조작감 타이머 — Update에서 갱신, FixedUpdate에서 소비
        private float _coyoteTimer;          // 발판을 떠난 뒤 남은 점프 유예
        private float _jumpBufferTimer;      // 착지 전에 누른 점프 기억
        private bool _isJumping;             // 상승 중인 점프가 진행 중인가 (가변 높이 판정용)

        // 대쉬 상태
        private float _dashTimer;            // 남은 대쉬 지속 시간
        private float _dashCooldownTimer;
        private int _airDashesLeft;
        private int _dashDirection = 1;
        private bool _dashQueued;

        // ── 외부 조회 ─────────────────────────────────────────────
        /// <summary>입력 소스. PlayerInteraction/PingEmitter/EmoteWheel이 공유 폴링한다.</summary>
        public IPlayerInput InputSource => _input;

        /// <summary>플레이어 ID (CombatEntity.OwnerPlayerId 위임, 미설정 시 -1).</summary>
        public int PlayerId => _entity != null ? _entity.OwnerPlayerId : -1;

        /// <summary>바라보는 방향 부호 (+1 오른쪽 / -1 왼쪽).</summary>
        public int FacingSign => _facingSign;

        public bool IsGravityInverted => _gravityInverted;

        /// <summary>대쉬 중인가 (연출·무적·애니메이션 판정용).</summary>
        public bool IsDashing => _dashTimer > 0f;

        /// <summary>대쉬 쿨타임 진행률 0(방금 씀)~1(사용 가능) — HUD 표시용.</summary>
        public float DashCooldownProgress =>
            dashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(_dashCooldownTimer / dashCooldown);

        /// <summary>중력 기준 '위' 방향 부호. 중력 반전 시 -1이 된다.</summary>
        private float UpSign => _gravityInverted ? -1f : 1f;

        /// <summary>이동 가능 게이트 — 사망 + 상태이상(속박/기절/빙결) 종합. 이동·점프가 공용으로 사용.</summary>
        public bool CanMove =>
            (_entity == null || !_entity.IsDead)
            && (_statusController == null || _statusController.CanMove);

        /// <summary>이동속도 — Core.StatCollection(StatType.MoveSpeed) 조회. 아이템 modifier가 즉시 반영된다.</summary>
        public float MoveSpeed => _stats != null ? _stats.GetValue(StatType.MoveSpeed) : fallbackMoveSpeed;

        /// <summary>접지 여부 — 발밑 원 판정. 중력 반전 시 머리 위를 검사한다.</summary>
        public bool IsGrounded
        {
            get
            {
                Vector2 offset = groundCheckOffset;
                if (_gravityInverted) offset.y = -offset.y;
                return Physics2D.OverlapCircle(_body.position + offset, groundCheckRadius, groundMask) != null;
            }
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _stats = GetComponent<PlayerStats>();
            _entity = GetComponent<CombatEntity>();
            _statusController = GetComponent<StatusEffectController>();
            _attacker = GetComponent<BasicAttacker>();
            _skillCaster = GetComponent<SkillCaster>();

            _input = new LegacyPlayerInput(); // 기본 로컬 입력 — SetInputSource로 교체 가능
            _baseGravityScale = Mathf.Approximately(_body.gravityScale, 0f) ? 1f : Mathf.Abs(_body.gravityScale);
            _lastBodyPosition = _body.position;
        }

        /// <summary>입력 소스 교체 (테스트 더미/네트워크 원격 입력). null이면 무시.</summary>
        public void SetInputSource(IPlayerInput input)
        {
            if (input != null) _input = input;
        }

        private void Update()
        {
            if (_input == null) return;

            float dt = Time.deltaTime;

            // ── 이동 입력 수집 (물리 적용은 FixedUpdate) ──
            _moveAxis = Mathf.Clamp(_input.MoveAxis, -1f, 1f);
            _runHeld = _input.RunHeld;
            _jumpHeld = _input.JumpHeld;

            // ── 조작감 타이머 ──
            // 코요테 타임: 접지 중엔 가득 채우고, 공중에선 줄어든다 → 발판을 막 벗어나도 잠시 점프 가능.
            if (IsGrounded)
            {
                _coyoteTimer = coyoteTime;
                _airDashesLeft = airDashCount; // 착지 시 공중 대쉬 회복
                if (_body.linearVelocity.y * UpSign <= 0.01f) _isJumping = false;
            }
            else
            {
                _coyoteTimer -= dt;
            }

            // 점프 버퍼: 착지 직전에 누른 점프를 기억했다가 착지 순간 발동.
            if (_input.JumpPressed) _jumpBufferTimer = jumpBufferTime;
            else _jumpBufferTimer -= dt;

            // 가변 점프 높이: 상승 중에 키를 떼면 즉시 상승을 잘라 낮게 뛴다.
            if (_isJumping && !_jumpHeld && _body.linearVelocity.y * UpSign > 0f)
            {
                Vector2 v = _body.linearVelocity;
                v.y *= jumpCutMultiplier;
                _body.linearVelocity = v;
                _isJumping = false;
            }

            // ── 대쉬 타이머 ──
            if (_dashTimer > 0f) _dashTimer -= dt;
            if (_dashCooldownTimer > 0f) _dashCooldownTimer -= dt;
            if (_input.DashPressed) _dashQueued = true;

            // 바라보는 방향 갱신 (변조 전 원 입력 기준 — 혼란 중에도 의도 방향을 바라본다)
            if (_moveAxis > 0.01f) _facingSign = 1;
            else if (_moveAxis < -0.01f) _facingSign = -1;
            // TODO(연출): Art.CharacterVisual flipX 연동 — 좌우 반전 스프라이트.

            // ── 전투 입력 (사망 중 차단 — 즉시부활 전제라 짧은 구간) ──
            if (_entity != null && _entity.IsDead) return;

            if (_input.AttackPressed)
                _attacker?.TryAttack(GetAimDirection()); // 간격/기절·빙결 게이트는 BasicAttacker 소관

            if (_input.SkillPressed)
                _skillCaster?.TryCastSkill();            // 쿨타임/침묵 게이트는 SkillCaster 소관
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // ── 대쉬 발동 판정 ──
            if (_dashQueued)
            {
                _dashQueued = false;
                TryStartDash();
            }

            // 대쉬 중에는 일반 이동/중력 로직을 건너뛰고 고정 속도로 밀어낸다.
            if (IsDashing)
            {
                Vector2 dashVelocity = new Vector2(_dashDirection * dashSpeed, 0f);
                if (dashIgnoresGravity)
                    _body.gravityScale = 0f; // 물리 단계에서 중력이 누적되지 않도록 완전히 차단
                else
                    dashVelocity.y = _body.linearVelocity.y;

                _body.linearVelocity = dashVelocity;

                TrackMovedDistance();
                return;
            }

            // ── 수평 이동 (가속 기반) ──
            // 상태이상 변조 경유: 공포(전체 반전)/혼란(x축 반전)/이동 차단 CC(0 벡터) — StatusEffectController 계약.
            Vector2 desired = new Vector2(_moveAxis, 0f);
            if (_statusController != null) desired = _statusController.MoveInputModifier(desired);
            if (_entity != null && _entity.IsDead) desired = Vector2.zero;

            float speed = MoveSpeed;
            if (_runHeld) speed *= runSpeedMultiplier; // 스태미나 없음 — 게이트 없이 상시 적용
            if (_statusController != null) speed *= _statusController.GetMoveSpeedMultiplier(); // 감전/둔화

            float targetSpeed = desired.x * speed;
            bool grounded = IsGrounded;
            bool accelerating = Mathf.Abs(targetSpeed) > 0.01f;

            // 즉시 대입 대신 가속/감속으로 접근 — 둔함(가속 부족)과 미끄러짐(감속 부족)을 각각 조절한다.
            float rate = grounded
                ? (accelerating ? groundAcceleration : groundDeceleration)
                : (accelerating ? airAcceleration : airDeceleration);

            Vector2 velocity = _body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, rate * dt);
            // TODO(물리): 넉백 임펄스(Combat.KnockbackInfo)와 직접 속도 대입 간 간섭 — 넉백 중 입력 가속 보류 검토.
            _body.linearVelocity = velocity;

            // ── 점프 (코요테 타임 + 버퍼, 중력 반전 대응) ──
            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f && CanMove)
            {
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;      // 연속 발동 방지
                _isJumping = true;

                Vector2 v = _body.linearVelocity;
                v.y = jumpSpeed * UpSign;
                _body.linearVelocity = v;
            }

            ApplyGravityFeel();

            TrackMovedDistance();
        }

        /// <summary>
        /// 중력 체감 보정. 하강할 때 중력을 키워 '붕 뜨는' 느낌을 없애고,
        /// 정점 부근에서는 살짝 줄여 공중 제어 여유를 준다.
        /// </summary>
        private void ApplyGravityFeel()
        {
            float verticalSpeed = _body.linearVelocity.y * UpSign; // 양수 = 상승
            float multiplier = 1f;

            if (verticalSpeed < -0.01f)
            {
                multiplier = fallGravityMultiplier;              // 하강 — 빠르게 떨어진다
            }
            else if (Mathf.Abs(verticalSpeed) < apexThreshold && !IsGrounded)
            {
                multiplier = apexGravityMultiplier;              // 정점 — 잠깐 체공
            }

            _body.gravityScale = _baseGravityScale * multiplier * UpSign;
        }

        /// <summary>이동 거리 훅 (출혈: 움직일수록 추가 피해 — StatusEffects.OnMoved).</summary>
        private void TrackMovedDistance()
        {
            float moved = Vector2.Distance(_body.position, _lastBodyPosition);
            _lastBodyPosition = _body.position;
            if (moved > 0f) _statusController?.OnMoved(moved);
            // NOTE(기획 확인 필요): 낙하/넉백에 의한 이동도 출혈 피해에 포함할지 — 우선 전체 이동 거리 산정.
        }

        // ── 대쉬 ──────────────────────────────────────────────────

        /// <summary>
        /// 대쉬 시작 시도. 쿨타임·이동 가능 여부·공중 횟수를 검사한다.
        /// 방향은 입력이 있으면 입력 방향, 없으면 바라보는 방향.
        /// </summary>
        private void TryStartDash()
        {
            if (IsDashing || _dashCooldownTimer > 0f) return;
            if (!CanMove) return; // 기절·빙결·속박 중 대쉬 불가

            bool grounded = IsGrounded;
            if (!grounded)
            {
                if (_airDashesLeft <= 0) return;
                _airDashesLeft--;
            }

            _dashDirection = Mathf.Abs(_moveAxis) > 0.01f ? (int)Mathf.Sign(_moveAxis) : _facingSign;
            _dashTimer = dashDuration;
            _dashCooldownTimer = dashCooldown;

            // 대쉬 중 낙하 누적 속도 제거 — 수평으로 곧게 나가도록
            if (dashIgnoresGravity)
            {
                Vector2 v = _body.linearVelocity;
                v.y = 0f;
                _body.linearVelocity = v;
            }

            if (dashInvincibility > 0f)
                _entity?.SetInvincibleFor(dashInvincibility);

            // TODO(연출): 잔상·먼지 파티클·대쉬 효과음 — Art 담당.
        }

        // ── 중력 반전 (밈 모드 규칙 훅) ──────────────────────────
        /// <summary>
        /// 밈 모드 '중력이 반전된다' 규칙 적용/해제. gravityScale 부호만 뒤집는다 (스펙 unityNotes ⑦).
        /// 밈 규칙 SO(GameFlow 소관)가 런 시작 시 호출한다.
        /// </summary>
        public void SetGravityInverted(bool inverted)
        {
            _gravityInverted = inverted;
            _body.gravityScale = _baseGravityScale * (inverted ? -1f : 1f);
            // TODO(연출): 상하 반전 스프라이트/착지 애니메이션 — Art.CharacterVisual 소관.
        }

        // ── 조준 ─────────────────────────────────────────────────
        /// <summary>
        /// 조준 방향 — 마우스 월드 위치 기준, 카메라 부재 시 바라보는 방향 폴백.
        /// TODO: Input System 교체 시 조준 입력도 입력 계층으로 이동 (IPlayerInput은 계약 8속성 고정이라 우선 여기서 처리).
        /// NOTE(기획 확인 필요): 마우스 조준 여부 문서 미정 — 설정에 '마우스 감도'가 있어 마우스 조준 전제로 구현.
        /// </summary>
        public Vector2 GetAimDirection()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                Vector2 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
                Vector2 dir = mouseWorld - _body.position;
                if (dir.sqrMagnitude > 0.0001f) return dir.normalized;
            }
            return new Vector2(_facingSign, 0f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 접지 판정 시각화 (중력 반전 상태 반영)
            Vector2 offset = groundCheckOffset;
            if (_gravityInverted) offset.y = -offset.y;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere((Vector2)transform.position + offset, groundCheckRadius);
        }
#endif
    }
}
