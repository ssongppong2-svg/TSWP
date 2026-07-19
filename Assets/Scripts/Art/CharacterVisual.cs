// 근거: 도트 시스템.md — 좌우 반전은 별도 스프라이트를 만들지 않고 flipX로 처리한다.
//       그림자는 원형·반투명·고정 크기. 애니메이션은 12FPS 기준.
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>
    /// 캐릭터 시각 표현(방향 반전 + 그림자). 플레이어·적·보스 공용.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterVisual : MonoBehaviour
    {
        [Header("스프라이트")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Animator — 클립은 12FPS(ArtConfig.animationFps) 기준으로 제작한다.")]
        [SerializeField] private Animator animator;

        [Header("그림자")]
        [SerializeField] private ShadowConfig shadowConfig;

        [Tooltip("그림자 렌더러(자식 오브젝트). 없으면 그림자를 표시하지 않는다.")]
        [SerializeField] private SpriteRenderer shadowRenderer;

        private bool _facingRight = true;

        public bool FacingRight => _facingRight;

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            ApplyShadowConfig();
        }

        /// <summary>바라보는 방향 설정. 별도 반전 스프라이트 없이 flipX만 사용한다.</summary>
        public void SetFacing(bool right)
        {
            if (_facingRight == right) return;
            _facingRight = right;

            if (spriteRenderer != null)
                spriteRenderer.flipX = !right;
        }

        /// <summary>이동 입력 부호로 방향을 갱신한다 (0이면 유지).</summary>
        public void SetFacingFromSign(int sign)
        {
            if (sign > 0) SetFacing(true);
            else if (sign < 0) SetFacing(false);
        }

        /// <summary>애니메이션 상태 전환 — 문자열 파라미터는 호출 측 enum 이름을 사용한다.</summary>
        public void PlayState(string stateName)
        {
            if (animator == null || string.IsNullOrEmpty(stateName)) return;
            animator.Play(stateName);
        }

        private void ApplyShadowConfig()
        {
            if (shadowConfig == null || shadowRenderer == null) return;

            shadowRenderer.color = shadowConfig.GetFinalColor();

            if (shadowConfig.shadowSprite != null)
                shadowRenderer.sprite = shadowConfig.shadowSprite;

            // 그림자 크기는 고정 — 점프 높이에 따라 변하지 않는다.
            shadowRenderer.transform.localScale = new Vector3(shadowConfig.size.x, shadowConfig.size.y, 1f);
            shadowRenderer.transform.localPosition = shadowConfig.offset;
        }
    }
}
