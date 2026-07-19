// 근거: 팔레트 시스템.md — UI 색상: 배경=어두운 회색, 텍스트=흰색, 강조=노랑, 경고=빨강, 성공=초록.
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>UI 공통 색상. 패널/텍스트/버튼이 하드코딩 대신 이 값을 참조한다.</summary>
    [CreateAssetMenu(menuName = "TSWP/Art/UI Colors", fileName = "UIColorConfig")]
    public class UIColorConfig : ScriptableObject
    {
        [Header("기본")]
        [Tooltip("배경 — 어두운 회색 (순수 검정 지양).")]
        public Color background = new Color(0.13f, 0.13f, 0.15f, 1f);

        [Tooltip("본문 텍스트 — 흰색.")]
        public Color text = Color.white;

        [Header("상태")]
        [Tooltip("강조 — 노랑.")]
        public Color accent = new Color(1f, 0.85f, 0.2f, 1f);

        [Tooltip("경고 — 빨강.")]
        public Color warning = new Color(0.9f, 0.25f, 0.25f, 1f);

        [Tooltip("성공 — 초록.")]
        public Color success = new Color(0.35f, 0.8f, 0.4f, 1f);

        [Header("보조")]
        [Tooltip("비활성 요소 (쿨타임 중 스킬 아이콘 등).")]
        public Color disabled = new Color(0.45f, 0.45f, 0.48f, 1f);
    }
}
