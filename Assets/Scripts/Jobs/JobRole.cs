// 근거: 직업 시스템.md — 역할군. 직업은 역할을 가지지만 역할을 강제하지 않는다.
// 역할은 UI 표시/설명용 태그일 뿐이며, 게임플레이 로직이 이 값에 의존해서는 안 된다 (스펙 unityNotes ⑦).
namespace TSWP.Jobs
{
    /// <summary>직업 역할군 태그 (참고용 — 강제 아님). JobDefinition.roles 배열로 복수 보유 가능.</summary>
    public enum JobRole
    {
        Melee,      // 근접 공격
        Ranged,     // 원거리 공격
        Support,    // 지원
        Defense,    // 방어
        Utility,    // 유틸리티
        Deployable, // 설치형
        Special,    // 특수 — 문서의 군중 제어(CC) 등 기타 역할 (ARCHITECTURE.md §4 명명 고정)
    }
}
