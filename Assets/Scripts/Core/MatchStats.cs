// 근거: 게임 시작과 선택, 직업, 플레이.md(결과 화면) / UI 시스템.md(결과 화면 데이터)
// 매치 통계는 RunManager가 GameEvents를 구독해 누적한다. UI/Online은 이 타입을 참조만 한다.
using System;
using System.Collections.Generic;

namespace TSWP.Core
{
    /// <summary>플레이어별 매치 누적 통계 (결과 화면 항목과 1:1).</summary>
    [Serializable]
    public sealed class PlayerMatchStats
    {
        public int PlayerId;
        public float DamageDealt;    // 가장 많은 피해
        public float HealingDone;    // 가장 많은 회복
        public int Kills;            // 가장 많은 처치
        public int Deaths;           // 가장 많이 사망
        public int Rescues;          // 가장 많은 구조
        public int ItemsAcquired;    // 가장 많은 아이템 획득
        public int TrollScore;       // 가장 많은 트롤(?) — TODO(밸런스): 산정 방식 문서 미정 (아군 피해 기반 후보)
        public int PingsUsed;
    }

    /// <summary>결과 화면 + 뒷풀이 '결과 확인'에서 사용하는 매치 결과.</summary>
    [Serializable]
    public sealed class MatchResult
    {
        public int MvpPlayerId;      // TODO(밸런스): MVP 산식 문서 미정
        public List<PlayerMatchStats> PerPlayerStats = new();
        public TimeSpan PlayTime;
        public List<string> ClearedBossIds = new();
        public List<string> AcquiredItemIds = new();
    }
}
