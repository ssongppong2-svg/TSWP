// 근거: 보스 시스템.md — 권장 패턴 구성 중 '특수 기술'. 보스 01 해치 퀸: 고치 소환 → 부화 시 거미 등장.
// 소환 자체는 여기서, '부화'는 BossCocoon이 담당한다(한 클래스 한 가지 일).
// 고치 프리팹이 없어도 게임이 멈추지 않도록 '적을 직접 지연 소환'하는 폴백을 둔다.
using System.Collections.Generic;
using UnityEngine;
using TSWP.Enemies;

namespace TSWP.Bosses
{
    /// <summary>보스 주변에 고치를 여러 개 소환하는 패턴. 고치는 시간이 지나면 적으로 부화한다.</summary>
    [CreateAssetMenu(menuName = "TSWP/Bosses/Patterns/Cocoon Spawn", fileName = "PatternBehaviour_Cocoon_")]
    public sealed class CocoonSpawnPatternBehaviour : BossPatternBehaviour
    {
        [Header("소환")]
        [Tooltip("한 번에 소환할 고치 개수.")]
        [SerializeField, Min(1)] private int cocoonCount = 3; // TODO(밸런스): 문서 미정

        [Tooltip("보스를 중심으로 고치를 배치할 반경.")]
        [SerializeField, Min(0.1f)] private float spawnRadius = 3.5f; // TODO(밸런스): 문서 미정

        [Tooltip("배치 시작 각도(도). 매번 같은 자리에 나오지 않도록 애셋별로 다르게 준다.")]
        [SerializeField] private float startAngleDegrees = 0f;

        [Tooltip("BossCocoon 컴포넌트를 가진 프리팹. 비면 고치 없이 적을 지연 소환한다(폴백).")]
        [SerializeField] private GameObject cocoonPrefab;

        [Header("부화")]
        [Tooltip("부화까지 걸리는 시간(초).")]
        [SerializeField, Min(0.1f)] private float hatchSeconds = 4f; // TODO(밸런스): 문서 미정

        [Tooltip("부화해서 나올 적 (예: 거미). Enemies.EnemyData 애셋.")]
        [SerializeField] private EnemyData hatchEnemy;

        [Header("제한")]
        [Tooltip("이 패턴으로 동시에 존재할 수 있는 고치 최대 수. 초과하면 그만큼 덜 소환한다(화면 폭주 방지).")]
        [SerializeField, Min(1)] private int maxAliveCocoons = 6; // TODO(밸런스): 문서 미정

        [Header("연출")]
        [SerializeField] private string spawnVfxId;

        [Tooltip("소환 후 경직 시간(초).")]
        [SerializeField, Min(0f)] private float recoverySeconds = 0.5f; // TODO(밸런스): 문서 미정

        public int CocoonCount => cocoonCount;
        public float SpawnRadius => spawnRadius;
        public float StartAngleDegrees => startAngleDegrees;
        public GameObject CocoonPrefab => cocoonPrefab;
        public float HatchSeconds => hatchSeconds;
        public EnemyData HatchEnemy => hatchEnemy;
        public int MaxAliveCocoons => maxAliveCocoons;
        public string SpawnVfxId => spawnVfxId;
        public float RecoverySeconds => recoverySeconds;

        public override BossPatternRunner CreateRunner() => new CocoonSpawnRunner(this);
    }

    /// <summary>CocoonSpawnPatternBehaviour의 실행 상태.</summary>
    public sealed class CocoonSpawnRunner : BossPatternRunner
    {
        private readonly CocoonSpawnPatternBehaviour _data;

        public CocoonSpawnRunner(CocoonSpawnPatternBehaviour data) : base(data)
        {
            _data = data;
        }

        protected override void OnActiveStart(BossPatternContext ctx)
        {
            int budget = Mathf.Max(0, _data.MaxAliveCocoons - CountAliveCocoons());
            int count = Mathf.Min(_data.CocoonCount, budget);

            if (count <= 0) return; // 이미 화면에 고치가 가득함 — 이번 시전은 빈 시전

            for (int i = 0; i < count; i++)
            {
                // 결정론적 배치 — 무작위를 쓰면 클라이언트마다 다른 위치가 된다.
                float angle = Mathf.Deg2Rad * _data.StartAngleDegrees + Mathf.PI * 2f * i / count;
                Vector2 position = ctx.BossPosition +
                                   new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _data.SpawnRadius;

                SpawnOne(ctx, position);
            }
        }

        protected override bool OnActiveTick(BossPatternContext ctx, float deltaTime)
        {
            // 고치는 스스로 살아간다 — 이 패턴은 소환 후 곧바로 끝난다.
            return StageElapsed >= ctx.Scale(_data.RecoverySeconds);
        }

        private void SpawnOne(BossPatternContext ctx, Vector2 position)
        {
            ctx.PlayVfx(_data.SpawnVfxId, position);

            if (_data.CocoonPrefab == null)
            {
                // 폴백: 고치 프리팹이 없으면 적을 곧바로 소환한다.
                // 게임이 멈추는 것보다 낫지만, '부화 전 파괴' 협동 요소는 사라진다.
                var spawner = SpawnManager.Instance;
                if (spawner != null && _data.HatchEnemy != null)
                    spawner.SpawnAt(_data.HatchEnemy, ctx.Difficulty, position);
                else
                    Debug.LogWarning($"[CocoonSpawnRunner] '{_data.name}': cocoonPrefab도 hatchEnemy/SpawnManager도 " +
                                     "없어 소환을 생략했습니다.", _data);
                return;
            }

            var instance = Object.Instantiate(_data.CocoonPrefab, position, Quaternion.identity);
            var cocoon = instance.GetComponent<BossCocoon>();
            if (cocoon == null)
            {
                Debug.LogError($"[CocoonSpawnRunner] '{_data.name}': cocoonPrefab에 BossCocoon 컴포넌트가 없습니다.", instance);
                Object.Destroy(instance);
                return;
            }

            cocoon.Initialize(_data.HatchEnemy, _data.HatchSeconds, ctx.Difficulty);
        }

        /// <summary>현재 씬에 살아있는 고치 수. Unity 6: FindObjectOfType는 제거됨 — FindObjectsByType 사용.</summary>
        private static int CountAliveCocoons()
        {
            var found = Object.FindObjectsByType<BossCocoon>(FindObjectsSortMode.None);
            return found != null ? found.Length : 0;
        }
    }
}
