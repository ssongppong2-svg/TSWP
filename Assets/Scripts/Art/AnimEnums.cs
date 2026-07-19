// 근거: 도트 시스템.md — 플레이어/보스/몬스터 애니메이션 목록, 이펙트 종류, 스프라이트 폴더 분류.
// 좌우 반전은 별도 스프라이트를 만들지 않고 SpriteRenderer.flipX로 처리한다 (CharacterVisual).
namespace TSWP.Art
{
    /// <summary>플레이어 애니메이션 상태.</summary>
    public enum PlayerAnimState
    {
        Idle,
        Run,
        Jump,
        Fall,
        Land,
        Attack,
        Skill,
        Hit,
        Death,
        Revive,
        Emote,
    }

    /// <summary>보스 애니메이션 상태.</summary>
    public enum BossAnimState
    {
        Idle,
        Walk,
        Attack,
        Skill,
        Stun,
        Enrage,   // 광폭화
        Hit,
        Death,
    }

    /// <summary>일반 몬스터 애니메이션 상태.</summary>
    public enum MonsterAnimState
    {
        Idle,
        Move,
        Attack,
        Hit,
        Death,
    }

    /// <summary>이펙트 종류. 색상 매핑은 EffectColorConfig가 담당한다.</summary>
    public enum EffectType
    {
        Explosion, // 폭발
        Slash,     // 베기
        Heal,      // 회복
        Poison,    // 독
        Fire,      // 화염
        Ice,       // 얼음
        Shock,     // 감전
        Bleed,     // 출혈
        Buff,      // 버프
        Debuff,    // 디버프
        Smoke,     // 연기
    }

    /// <summary>스프라이트 분류 — 폴더 구조 및 파일명 규칙(category_name_action)의 category에 대응.</summary>
    public enum SpriteCategory
    {
        Player,
        Boss,
        Enemy,
        Item,
        Tiles,
        UI,
        Effect,
        Emote,
        Background,
    }
}
