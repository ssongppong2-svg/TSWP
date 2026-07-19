// 근거: 게임 시작과 선택, 직업, 플레이.md / 전투 시스템.md
// 공유 부활 횟수 = 인원 × 3. 사망·낙사 모두 이 시스템의 TryConsume 단일 경로를 탄다.
// SYNC: 호스트 권위, 추후 NGO NetworkVariable 동기화 대상.
using System;

namespace TSWP.Core
{
    public sealed class SharedReviveSystem
    {
        public int Remaining { get; private set; }

        public event Action<int> RemainingChanged;

        public void Initialize(int playerCount)
        {
            Remaining = GameRules.GetSharedReviveCount(playerCount);
            RemainingChanged?.Invoke(Remaining);
            GameEvents.RaiseReviveCountChanged(Remaining);
        }

        /// <summary>
        /// 부활 1회 소모 시도. 소진 상태면 false (해당 플레이어는 부활 불가).
        /// NOTE(기획 확인 필요): 문서상 '사망 시 즉시 부활'이지만 E키 상호작용에 '팀원 부활',
        /// 결과 화면에 '가장 많은 구조' 항목이 있음 — 즉시부활/구조부활 양쪽 모두 이 메서드를 공용 진입점으로 사용한다.
        /// </summary>
        public bool TryConsume()
        {
            if (Remaining <= 0) return false;
            Remaining--;
            RemainingChanged?.Invoke(Remaining);
            GameEvents.RaiseReviveCountChanged(Remaining);
            GameEvents.RaiseStatCounter("revive.count", 1);
            return true;
        }

        /// <summary>게임 오버 = 부활 소진 + 전원 사망. (생존자가 있으면 계속 진행)</summary>
        public bool IsGameOver(int alivePlayerCount) => Remaining <= 0 && alivePlayerCount <= 0;
    }
}
