// 근거: 퍼즐 시스템.md — 점프 퍼즐(정확한 점프·타이밍), 환경 요소로 '움직이는 발판'이 있다.
// 근거: 보스 시스템.md — Hatch Queen 보스방에서 높은 곳(약점)으로 올라가는 수단.
// 근거: 조작과 시스템.md — 점프는 플랫폼 이동·함정 회피·보스 패턴 회피·퍼즐에 두루 쓰인다.
//
// Bosses/JumpPlatformGimmick은 '발판을 열고 닫는' 기믹이고, 이 컴포넌트는 '밟으면 튀어오르는' 발판 자체다.
using UnityEngine;
using TSWP.Player;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 밟으면 위로 튀어오르는 발판. 보스방에서 약점까지 올라가는 용도로 쓴다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class JumpPlatform : MonoBehaviour
    {
        [Header("도약")]
        [Tooltip("튀어오르는 속도. 플레이어 기본 점프(12)보다 크게 잡아야 발판다운 느낌이 난다.")]
        [SerializeField, Min(0.1f)] private float launchSpeed = 20f; // TODO(밸런스): 문서 미정

        [Tooltip("같은 대상이 다시 튀어오르기까지의 간격(초). 연속 발동으로 무한 상승하는 것을 막는다.")]
        [SerializeField, Min(0f)] private float cooldown = 0.35f;

        [Tooltip("점프 키를 누르고 있으면 더 높이 — 조작 개입 여지를 준다. 0이면 사용 안 함.")]
        [SerializeField, Min(0f)] private float heldBonus = 4f;

        [Header("대상")]
        [Tooltip("적도 튀어오르게 할지. 협동/트롤 상황을 만들 수 있다.")]
        [SerializeField] private bool affectsEnemies = true;

        [Header("연출")]
        [Tooltip("발동 시 재생할 이펙트 id (Art.VfxId). 비우면 없음.")]
        [SerializeField] private string launchVfxId = TSWP.Art.VfxId.JumpDust;

        [Tooltip("발동 시 눌렸다 돌아오는 시각 효과의 깊이(유닛). 0이면 사용 안 함.")]
        [SerializeField, Min(0f)] private float squashDepth = 0.12f;

        [SerializeField, Min(0.01f)] private float squashRecoverSpeed = 6f;

        [Tooltip("발동 순간 번쩍이는 색. 스프라이트가 없으면 흰 사각형이 자동 생성된다(프로토타입).")]
        [SerializeField] private Color launchFlashColor = new Color(0.4f, 0.9f, 1f, 1f);

        [SerializeField] private PuzzleElementVisual visual = new PuzzleElementVisual();

        private Vector3 _restPosition;
        private float _squashOffset;

        /// <summary>대상별 쿨타임 — 여러 명이 동시에 밟아도 각자 판정된다.</summary>
        private readonly System.Collections.Generic.Dictionary<Transform, float> _lastLaunch = new();

        /// <summary>정리용 임시 버퍼 — 순회 중 삭제를 피한다.</summary>
        private readonly System.Collections.Generic.List<Transform> _pruneBuffer = new();

        private void Awake()
        {
            _restPosition = transform.position;
            visual.Bind(this);
        }

        private void OnTriggerEnter2D(Collider2D other) => TryLaunch(other);

        // 발판 위에 계속 서 있는 경우도 처리한다(Enter만 쓰면 가만히 있을 때 발동하지 않는다).
        private void OnTriggerStay2D(Collider2D other) => TryLaunch(other);

        private void OnCollisionEnter2D(Collision2D collision) => TryLaunch(collision.collider);

        // 트리거가 아닌 '단단한' 발판 위에 그냥 서 있는 경우도 처리한다.
        private void OnCollisionStay2D(Collision2D collision) => TryLaunch(collision.collider);

        private void TryLaunch(Collider2D other)
        {
            if (other == null) return;

            var body = other.attachedRigidbody;
            if (body == null) return;

            // 아래에서 위로 통과하는 중이면 발동하지 않는다 — 위에서 밟았을 때만 튄다.
            if (body.linearVelocity.y > 0.1f) return;

            // 콜라이더가 자식에 달린 구성도 지원한다.
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null && !affectsEnemies) return;

            float now = Time.time;
            if (_lastLaunch.TryGetValue(other.transform, out float last) && now - last < cooldown) return;

            PruneLaunchHistory();
            _lastLaunch[other.transform] = now;

            float speed = launchSpeed;

            // 점프 키를 누르고 있으면 더 높이 — 플레이어가 개입할 여지를 남긴다.
            if (player != null && heldBonus > 0f)
            {
                var input = player.InputSource;
                if (input != null && input.JumpHeld) speed += heldBonus;
            }

            body.linearVelocity = new Vector2(body.linearVelocity.x, speed);

            PlayFeedback(other.name, speed);
        }

        /// <summary>파괴된 대상의 기록이 쌓이지 않도록 정리한다.</summary>
        private void PruneLaunchHistory()
        {
            if (_lastLaunch.Count < 16) return;

            _pruneBuffer.Clear();
            foreach (var pair in _lastLaunch)
                if (pair.Key == null) _pruneBuffer.Add(pair.Key);

            for (int i = 0; i < _pruneBuffer.Count; i++)
                _lastLaunch.Remove(_pruneBuffer[i]);
        }

        private void PlayFeedback(string launchedName, float speed)
        {
            _squashOffset = squashDepth;
            visual.Flash(launchFlashColor); // 발판이 반응했다는 것이 색으로 보인다

            PuzzleLog.Record(this, $"{name}: '{launchedName}' 도약 (속도 {speed:0.#})");

            if (string.IsNullOrEmpty(launchVfxId)) return;

            // 씬에 이펙트 시스템이 없어도 조용히 생략된다.
            var spawner = TSWP.Art.VfxSpawner.Instance;
            if (spawner != null) spawner.Play(launchVfxId, transform.position);
        }

        private void Update()
        {
            visual.Tick(Time.deltaTime);

            if (_squashOffset <= 0.0001f) return;

            // 눌렸다 돌아오는 연출 — 발판이 반응한다는 인상을 준다.
            _squashOffset = Mathf.MoveTowards(_squashOffset, 0f, squashRecoverSpeed * Time.deltaTime * squashDepth * 10f);
            transform.position = _restPosition + Vector3.down * _squashOffset;
        }

        /// <summary>기믹이 발판을 껐다 켤 때 위치 기준을 다시 잡는다.</summary>
        public void ResetRestPosition()
        {
            _restPosition = transform.position;
            _squashOffset = 0f;
            _lastLaunch.Clear();
        }
    }
}
