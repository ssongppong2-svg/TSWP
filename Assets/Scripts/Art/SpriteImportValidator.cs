// 근거: 도트 시스템.md — 파일명 규칙 category_name_action.png (예: player_warrior_idle.png),
//       임포트 설정 Filter Mode = Point, Compression = None, 스프라이트 크기는 8의 배수 권장.
// 에디터 전용 — 빌드에 포함되지 않는다.
#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace TSWP.Art
{
    /// <summary>
    /// 스프라이트 임포트 규칙 검증 유틸. 메뉴에서 수동 실행한다.
    /// TODO: AssetPostprocessor로 자동 적용까지 확장 (임포트 시점에 Point/None 강제).
    /// </summary>
    public static class SpriteImportValidator
    {
        private const string MenuPath = "TSWP/Art/스프라이트 규칙 검증";

        [MenuItem(MenuPath)]
        public static void ValidateAll()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" });

            if (guids.Length == 0)
            {
                Debug.Log("[SpriteImportValidator] Assets/Sprites에 검사할 텍스처가 없습니다.");
                return;
            }

            int issues = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                issues += ValidateOne(path);
            }

            Debug.Log($"[SpriteImportValidator] 검사 완료 — 텍스처 {guids.Length}개, 문제 {issues}건.");
        }

        private static int ValidateOne(string path)
        {
            int issues = 0;

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return 0;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);

            // ① 파일명 규칙: category_name_action
            if (!IsValidFileName(fileName))
            {
                Debug.LogWarning($"[SpriteImportValidator] 파일명 규칙 위반 (category_name_action): {path}");
                issues++;
            }

            // ② Filter Mode = Point — 픽셀이 뭉개지면 시인성 원칙이 깨진다
            if (importer.filterMode != FilterMode.Point)
            {
                Debug.LogWarning($"[SpriteImportValidator] Filter Mode는 Point여야 합니다: {path}");
                issues++;
            }

            // ③ Compression = None
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                Debug.LogWarning($"[SpriteImportValidator] Compression은 None이어야 합니다: {path}");
                issues++;
            }

            return issues;
        }

        /// <summary>category_name_action 형식인지 검사 (밑줄로 구분된 3토막 이상).</summary>
        private static bool IsValidFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;

            string[] parts = fileName.Split('_');
            if (parts.Length < 3) return false;

            // 첫 토막은 SpriteCategory 이름과 대소문자 무시 일치해야 한다.
            return Enum.TryParse(parts[0], true, out SpriteCategory _);
        }
    }
}
#endif
