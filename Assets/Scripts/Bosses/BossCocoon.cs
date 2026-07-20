// 근거: 보스 시스템.md — 기믹은 '플레이어가 반응할 상황'을 만든다(30초 법칙) /
//       보스 01 해치 퀸: 고치를 소환하고, 부화하면 거미가 나온다.
// 협동 설계: 고치를 부화 전에 부수면 거미가 나오지 않는다 → 딜과 정리 사이의 우선순위 판단이 생긴다.
// 보스 전용이 아니다 — '시간이 지나면 적으로 부화하는 파괴 가능 오브젝트'라는 일반 개념이다.
using UnityEngine;
using TSWP.Combat;
using TSWP.Core;
using TSWP.Enemies;

namespace TSWP.Bosses
{
    /// <summary>
    /// 부화형 소환물. 지정 시간이 지나면 EnemyData의 적을 스폰하고 사라진다.
    /// 부화 전에 파괴되면 아무것도 나오지 않는다.
    /// SYNC: 부화 타이머·스폰 판정은 호스트 권위.
    /// </summary>
    [RequireComponent(typeof(CombatEntity))]
    public sealed class BossCocoon : MonoBehaviour
    {
        [Header("부화")]
        [Tooltip("부화까지 걸리는 시간(초). Initialize로 덮어쓸 수 있다.")]
        [SerializeField, Min(0.1f)] private float hatchSeconds = 4f; // TODO(밸런스): 문서 미정

        [Tooltip("부화 시 스폰할 적. Initialize로 주입하는 것이 기본이고, 이 값은 단독 테스트용 폴백이다.")]
        [SerializeField] private EnemyData hatchEnemy;

        [Tooltip("부화 시 재생할 이펙트 id (Art.VfxId). 비우면 재생하지 않는다.")]
        [SerializeField] private string hatchVfxId;

        [Tooltip("부화 잔여 시간을 스케일로 표현할 스프라이트(선택). 없으면 생략한다.")]
        [SerializeField] private Transform pulseTransform;

        private CombatEntity _entity;
        private Difficulty _difficulty = Difficulty.Human;
        private float _timer;
        private bool _resolved; // 부화 또는 파괴로 이미 처리 완료

        /// <summary>부화까지 남은 비율 0~1 (UI/연출용).</summary>
        public float HatchProgress => hatchSeconds <= 0f ? 1f : Mathf.Clamp01(_timer / hatchSeconds);

        private void Awake()
        {
            _entity = GetComponent<CombatEntity>();
            _entity.SetTeam(TeamType.Enemies); // 아군 판정은 TeamType 비교 (레이어 아님)
        }

        private void OnEnable() => _entity.Died += OnDied;
        private void OnDisable() => _entity.Died -= OnDied;

        /// <summary>소환 패턴이 호출하는 주입 지점.</summary>
        public void Initialize(EnemyData enemy, float seconds, Difficulty difficulty)
        {
            if (enemy != null) hatchEnemy = enemy;
            if (seconds > 0f) hatchSeconds = seconds;
            _difficulty = difficulty;
            _timer = 0f;
            _resolved = false;
        }

        private void Update()
        {
            if (_resolved) return;

            _timer += Time.deltaTime;

            // 부화가 임박할수록 커진다 — 예고 연출. pulseTransform이 없으면 조용히 생략된다.
            if (pulseTransform != null)
                pulseTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.25f, HatchProgress);

            if (_timer >= hatchSeconds)
                Hatch();
        }

        private void Hatch()
        {
            _resolved = true;

            if (!string.IsNullOrEmpty(hatchVfxId))
                Art.VfxSpawner.Instance?.Play(hatchVfxId, transform.position);

            // SpawnAt은 '연출상 위치가 정해진' 소환용 진입점이다(배치 규칙 검사는 호출측 책임).
            // 여기서 스폰한 적은 SpawnManager가 RoomManager 전멸 카운트에 등록하므로,
            // 부화한 거미를 정리해야 방이 클리어된다.
            var spawner = SpawnManager.Instance;
            if (spawner != null && hatchEnemy != null)
                spawner.SpawnAt(hatchEnemy, _difficulty, transform.position);
            else
                Debug.LogWarning($"[BossCocoon] '{name}': SpawnManager 또는 부화 적(EnemyData)이 없어 부화를 생략했습니다.", this);

            Destroy(gameObject);
        }

        private void OnDied(CombatEntity entity)
        {
            // 부화 전 파괴 — 거미는 나오지 않는다 (협동 보상).
            _resolved = true;
            Destroy(gameObject);
        }
    }
}
