// 근거: 전투 시스템.md — "공격은 명확해야 한다" (피격이 눈에 보여야 한다).
// 근거: 성능 감사 보고 §6 — 타격마다 코루틴 + GetComponentInChildren을 돌리던 플래시를
//   엔티티 쪽 타이머로 옮긴다. 코루틴이 겹치며 원본 색으로 '흰색'을 붙잡아 캐릭터가
//   영구히 하얗게 남던 버그를 구조적으로 제거한다 (원본 색은 '플래시 중이 아닐 때'만 캡처).
using UnityEngine;

namespace TSWP.Combat
{
    /// <summary>
    /// 피격 플래시 담당 컴포넌트. HitFeedback이 대상에 없으면 자동으로 붙인다.
    /// 스프라이트가 없는 유닛(구조물 스텁 등)에서는 조용히 아무 것도 하지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HitFlash : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Color _baseColor = Color.white;
        private float _remaining;
        private bool _flashing;

        private void Awake()
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_renderer != null) _baseColor = _renderer.color;
            enabled = false; // 평시 Update 비용 0 — 플래시가 시작될 때만 켠다.
        }

        /// <summary>
        /// 다른 시스템(상태이상 틴트·허수아비 등)이 기본 색을 바꿨을 때 알려준다.
        /// 알려주지 않아도 다음 플래시 시작 시점에 현재 색을 다시 캡처하므로 치명적이지 않다.
        /// </summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            if (!_flashing && _renderer != null) _renderer.color = color;
        }

        /// <summary>플래시 시작. 이미 진행 중이면 지속시간만 갱신한다(색 캡처를 다시 하지 않는다 = 흰색 고착 방지).</summary>
        public void Flash(Color color, float duration)
        {
            if (_renderer == null || duration <= 0f) return;

            if (!_flashing)
            {
                _baseColor = _renderer.color; // 진행 중이 아닐 때만 원본을 캡처한다
                _flashing = true;
            }

            _remaining = Mathf.Max(_remaining, duration);
            _renderer.color = color;
            enabled = true;
        }

        private void Update()
        {
            // 히트스톱(timeScale 0 근처) 중에도 플래시는 흘러야 한다 → unscaled.
            _remaining -= Time.unscaledDeltaTime;
            if (_remaining > 0f) return;

            Restore();
        }

        private void OnDisable()
        {
            // 비활성화 중에도 색이 흰 채로 남지 않게 한다.
            if (_flashing) Restore();
        }

        private void Restore()
        {
            _flashing = false;
            _remaining = 0f;
            if (_renderer != null) _renderer.color = _baseColor;
            enabled = false;
        }
    }
}
