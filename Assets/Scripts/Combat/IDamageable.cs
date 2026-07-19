// 근거: 전투 시스템.md — 플레이어·적·보스·구조물은 모두 동일한 전투 규칙을 따른다.
namespace TSWP.Combat
{
    public interface IDamageable
    {
        void TakeDamage(in DamageInfo info);
    }
}
