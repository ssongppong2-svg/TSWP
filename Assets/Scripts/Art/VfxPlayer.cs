// 근거: 도트 시스템.md — 12FPS 애니메이션, 좌우 반전은 flipX로 처리.
// 이펙트 1개 인스턴스의 재생을 담당한다. 재생이 끝나면 스스로 풀에 반납한다.
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

        /// <summary>재생 중인가.</summary>
        public bool IsPlaying => _playing;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>이펙트 재생 시작.</summary>
        /// <param name="definition">이펙트 정의</param>
        /// <param name="flipX">좌우 반전 (공격 방향)</param>
        /// <param name="tint">추가 색조. null이면 흰색(원본 색 유지)</param>
        public void Play(VfxDefinition definition, bool flipX = false, Color? tint = null)
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

            _renderer.sprite = _frames[0];
            _renderer.flipX = definition.canFlip && flipX;
            _renderer.color = tint ?? Color.white;
            _renderer.sortingOrder = definition.sortingOrder;

            transform.localScale = Vector3.one * definition.scale;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_playing || _frames == null || _frames.Length == 0) return;

            // 히트스톱 중에도 이펙트는 흘러야 자연스럽다 → unscaled 사용.
            _timer += Time.unscaledDeltaTime;

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
            _playing = false;
            gameObject.SetActive(false);
            VfxSpawner.Instance?.Release(this);
        }
    }
}
