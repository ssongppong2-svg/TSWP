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

        // 페이드아웃 — 마지막 N프레임 동안 알파를 줄여 이펙트가 뚝 끊기지 않게 한다.
        private Color _baseColor = Color.white;
        private int _fadeOutFrames;

        // 추적 (상태이상 등 캐릭터에 붙는 이펙트)
        private Transform _follow;
        private Vector3 _followOffset;

        public bool IsPlaying => _playing;

        /// <summary>
        /// 현재 풀에 들어가 있는가. 이중 반납을 막는 자물쇠 —
        /// 이 값이 없으면 Play 실패 시 풀에 들어가면서도 '사용 중'으로 세어져
        /// 카운터가 새고, 결국 모든 이펙트가 차단된다.
        /// </summary>
        internal bool IsPooled { get; set; } = true;

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

            // 반복 재생(상태이상 등)은 끝이 없으므로 페이드아웃 대상이 아니다.
            _fadeOutFrames = definition.loop ? 0 : Mathf.Clamp(definition.fadeOutFrames, 0, _frames.Length - 1);
            _baseColor = tint ?? Color.white;

            _renderer.sprite = _frames[0];
            _renderer.flipX = definition.canFlip && flipX;
            _renderer.color = _baseColor;
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

            UpdateFadeAlpha(frameDuration);
        }

        /// <summary>
        /// 마지막 N프레임 구간에서 알파를 선형으로 줄인다.
        /// 프레임 단위로만 줄이면 12FPS에서 계단이 보이므로 프레임 진행률까지 섞어 부드럽게 만든다.
        /// </summary>
        private void UpdateFadeAlpha(float frameDuration)
        {
            if (_fadeOutFrames <= 0) return;

            // 현재 프레임을 포함해 남은 프레임 수.
            int remaining = _frames.Length - _frameIndex;
            if (remaining > _fadeOutFrames) return; // 아직 페이드 구간이 아니다

            float progress = frameDuration > 0f ? Mathf.Clamp01(_timer / frameDuration) : 0f;
            float t = Mathf.Clamp01((remaining - progress) / (_fadeOutFrames + 1f));

            Color c = _baseColor;
            c.a *= t;
            _renderer.color = c;
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
