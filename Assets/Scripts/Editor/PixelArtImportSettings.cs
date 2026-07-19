// 근거: 도트 시스템.md — 임포트 설정은 Filter Mode = Point, Compression = None, Mip Map 없음.
//       픽셀이 뭉개지면 시인성 원칙이 깨진다. 180장을 손으로 설정할 수 없으므로 임포트 시 자동 적용한다.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TSWP.EditorTools
{
    /// <summary>
    /// Assets/Sprites 아래 텍스처에 픽셀아트 임포트 규칙을 강제한다.
    /// 이미 임포트된 에셋에는 적용되지 않으므로, 규칙 변경 시 [TSWP > 스프라이트 재임포트]를 실행한다.
    /// </summary>
    public class PixelArtImportSettings : AssetPostprocessor
    {
        private const string SpriteRoot = "Assets/Sprites";
        private const string VfxRoot = "Assets/Sprites/VFX";

        /// <summary>일반 스프라이트 PPU — 타일 16px 기준 (도트 시스템.md).</summary>
        private const int DefaultPixelsPerUnit = 16;

        /// <summary>
        /// 이펙트 PPU — 셀이 64px이라 16으로 두면 4유닛(캐릭터의 2배)이 되어 과도하다.
        /// 32로 두면 2유닛 = 캐릭터 키와 비슷해 타격 이펙트로 적당하다.
        /// </summary>
        private const int VfxPixelsPerUnit = 32;

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(SpriteRoot)) return;

            var importer = (TextureImporter)assetImporter;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;           // 픽셀 보존 — 필수
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;

            importer.spritePixelsPerUnit = assetPath.StartsWith(VfxRoot)
                ? VfxPixelsPerUnit
                : DefaultPixelsPerUnit;
        }

        [MenuItem("TSWP/스프라이트 재임포트 (픽셀아트 규칙 적용)", priority = 20)]
        private static void ReimportAllSprites()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteRoot });
            if (guids.Length == 0)
            {
                Debug.Log("[TSWP] 재임포트할 스프라이트가 없습니다.");
                return;
            }

            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    EditorUtility.DisplayProgressBar("스프라이트 재임포트", path, (float)i / guids.Length);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[TSWP] 스프라이트 {guids.Length}장에 픽셀아트 규칙을 적용했습니다.");
        }
    }
}
#endif
