// 테스트 씬 자동 생성기 (에디터 전용).
// 메뉴 [TSWP > 테스트 씬 만들기]를 누르면 플레이 가능한 프로토타입 씬을 만들고 열어준다.
//
// 이 스크립트가 필요한 이유:
//   PlayerController의 groundMask 같은 private 필드는 인스펙터로만 설정할 수 있는데,
//   기본값(~0 = 모든 레이어)이면 접지 판정이 자기 콜라이더를 감지해 무한 점프가 된다.
//   여기서는 SerializedObject로 정확히 Ground 레이어만 지정한다.
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TSWP.Combat;
using TSWP.Core;
using TSWP.Jobs;
using TSWP.Player;
using TSWP.Sandbox;
using TSWP.StatusEffects;

namespace TSWP.EditorTools
{
    public static class SandboxSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Sandbox.unity";
        private const string SpriteFolder = "Assets/Sprites/Placeholder";
        private const string GroundLayerName = "Ground";

        [MenuItem("TSWP/테스트 씬 만들기", priority = 0)]
        public static void BuildAndOpen()
        {
            EnsureGroundLayer();
            Sprite square = EnsureSquareSprite();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();
            var player = CreatePlayer(square);
            CreateGround(square);
            CreateDummies(square);
            CreateCamera(player);
            CreateHud(player);
            CreateManagers();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);

            AddSceneToBuildSettings();

