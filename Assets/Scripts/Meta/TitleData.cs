// 근거: 이름 시스템.md — 칭호는 닉네임 앞에 붙는다("[폭탄 전문가] ssong").
//       칭호는 업적/이벤트/시즌/개발자 이벤트로 획득하며 색상을 가진다(기본 흰색, 전설 금색, 개발자 보라색).
using UnityEngine;

namespace TSWP.Meta
{
    /// <summary>
    /// 칭호 정의 SO. 실제 Color 값은 TSWP.Art.TitleColorConfig가 매핑한다 (색상 추후 변경 가능).
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Meta/Title", fileName = "Title_")]
    public class TitleData : ScriptableObject
    {
        [Header("식별")]
        public string titleId;

        [Tooltip("표시 문구 (예: 폭탄 전문가, 신입 모험가).")]
        public string displayText;

        [Header("표시")]
        public TitleColorType colorType = TitleColorType.Default;

        [Header("획득")]
        public TitleSource source = TitleSource.Achievement;

        [Tooltip("업적으로 획득하는 경우 해당 업적 id (참고용).")]
        public string sourceAchievementId;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(titleId))
                Debug.LogWarning($"[TitleData] '{name}': titleId가 비어 있습니다.", this);

            if (source == TitleSource.DeveloperEvent && colorType != TitleColorType.Developer)
                Debug.LogWarning($"[TitleData] '{name}': 개발자 이벤트 칭호는 Developer 색상을 권장합니다.", this);
        }
#endif
    }
}
