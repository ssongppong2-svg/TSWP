// 근거: ARCHITECTURE.md §4 TSWP.Player 절 — "퍼즐의 직업 상호작용은 능력 인터페이스로" (Player 폴더에 정의, Jobs가 구현).
// 근거: 조작과 시스템.md 개발 원칙 — "모든 퍼즐은 협동을 요구한다" (직업별 능력 조합이 협동 퍼즐의 축).
// Puzzles는 플레이어 오브젝트에서 GetComponent<I...>로 능력 보유 여부만 질의한다 —
// jobId 문자열 분기 금지(데이터/능력 주도, ARCHITECTURE.md: 직업 enum 금지 원칙과 동일 취지).
// 구현 예정 매핑(직업 문서 소관, 여기서는 예시): architect=발판 건설, bomber=벽 파괴, shieldbearer=함정 차단,
// archer/mage=원거리 장치 작동, doctor=독 지원.
using UnityEngine;

namespace TSWP.Player
{
    /// <summary>발판/구조물 건설 능력 (예: architect). 퍼즐의 '건설 지점' 장치가 질의한다.</summary>
    public interface IPlatformBuilder
    {
        /// <summary>해당 위치에 건설 가능한지 (지형/개수 제한 검사).</summary>
        bool CanBuildAt(Vector2 worldPosition);

        /// <summary>발판 건설 시도. 성공 시 true. 건설물은 Combat.Structure(architectBuilt 예외 규칙) 연계.</summary>
        bool TryBuildPlatform(Vector2 worldPosition);
    }

    /// <summary>벽/구조물 파괴 능력 (예: bomber — 구조물은 폭발 공격만 파괴 가능, 전투 시스템.md).</summary>
    public interface IWallBreaker
    {
        /// <summary>대상 벽/구조물 파괴 시도. 성공 시 true.</summary>
        bool TryBreakWall(GameObject wallObject);
    }

    /// <summary>함정 차단/무력화 능력 (예: shieldbearer — 함정 위를 막아 팀원 통행 보조).</summary>
    public interface ITrapBlocker
    {
        /// <summary>대상 함정 차단 시도. 성공 시 true.</summary>
        bool TryBlockTrap(GameObject trapObject);
    }

    /// <summary>원거리 장치 작동 능력 (예: archer/mage — 멀리 있는 버튼·표적을 명중시켜 작동).</summary>
    public interface IRangedActivator
    {
        /// <summary>지정 지점의 장치를 원거리에서 작동 시도. 성공 시 true (사거리 검사 포함).</summary>
        bool TryActivateAt(Vector2 targetWorldPosition);
    }

    /// <summary>독 지원 능력 (예: doctor — 독을 퍼즐 촉매/지원 요소로 사용).</summary>
    public interface IPoisonSupport
    {
        /// <summary>대상에 독 지원 적용 시도. 성공 시 true (상태이상 부여는 StatusEffects 경유).</summary>
        bool TryApplyPoison(GameObject target);
    }
}
