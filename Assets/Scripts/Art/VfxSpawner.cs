// 8인 협동 + 다수의 적이 동시에 싸우므로 이펙트 생성이 잦다.
// GameObject를 매번 생성/파괴하면 GC가 튀므로 풀링한다.
// 하나의 연출은 여러 레이어를 지연·오프셋·회전을 달리해 겹쳐 만든다 (타격감의 핵심).
using System.Collections;
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

        private readonly Stack<VfxPlayer> _pool = new Stack<VfxPlayer>();
        private int _activeCount;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            for (int i = 0; i < prewarmCount; i++)
                _pool.Push(CreateInstance());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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

            for (int i = 0; i < entry.layers.Count; i++)
            {
                var layer = entry.layers[i];
                if (layer == null || layer.definition == null) continue;

                if (layer.delay > 0f)
                    StartCoroutine(PlayDelayed(layer, position, flipX, tint, rotation, follow));
                else
                    PlayLayer(layer, position, flipX, tint, rotation, follow);
            }
        }

        private IEnumerator PlayDelayed(VfxLayer layer, Vector3 position, bool flipX, Color? tint,
                                        float rotation, Transform follow)
        {
            yield return new WaitForSecondsRealtime(layer.delay);
            PlayLayer(layer, position, flipX, tint, rotation, follow);
        }

        private VfxPlayer PlayLayer(VfxLayer layer, Vector3 position, bool flipX, Color? tint,
                                    float rotation, Transform follow)
        {
            if (_activeCount >= maxActive) return null;

            VfxPlayer player = _pool.Count > 0 ? _pool.Pop() : CreateInstance();
            if (player == null) return null;

            // 좌우 반전 시 오프셋도 뒤집어야 방향이 맞는다.
            Vector3 offset = layer.offset;
            if (flipX) offset.x = -offset.x;

            float finalRotation = rotation + layer.rotation;
            if (layer.randomRotation) finalRotation += Random.Range(0f, 360f);

            Color finalTint = tint ?? layer.tint;

            // 풀에서 꺼낸 시점에 '사용 중'으로 표시한다.
            // Play가 실패해 즉시 Stop→Release로 되돌아와도 카운터가 정확히 상쇄된다.
            player.IsPooled = false;
            _activeCount++;

            player.transform.position = position + offset;
            player.Play(layer.definition, flipX, finalTint, layer.scaleMultiplier, finalRotation, layer.speedMultiplier);

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

            var player = PlayLayer(layer, target.position + offset, false, tint, 0f, target);
            return player;
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
            player.transform.SetParent(transform);
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
        public string DebugStatus => $"활성 {_activeCount} / 풀 {_pool.Count}";
#endif

#if UNITY_EDITOR
        public void SetLibrary(VfxLibrary value) => library = value;
#endif
    }
}
