// 근거: 도트 시스템.md — 12FPS 애니메이션, 좌우 반전은 flipX로 처리.
// 이펙트 1개 인스턴스의 재생을 담당한다. 재생이 끝나면 스스로 풀에 반납한다.
// 상태이상처럼 캐릭터를 따라다녀야 하는 이펙트를 위해 추적 대상을 받을 수 있다.
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>스프라이트 시트 이펙트 재생기. VfxSpawner가 풀에서 꺼내 사용한다.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class VfxPlayer : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Sprite[] _frames;
        private VfxDefinition _definition;
        private float _timer;
        private int _frameIndex;
        private bool _playing;
        private float _speedMultiplier = 1f;

        // 추적 (상태이상 등 캐릭터에 붙는 이펙트)
        private Transform _follow;
        private Vector3 _followOffset;

        public bool IsPlaying => _playing;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>이펙트 재생 시작.</summary>
        public void Play(VfxDefinition definition, bool flipX = false, Color? tint = null,
                         float scaleMultiplier = 1f, float rotation = 0f, float speedMultiplier = 1f)
        {
            _definition = definition;
            _frames = definition != null ? definition.GetFrames() : null;

            if (_frames == null || _frames.Length == 0)
            {
                Stop();
                return;
            }

            _frameIndex = 0;
            _timer = 0f;
            _playing = true;
            _speedMultiplier = Mathf.Max(0.1f, speedMultiplier);

            _renderer.sprite = _frames[0];
            _renderer.flipX = definition.canFlip && flipX;
            _renderer.color = tint ?? Color.white;
            _renderer.sortingOrder = definition.sortingOrder;

            transform.localScale = Vector3.one * (definition.scale * scaleMultiplier);
            transform.rotation = Quaternion.Euler(0f, 0f, rotation);

            gameObject.SetActive(true);
        }

        /// <summary>추적 대상 지정 — 매 프레임 대상 위치 + 오프셋으로 따라간다.</summary>
        public void SetFollow(Transform target, Vector3 offset)
        {
            _follow = target;
            _followOffset = offset;
        }

        private void LateUpdate()
        {
            if (_follow == null) return;

            if (!_follow.gameObject.activeInHierarchy)
            {
                Stop();
                return;
            }

            transform.position = _follow.position + _followOffset;
        }

        private void Update()
        {
            if (!_playing || _frames == null || _frames.Length == 0) return;

            // 히트스톱 중에도 이펙트는 흘러야 자연스럽다 → unscaled 사용.
            _timer += Time.unscaledDeltaTime * _speedMultiplier;

            float frameDuration = 1f / Mathf.Max(1f, _definition.fps);
            while (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                _frameIndex++;

                if (_frameIndex >= _frames.Length)
                {
                    if (_definition.loop)
                    {
                        _frameIndex = 0;
                    }
                    else
                    {
                        Stop();
                        return;
                    }
                }

                _renderer.sprite = _frames[_frameIndex];
            }
        }

        /// <summary>재생 중지 후 풀에 반납.</summary>
        public void Stop()
        {
            if (!_playing && !gameObject.activeSelf) return;

            _playing = false;
            _follow = null;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(false);
            VfxSpawner.Instance?.Release(this);
        }
    }
}
