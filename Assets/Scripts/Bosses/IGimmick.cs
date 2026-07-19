// 근거: 보스 시스템.md — 기믹: 모든 보스는 최소 1개의 핵심 기믹을 가진다.
//       난이도 변경 시에도 기믹은 불변 (난이도는 체력/공격력/패턴 속도 3종만 조정).
// 스펙 unityNotes ④ — 협동 퍼즐/기믹은 인터페이스로 추상화해 보스별 구현을 플러그인화한다.
using System;

namespace TSWP.Bosses
{
    /// <summary>
    /// 보스별 핵심 기믹 플러그인 인터페이스.
    /// 구현체는 보스 프리팹의 MonoBehaviour 컴포넌트로 부착한다 (BossController가 GetComponents로 수집).
    /// 예시: 약점 노출, 버튼 작동, 구조물 파괴, 위치 이동, 역할 분담, 폭탄 사용, 반사.
    /// SYNC: 기믹 진행 상태는 호스트 권위, 추후 NGO NetworkVariable.
    /// </summary>
    public interface IGimmick
    {
        /// <summary>기믹 식별자 (동기화/업적 집계용).</summary>
        string GimmickId { get; }

        /// <summary>기믹 분류 (7종 예시 — GimmickType).</summary>
        GimmickType Type { get; }

        /// <summary>현재 작동 중인지.</summary>
        bool IsRunning { get; }

        /// <summary>기믹 1사이클 완료 통지 (예: 약점 노출 종료, 구조물 파괴 완료). BossController가 구독해 페이싱에 반영.</summary>
        event Action<IGimmick> Completed;

        /// <summary>기믹 작동 시작. 소유 보스 컨트롤러를 전달받아 컨텍스트(위치/페이즈)를 조회한다.</summary>
        void Activate(BossController owner);

        /// <summary>기믹 강제 중단 (보스 사망/페이즈 전환 시 정리).</summary>
        void Interrupt();
    }
}
