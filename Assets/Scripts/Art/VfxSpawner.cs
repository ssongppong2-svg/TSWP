// 8인 협동 + 다수의 적이 동시에 싸우므로 이펙트 생성이 잦다.
// GameObject를 매번 생성/파괴하면 GC가 튀므로 풀링한다.
// 하나의 연출은 여러 레이어를 지연·오프셋·회전을 달리해 겹쳐 만든다 (타격감의 핵심).
//
// 성능 설계 두 가지:
//  1) 지연 레이어를 코루틴으로 처리하면 타격 1회당 (코루틴 + WaitForSecondsRealtime + 이터레이터)가
//     레이어 수만큼 생긴다. 8인이 초당 수십 회 때리면 전부 순수 가비지다.
//     → 예약 리스트 + Update 스캔으로 바꿔 할당을 0으로 만든다.
//  2) 정의의 프레임(Sprite)은 최초 재생 순간에 만들어진다(= 첫 타격에 히칭).
//     → Awake에서 라이브러리를 프리워밍해 그 비용을 로딩 시점으로 옮긴다.
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>
    /// 이펙트 스폰 단일 창구. 씬에 1개 배치하고, 없으면 호출은 조용히 무시된다
    /// (게임 로직이 연출 부재로 실패하지 않는다).
    /// </summary>
    public class VfxSpawner : MonoBehaviour
    {
        public static VfxSpawner Instance { get; private set; }

        [Header("카탈로그")]
        [SerializeField] private VfxLibrary library;

        [Header("풀")]
        [SerializeField, Min(0)] private int prewarmCount = 24;
        [SerializeField, Min(1)] private int maxActive = 160;

        [Tooltip("대기 중인 지연 레이어의 최대 개수. 넘치면 새 예약을 버린다(연출만 생략).")]
        [SerializeField, Min(8)] private int maxPending = 128;

        [Header("프레임 프리워밍")]
        [Tooltip("Awake에서 라이브러리의 모든 정의 프레임을 미리 만든다. 첫 타격 히칭을 없앤다.")]
        [SerializeField] private bool prewarmLibrary = true;

        [Tooltip("프리워밍을 여러 프레임에 나눠 처리한다. 끄면 Awake 한 프레임에 전부 처리한다.")]
        [SerializeField] private bool spreadPrewarm = true;

        [Tooltip("한 프레임에 만들 정의 수. 값이 작을수록 부드럽지만 준비가 늦어진다.")]
        [SerializeField, Min(1)] private int prewarmDefinitionsPerFrame = 2;

        [Header("우선순위")]
        [Tooltip("장식용(투사체 꼬리·먼지 등) 이펙트 id. 풀이 포화되면 이것부터 버려 타격 이펙트를 보존한다.")]
        [SerializeField]
        private string[] decorativeVfxIds =
        {
            VfxId.ProjectileFly,
            VfxId.DashTrail,
            VfxId.JumpDust,
            VfxId.LandDust,
        };

        [Tooltip("장식용 이펙트에게 내주지 않고 남겨 두는 풀 여유분. 타격/폭발이 조용히 사라지는 것을 막는다.")]
        [SerializeField, Min(0)] private int decorativeReserve = 32;

        /// <summary>지연 재생 예약. struct + List로 관리해 프레임당 할당을 만들지 않는다.</summary>
        private struct PendingLayer
        {
            public VfxLayer layer;
            public Vector3 position;
            public bool flipX;
            public Color? tint;
            public float rotation;
            public Transform follow;
            public bool requiresFollow;
            public bool decorative;
            public float dueTime; // unscaledTime 기준 — 히트스톱 중에도 이펙트는 흘러야 한다
        }

        private readonly Stack<VfxPlayer> _pool = new Stack<VfxPlayer>();
        private readonly List<PendingLayer> _pending = new List<PendingLayer>();
        private readonly HashSet<string> _decorative = new HashSet<string>();
        private int _activeCount;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            for (int i = 0; i < decorativeVfxIds.Length; i++)
                if (!string.IsNullOrEmpty(decorativeVfxIds[i])) _decorative.Add(decorativeVfxIds[i]);

            for (int i = 0; i < prewarmCount; i++)
                _pool.Push(CreateInstance());

            if (prewarmLibrary && library != null)
            {
                if (spreadPrewarm) StartCoroutine(library.PrewarmRoutine(prewarmDefinitionsPerFrame));
                else library.Prewarm();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDisable()
        {
            // 비활성화되면 예약은 의미가 없다. 남겨 두면 다시 켜질 때 한꺼번에 터진다.
            _pending.Clear();
        }

        /// <summary>
        /// 합성 이펙트를 재생한다. 등록된 레이어를 전부 겹쳐 재생하며, 지연이 있는 레이어는 나중에 나온다.
        /// </summary>
        public void Play(string vfxId, Vector3 position, bool flipX = false, Color? tint = null,
                         float rotation = 0f, Transform follow = null)
        {
            if (library == null) return;

            var entry = library.Find(vfxId);
            if (entry == null || entry.layers == null) return;

            bool decorative = _decorative.Contains(vfxId);
            float now = Time.unscaledTime;

            for (int i = 0; i < entry.layers.Count; i++)
            {
                var layer = entry.layers[i];
                if (layer == null || layer.definition == null) continue;

                if (layer.delay > 0f)
                {
                    if (_pending.Count >= maxPending) continue;

                    _pending.Add(new PendingLayer
                    {
                        layer = layer,
                        position = position,
                        flipX = flipX,
                        tint = tint,
                        rotation = rotation,
                        follow = follow,
                        requiresFollow = follow != null,
                        decorative = decorative,
                        dueTime = now + layer.delay,
                    });
                }
                else
                {
                    PlayLayer(layer, position, flipX, tint, rotation, follow, decorative);
                }
            }
        }

        /// <summary>
        /// 지연 레이어의 재생 시각을 확인한다. 코루틴 대신 이 스캔을 쓰면
        /// 타격마다 생기던 이터레이터/WaitForSecondsRealtime 할당이 전부 사라진다.
        /// </summary>
        private void Update()
        {
            if (_pending.Count == 0) return;

            float now = Time.unscaledTime;

            // 뒤에서부터 지우면 인덱스가 밀리지 않는다.
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                if (now < p.dueTime) continue;

                _pending.RemoveAt(i);

                // 붙어 있어야 할 대상이 그 사이 사라졌으면 재생하지 않는다.
                if (p.requiresFollow && p.follow == null) continue;

                PlayLayer(p.layer, p.position, p.flipX, p.tint, p.rotation, p.follow, p.decorative);
            }
        }

        private VfxPlayer PlayLayer(VfxLayer layer, Vector3 position, bool flipX, Color? tint,
                                    float rotation, Transform follow, bool decorative)
        {
            // 장식용은 풀 여유분을 남겨 두고 먼저 포기한다 — 타격 이펙트(정보)가 살아남아야 한다.
            int limit = decorative ? Mathf.Max(0, maxActive - decorativeReserve) : maxActive;
            if (_activeCount >= limit) return null;

            VfxPlayer player = _pool.Count > 0 ? _pool.Pop() : CreateInstance();
            if (player == null) return null;

            // 좌우 반전 시 오프셋도 뒤집어야 방향이 맞는다.
            Vector3 offset = layer.offset;
            if (flipX) offset.x = -offset.x;

            // 위치 흔들림 — 같은 자리를 반복해 때려도 매번 다르게 보인다.
            if (layer.positionJitter > 0f)
            {
                Vector2 jitter = Random.insideUnitCircle * layer.positionJitter;
                offset.x += jitter.x;
                offset.y += jitter.y;
            }

            float finalRotation = rotation + layer.rotation;
            if (layer.randomRotation) finalRotation += Random.Range(0f, 360f);

            float finalScale = layer.scaleMultiplier;
            if (layer.scaleJitter > 0f)
                finalScale *= 1f + Random.Range(-layer.scaleJitter, layer.scaleJitter);

            Color finalTint = tint ?? layer.tint;

            // 풀에서 꺼낸 시점에 '사용 중'으로 표시한다.
            // Play가 실패해 즉시 Stop→Release로 되돌아와도 카운터가 정확히 상쇄된다.
            player.IsPooled = false;
            _activeCount++;

            player.transform.position = position + offset;
            player.Play(layer.definition, flipX, finalTint, finalScale, finalRotation, layer.speedMultiplier);

            // Play가 실패했으면 이미 반납된 상태 — 호출 측에 넘기지 않는다.
            if (player.IsPooled) return null;

            if (follow != null) player.SetFollow(follow, offset);

            return player;
        }

        /// <summary>
        /// 캐릭터에 붙어 반복 재생되는 이펙트(상태이상 등)를 시작한다.
        /// 반환된 VfxPlayer를 보관했다가 Stop()으로 끄면 된다.
        /// </summary>
        public VfxPlayer PlayAttached(string vfxId, Transform target, Vector3 offset, Color? tint = null)
        {
            if (library == null || target == null) return null;

            var entry = library.Find(vfxId);
            if (entry == null || entry.layers == null || entry.layers.Count == 0) return null;

            // 부착형은 첫 레이어만 사용한다 (여러 장이 계속 겹치면 화면이 지저분해진다).
            var layer = entry.layers[0];
            if (layer == null || layer.definition == null) return null;

            // 부착형은 상태 표시라 정보가치가 높다 — 장식용으로 취급하지 않는다.
            return PlayLayer(layer, target.position + offset, false, tint, 0f, target, decorative: false);
        }

        /// <summary>
        /// VfxPlayer가 재생을 마치면 스스로 호출한다.
        /// 이중 호출되어도 안전하다 — 같은 인스턴스가 풀에 두 번 들어가면
        /// 서로 다른 두 이펙트가 하나의 오브젝트를 공유해 화면이 깨진다.
        /// </summary>
        public void Release(VfxPlayer player)
        {
            if (player == null || player.IsPooled) return;

            player.IsPooled = true;
            _activeCount = Mathf.Max(0, _activeCount - 1);

            // 이미 자식이면 SetParent를 부르지 않는다 — 매번 부르면 불필요한 계층 갱신이 발생한다.
            if (player.transform.parent != transform) player.transform.SetParent(transform);

            _pool.Push(player);
        }

        private VfxPlayer CreateInstance()
        {
            var go = new GameObject("Vfx");
            go.transform.SetParent(transform);
            go.AddComponent<SpriteRenderer>();
            var player = go.AddComponent<VfxPlayer>();
            go.SetActive(false);
            player.IsPooled = true;
            return player;
        }

#if UNITY_EDITOR
        /// <summary>진단용 — 인스펙터에서 누수를 확인할 수 있다.</summary>
        public string DebugStatus => $"활성 {_activeCount} / 풀 {_pool.Count} / 대기 {_pending.Count}";

        public void SetLibrary(VfxLibrary value) => library = value;
#endif
    }
}
