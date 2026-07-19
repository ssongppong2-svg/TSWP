// 근거: 보스 시스템.md — 협동 퍼즐: 모든 보스는 협동 퍼즐을 반드시 포함한다.
//       퍼즐은 혼자 해결하기 어렵도록 설계한다 (최소 참여 인원 필요 — 스펙 CoopPuzzleType notes).
// 스펙 unityNotes ④·⑥ — 인터페이스로 플러그인화, 동시성 판정(동시 레버 등)은 허용 오차 필요.
using System;

namespace TSWP.Bosses
{
    /// <summary>
    /// 보스별 협동 퍼즐 플러그인 인터페이스.
    /// 구현체는 보스 방(프리팹 하위)의 MonoBehaviour 컴포넌트 (BossController가 GetComponentsInChildren로 수집).
    /// 예시: 동시에 레버 당기기, 발판 유지, 폭탄 전달, 팀원 보호, 여러 위치에서 동시에 공격.
    /// 진행 상황은 이벤트 기반으로 보스 AI(BossAIContext.PuzzleProgress)에 공급된다.
    /// SYNC: 퍼즐 진행/완료 판정은 호스트 권위. 동시 입력(레버 등)은 허용 오차 창을 두고 판정한다.
    /// </summary>
    public interface ICoopPuzzle
    {
        /// <summary>퍼즐 식별자 (동기화/업적 집계용).</summary>
        string PuzzleId { get; }

        /// <summary>퍼즐 분류 (5종 — CoopPuzzleType).</summary>
        CoopPuzzleType Type { get; }

        /// <summary>최소 참여 인원 — 혼자 해결하기 어렵도록 2 이상 권장. TODO(밸런스): 퍼즐별 문서 미정.</summary>
        int MinPlayers { get; }

        /// <summary>진행도 0~1. 보스 AI가 방해 패턴 트리거 등에 사용.</summary>
        float Progress { get; }

        /// <summary>해결 완료 여부.</summary>
        bool IsSolved { get; }

        /// <summary>퍼즐 해결 통지 → BossController가 PatternChange 단계로 전이.</summary>
        event Action<ICoopPuzzle> Solved;

        /// <summary>진행도 변화 통지 (0~1) → BossAIContext 갱신 (Update 폴링 대신 이벤트 — ARCHITECTURE.md §3-8).</summary>
        event Action<ICoopPuzzle, float> ProgressChanged;

        /// <summary>퍼즐 가동 시작 (협동 퍼즐 단계 진입 시 BossController가 호출).</summary>
        void Begin(BossController owner);

        /// <summary>퍼즐 강제 중단/초기화 (보스 사망·페이즈 강제 전환 시 정리).</summary>
        void Cancel();
    }
}
