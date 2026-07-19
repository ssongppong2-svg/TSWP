// 근거: 퍼즐 시스템.md — 트롤 퍼즐 시스템: 잘못 누름→몬스터 소환, 레버 역조작→함정 개방,
//       폭탄 오투척→다리 붕괴, 발판 이탈→전원 감금, 상자 오조작→숨겨진 적 등장.
// 트롤 원칙: 고의적 방해를 유도하지 않되, 실수는 웃음이 되어야 하고 피드백으로 학습 가능해야 한다.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 오조작 → 결과 매핑 데이터. 요소별로 인스펙터에서 구성한다.
    /// </summary>
    [Serializable]
    public class TrollOutcome
    {
        [Tooltip("어떤 잘못된 조작인가.")]
        public WrongActionType wrongAction = WrongActionType.WrongButtonPress;

        [Tooltip("그 결과 무엇이 벌어지는가.")]
        public PuzzleFailurePenalty consequence = PuzzleFailurePenalty.SpawnEnemies;

        [Tooltip("결과를 알리는 연출 — 원인을 즉시 이해시켜 같은 실수를 반복하지 않게 한다.")]
        public List<FailureFeedbackType> feedbacks = new List<FailureFeedbackType>
        {
            FailureFeedbackType.SoundEffect,
            FailureFeedbackType.WarningIcon,
        };

        [Tooltip("이 오조작이 퍼즐 전체 실패로 이어지는가. false면 결과만 발생하고 퍼즐은 계속된다.")]
        public bool causesPuzzleFailure;

        [Tooltip("상황 설명(개발 메모) — 스트리머 포인트가 되는 장면인지 기록한다.")]
        [TextArea] public string designerNote;
    }
}
