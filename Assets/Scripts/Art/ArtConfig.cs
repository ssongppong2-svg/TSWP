// 근거: 도트 시스템.md — 기준 해상도 1920x1080(16:9), 스프라이트는 8의 배수 권장,
//       캐릭터 32x32, 보스 64/96/128, 소품 16x16, 타일 16 또는 32, 애니메이션 12FPS.
// 임포트 설정: Filter Mode = Point (No Filter), Compression = None, PPU는 타일 크기에 맞춘다.
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>
    /// 아트 규격 상수 모음. 스프라이트 검증(SpriteImportValidator)과 카메라 설정이 이 값을 참조한다.
    /// </summary>
    [CreateAssetMenu(menuName = "TSWP/Art/Art Config", fileName = "ArtConfig")]
    public class ArtConfig : ScriptableObject
    {
        [Header("해상도")]
        [Tooltip("기준 해상도 — 16:9.")]
        public Vector2Int baseResolution = new Vector2Int(1920, 1080);

        [Header("스프라이트 규격")]
        [Tooltip("스프라이트 한 변은 이 값의 배수를 권장한다.")]
        [Min(1)] public int spriteSizeMultiple = 8;

        [Tooltip("플레이어 캐릭터 크기(px).")]
        [Min(1)] public int playerSpriteSize = 32;

        [Tooltip("보스 크기(px) — 3종. 밈 모드의 '보스 크기 랜덤'은 이 중에서 고른다.")]
        public int[] bossSpriteSizes = { 64, 96, 128 };

        [Tooltip("상자/폭탄/아이템 등 소품 크기(px).")]
        [Min(1)] public int smallObjectSize = 16;

        [Tooltip("타일 크기(px) — 16 또는 32.")]
        public int[] tileSizes = { 16, 32 };

        [Header("애니메이션")]
        [Tooltip("픽셀아트 기준 프레임레이트.")]
        [Min(1)] public int animationFps = 12;

        [Header("임포트")]
        [Tooltip("Pixels Per Unit — 타일 16px 기준.")]
        [Min(1)] public int pixelsPerUnit = 16;

        /// <summary>
        /// 임포트 필수 설정 (AssetPostprocessor에서 강제):
        ///   Filter Mode = Point, Compression = None, Mip Maps = Off, Wrap Mode = Clamp.
        /// 이 값이 어긋나면 픽셀이 뭉개져 시인성 원칙이 깨진다.
        /// </summary>
        public const FilterMode RequiredFilterMode = FilterMode.Point;

        /// <summary>스프라이트 크기가 권장 배수를 따르는지 검사한다.</summary>
        public bool IsValidSpriteSize(int size) => size > 0 && size % spriteSizeMultiple == 0;

        /// <summary>보스 크기로 허용되는 값인지 검사한다.</summary>
        public bool IsValidBossSize(int size)
        {
            for (int i = 0; i < bossSpriteSizes.Length; i++)
                if (bossSpriteSizes[i] == size) return true;
            return false;
        }
    }
}