            Debug.Log("[TSWP] 테스트 씬을 만들었습니다. 상단 ▶ 재생 버튼을 누르면 바로 조작할 수 있습니다.\n" +
                      "A/D 이동, Space 점프, Shift 달리기, 좌클릭 공격.");
            EditorUtility.DisplayDialog("TSWP",
                "테스트 씬이 준비됐습니다.\n\n▶ 재생 버튼을 누르세요.\n\nA/D 이동 · Space 점프 · Shift 달리기 · 좌클릭 공격", "확인");
        }

        // ── 플레이어 ──────────────────────────────────────────────

        private static GameObject CreatePlayer(Sprite sprite)
        {
            var go = new GameObject("Player");
            go.transform.position = new Vector3(0f, 1f, 0f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.35f, 0.6f, 0.95f); // 용사 = 파랑 (팔레트 시스템.md)
            renderer.sortingOrder = 10;

            // 임시 몸통 0.8 x 1.0 — 콜라이더 하단이 정확히 중심에서 -0.5가 되어 접지 판정이 단순해진다.
            go.transform.localScale = new Vector3(0.8f, 1f, 1f);

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 1f);

            // 마찰 0 — 공중에서 벽을 밀고 있어도 벽에 붙지 않고 정상 낙하한다.
            // PlayerController가 매 FixedUpdate에 velocity.x를 직접 지정하므로 마찰이 필요 없다.
            collider.sharedMaterial = EnsureZeroFrictionMaterial();

            // 전투 — 플레이어 진영, playerId 0
            var entity = go.AddComponent<CombatEntity>();
            SetPrivateField(entity, "team", (int)TeamType.Players, isEnum: true);
            SetPrivateField(entity, "ownerPlayerId", 0);
            SetPrivateField(entity, "maxHp", 100f);

            go.AddComponent<StatusEffectController>();
            go.AddComponent<PlayerStats>();
            go.AddComponent<BasicAttacker>();

            var controller = go.AddComponent<PlayerController>();

            // 접지 판정은 Ground 레이어만 본다 (자기 콜라이더 오검출 방지 — 무한 점프 차단)
            var so = new SerializedObject(controller);
            var maskProp = so.FindProperty("groundMask");
            if (maskProp != null) maskProp.intValue = 1 << LayerMask.NameToLayer(GroundLayerName);

            // 콜라이더 하단(-0.5) 바로 아래를 검사
            var offsetProp = so.FindProperty("groundCheckOffset");
            if (offsetProp != null) offsetProp.vector2Value = new Vector2(0f, -0.5f);

            // 콜라이더 하단(-0.5)에서 아래로 0.15 — 지면과 확실히 겹치되 공중에서는 닿지 않는 여유값
            var radiusProp = so.FindProperty("groundCheckRadius");
            if (radiusProp != null) radiusProp.floatValue = 0.15f;

            so.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<PlayerInteraction>();

            return go;
        }

        // ── 지형 ──────────────────────────────────────────────────

        private static void CreateGround(Sprite sprite)
        {
            int groundLayer = LayerMask.NameToLayer(GroundLayerName);
            var root = new GameObject("Level");

            // 바닥
            CreateBlock(root, sprite, "Floor", new Vector2(0f, -1f), new Vector2(40f, 1f), groundLayer,
                        new Color(0.28f, 0.30f, 0.34f));

            // 점프로 올라갈 발판들 — 점프 높이와 거리 감각을 확인하기 위한 배치
            CreateBlock(root, sprite, "Platform_1", new Vector2(5f, 1.2f), new Vector2(3f, 0.5f), groundLayer,
                        new Color(0.35f, 0.38f, 0.42f));
            CreateBlock(root, sprite, "Platform_2", new Vector2(10f, 3f), new Vector2(3f, 0.5f), groundLayer,
                        new Color(0.35f, 0.38f, 0.42f));
            CreateBlock(root, sprite, "Platform_3", new Vector2(-6f, 2f), new Vector2(4f, 0.5f), groundLayer,
                        new Color(0.35f, 0.38f, 0.42f));

            // 좌우 벽 — 밖으로 나가지 않도록
            CreateBlock(root, sprite, "Wall_Left", new Vector2(-20f, 3f), new Vector2(1f, 10f), groundLayer,
                        new Color(0.22f, 0.24f, 0.28f));
            CreateBlock(root, sprite, "Wall_Right", new Vector2(20f, 3f), new Vector2(1f, 10f), groundLayer,
                        new Color(0.22f, 0.24f, 0.28f));
        }

        private static void CreateBlock(GameObject parent, Sprite sprite, string name,
                                        Vector2 position, Vector2 size, int layer, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.layer = layer;

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 0;

            go.AddComponent<BoxCollider2D>();
        }

        // ── 허수아비 ──────────────────────────────────────────────

        private static void CreateDummies(Sprite sprite)
        {
            var root = new GameObject("Dummies");
            CreateDummy(root, sprite, "Dummy_A", new Vector2(3f, 0f));
            CreateDummy(root, sprite, "Dummy_B", new Vector2(-3f, 0f));
            CreateDummy(root, sprite, "Dummy_C", new Vector2(10f, 4f)); // 발판 위 — 점프 공격 확인용
        }

        private static void CreateDummy(GameObject parent, Sprite sprite, string name, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.position = position;
            go.transform.localScale = new Vector3(0.8f, 1f, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 5;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 1f);

            var entity = go.AddComponent<CombatEntity>();
            SetPrivateField(entity, "team", (int)TeamType.Enemies, isEnum: true);
            SetPrivateField(entity, "ownerPlayerId", -1);
            SetPrivateField(entity, "maxHp", 50f);
            SetPrivateField(entity, "autoReviveOnDeath", false);

            go.AddComponent<StatusEffectController>();
            go.AddComponent<TestDummy>();
        }

        // ── 카메라 / HUD / 매니저 ─────────────────────────────────

        private static void CreateCamera(GameObject player)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 1.5f, -10f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.14f); // 배경 = 어두운 회색 (팔레트 시스템.md)
            cam.clearFlags = CameraClearFlags.SolidColor;

            go.AddComponent<AudioListener>();

            var follow = go.AddComponent<SandboxCamera>();
            follow.SetTarget(player.transform);
        }

        private static void CreateHud(GameObject player)
        {
            var go = new GameObject("SandboxHud");
            var hud = go.AddComponent<SandboxHud>();
            hud.SetPlayer(player.GetComponent<PlayerController>());
        }

        private static void CreateManagers()
        {
            // 게임 흐름/런 매니저 — 공유 부활 횟수 등 Core 시스템 동작 확인용.
            var go = new GameObject("GameManagers");
            go.AddComponent<GameFlowManager>();
            go.AddComponent<RunManager>();
        }

        private static void CreateLighting()
        {
            // 2D URP는 별도 광원 없이도 Sprite-Unlit으로 보이므로 환경광만 정리한다.
            RenderSettings.ambientLight = Color.white;
        }

        // ── 유틸 ──────────────────────────────────────────────────

        /// <summary>인스펙터 전용 private 필드를 SerializedObject로 설정한다.</summary>
        private static void SetPrivateField(Object target, string fieldName, object value, bool isEnum = false)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[SandboxSceneBuilder] '{target.GetType().Name}'에 '{fieldName}' 필드가 없습니다.");
                return;
            }

            switch (value)
            {
                case int i when isEnum: prop.enumValueIndex = i; break;
                case int i: prop.intValue = i; break;
                case float f: prop.floatValue = f; break;
                case bool b: prop.boolValue = b; break;
                default:
                    Debug.LogWarning($"[SandboxSceneBuilder] 지원하지 않는 타입: {value?.GetType().Name}");
                    return;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Ground 레이어가 없으면 빈 사용자 레이어에 추가한다.</summary>
        private static void EnsureGroundLayer()
        {
            if (LayerMask.NameToLayer(GroundLayerName) != -1) return;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            // 0~7은 Unity 예약 레이어이므로 8번부터 탐색한다.
            for (int i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(element.stringValue)) continue;

                element.stringValue = GroundLayerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"[TSWP] '{GroundLayerName}' 레이어를 {i}번에 추가했습니다.");
                return;
            }

            Debug.LogError("[TSWP] 빈 레이어 슬롯이 없어 Ground 레이어를 만들지 못했습니다.");
        }

        /// <summary>
        /// 마찰 0 물리 머티리얼. 공중에서 벽에 밀착했을 때 마찰로 낙하가 멈추는 문제를 막는다
        /// (2D 플랫포머의 전형적인 '벽 끼임' 현상).
        /// </summary>
        private static PhysicsMaterial2D EnsureZeroFrictionMaterial()
        {
            const string path = "Assets/Settings/Physics/NoFriction.physicsMaterial2D";

            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var material = new PhysicsMaterial2D("NoFriction")
            {
                friction = 0f,   // 벽·바닥과의 마찰 제거
                bounciness = 0f, // 튕김 없음
            };

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>임시 흰 사각형 스프라이트를 생성한다 (실제 도트는 추후 교체).</summary>
        private static Sprite EnsureSquareSprite()
        {
            const string path = SpriteFolder + "/square.png";

            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(SpriteFolder);

            // 16x16 흰 사각형 — PPU 16이므로 월드 1유닛에 대응한다 (도트 시스템.md).
            var texture = new Texture2D(16, 16);
            var pixels = new Color32[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            // 픽셀아트 임포트 규칙: Point 필터, 무압축, PPU 16 (도트 시스템.md)
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>빌드 설정에 테스트 씬을 등록한다 (없을 때만).</summary>
        private static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == ScenePath) return;

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = updated;
        }
    }
}
#endif
