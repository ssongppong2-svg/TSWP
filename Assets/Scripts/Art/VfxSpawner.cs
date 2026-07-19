// 8인 협동 + 다수의 적이 동시에 싸우므로 이펙트 생성이 잦다.
// GameObject를 매번 생성/파괴하면 GC가 튀므로 풀링한다 (개발 우선순위 7 '최적화'보다 앞서는 안정성 문제).
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
        [Tooltip("시작 시 미리 만들어 둘 인스턴스 수.")]
        [SerializeField, Min(0)] private int prewarmCount = 16;

        [Tooltip("동시에 존재할 수 있는 최대 이펙트 수. 초과 요청은 무시된다.")]
        [SerializeField, Min(1)] private int maxActive = 128;

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

        /// <summary>이펙트를 특정 위치에 재생한다.</summary>
        /// <param name="vfxId">VfxId 상수</param>
        /// <param name="position">월드 위치</param>
        /// <param name="flipX">좌우 반전</param>
        /// <param name="tint">추가 색조 (null이면 원본 색)</param>
        public VfxPlayer Play(string vfxId, Vector3 position, bool flipX = false, Color? tint = null)
        {
            if (library == null) return null;

            var definition = library.Find(vfxId);
            if (definition == null) return null;

            return Play(definition, position, flipX, tint);
        }

        public VfxPlayer Play(VfxDefinition definition, Vector3 position, bool flipX = false, Color? tint = null)
        {
            if (definition == null) return null;
            if (_activeCount >= maxActive) return null; // 과부하 방지

            VfxPlayer player = _pool.Count > 0 ? _pool.Pop() : CreateInstance();
            if (player == null) return null;

            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            player.Play(definition, flipX, tint);
            _activeCount++;
            return player;
        }

        /// <summary>VfxPlayer가 재생을 마치면 스스로 호출한다.</summary>
        public void Release(VfxPlayer player)
        {
            if (player == null) return;

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
            return player;
        }

#if UNITY_EDITOR
        /// <summary>테스트 씬 빌더가 카탈로그를 주입할 때 사용.</summary>
        public void SetLibrary(VfxLibrary value) => library = value;
#endif
    }
}
