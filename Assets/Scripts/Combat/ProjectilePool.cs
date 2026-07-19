// 근거: 전투 시스템.md — 궁수/마법사/저격수 등 원거리 공격이 상시 존재한다.
// 근거: 성능 감사 보고 §4 — 발당 Instantiate/Destroy는 Rigidbody2D+Collider2D 생성/파괴로
//   물리 브로드페이즈 갱신을 동반하고, Destroy가 프레임 끝에 몰려 끊김을 만든다. 풀링으로 대체한다.
// 사용법(발사 측): ProjectilePool.Spawn(prefab, position, rotation) → SetSpeed/SetObstacleMask → Launch.
//   반납은 Projectile이 스스로 한다 (수명 종료·착탄). 호출 측이 Destroy를 부르지 않는다.
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Combat
{
    /// <summary>
    /// 프리팹별 투사체 풀. 씬에 배치해도 되고, 배치하지 않으면 첫 사용 시 자동 생성된다
    /// (연출·최적화 컴포넌트가 없어도 게임 로직이 실패하지 않는다는 규칙을 지킨다).
    /// </summary>
    public class ProjectilePool : MonoBehaviour
    {
        public static ProjectilePool Instance { get; private set; }

        [Tooltip("프리팹 하나당 보관할 최대 개수. 초과 반납분은 파괴한다.")]
        [SerializeField, Min(1)] private int maxPerPrefab = 48; // TODO(밸런스): 문서 미정

        /// <summary>프리팹(원본) → 대기 중인 인스턴스.</summary>
        private readonly Dictionary<Projectile, Stack<Projectile>> _pools = new Dictionary<Projectile, Stack<Projectile>>();

        private static bool _quitting;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit() => _quitting = true;

        // ── 공개 API ──────────────────────────────────────────────

        /// <summary>
        /// 투사체를 꺼낸다. 풀이 비어 있으면 새로 만든다.
        /// 반환 직후 SetSpeed / SetObstacleMask / Launch를 호출하면 된다.
        /// </summary>
        public static Projectile Spawn(Projectile prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            ProjectilePool pool = EnsureInstance();
            if (pool == null) // 종료 중 등 — 폴백
                return Instantiate(prefab, position, rotation);

            return pool.SpawnInternal(prefab, position, rotation);
        }

        /// <summary>
        /// 투사체를 반납한다. Projectile이 스스로 호출하므로 보통 직접 부를 일은 없다.
        /// 풀에서 나오지 않은 인스턴스(에디터 배치 등)는 파괴한다.
        /// </summary>
        public static void Despawn(Projectile projectile)
        {
            if (projectile == null) return;

            ProjectilePool pool = Instance;
            Projectile prefab = projectile.PoolPrefab;

            if (_quitting || pool == null || prefab == null)
            {
                Destroy(projectile.gameObject);
                return;
            }

            pool.DespawnInternal(prefab, projectile);
        }

        /// <summary>미리 만들어 둔다 — 첫 발사 순간의 생성 비용을 없앤다 (방 진입 로딩 등에서 호출).</summary>
        public static void Prewarm(Projectile prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            ProjectilePool pool = EnsureInstance();
            if (pool == null) return;

            Stack<Projectile> stack = pool.GetStack(prefab);
            for (int i = 0; i < count && stack.Count < pool.maxPerPrefab; i++)
            {
                Projectile created = pool.CreateInstance(prefab);
                created.gameObject.SetActive(false);
                created.transform.SetParent(pool.transform, false);
                stack.Push(created);
            }
        }

        // ── 내부 ──────────────────────────────────────────────────

        private static ProjectilePool EnsureInstance()
        {
            if (Instance != null) return Instance;
            if (_quitting || !Application.isPlaying) return null;

            var go = new GameObject("ProjectilePool");
            return go.AddComponent<ProjectilePool>();
        }

        private Stack<Projectile> GetStack(Projectile prefab)
        {
            if (!_pools.TryGetValue(prefab, out Stack<Projectile> stack))
            {
                stack = new Stack<Projectile>();
                _pools[prefab] = stack;
            }
            return stack;
        }

        private Projectile SpawnInternal(Projectile prefab, Vector3 position, Quaternion rotation)
        {
            Stack<Projectile> stack = GetStack(prefab);

            Projectile projectile = null;
            while (stack.Count > 0 && projectile == null)
                projectile = stack.Pop(); // 씬 전환 등으로 파괴된 항목을 걸러낸다

            if (projectile == null)
                projectile = CreateInstance(prefab);

            Transform t = projectile.transform;
            t.SetParent(null, false);
            t.SetPositionAndRotation(position, rotation);
            projectile.gameObject.SetActive(true);
            return projectile;
        }

        private void DespawnInternal(Projectile prefab, Projectile projectile)
        {
            Stack<Projectile> stack = GetStack(prefab);
            if (stack.Count >= maxPerPrefab)
            {
                Destroy(projectile.gameObject);
                return;
            }

            projectile.gameObject.SetActive(false);
            projectile.transform.SetParent(transform, false);
            stack.Push(projectile);
        }

        private Projectile CreateInstance(Projectile prefab)
        {
            Projectile created = Instantiate(prefab);
            created.PoolPrefab = prefab; // 어느 풀로 돌아갈지 기억시킨다
            return created;
        }
    }
}
