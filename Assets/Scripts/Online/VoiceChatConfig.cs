// 근거: 온라인 시스템.md / 조작과 시스템.md — 음성 채팅은 항상 활성화(Open Mic)이며 Push To Talk는 지원하지 않는다.
//       음성은 거리·벽·방 구조의 영향을 받는다. 멀어지면 작아지고, 벽 뒤에서는 거의 들리지 않으며, 방마다 울림이 다르다.
//       음성 시스템은 게임 플레이의 핵심 요소다.
// TODO(Vivox 또는 Steam Voice): 실제 음성 전송은 미도입. 이 SO는 감쇠 파라미터만 보관한다.
using UnityEngine;

namespace TSWP.Online
{
    /// <summary>
    /// 거리 기반 음성 감쇠 설정. PTT 코드 경로는 만들지 않는다(문서상 미지원).
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Online/Voice Chat Config", fileName = "VoiceChatConfig")]
    public class VoiceChatConfig : ScriptableObject
    {
        [Header("거리 감쇠")]
        [Tooltip("이 거리를 넘으면 목소리가 들리지 않는다.")]
        [Min(1f)] public float maxAudibleDistance = 20f; // TODO(밸런스): 문서 미정

        [Tooltip("거리(0~1 정규화) → 음량 배율. 멀수록 작아진다.")]
        public AnimationCurve distanceFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("벽 차폐")]
        [Tooltip("벽 뒤에 있을 때 곱해지는 음량 배율 — 거의 들리지 않아야 한다.")]
        [Range(0f, 1f)] public float wallOcclusionFactor = 0.1f; // TODO(밸런스): 문서 미정

        [Tooltip("차폐 판정에 사용할 레이어 (Physics2D.Linecast).")]
        public LayerMask occlusionMask;

        [Header("방 울림")]
        [Tooltip("방마다 다른 울림을 적용할지. 실제 리버브 프리셋은 VoiceZone이 지정한다.")]
        public bool echoEnabled = true;

        [Header("밈 모드")]
        [Tooltip("밈 모드의 '음성이 이상하게 변조된다' 규칙용 훅. 기본은 비활성.")]
        public bool voiceModulationEnabled;

        [Tooltip("변조 강도 (밈 모드에서만 사용).")]
        [Range(0f, 1f)] public float modulationAmount = 0.5f;

        /// <summary>
        /// 두 지점 사이의 음량 배율을 계산한다.
        /// 거리 감쇠 × (벽이 막혔으면 차폐 계수).
        /// </summary>
        public float CalculateVolume(Vector2 listener, Vector2 speaker)
        {
            float distance = Vector2.Distance(listener, speaker);
            if (distance >= maxAudibleDistance) return 0f;

            float normalized = distance / maxAudibleDistance;
            float volume = Mathf.Clamp01(distanceFalloff.Evaluate(normalized));

            // 벽 차폐 — 시야가 막히면 거의 들리지 않는다.
            if (Physics2D.Linecast(listener, speaker, occlusionMask))
                volume *= wallOcclusionFactor;

            return volume;
        }
    }
}
