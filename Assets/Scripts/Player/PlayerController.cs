// 근거: 조작과 시스템.md — A/D 이동, Space 점프, LShift 달리기(스태미나 없음, 이동속도 증가만), 좌클릭 기본 공격, Q 스킬.
// 근거: 게임 시작과 선택, 직업, 플레이.md — 밈 모드 '중력이 반전된다' → Rigidbody2D.gravityScale 부호 반전 대응.
// 근거: 상태이상 시스템.md — 이동은 StatusEffectController의 CanMove / MoveInputModifier(공포·혼란) / 이동속도 배율만 질의,
//       이동 거리는 OnMoved 훅으로 전달 (출혈: 움직일수록 추가 피해).
// 근거: 전투 시스템.md — 사망 시 즉시 부활(공유 부활 1회 소모). 사망/부활 판정은 Combat.CombatEntity 소관이며
//       이 컴포넌트는 IsDead / Died 이벤트만 참조해 조작을 정지·복구한다 (부활 횟수 판정 중복 금지 — ARCHITECTURE.md §5).
// 근거: 도트 시스템.md / 이펙트 — 착지·점프·대쉬 잔상은 Art.VfxSpawner 경유. 씬에 이펙트 시스템이 없어도 조용히 생략된다.
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
        [SerializeField] private float coyoteTime = 0.12f;
        [Tooltip("착지 직전에 누른 점프를 기억하는 시간(초). 착지 순간 자동 발동한다.")]
        [SerializeField] private float jumpBufferTime = 0.1f;
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

        [Header("낙하 / 벽")]
        [Tooltip("낙하 속도 상한(초당). 너무 빨리 떨어져 화면 밖으로 사라지거나 얇은 지형을 통과하는 것을 막는다. 0 이하면 제한 없음.")]
        [SerializeField] private float maxFallSpeed = 22f;        // TODO(밸런스): 문서 미정
        // NOTE(문서 갱신 필요): 벽 슬라이드는 조작과 시스템.md v1.1의 조작/이동 규칙에 없는 신규 기능이다.
        //   기획 확정 전까지 기본 비활성으로 두고, 켠 팀만 체험할 수 있게 한다.
        [Tooltip("공중에서 벽에 밀착하면 낙하가 느려진다. 문서 미확정 기능이라 기본 꺼짐.")]
        [SerializeField] private bool enableWallSlide = false;
        [Tooltip("벽 슬라이드 중 최대 낙하 속도(초당).")]
        [SerializeField] private float wallSlideSpeed = 3f;       // TODO(밸런스): 문서 미정
        [Tooltip("벽 판정 지점 오프셋. x는 바라보는/입력 방향으로 부호가 뒤집힌다.")]
        [SerializeField] private Vector2 wallCheckOffset = new Vector2(0.45f, 0f);
        [SerializeField] private float wallCheckRadius = 0.12f;

        [Header("피격 반응")]
        [Tooltip("피격 직후 조작 불가 시간(초). 길면 답답하므로 상한을 0.1초로 강제한다.")]
        [Range(0f, 0.1f)][SerializeField] private float hitstunDuration = 0.08f; // TODO(밸런스): 문서 미정
        // NOTE(기획 확인 필요): 피격 시 대쉬 쿨타임 초기화 여부 — '반격 회피 기회 제공'(켬) vs '대쉬 남용 방지'(끔).
        //   게임 성경.md "모든 강력한 능력에는 반드시 위험이 따른다"에 따라 기본은 끔.
        [SerializeField] private bool resetDashCooldownOnHit = false;

        [Header("숨은 보정 (Game Feel)")]
        // 근거: 숨겨진 보정 설계 원칙 — 실력을 대체하지 않고 입력 의도만 보조한다. 값은 보수적으로,
        //   플레이어가 보정의 존재를 눈치채지 못할 만큼 자연스럽게 동작해야 한다.
        [Tooltip("공격 입력을 기억하는 시간(초). 공격 간격·피격 경직 중에 누른 공격이 가능해지는 순간 자동 발동한다.")]
        [SerializeField] private float attackBufferTime = 0.25f;
        [Tooltip("대쉬 입력을 기억하는 시간(초). 쿨타임·피격 경직 중에 누른 대쉬가 가능해지는 순간 즉시 발동한다.")]
        [SerializeField] private float dashBufferTime = 0.15f;
        [Tooltip("발끝 좌우 추가 판정 폭. 플랫폼 끝에 발끝만 걸쳐도 접지로 인정한다(중앙+좌우 3점 판정). 0 이하면 중앙 1점만 검사.")]
        [SerializeField] private float groundCheckExtent = 0.18f;
        [Tooltip("모서리 보정 — 점프가 플랫폼 립(모서리)에 아슬하게 걸렸을 때 위로 소량씩 밀어 올려 자연스럽게 올라서게 한다.")]
        [SerializeField] private bool enableLedgeCorrection = true;
        [Tooltip("모서리 보정이 허용하는 립 높이(발 기준). 발 높이엔 벽이 닿고 이 높이엔 안 닿을 때만 보정한다.")]
        [SerializeField] private float ledgeCorrectionHeight = 0.3f;
        [Tooltip("모서리 보정의 초당 상승 상한. 낮을수록 티가 안 나고, 높을수록 빠르게 올라선다.")]
        [SerializeField] private float ledgeCorrectionSpeed = 8f;
        [Tooltip("이 낙하 속도(초당)보다 빠르게 떨어지는 중에는 모서리 보정을 하지 않는다 — 낙하 의도를 거스르면 티가 난다.")]
        [SerializeField] private float ledgeCorrectionMaxFall = 2f;

        [Header("이동 연출 훅 (Art.VfxSpawner 없으면 조용히 생략)")]
        [Tooltip("이동 관련 이펙트 재생 여부. 끄면 착지/점프/대쉬 이펙트를 전부 생략한다.")]
        [SerializeField] private bool playMovementVfx = true;
        [Tooltip("착지 먼지를 재생할 최소 낙하 속도. 이보다 느리게 착지하면 먼지를 내지 않는다.")]
        [SerializeField] private float landDustMinFallSpeed = 4f; // TODO(밸런스): 문서 미정
        [Tooltip("대쉬 잔상 재생 간격(초). 0 이하면 대쉬 시작 시 1회만 재생한다.")]
        [SerializeField] private float dashTrailInterval = 0.04f; // TODO(밸런스): 문서 미정

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
        private float _attackBufferTimer;    // 공격 간격·경직 중에 누른 공격 기억 (Update에서 충전·소비)
        private bool _isJumping;             // 상승 중인 점프가 진행 중인가 (가변 높이 판정용)

        /// <summary>접지 판정 버퍼 — 매 프레임 할당을 피한다.</summary>
        private readonly Collider2D[] _groundHits = new Collider2D[8];
        private ContactFilter2D _groundFilter;

        /// <summary>벽 판정 버퍼 — 벽 슬라이드가 꺼져 있으면 질의 자체를 하지 않는다.</summary>
        private readonly Collider2D[] _wallHits = new Collider2D[4];
        private ContactFilter2D _wallFilter;

        // 접지 캐시 — 감사 #28: 프로퍼티가 프레임당 4회 이상 물리 질의를 하던 문제.
        // Update / FixedUpdate 시작 시 각 1회만 계산하고 그 값을 프레임 내내 공유한다.
        private bool _isGrounded;
        private bool _groundedLastFixed;     // 직전 물리 프레임의 접지 상태 (착지/이륙 전이 감지)
        private float _fallSpeedLastFixed;   // 직전 물리 프레임의 낙하 속도(양수) — 착지 먼지 여부 판정

        // 대쉬 상태
        private float _dashTimer;            // 남은 대쉬 지속 시간
        private float _dashCooldownTimer;
        private int _airDashesLeft;
        private int _dashDirection = 1;
        private float _dashBufferTimer;      // 대쉬 입력 버퍼 — FixedUpdate가 성공할 때까지 재시도 (쿨타임/경직 해제 순간 즉시 발동)
        private float _dashTrailTimer;       // 대쉬 잔상 재생 간격 타이머

        // 피격/사망 상태
        private float _hitstunTimer;         // 피격 경직 잔여 시간 (짧게 — 0.1초 상한)
        private bool _wallSliding;
        private bool _deadLatch;             // 사망 처리 완료 표시 — IsDead가 다시 false가 되면 부활로 간주

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

        /// <summary>피격 경직 중인가 (0.1초 이하의 짧은 조작 불가 — 애니메이션/연출 판정용).</summary>
        public bool IsHitstunned => _hitstunTimer > 0f;

        /// <summary>벽 슬라이드 중인가 (기본 비활성 기능 — Art 연출 판정용).</summary>
        public bool IsWallSliding => _wallSliding;

        /// <summary>대쉬 쿨타임 진행률 0(방금 씀)~1(사용 가능) — HUD 표시용.</summary>
        public float DashCooldownProgress =>
            dashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(_dashCooldownTimer / dashCooldown);

        /// <summary>중력 기준 '위' 방향 부호. 중력 반전 시 -1이 된다.</summary>
        private float UpSign => _gravityInverted ? -1f : 1f;

        /// <summary>이동 가능 게이트 — 사망 + 상태이상(속박/기절/빙결) 종합. 이동·점프가 공용으로 사용.</summary>
        public bool CanMove =>
            (_entity == null || !_entity.IsDead)
            && (_statusController == null || _statusController.CanMove);

        /// <summary>조작 가능 게이트 — CanMove + 피격 경직. 점프/대쉬/공격 입력이 공용으로 사용한다.</summary>
        public bool CanControl => CanMove && !IsHitstunned;

        /// <summary>이동속도 — Core.StatCollection(StatType.MoveSpeed) 조회. 아이템 modifier가 즉시 반영된다.</summary>
        public float MoveSpeed => _stats != null ? _stats.GetValue(StatType.MoveSpeed) : fallbackMoveSpeed;

        /// <summary>
        /// 접지 여부 — Update / FixedUpdate 시작 시 1회 계산한 캐시 값.
        /// (감사 #28: 프로퍼티가 매번 물리 질의를 해 프레임당 4회 이상 호출되던 문제 — 8인이면 부담이 크다)
        /// </summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>접지 판정 지점 — 발밑. 중력 반전 시 머리 위가 된다.</summary>
        private Vector2 GroundCheckPoint()
        {
            Vector2 offset = groundCheckOffset;
            if (_gravityInverted) offset.y = -offset.y;
            return _body.position + offset;
        }

        /// <summary>
        /// 실제 접지 물리 질의 — 중앙 + 좌우 발끝 3점 판정 (착지 보조).
        /// 플랫폼 끝에 발끝만 걸쳐도 접지로 인정해 코요테/점프/대쉬 회복이 자연스러워진다.
        /// 중앙이 닿으면 조기 반환해 추가 물리 질의를 아낀다 (대부분의 프레임은 1회 질의로 끝난다).
        /// 자기 콜라이더는 제외하므로, groundMask에 캐릭터 레이어를 포함시켜
        /// 적을 밟고 있을 때도 정상적으로 접지로 인식할 수 있다.
        /// </summary>
        private bool ProbeGround()
        {
            _groundFilter.SetLayerMask(groundMask);
            _groundFilter.useLayerMask = true;
            _groundFilter.useTriggers = false; // 트리거는 밟을 수 없다

            Vector2 center = GroundCheckPoint();
            if (ProbeGroundAt(center)) return true;          // 중앙 우선 — 조기 반환
            if (groundCheckExtent <= 0f) return false;

            Vector2 extent = new Vector2(groundCheckExtent, 0f);
            return ProbeGroundAt(center - extent) || ProbeGroundAt(center + extent);
        }

        /// <summary>단일 지점 접지 질의. _groundFilter는 ProbeGround가 미리 설정한다.</summary>
        private bool ProbeGroundAt(Vector2 point)
        {
            int count = Physics2D.OverlapCircle(point, groundCheckRadius, _groundFilter, _groundHits);

            for (int i = 0; i < count; i++)
            {
                var hit = _groundHits[i];
                if (hit == null) continue;

                // 자기 자신(및 자식 콜라이더)은 접지 대상이 아니다.
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                return true;
            }

            return false;
        }

        /// <summary>
        /// 벽 밀착 판정 (벽 슬라이드 + 모서리 보정 공용). heightOffset은 판정 지점 y 추가 오프셋
        /// (모서리 보정이 발 높이/립 높이 두 지점을 검사할 때 사용 — 부호는 호출부가 UpSign으로 조정).
        /// 두 기능이 모두 꺼져 있으면 호출되지 않으므로 기본 상태에서는 추가 물리 질의가 발생하지 않는다.
        /// </summary>
        private bool ProbeWall(int dirSign, float heightOffset = 0f)
        {
            _wallFilter.SetLayerMask(groundMask); // 벽도 지형 — 별도 마스크가 생기면 분리한다
            _wallFilter.useLayerMask = true;
            _wallFilter.useTriggers = false;

            Vector2 point = _body.position + new Vector2(wallCheckOffset.x * dirSign, wallCheckOffset.y + heightOffset);
            int count = Physics2D.OverlapCircle(point, wallCheckRadius, _wallFilter, _wallHits);

            for (int i = 0; i < count; i++)
            {
                var hit = _wallHits[i];
                if (hit == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                // 적/타 플레이어의 몸통은 벽·립이 아니다 — groundMask가 캐릭터 레이어(Default)를 포함해도
                // 엔티티 콜라이더는 제외한다. 이게 없으면 공중에서 적 방향으로 밀기만 해도 모서리 보정이
                // 적의 상단을 '립'으로 오인해 적 머리 위로 밀어 올린다 (접지의 '적 밟기' 지원과는 무관한 경로).
                if (hit.GetComponentInParent<CombatEntity>() != null) continue;

                return true;
            }
            return false;
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

        // 피격/사망은 폴링 대신 CombatEntity의 C# event를 구독한다 (ARCHITECTURE.md §3-8).
        // 부활은 CombatEntity에 전용 이벤트가 없으므로 IsDead 플래그 전이로 감지한다(아래 Update).
        private void OnEnable()
        {
            if (_entity == null) return;
            _entity.Damaged += OnEntityDamaged;
            _entity.Died += OnEntityDied;
        }

        private void OnDisable()
        {
            if (_entity == null) return;
            _entity.Damaged -= OnEntityDamaged;
            _entity.Died -= OnEntityDied;
        }

        /// <summary>입력 소스 교체 (테스트 더미/네트워크 원격 입력). null이면 무시.</summary>
        public void SetInputSource(IPlayerInput input)
        {
            if (input != null) _input = input;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 부활 감지 — Died 이후 IsDead가 다시 false가 되면 조작을 복구한다.
            // (CombatEntity.Revive는 Died 호출 스택 안에서 즉시 실행될 수 있어, 다음 프레임에 확인한다)
            if (_deadLatch && (_entity == null || !_entity.IsDead))
                OnRevived();

            // 접지 캐시 갱신 — 이번 프레임의 모든 IsGrounded 조회가 이 값을 공유한다.
            _isGrounded = ProbeGround();

            if (_hitstunTimer > 0f) _hitstunTimer -= dt;

            if (_input == null) return;

            // 맵 인트로(시네마틱) 중에는 조작을 받지 않는다.
            // 스킵도 아무 키나 누르는 방식이라, 그 입력이 이동·공격으로 새어 나가면 안 된다.
            if (TSWP.Map.MapIntroManager.Instance != null && TSWP.Map.MapIntroManager.Instance.IsPlaying)
            {
                _moveAxis = 0f;
                _runHeld = false;
                _jumpHeld = false;
                _jumpBufferTimer = 0f;
                _attackBufferTimer = 0f; // 스킵 입력이 버퍼에 남아 인트로 종료 직후 발동되면 안 된다
                _dashBufferTimer = 0f;
                return;
            }

            // ── 사망 게이트 (구조부활 대비) ──
            // 사망 중에는 버퍼·코요테를 충전하지 않는다. 소비는 FixedUpdate에, 부활 정리(OnRevived)는 Update에
            // 있어 구조부활(E키)이 이 컴포넌트의 Update 이후에 실행되면 다음 프레임 FixedUpdate가 정리보다
            // 먼저 돌므로, 사망 중 눌린 입력이 부활 즉시 대쉬/점프로 터질 수 있다 — 충전 자체를 막아
            // 스크립트 실행 순서 의존을 없앤다 (기본값 즉시부활은 사망 프레임이 없어 영향 없음).
            if (_entity != null && _entity.IsDead)
            {
                _moveAxis = 0f;
                _runHeld = false;
                _jumpHeld = false;
                return;
            }

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

            // ── 대쉬 타이머 / 입력 버퍼 ──
            if (_dashTimer > 0f) _dashTimer -= dt;
            if (_dashCooldownTimer > 0f) _dashCooldownTimer -= dt;

            // 대쉬 버퍼: 쿨타임·경직 중에 누른 대쉬를 기억했다가 가능해지는 순간 발동 (소비는 FixedUpdate).
            if (_input.DashPressed) _dashBufferTimer = dashBufferTime;
            else _dashBufferTimer -= dt;

            // 공격 버퍼: 공격 간격·경직 중에 누른 공격을 기억한다 (소비는 아래 전투 입력 구간).
            // 게이트(사망/경직)에 막힌 프레임에도 충전은 유지된다 — 경직이 풀리는 순간 발동시키기 위함.
            if (_input.AttackPressed) _attackBufferTimer = attackBufferTime;
            else _attackBufferTimer -= dt;

            // 바라보는 방향 갱신 (변조 전 원 입력 기준 — 혼란 중에도 의도 방향을 바라본다)
            if (_moveAxis > 0.01f) _facingSign = 1;
            else if (_moveAxis < -0.01f) _facingSign = -1;
            // TODO(연출): Art.CharacterVisual flipX 연동 — 좌우 반전 스프라이트.

            // ── 전투 입력 (사망 중 차단 — 즉시부활 전제라 짧은 구간, 피격 경직 중에도 차단) ──
            if (_entity != null && _entity.IsDead) return;
            if (IsHitstunned) return;

            // 공격 버퍼 소비: TryAttack이 true(실제 발동)를 반환해야 비운다 — 간격/기절·빙결 게이트는 BasicAttacker 소관.
            // 조준(GetAimDirection)은 발동 시점에 다시 계산해 버퍼 중 바뀐 최신 조준 의도를 반영한다.
            if (_attackBufferTimer > 0f && _attacker != null && _attacker.TryAttack(GetAimDirection()))
                _attackBufferTimer = 0f;

            if (_input.SkillPressed)
                _skillCaster?.TryCastSkill();            // 쿨타임/침묵 게이트는 SkillCaster 소관 — 스킬은 버퍼 대상 아님
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // 접지 캐시 갱신 (물리 프레임 1회) 후 착지/이륙 전이 연출 처리.
            _isGrounded = ProbeGround();
            HandleGroundTransition();

            // ── 대쉬 발동 판정 (버퍼 소비) ──
            // 실패(쿨타임/경직/공중 횟수 소진)해도 버퍼가 남아 있는 동안 매 물리 프레임 재시도한다
            // → 쿨타임이 끝나는 순간·공격 경직이 풀리는 순간 즉시 발동된다.
            if (_dashBufferTimer > 0f && TryStartDash())
                _dashBufferTimer = 0f;

            // 대쉬 중에는 일반 이동/중력 로직을 건너뛰고 고정 속도로 밀어낸다.
            if (IsDashing)
            {
                Vector2 dashVelocity = new Vector2(_dashDirection * dashSpeed, 0f);
                if (dashIgnoresGravity)
                    _body.gravityScale = 0f; // 물리 단계에서 중력이 누적되지 않도록 완전히 차단
                else
                    dashVelocity.y = _body.linearVelocity.y;

                _body.linearVelocity = dashVelocity;

                UpdateDashTrail(dt);
                _wallSliding = false;
                TrackMovedDistance();
                EndPhysicsFrame();
                return;
            }

            // ── 수평 이동 (가속 기반) ──
            // 상태이상 변조 경유: 공포(전체 반전)/혼란(x축 반전)/이동 차단 CC(0 벡터) — StatusEffectController 계약.
            // 피격 경직 중에는 수평 속도에 아예 손대지 않는다 — 넉백 임펄스를 감속으로 지우지 않기 위함.
            if (!IsHitstunned)
            {
                Vector2 desired = new Vector2(_moveAxis, 0f);
                if (_statusController != null) desired = _statusController.MoveInputModifier(desired);
                if (_entity != null && _entity.IsDead) desired = Vector2.zero;

                float speed = MoveSpeed;
                if (_runHeld) speed *= runSpeedMultiplier; // 스태미나 없음 — 게이트 없이 상시 적용
                if (_statusController != null) speed *= _statusController.GetMoveSpeedMultiplier(); // 감전/둔화

                float targetSpeed = desired.x * speed;
                bool accelerating = Mathf.Abs(targetSpeed) > 0.01f;

                // 즉시 대입 대신 가속/감속으로 접근 — 둔함(가속 부족)과 미끄러짐(감속 부족)을 각각 조절한다.
                float rate = _isGrounded
                    ? (accelerating ? groundAcceleration : groundDeceleration)
                    : (accelerating ? airAcceleration : airDeceleration);

                Vector2 velocity = _body.linearVelocity;
                velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, rate * dt);
                // TODO(물리): 넉백 임펄스(Combat.KnockbackInfo)와 직접 속도 대입 간 간섭 — 넉백 중 입력 가속 보류 검토.
                _body.linearVelocity = velocity;
            }

            // ── 점프 (코요테 타임 + 버퍼, 중력 반전 대응) ──
            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f && CanControl)
            {
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;      // 연속 발동 방지
                _isJumping = true;

                Vector2 v = _body.linearVelocity;
                v.y = jumpSpeed * UpSign;
                _body.linearVelocity = v;

                PlayVfx(Art.VfxId.JumpDust, GroundCheckPoint()); // 발밑에서 먼지가 튄다
            }

            ApplyLedgeCorrection(dt);
            ApplyGravityFeel();
            ApplyWallSlide();
            ClampFallSpeed();

            TrackMovedDistance();
            EndPhysicsFrame();
        }

        /// <summary>물리 프레임 마감 — 다음 프레임의 착지 판정에 쓸 상태를 기록한다.</summary>
        private void EndPhysicsFrame()
        {
            _groundedLastFixed = _isGrounded;
            float verticalSpeed = _body.linearVelocity.y * UpSign;
            _fallSpeedLastFixed = verticalSpeed < 0f ? -verticalSpeed : 0f;
        }

        /// <summary>
        /// 착지/이륙 전이 연출. 직전 프레임의 낙하 속도로 착지 강도를 판단한다
        /// (착지한 프레임에는 이미 속도가 0으로 눌려 있어 현재 속도로는 판정할 수 없다).
        /// </summary>
        private void HandleGroundTransition()
        {
            if (_isGrounded == _groundedLastFixed) return;

            if (_isGrounded)
            {
                _airDashesLeft = airDashCount; // 착지 시 공중 대쉬 회복
                if (_fallSpeedLastFixed >= landDustMinFallSpeed)
                    PlayVfx(Art.VfxId.LandDust, GroundCheckPoint());
            }
            // 이륙(접지 → 공중) 연출은 점프 발동 지점에서 이미 재생하므로 여기서는 처리하지 않는다
            // (낙하로 발판을 벗어난 경우까지 먼지를 내면 과해진다).
        }

        /// <summary>
        /// 낙하 속도 상한. 지나치게 빨라져 화면 밖으로 사라지거나 얇은 지형을 통과하는 것을 막는다.
        /// 중력 반전 상태에서는 '아래'가 뒤집히므로 UpSign 기준으로 계산한다.
        /// </summary>
        private void ClampFallSpeed()
        {
            if (maxFallSpeed <= 0f) return;

            float verticalSpeed = _body.linearVelocity.y * UpSign; // 음수 = 하강
            if (verticalSpeed >= -maxFallSpeed) return;

            Vector2 v = _body.linearVelocity;
            v.y = -maxFallSpeed * UpSign;
            _body.linearVelocity = v;
        }

        /// <summary>
        /// 벽 슬라이드 — 공중에서 벽 방향으로 입력을 유지하면 낙하가 느려진다.
        /// NOTE(문서 갱신 필요): 조작과 시스템.md에 없는 기능이라 enableWallSlide 기본 false.
        /// </summary>
        private void ApplyWallSlide()
        {
            _wallSliding = false;
            if (!enableWallSlide || _isGrounded || !CanControl) return;

            float verticalSpeed = _body.linearVelocity.y * UpSign;
            if (verticalSpeed >= 0f) return; // 상승 중에는 붙지 않는다

            int pressSign = Mathf.Abs(_moveAxis) > 0.01f ? (int)Mathf.Sign(_moveAxis) : 0;
            if (pressSign == 0 || !ProbeWall(pressSign)) return;

            _wallSliding = true;
            float limit = -Mathf.Abs(wallSlideSpeed);
            if (verticalSpeed < limit)
            {
                Vector2 v = _body.linearVelocity;
                v.y = limit * UpSign;
                _body.linearVelocity = v;
            }
            // TODO(연출): 벽 마찰 먼지 이펙트 — 기능 확정 후 Art와 협의.
        }

        /// <summary>
        /// 모서리 보정 (숨은 보정) — 점프/이동 중 발끝이 플랫폼 립에 아슬하게 걸렸을 때,
        /// 위로 소량씩 밀어 올려 자연스럽게 올라서게 한다.
        /// 발 높이에는 벽이 닿고 발+ledgeCorrectionHeight 높이에는 닿지 않으면 립이 그 사이에 있다고 본다.
        /// 프레임당 이동량을 ledgeCorrectionSpeed * dt로 묶어 순간이동처럼 보이지 않게 한다.
        /// 보정은 위로만 하며(진동 금지), 접지되거나 립을 넘어서면 조건이 깨져 자연 종료된다.
        /// </summary>
        private void ApplyLedgeCorrection(float dt)
        {
            if (!enableLedgeCorrection || _isGrounded || IsDashing || !CanControl) return;

            // 빠르게 떨어지는 중에는 개입하지 않는다 — 낙하 의도를 보정으로 거스르면 티가 난다.
            float verticalSpeed = _body.linearVelocity.y * UpSign; // 음수 = 하강 (중력 반전 대응)
            if (verticalSpeed <= -Mathf.Abs(ledgeCorrectionMaxFall)) return;

            // 벽 방향으로 미는 수평 입력이 있어야 한다 — 실력을 대체하지 않고 입력 의도만 보조한다.
            int pressSign = Mathf.Abs(_moveAxis) > 0.01f ? (int)Mathf.Sign(_moveAxis) : 0;
            if (pressSign == 0) return;

            // 발 높이 벽 질의 지점 — GroundCheckPoint와 동일한 중력 반전 규칙으로 발 쪽 y를 잡는다.
            float footHeight = groundCheckOffset.y * UpSign;
            if (!ProbeWall(pressSign, footHeight)) return;                                  // 발이 벽에 막혀 있고
            if (ProbeWall(pressSign, footHeight + ledgeCorrectionHeight * UpSign)) return;  // 그 위는 뚫려 있어야 립이다

            // 위로 소량씩만 이동 (초당 ledgeCorrectionSpeed 상한). 중력 반전 시 '위'는 아래가 된다.
            float rise = ledgeCorrectionSpeed * dt;

            // 머리 위 여유 확인 — 립 위에 낮은 천장이 있으면 보정을 포기한다.
            // (이 이동은 충돌을 무시하므로 천장과 겹치면 솔버가 매 스텝 되밀어 끼임/진동이 생긴다)
            // 머리 높이는 발 오프셋의 대칭 지점으로 근사하고, dirSign 0으로 수직 지점만 검사한다.
            if (ProbeWall(0, (Mathf.Abs(groundCheckOffset.y) + rise) * UpSign)) return;

            // _body.position 직접 대입은 그 스텝의 보간(Interpolate)을 리셋해 화면이 한 프레임 튄다 —
            // MovePosition은 보간을 유지한 채 다음 물리 스텝에 이동을 반영한다 (숨은 보정이 티 나면 안 된다).
            _body.MovePosition(_body.position + new Vector2(0f, rise * UpSign));
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
        /// 대쉬 시작 시도. 쿨타임·이동 가능 여부·공중 횟수를 검사하고, 실제로 발동했으면 true.
        /// 실패 시 대쉬 버퍼가 남아 있는 동안 FixedUpdate가 재시도한다 (버퍼 소비 판정용 반환값).
        /// 방향은 입력이 있으면 입력 방향, 없으면 바라보는 방향.
        /// </summary>
        private bool TryStartDash()
        {
            if (IsDashing || _dashCooldownTimer > 0f) return false;
            if (!CanControl) return false; // 기절·빙결·속박·피격 경직 중 대쉬 불가

            if (!_isGrounded)
            {
                if (_airDashesLeft <= 0) return false;
                _airDashesLeft--;
            }

            _dashDirection = Mathf.Abs(_moveAxis) > 0.01f ? (int)Mathf.Sign(_moveAxis) : _facingSign;
            _dashTimer = dashDuration;
            _dashCooldownTimer = dashCooldown;
            _dashTrailTimer = 0f; // 시작 즉시 첫 잔상을 남긴다

            // 대쉬 중 낙하 누적 속도 제거 — 수평으로 곧게 나가도록
            if (dashIgnoresGravity)
            {
                Vector2 v = _body.linearVelocity;
                v.y = 0f;
                _body.linearVelocity = v;
            }

            if (dashInvincibility > 0f)
                _entity?.SetInvincibleFor(dashInvincibility);

            // TODO(연출): 대쉬 효과음 — Audio 담당. 잔상은 UpdateDashTrail이 처리한다.
            return true;
        }

        /// <summary>
        /// 대쉬 잔상 — 일정 간격으로 VfxId.DashTrail을 현재 위치에 남긴다.
        /// 이펙트 시스템이 씬에 없으면 조용히 생략된다(게임 로직 영향 없음).
        /// </summary>
        private void UpdateDashTrail(float dt)
        {
            if (dashTrailInterval <= 0f)
            {
                // 간격이 0 이하면 대쉬 시작 프레임에만 1회 재생한다.
                if (_dashTrailTimer <= 0f)
                {
                    PlayVfx(Art.VfxId.DashTrail, transform.position);
                    _dashTrailTimer = 1f; // 재생 완료 표시(다시 0이 되는 건 대쉬 시작 시점뿐)
                }
                return;
            }

            _dashTrailTimer -= dt;
            if (_dashTrailTimer > 0f) return;

            _dashTrailTimer = dashTrailInterval;
            PlayVfx(Art.VfxId.DashTrail, transform.position);
        }

        /// <summary>
        /// 이동 연출 재생 공통 창구. VfxSpawner가 씬에 없거나 파괴되었으면 조용히 생략한다
        /// (감사 #8: `?.`는 Unity의 오버로드된 == 를 우회하므로 명시적 null 비교를 쓴다).
        /// </summary>
        private void PlayVfx(string vfxId, Vector3 position)
        {
            if (!playMovementVfx || string.IsNullOrEmpty(vfxId)) return;

            var spawner = Art.VfxSpawner.Instance;
            if (spawner == null) return;

            spawner.Play(vfxId, position, flipX: _facingSign < 0);
        }

        // ── 피격 / 사망 / 부활 ────────────────────────────────────

        /// <summary>
        /// 피격 반응 — 아주 짧은 조작 불가(hitstun). 길면 답답하므로 인스펙터 상한이 0.1초로 묶여 있다.
        /// 실제 피해/넉백/상태이상 처리는 Combat.DamageSystem 단일 파이프라인 소관이며 여기서는 조작만 다룬다.
        /// </summary>
        private void OnEntityDamaged(DamageInfo info)
        {
            if (hitstunDuration > 0f)
                _hitstunTimer = Mathf.Max(_hitstunTimer, Mathf.Min(hitstunDuration, 0.1f));

            // NOTE(기획 확인 필요): 피격 시 대쉬 쿨타임 초기화 — 기본 꺼짐(위 SerializeField 주석 참고).
            if (resetDashCooldownOnHit)
            {
                _dashCooldownTimer = 0f;
                if (_isGrounded) _airDashesLeft = airDashCount;
            }
        }

        /// <summary>
        /// 사망 — 이동을 즉시 멈추고 진행 중이던 대쉬/점프 상태를 정리한다.
        /// 부활 판정 자체는 Core.SharedReviveSystem 단일 경로(CombatEntity)가 담당하며 여기서는 조작만 다룬다.
        /// </summary>
        private void OnEntityDied(CombatEntity entity)
        {
            _deadLatch = true;

            _moveAxis = 0f;
            _dashTimer = 0f;
            _dashBufferTimer = 0f;
            _dashTrailTimer = 0f;
            _jumpBufferTimer = 0f;
            _attackBufferTimer = 0f; // 사망 직전 입력이 부활 직후 터지면 안 된다
            _coyoteTimer = 0f;
            _isJumping = false;
            _hitstunTimer = 0f;
            _wallSliding = false;

            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
                // 대쉬 중 사망 시 gravityScale이 0으로 남는 것을 막는다.
                _body.gravityScale = _baseGravityScale * UpSign;
            }
            // TODO(연출): 사망 애니메이션/시체 처리 — Art.CharacterVisual 소관.
        }

        /// <summary>부활 복구 — 조작 상태를 초기화한다. (CombatEntity에 부활 이벤트가 없어 IsDead 전이로 감지)</summary>
        private void OnRevived()
        {
            _deadLatch = false;
            _hitstunTimer = 0f;
            _dashCooldownTimer = 0f;
            _airDashesLeft = airDashCount;
            _dashBufferTimer = 0f;
            _jumpBufferTimer = 0f;
            _attackBufferTimer = 0f; // 사망 중 눌린 입력이 부활 순간 발동되는 것을 막는다
            _isJumping = false;

            // 부활 지점으로 순간이동했을 수 있으므로 이동 거리 누적 기준을 다시 잡는다
            // (출혈 OnMoved에 순간이동 거리가 통째로 들어가는 것을 막는다).
            if (_body != null) _lastBodyPosition = _body.position;
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
            // 접지 판정 시각화 — 중앙 + 좌우 발끝 3점 (중력 반전 상태 반영)
            Vector2 offset = groundCheckOffset;
            if (_gravityInverted) offset.y = -offset.y;
            Gizmos.color = Color.green;
            Vector2 groundCenter = (Vector2)transform.position + offset;
            Gizmos.DrawWireSphere(groundCenter, groundCheckRadius);
            if (groundCheckExtent > 0f)
            {
                Vector2 extent = new Vector2(groundCheckExtent, 0f);
                Gizmos.DrawWireSphere(groundCenter - extent, groundCheckRadius);
                Gizmos.DrawWireSphere(groundCenter + extent, groundCheckRadius);
            }

            // 벽 판정 시각화 (기능이 켜져 있을 때만)
            if (!enableWallSlide) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere((Vector2)transform.position + new Vector2(wallCheckOffset.x, wallCheckOffset.y), wallCheckRadius);
            Gizmos.DrawWireSphere((Vector2)transform.position + new Vector2(-wallCheckOffset.x, wallCheckOffset.y), wallCheckRadius);
        }
#endif
    }
}
