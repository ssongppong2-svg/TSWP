// 근거: 게임 시작과 선택, 직업, 플레이.md / 전투 시스템.md / 아이템 시스템.md / 맵 시스템.md
// 설계 문서에 수치가 명시된 규칙만 상수로 둔다. 미정 수치는 각 SO의 SerializeField로 노출한다.
namespace TSWP.Core
{
    public static class GameRules
    {
        /// <summary>최대 플레이 인원.</summary>
        public const int MaxPlayers = 8;

        /// <summary>공유 부활 횟수 = 인원 × 3.</summary>
        public const int RevivesPerPlayer = 3;

        /// <summary>아이템 장착 슬롯 수.</summary>
        public const int EquipSlotCount = 5;

        /// <summary>아군 공격 기본 피해 배율 (원 피해의 50%). 일부 스킬은 FriendlyFireRule로 오버라이드.</summary>
        public const float FriendlyFireDamageRatio = 0.5f;

        /// <summary>기본 치명타 확률 0% — 치명타는 아이템·버프로만 획득.</summary>
        public const float BaseCritChance = 0f;

        /// <summary>보스 처치 드롭 아이템 개수 범위 (3~4개, 자유 경쟁 선취득).</summary>
        public const int BossDropCountMin = 3;
        public const int BossDropCountMax = 4;

        /// <summary>보스 총 15종, 순서대로 공략, 각 보스는 런에서 1회만 등장.</summary>
        public const int TotalBossCount = 15;

        /// <summary>공유 부활 횟수 초기값. // SYNC: 호스트 권위, 추후 NGO NetworkVariable</summary>
        public static int GetSharedReviveCount(int playerCount) => playerCount * RevivesPerPlayer;

        /// <summary>시작 아이템 드롭 개수 = floor(인원 × 3 / 5). 2명→1, 4명→2, 6명→3, 8명→4.</summary>
        public static int GetStartItemCount(int playerCount) => playerCount * 3 / 5;

        /// <summary>보스 드롭 개수 굴림 (3~4개).</summary>
        public static int RollBossDropCount(System.Random rng) => rng.Next(BossDropCountMin, BossDropCountMax + 1);
    }
}
