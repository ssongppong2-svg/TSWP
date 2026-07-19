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
        [SerializeField] private float jumpSpeed = 12f;           // TODO(밸런스): 문서 미정 — 점프 초기 속도

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
        private bool _jumpQueued;            // Update에서 에지 수집 → FixedUpdate에서 소비 (물리 프레임 유실 방지)
        private int _facingSign = 1;         // +1 오른쪽 / -1 왼쪽 (Art.CharacterVisual flipX 연동 예정)
        private bool _gravityInverted;       // 밈 규칙 '중력 반전' 상태 // SYNC: 호스트 권위
        private float _baseGravityScale = 1f;
        private Vector2 _lastBodyPosition;

        // ── 외부 조회 ─────────────────────────────────────────────
        /// <summary>입력 소스. PlayerInteraction/PingEmitter/EmoteWheel이 공유 폴링한다.</summary>
        public IPlayerInput InputSource => _input;

        /// <summary>플레이어 ID (CombatEntity.OwnerPlayerId 위임, 미설정 시 -1).</summary>
        public int PlayerId => _entity != null ? _entity.OwnerPlayerId : -1;

        /// <summary>바라보는 방향 부호 (+1 오른쪽 / -1 왼쪽).</summary>
        public int FacingSign => _facingSign;

        public bool IsGravityInverted => _gravityInverted;

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

            // ── 이동 입력 수집 (물리 적용은 FixedUpdate) ──
            _moveAxis = Mathf.Clamp(_input.MoveAxis, -1f, 1f);
            _runHeld = _input.RunHeld;
            if (_input.JumpPressed) _jumpQueued = true;

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
            // ── 수평 이동 ──
            // 상태이상 변조 경유: 공포(전체 반전)/혼란(x축 반전)/이동 차단 CC(0 벡터) — StatusEffectController 계약.
            Vector2 desired = new Vector2(_moveAxis, 0f);
            if (_statusController != null) desired = _statusController.MoveInputModifier(desired);
            if (_entity != null && _entity.IsDead) desired = Vector2.zero;

            float speed = MoveSpeed;
            if (_runHeld) speed *= runSpeedMultiplier; // 스태미나 없음 — 게이트 없이 상시 적용
            if (_statusController != null) speed *= _statusController.GetMoveSpeedMultiplier(); // 감전/둔화

            Vector2 velocity = _body.linearVelocity;
            velocity.x = desired.x * speed;
            // TODO(물리): 넉백 임펄스(Combat.KnockbackInfo)와 직접 속도 대입 간 간섭 — 넉백 중 입력 가속 보류 검토.
            _body.linearVelocity = velocity;

            // ── 점프 (중력 반전 대응: 반전 시 아래로 도약) ──
            if (_jumpQueued)
            {
                _jumpQueued = false;
                if (CanMove && IsGrounded)
                {
                    Vector2 v = _body.linearVelocity;
                    v.y = jumpSpeed * (_gravityInverted ? -1f : 1f);
                    _body.linearVelocity = v;
                    // TODO(조작감): 코요테 타임/점프 버퍼/가변 점프 높이 — 문서 미정 (점프는 플랫폼·회피·퍼즐 핵심).
                }
            }

            // ── 이동 거리 훅 (출혈: 움직일수록 추가 피해 — StatusEffects.OnMoved) ──
            float moved = Vector2.Distance(_body.position, _lastBodyPosition);
            _lastBodyPosition = _body.position;
            if (moved > 0f) _statusController?.OnMoved(moved);
            // NOTE(기획 확인 필요): 낙하/넉백에 의한 이동도 출혈 피해에 포함할지 — 우선 전체 이동 거리 산정.
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
