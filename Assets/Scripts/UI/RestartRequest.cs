// 근거: 게임 시작과 선택, 직업, 플레이.md — 결과/뒷풀이 이후 방장이 '다시 플레이'를 선택하면 새 런이 시작된다.
//       ARCHITECTURE.md §3-5 — UI는 게임 로직을 직접 조작하지 않는다. 여기서는 "요청"만 발행한다.
// 결과 화면·게임오버 화면의 R키가 실제 재시작으로 이어지려면 누군가 이 요청을 받아야 한다.
// Core 담당(런 부트스트랩)이 Requested를 구독해 RunManager.StartRun(...)을 호출하면 루프가 닫힌다.
// 구독자가 아무도 없으면 문서상 '다시 플레이' 경로(GameFlowManager.HostChoseReplay)만이라도 태운다.
using System;
using UnityEngine;
using TSWP.Core;

namespace TSWP.UI
{
    /// <summary>재시작 요청 허브. UI(오버레이의 R키) → 런 부트스트랩 단방향.</summary>
    public static class RestartRequest
    {
        /// <summary>재시작 요청. Core 측 부트스트랩이 구독한다.</summary>
        public static event Action Requested;

        /// <summary>실제 재시작을 수행할 구독자가 있는지. 없으면 UI가 안내 문구를 바꿔 보여준다.</summary>
        public static bool HasHandler => Requested != null;

        // 결과 화면과 게임오버 화면이 동시에 떠 있는 상황에서도 한 프레임에 두 번 재시작되지 않게 한다.
        private static int _lastRaisedFrame = -1;

        /// <summary>재시작을 요청한다. 처리(또는 흐름 전환)가 일어났으면 true.</summary>
        public static bool Raise()
        {
            if (_lastRaisedFrame == Time.frameCount) return false;
            _lastRaisedFrame = Time.frameCount;

            var handler = Requested;
            if (handler != null)
            {
                handler.Invoke();
                return true;
            }

            // 폴백: 재시작 구현이 아직 없어도 흐름은 '다시 플레이'로 되돌려 둔다 (없으면 조용히 생략).
            var flow = GameFlowManager.Instance;
            if (flow != null)
            {
                flow.HostChoseReplay();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 도메인 리로드를 끄고 플레이하는 설정에서 이전 세션의 구독자가 남는 것을 막는다.
        /// (정적 이벤트는 씬 로드로 초기화되지 않는다.)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Requested = null;
            _lastRaisedFrame = -1;
        }
    }
}
