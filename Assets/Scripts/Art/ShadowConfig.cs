// 근거: 팔레트 시스템.md / 도트 시스템.md — 그림자는 순수 검정을 사용하지 않는다(짙은 남색 또는 짙은 회색).
//       투명도 0.4~0.6, 형태는 원형, 크기는 고정.
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>캐릭터·오브젝트 그림자 규칙.</summary>
    [CreateAssetMenu(menuName = "TSWP/Art/Shadow Config", fileName = "ShadowConfig")]
    public class ShadowConfig : ScriptableObject
    {
        [Header("색")]
        [Tooltip("짙은 남색 또는 짙은 회색 — 순수 검정(0,0,0) 금지.")]
        public Color color = new Color(0.09f, 0.10f, 0.18f, 1f);

        [Tooltip("그림자 투명도 (문서 권장 0.4~0.6).")]
        [Range(0f, 1f)] public float alpha = 0.5f;

        [Header("형태")]
        [Tooltip("원형 그림자 스프라이트.")]
        public Sprite shadowSprite;

        [Tooltip("고정 크기 — 높이에 따라 변하지 않는다.")]
        public Vector2 size = new Vector2(1f, 0.35f);

        [Tooltip("발밑 오프셋.")]
        public Vector2 offset = new Vector2(0f, -0.05f);

        /// <summary>알파가 적용된 최종 색.</summary>
        public Color GetFinalColor()
        {
            var c = color;
            c.a = alpha;
            return c;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 순수 검정 금지 규칙 확인
            if (color.r <= 0.01f && color.g <= 0.01f && color.b <= 0.01f)
                Debug.LogWarning($"[ShadowConfig] '{name}': 순수 검정 그림자는 사용하지 않습니다 — 짙은 남색/회색을 쓰세요.", this);

            if (alpha < 0.4f || alpha > 0.6f)
                Debug.LogWarning($"[ShadowConfig] '{name}': 그림자 투명도 권장 범위는 0.4~0.6입니다 (현재 {alpha:0.00}).", this);
        }
#endif
    }
}
