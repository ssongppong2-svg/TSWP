// 근거: 퍼즐 시스템.md — 실패 연출 5종(화면 흔들림/효과음/경고 아이콘/NPC 반응/보스의 웃음).
//       목적은 벌이 아니라 피드백이다: "같은 실수를 반복하지 않도록 피드백을 제공한다".
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 실패/오조작 연출 실행기. 연출 구현 자체는 TODO로 두고 호출 흐름만 완성한다.
    /// 접근성 설정(화면 흔들림 감소)을 존중해야 하므로 흔들림은 UI 설정을 확인한 뒤 실행한다.
    /// </summary>
    public class PuzzleFeedbackService : MonoBehaviour
    {
        [Header("연출 리소스")]
        [SerializeField] private AudioClip failureSound;
        [SerializeField] private GameObject warningIconPrefab;

        [Header("화면 흔들림")]
        [SerializeField, Min(0f)] private float shakeDuration = 0.2f;   // TODO(밸런스): 문서 미정
        [SerializeField, Min(0f)] private float shakeMagnitude = 0.15f; // TODO(밸런스): 문서 미정

        /// <summary>연출 목록을 순회 실행한다.</summary>
        public void Play(IReadOnlyList<FailureFeedbackType> feedbacks, Vector3 position)
        {
            if (feedbacks == null) return;

            for (int i = 0; i < feedbacks.Count; i++)
                Play(feedbacks[i], position);
        }

        public void Play(FailureFeedbackType feedback, Vector3 position)
        {
            switch (feedback)
            {
                case FailureFeedbackType.ScreenShake:
                    // TODO(연출): 카메라 흔들림. 접근성 설정 'reduceScreenShake'가 켜져 있으면 생략해야 한다.
                    break;

                case FailureFeedbackType.SoundEffect:
                    if (failureSound != null)
                        AudioSource.PlayClipAtPoint(failureSound, position);
                    break;

                case FailureFeedbackType.WarningIcon:
                    if (warningIconPrefab != null)
                        Instantiate(warningIconPrefab, position, Quaternion.identity);
                    break;

                case FailureFeedbackType.NpcReaction:
                    // TODO(연출): NPC 반응 대사/애니메이션.
                    break;

                case FailureFeedbackType.BossLaugh:
                    // TODO(연출): 보스 웃음 — 보스전 중일 때만 유효.
                    break;
            }
        }
    }
}
