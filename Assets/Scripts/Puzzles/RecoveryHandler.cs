// 근거: 퍼즐 시스템.md — 리커버리 시스템: 실패한 퍼즐은 반드시 재도전할 수 있어야 한다.
//       "게임 진행이 막혀서는 안 된다" — 이 클래스의 유일한 불변식은 '어떤 경로로든 Active로 돌아간다'이다.
using System.Collections;
using UnityEngine;

namespace TSWP.Puzzles
{
    /// <summary>
    /// 퍼즐 실패 후 재도전 준비를 담당한다. 모든 RecoveryMethod는 반드시 CompleteRecovery로 수렴한다
    /// (소프트락 금지 불변식 — 어떤 분기도 재도전 없이 끝나지 않는다).
    /// </summary>
    public class RecoveryHandler
    {
        private readonly PuzzleController _controller;
        private Coroutine _running;

        public RecoveryHandler(PuzzleController controller)
        {
            _controller = controller;
        }

        public void Begin(PuzzleDefinition definition, MonoBehaviour host)
        {
            if (host == null || _controller == null) return;

            if (_running != null)
                host.StopCoroutine(_running);

            _running = host.StartCoroutine(Run(definition));
        }

        private IEnumerator Run(PuzzleDefinition definition)
        {
            RecoveryMethod method = definition != null ? definition.Recovery : RecoveryMethod.ResetButtons;
            float wait = definition != null ? definition.RecoveryWaitSeconds : 3f;

            switch (method)
            {
                case RecoveryMethod.WaitTimer:
                    yield return new WaitForSeconds(wait);
                    break;

                case RecoveryMethod.SpawnNewLever:
                    // TODO: 새 레버 오브젝트 생성 — 기존 레버가 파괴된 경우의 복구 경로.
                    yield return new WaitForSeconds(wait);
                    break;

                case RecoveryMethod.ResetButtons:
                    // 요소 초기화는 컨트롤러의 ResetPuzzle이 수행한다 (CompleteRecovery 내부).
                    yield return new WaitForSeconds(wait);
                    break;

                case RecoveryMethod.OpenAlternatePath:
                    // TODO: 우회 경로 개방을 Map 시스템에 요청 — 퍼즐 자체는 포기 가능해야 한다.
                    yield return new WaitForSeconds(wait);
                    break;
            }

            // 어떤 분기를 타든 반드시 여기로 온다 — 재도전 보장.
            _controller.CompleteRecovery();
            _running = null;
        }
    }
}
