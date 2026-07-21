// 테스트 씬 자동 생성기 (에디터 전용).
// 메뉴 [TSWP > 테스트 씬 만들기]를 누르면 플레이 가능한 프로토타입 씬을 만들고 열어준다.
//
// 이 스크립트가 필요한 이유:
//   PlayerController의 groundMask 같은 private 필드는 인스펙터로만 설정할 수 있는데,
//   기본값(~0 = 모든 레이어)이면 접지 판정이 자기 콜라이더를 감지해 무한 점프가 된다.
//   여기서는 SerializedObject로 정확히 Ground 레이어만 지정한다.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TSWP.Art;
using TSWP.Combat;
using TSWP.Bosses;
using TSWP.Core;
using TSWP.Enemies;
using TSWP.Jobs;
using TSWP.Map;
using TSWP.Items;
using TSWP.Meta;
using TSWP.UI;
using TSWP.Puzzles;
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
            CreatePostFx(player);
            CreateVfx(player);
            CreateAttackerEnemy(square);
            CreateRangedEnemy(square);
            CreateTestBoss(square);
            CreateMapIntro();
            CreateShowcase(square, player);

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
            go.AddComponent<StatusEffectVisual>(); // 상태이상 부착 이펙트 + 머리 위 표시
            go.AddComponent<PlayerStats>();
            go.AddComponent<BasicAttacker>();

            var controller = go.AddComponent<PlayerController>();

            // 접지 판정 대상 = 지형 + 캐릭터(Default).
            // PlayerController가 자기 콜라이더를 걸러내므로 Default를 포함해도 무한 점프가 나지 않고,
            // 적을 밟고 있을 때도 정상적으로 접지로 인식된다.
            var so = new SerializedObject(controller);
            var maskProp = so.FindProperty("groundMask");
            if (maskProp != null)
                maskProp.intValue = (1 << LayerMask.NameToLayer(GroundLayerName)) | (1 << 0); // 0 = Default

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
            go.AddComponent<StatusEffectVisual>();
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

            // 색수차 등 상태이상 화면 효과를 쓰려면 후처리를 켜야 한다 (URP).
            var urpData = go.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = true;

            // 화면 흔들림 — 오프셋만 제공하고 추적 로직이 더한다(직접 위치를 만지면 추적과 충돌).
            go.AddComponent<CameraShake>();

            var follow = go.AddComponent<SandboxCamera>();
            follow.SetTarget(player.transform);
        }

        private static void CreateHud(GameObject player)
        {
            // 조작감 확인용 정보 패널 — 우측 상단
            var go = new GameObject("SandboxHud");
            var hud = go.AddComponent<SandboxHud>();
            hud.SetPlayer(player.GetComponent<PlayerController>());

            // 실제 게임 UI — 좌측 상단 (HP·스킬·아이템·상태이상·부활 횟수·방 번호)
            var hudGo = new GameObject("GameplayHud");
            var gameplayHud = hudGo.AddComponent<TSWP.UI.GameplayHud>();
            hudGo.AddComponent<TSWP.UI.BossHealthBar>();

            // HUD 브리지 — 이것이 없으면 스킬 쿨타임과 상태이상 칸이 영원히 비어 있다.
            var statusController = player.GetComponent<StatusEffectController>();

            var statusBridge = hudGo.AddComponent<TSWP.UI.StatusEffectHudBridge>();
            var sbSo = new SerializedObject(statusBridge);
            SetPrivateFieldObject2(sbSo, "source", statusController);
            SetPrivateFieldObject2(sbSo, "hud", gameplayHud);
            sbSo.ApplyModifiedPropertiesWithoutUndo();

            var skillBridge = hudGo.AddComponent<TSWP.UI.SkillCooldownHudBridge>();
            var kbSo = new SerializedObject(skillBridge);
            SetPrivateFieldObject2(kbSo, "statusController", statusController);
            SetPrivateFieldObject2(kbSo, "hud", gameplayHud);
            var casters = kbSo.FindProperty("casters");
            if (casters != null)
            {
                var caster = player.GetComponent<SkillCaster>();
                casters.arraySize = caster != null ? 1 : 0;
                if (caster != null) casters.GetArrayElementAtIndex(0).objectReferenceValue = caster;
            }
            kbSo.ApplyModifiedPropertiesWithoutUndo();

            // 알림 토스트 — 매니저(큐)와 뷰(표시)를 함께 붙인다. 뷰가 없으면 알림이 보이지 않는다.
            var notifyGo = new GameObject("Notifications");
            notifyGo.AddComponent<TSWP.UI.NotificationManager>();
            notifyGo.AddComponent<TSWP.UI.NotificationView>();

            // 패널 관리 / 설정 저장
            var uiGo = new GameObject("UIManagers");
            uiGo.AddComponent<TSWP.UI.UIManager>();
            uiGo.AddComponent<TSWP.UI.SettingsManager>();
        }

        private static void SetPrivateFieldObject2(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void CreateManagers()
        {
            // 게임 흐름/런 매니저 — 공유 부활 횟수 등 Core 시스템 동작 확인용.
            var go = new GameObject("GameManagers");
            go.AddComponent<GameFlowManager>();
            go.AddComponent<RunManager>();

            // 타격 연출 — DamageSystem이 피해 적용 직후 자동 호출한다.
            go.AddComponent<TSWP.Combat.HitFeedback>();

            // 데미지 숫자 — 밸런스를 눈으로 확인하는 프로토타입 핵심 도구.
            go.AddComponent<TSWP.UI.DamageNumberSpawner>();

            // 투사체 풀 — Instantiate/Destroy 반복 제거.
            go.AddComponent<TSWP.Combat.ProjectilePool>();
        }

        // ── 맵 인트로 ─────────────────────────────────────────────

        /// <summary>
        /// 테스트용 보스 — BossHealthBar와 패턴 연출을 눈으로 확인하기 위해 배치한다.
        /// 패턴 에셋은 ForestPrototypeBuilder가 만든 것을 재사용한다(없으면 체력바만 보인다).
        /// </summary>
        private static void CreateTestBoss(Sprite sprite)
        {
            // 보스 데이터(+패턴 5종)를 여기서 직접 만든다.
            // 예전에는 없으면 경고만 하고 넘어갔는데, BossController는 데이터 없이 동작할 수 없어
            // 스스로 비활성화된다 — 결국 보스가 아무것도 하지 않는 상태로 씬이 만들어졌다.
            var data = ForestPrototypeBuilder.EnsureHatchQueenData();

            var go = new GameObject("Boss_HatchQueen");
            go.transform.position = new Vector3(16f, 2f, 0f); // 맵 오른쪽 — 다가가면 전투 시작
            go.transform.localScale = new Vector3(3f, 2.6f, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.45f, 0.15f, 0.35f);
            renderer.sortingOrder = 8;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 1f);
            collider.sharedMaterial = EnsureZeroFrictionMaterial();

            var entity = go.AddComponent<CombatEntity>();
            SetPrivateField(entity, "team", (int)TeamType.Enemies, isEnum: true);
            SetPrivateField(entity, "ownerPlayerId", -1);
            SetPrivateField(entity, "maxHp", 1850f);
            SetPrivateField(entity, "autoReviveOnDeath", false);
            SetPrivateField(entity, "isKnockbackImmune", true);

            go.AddComponent<StatusEffectController>();
            go.AddComponent<StatusEffectVisual>();

            var boss = go.AddComponent<BossController>();
            if (data != null) SetPrivateFieldObject(boss, "data", data);
        }

        // ── 전시장 ────────────────────────────────────────────────
        // 만든 기능이 화면에 보여야 검증이 된다. 아이템·직업 스킬·업적·이모트·핑·미니맵·
        // 상호작용 오브젝트를 전부 배치하고 뷰를 붙인다.

        private static void CreateShowcase(Sprite sprite, GameObject player)
        {
            AttachPlayerViews(player);
            CreateDebugTools(player);
            CreateInteractableRow(sprite);
        }

        /// <summary>플레이어에 직업·스킬·이모트·핑 관련 컴포넌트를 붙인다.</summary>
        private static void AttachPlayerViews(GameObject player)
        {
            // 직업 — Q 스킬이 동작하려면 JobDefinition이 필요하다.
            var jobBootstrap = player.AddComponent<JobBootstrapper>();
            SetPrivateFieldObject(jobBootstrap, "job", EnsureWarriorJob());

            player.AddComponent<SkillCaster>();
            player.AddComponent<PassiveHolder>();
            player.AddComponent<PlayerEquipment>();

            // 이모트: T로 휠을 열고 머리 위에 표시
            player.AddComponent<EmoteWheel>();
            player.AddComponent<EmoteWheelView>();
            player.AddComponent<EmoteOverheadView>();

            // 핑: 휠클릭
            player.AddComponent<PingEmitter>();

            // 대쉬 통계 — 업적 카운터로 흘려보낸다
            player.AddComponent<DashStatReporter>();
        }

        /// <summary>디버그 뷰와 도구를 한곳에 모은다. 키 충돌을 여기서 정리한다.</summary>
        private static void CreateDebugTools(GameObject player)
        {
            var go = new GameObject("ShowcaseTools");

            // 아이템 스포너 — F5로 전 아이템을 바닥에 뿌린다 (기본 F1/F2는 다른 뷰와 겹친다)
            var spawner = go.AddComponent<ItemDebugSpawner>();
            var spSo = new SerializedObject(spawner);
            var itemList = spSo.FindProperty("items");
            if (itemList != null)
            {
                var items = LoadAll<ItemDefinition>("Assets/Settings/Items");
                itemList.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++)
                    itemList.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
            }
            SetKey(spSo, "spawnAllKey", KeyCode.F5);
            SetKey(spSo, "spawnRandomKey", KeyCode.F6);
            SetPrivateFieldObject2(spSo, "spawnOrigin", player.transform);
            spSo.ApplyModifiedPropertiesWithoutUndo();

            // 업적 — F7 토글, F8 칭호 순환
            var achievements = go.AddComponent<AchievementView>();
            var acSo = new SerializedObject(achievements);
            SetKey(acSo, "toggleKey", KeyCode.F7);
            SetKey(acSo, "cycleTitleKey", KeyCode.F8);
            SetKey(acSo, "debugAddKey", KeyCode.F9);
            acSo.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<AchievementManager>();
            go.AddComponent<LocalPlayerIdentity>();

            // 퍼즐 상태 HUD — F3 토글, 기본 꺼둔다(화면이 복잡해진다)
            var puzzleHud = go.AddComponent<PuzzleDebugHud>();
            var puSo = new SerializedObject(puzzleHud);
            var visible = puSo.FindProperty("visibleOnStart");
            if (visible != null) visible.boolValue = false;
            SetKey(puSo, "toggleKey", KeyCode.F3);
            puSo.ApplyModifiedPropertiesWithoutUndo();

            // 미니맵 · 상호작용 안내 · 핑 마커
            go.AddComponent<MinimapView>();
            go.AddComponent<PingMarkerView>();

            var prompt = go.AddComponent<InteractionPromptView>();
            SetPrivateFieldObject(prompt, "source", player.GetComponent<PlayerInteraction>());
        }

        /// <summary>상호작용 오브젝트 전시 — 왼쪽에 한 줄로 늘어놓아 하나씩 만져볼 수 있게 한다.</summary>
        private static void CreateInteractableRow(Sprite sprite)
        {
            var root = new GameObject("Interactables");
            float y = 0.2f;

            // 레버 — 단독으로도 당겨진다
            var lever = MakeInteractable(root, sprite, "Lever", new Vector2(-9f, y),
                                         new Vector2(0.6f, 1.2f), new Color(0.7f, 0.6f, 0.3f));
            lever.AddComponent<PuzzleLever>();

            // 버튼
            var button = MakeInteractable(root, sprite, "Button", new Vector2(-11f, y - 0.3f),
                                          new Vector2(1.2f, 0.5f), new Color(0.85f, 0.75f, 0.25f));
            button.AddComponent<PuzzleButton>();

            // 압력 발판 — 밟으면 반응
            var plate = MakeInteractable(root, sprite, "PressurePlate", new Vector2(-13f, -0.4f),
                                         new Vector2(2f, 0.3f), new Color(0.5f, 0.7f, 0.9f));
            plate.AddComponent<PressurePlate>();

            // 밀 수 있는 상자
            var box = MakeInteractable(root, sprite, "PushableBox", new Vector2(-15f, 0.5f),
                                       new Vector2(1f, 1f), new Color(0.6f, 0.45f, 0.3f), physical: true);
            box.AddComponent<PushableBox>();

            // 운반 오브젝트
            var carry = MakeInteractable(root, sprite, "CarryObject", new Vector2(-17f, 0.4f),
                                         new Vector2(0.8f, 0.8f), new Color(0.75f, 0.75f, 0.5f), physical: true);
            carry.AddComponent<CarryObject>();

            // 점프 발판
            var jump = MakeInteractable(root, sprite, "JumpPlatform", new Vector2(-7f, -0.4f),
                                        new Vector2(2f, 0.4f), new Color(0.35f, 0.8f, 0.9f));
            var jumpTrigger = jump.AddComponent<BoxCollider2D>();
            jumpTrigger.isTrigger = true;
            jumpTrigger.size = new Vector2(1f, 4f);
            jumpTrigger.offset = new Vector2(0f, 2f);
            jump.AddComponent<JumpPlatform>();

            // 폭탄 — 들고 던지면 터진다
            var bomb = MakeInteractable(root, sprite, "BombObject", new Vector2(-5f, 0.3f),
                                        new Vector2(0.6f, 0.6f), new Color(0.3f, 0.3f, 0.35f), physical: true);
            bomb.AddComponent<BombObject>();
        }

        private static GameObject MakeInteractable(GameObject parent, Sprite sprite, string name,
                                                   Vector2 position, Vector2 size, Color color,
                                                   bool physical = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 4;

            var collider = go.AddComponent<BoxCollider2D>();
            if (!physical)
            {
                // 상호작용 범위 — 넉넉하게 잡아 E키가 잘 닿게 한다.
                collider.isTrigger = true;
                collider.size = new Vector2(2f, 3f);
            }
            else
            {
                var body = go.AddComponent<Rigidbody2D>();
                body.gravityScale = 3f;
                body.freezeRotation = true;
                collider.sharedMaterial = EnsureZeroFrictionMaterial();
            }

            return go;
        }

        /// <summary>용사 직업 — Q 강타. 강력한 대신 자기 체력을 소모한다(게임 성경: 위험 동반).</summary>
        private static JobDefinition EnsureWarriorJob()
        {
            const string folder = "Assets/Settings/Jobs";
            Directory.CreateDirectory(folder);

            const string skillPath = folder + "/Skill_WarriorSmash.asset";
            var skill = AssetDatabase.LoadAssetAtPath<WarriorSmashSkill>(skillPath);
            if (skill == null)
            {
                skill = ScriptableObject.CreateInstance<WarriorSmashSkill>();
                AssetDatabase.CreateAsset(skill, skillPath);
            }
            var skSo = new SerializedObject(skill);
            SetStr2(skSo, "skillId", "skill.warrior.smash");
            SetStr2(skSo, "displayName", "강타");
            SetFloat2(skSo, "cooldown", 4f);
            skSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skill);

            const string jobPath = folder + "/Job_Warrior.asset";
            var job = AssetDatabase.LoadAssetAtPath<JobDefinition>(jobPath);
            if (job == null)
            {
                job = ScriptableObject.CreateInstance<JobDefinition>();
                AssetDatabase.CreateAsset(job, jobPath);
            }

            var jbSo = new SerializedObject(job);
            SetStr2(jbSo, "jobId", "warrior");
            SetStr2(jbSo, "displayName", "용사");
            SetPrivateFieldObject2(jbSo, "activeSkill", skill);

            var color = jbSo.FindProperty("jobColor");
            if (color != null) color.colorValue = new Color(0.35f, 0.6f, 0.95f); // 용사 = 파랑

            // 기본 공격 — 검
            var basic = jbSo.FindProperty("basicAttack");
            if (basic != null)
            {
                SetRelative(basic, "damage", 12f);
                SetRelative(basic, "range", 1.6f);
                SetRelative(basic, "attackSpeed", 1.2f);
                SetRelative(basic, "attackVfxId", VfxId.Slash);
                SetRelative(basic, "knockbackForce", 5f);
            }

            jbSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(job);
            AssetDatabase.SaveAssets();
            return job;
        }

        // ── 전시장 헬퍼 ───────────────────────────────────────────

        private static List<T> LoadAll<T>(string folder) where T : Object
        {
            var result = new List<T>();
            if (!Directory.Exists(folder)) return result;

            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (asset != null) result.Add(asset);
            }
            return result;
        }

        private static void SetKey(SerializedObject so, string field, KeyCode key)
        {
            var p = so.FindProperty(field);
            if (p != null) p.intValue = (int)key;
        }

        private static void SetStr2(SerializedObject so, string field, string value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.stringValue = value;
        }

        private static void SetFloat2(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }

        private static void SetRelative(SerializedProperty parent, string field, object value)
        {
            var p = parent.FindPropertyRelative(field);
            if (p == null) return;

            switch (value)
            {
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case string s: p.stringValue = s; break;
            }
        }

        /// <summary>맵 진입 시네마틱. 맵마다 MapIntroData 에셋만 만들면 재사용된다.</summary>
        private static void CreateMapIntro()
        {
            var data = EnsureForestIntroData();

            var go = new GameObject("MapIntroManager");
            var manager = go.AddComponent<MapIntroManager>();
            manager.SetIntro(data);
        }

        /// <summary>Map 01 — THE FOREST 인트로 데이터.</summary>
        private static MapIntroData EnsureForestIntroData()
        {
            const string folder = "Assets/Settings/MapIntro";
            const string path = folder + "/MapIntro_Forest.asset";
            Directory.CreateDirectory(folder);

            var data = AssetDatabase.LoadAssetAtPath<MapIntroData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<MapIntroData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.mapId = "forest";
            data.title = "THE FOREST";
            data.subtitle = "Map 01";

            // 암전 → 밝아짐 → 제목(3.5초 유지) → 사라짐 → 조작
            data.blackHold = 1.2f;
            data.fadeInDuration = 1.6f;
            data.titleDelay = 0.8f;
            data.titleFadeIn = 1.0f;
            data.titleHold = 3.5f;   // 문서 요구: 3~4초
            data.titleFadeOut = 1.2f;
            data.tailDuration = 0.4f;

            // 카메라가 오른쪽으로 천천히 이동하며 숲을 훑는다
            data.cameraMove = MapIntroCameraMove.PanRight;
            data.cameraStartOffset = new Vector2(-6f, 2f);
            data.cameraDistance = 10f;

            // 팔레트 시스템.md — 암전은 순수 검정 대신 짙은 남색
            data.fadeColor = new Color(0.04f, 0.05f, 0.08f, 1f);
            data.titleColor = Color.white;
            data.subtitleColor = new Color(0.78f, 0.82f, 0.78f, 1f); // 숲 = 옅은 초록빛

            // 테스트 씬에서는 매번 재생되어야 연출을 확인할 수 있다.
            // 실제 게임(Forest 씬)에서는 true로 두어 재입장 시 생략한다.
            data.playOnlyOnce = false;
            data.skippable = true;

            // TODO(사운드): ambientSound(새소리·바람)와 bgm 에셋이 준비되면 여기에 연결한다.
            //   현재 오디오 에셋이 없어 비워 둔다 — 없어도 연출은 정상 동작한다.

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        /// <summary>상태이상 화면 효과(색수차 등)용 전역 Volume과 디버그 키를 구성한다.</summary>
        private static void CreatePostFx(GameObject player)
        {
            var go = new GameObject("StatusEffectPostFx");

            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f; // 기본 볼륨보다 우선

            go.AddComponent<StatusEffectPostFx>();

            // 디버그 키 — 1~4로 상태이상을 걸어 화면 효과를 확인한다.
            var debugKeys = go.AddComponent<SandboxDebugKeys>();
            debugKeys.SetTarget(player.GetComponent<StatusEffectController>());
            debugKeys.SetEffects(EnsureTestStatusEffects());
        }

        // ── 이펙트 ────────────────────────────────────────────────

        /// <summary>
        /// 이펙트 카탈로그를 만들고 스포너를 배치한다.
        /// 어떤 시트를 쓸지는 180장의 실제 픽셀을 분석해(모양/크기/프레임 수) 용도별로 골랐다.
        /// </summary>
        private static void CreateVfx(GameObject player)
        {
            var library = EnsureVfxLibrary();

            var go = new GameObject("VfxSpawner");
            var spawner = go.AddComponent<VfxSpawner>();
            spawner.SetLibrary(library);

            // 플레이어 기본 공격에 베기 이펙트 연결
            var attacker = player.GetComponent<BasicAttacker>();
            if (attacker != null)
            {
                var so = new SerializedObject(attacker);
                var profile = so.FindProperty("profile");
                if (profile != null)
                {
                    var vfxId = profile.FindPropertyRelative("attackVfxId");
                    if (vfxId != null) vfxId.stringValue = VfxId.Slash;

                    var range = profile.FindPropertyRelative("range");
                    if (range != null) range.floatValue = 1.6f; // 검 사거리 — 허수아비에 닿도록

                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        /// <summary>
        /// 용도별 이펙트 정의와 카탈로그를 생성한다.
        /// (시트, 색상 행, 프레임 수)는 픽셀 분석 결과에 근거해 선택했다.
        /// </summary>
        private static VfxLibrary EnsureVfxLibrary()
        {
            const string folder = "Assets/Settings/VFX";
            const string libPath = folder + "/VfxLibrary.asset";
            Directory.CreateDirectory(folder);

            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(libPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<VfxLibrary>();
                AssetDatabase.CreateAsset(library, libPath);
            }

            // 하나의 연출을 여러 시트로 겹쳐 만든다 — 층이 생기면 타격감이 크게 살아난다.
            // (시트, 색상 행, PPU, 크기, FPS, 반복) → 정의 에셋
            var entries = new List<VfxLibrary.Entry>
            {
                // ── 검 베기 ── 궤적 + 앞쪽 스파크 + 뒤따르는 사선 파동
                Composite(folder, VfxId.Slash,
                    Layer("Part2/70.png",  VfxRow.Neutral, 36f, 1.35f, 16f, rotation: -18f),
                    Layer("Part8/397.png", VfxRow.Neutral, 44f, 1.0f,  18f, delay: 0.02f, offset: new Vector2(0.35f, 0f), randomRot: true),
                    Layer("Part6/276.png", VfxRow.Neutral, 40f, 1.15f, 18f, delay: 0.05f, rotation: 14f, speed: 1.3f)),

                // ── 일반 타격 ── 코어 + 튀는 스파크 2겹
                Composite(folder, VfxId.HitNeutral,
                    Layer("Part7/335.png",  VfxRow.Neutral, 44f, 1.1f, 18f, randomRot: true),
                    Layer("Part11/518.png", VfxRow.Neutral, 40f, 1.2f, 20f, delay: 0.02f, randomRot: true),
                    Layer("Part6/284.png",  VfxRow.Fire,    48f, 0.9f, 20f, delay: 0.05f, offset: new Vector2(0.2f, 0.15f), randomRot: true)),

                // ── 치명타 ── 큰 폭발 + 충격파 + 스파크
                Composite(folder, VfxId.HitCritical,
                    Layer("Part2/79.png",   VfxRow.Fire, 34f, 1.3f, 18f),
                    Layer("Part1/03.png",   VfxRow.Fire, 30f, 1.0f, 16f, delay: 0.03f),
                    Layer("Part11/517.png", VfxRow.Fire, 36f, 1.3f, 20f, delay: 0.06f, randomRot: true)),

                // ── 출혈 ──
                Composite(folder, VfxId.HitBlood,
                    Layer("Part11/528.png", VfxRow.Blood, 44f, 1.0f, 16f, randomRot: true)),

                // ── 폭발 ──
                Composite(folder, VfxId.Explosion,
                    Layer("Part1/03.png",  VfxRow.Fire, 28f, 1.3f, 15f),
                    Layer("Part2/70.png",  VfxRow.Fire, 30f, 1.4f, 16f, delay: 0.04f),
                    Layer("Part8/397.png", VfxRow.Fire, 36f, 1.2f, 18f, delay: 0.07f, randomRot: true)),

                // ── 투사체 꼬리 ── 작고 짧게, 촘촘히 남는다
                Composite(folder, VfxId.ProjectileFly,
                    Layer("Part11/518.png", VfxRow.Arcane, 64f, 0.8f, 22f)),

                // ── 투사체 착탄 ──
                Composite(folder, VfxId.ProjectileImpact,
                    Layer("Part7/335.png",  VfxRow.Arcane, 44f, 1.0f, 18f, randomRot: true),
                    Layer("Part11/518.png", VfxRow.Arcane, 42f, 1.1f, 20f, delay: 0.02f, randomRot: true)),

                // ── 이동 ──
                Composite(folder, VfxId.DashTrail,
                    Layer("Part1/05.png", VfxRow.Neutral, 44f, 1.2f, 18f)),
                Composite(folder, VfxId.LandDust,
                    Layer("Part8/375.png", VfxRow.Earth, 40f, 1.1f, 16f)),
                Composite(folder, VfxId.JumpDust,
                    Layer("Part8/375.png", VfxRow.Earth, 48f, 0.8f, 18f)),

                // ── 상태이상 (캐릭터에 붙어 반복 재생) ──
                Composite(folder, VfxId.StatusBurn,
                    Layer("Part5/241.png", VfxRow.Fire, 44f, 1.0f, 12f, loop: true)),
                Composite(folder, VfxId.StatusPoison,
                    Layer("Part5/241.png", VfxRow.Poison, 44f, 1.0f, 10f, loop: true)),
                Composite(folder, VfxId.StatusFreeze,
                    Layer("Part2/79.png", VfxRow.Ice, 40f, 1.0f, 10f, loop: true)),
                Composite(folder, VfxId.StatusShock,
                    Layer("Part8/397.png", VfxRow.Fire, 38f, 1.1f, 20f, loop: true)),
                Composite(folder, VfxId.StatusCurse,
                    Layer("Part2/79.png", VfxRow.Arcane, 40f, 1.0f, 12f, loop: true)),
                Composite(folder, VfxId.StatusConfusion,
                    Layer("Part2/79.png", VfxRow.Arcane, 40f, 1.05f, 14f, loop: true)),
                Composite(folder, VfxId.StatusFear,
                    Layer("Part11/529.png", VfxRow.Void, 44f, 1.0f, 10f, loop: true)),

                // ── 회복 / 사망 ──
                Composite(folder, VfxId.Heal,
                    Layer("Part11/529.png", VfxRow.Ice, 44f, 1.0f, 14f)),
                Composite(folder, VfxId.Death,
                    Layer("Part10/484.png", VfxRow.Dusk, 32f, 1.1f, 14f),
                    Layer("Part2/70.png",   VfxRow.Dusk, 34f, 1.2f, 16f, delay: 0.05f)),
            };

            entries.RemoveAll(e => e == null || e.layers.Count == 0);
            library.SetEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"[TSWP] 이펙트 {entries.Count}종을 카탈로그에 등록했습니다.");
            return library;
        }

        /// <summary>레이어 1장의 사양. 실제 VfxDefinition 에셋은 Composite에서 만든다.</summary>
        private class LayerSpec
        {
            public string sheet;
            public VfxRow row;
            public float ppu;
            public float scale;
            public float fps;
            public bool loop;
            public float delay;
            public Vector2 offset;
            public float rotation;
            public bool randomRot;
            public float speed = 1f;
        }

        private static LayerSpec Layer(string sheet, VfxRow row, float ppu, float scale, float fps,
                                       bool loop = false, float delay = 0f, Vector2 offset = default,
                                       float rotation = 0f, bool randomRot = false, float speed = 1f)
        {
            return new LayerSpec
            {
                sheet = sheet, row = row, ppu = ppu, scale = scale, fps = fps, loop = loop,
                delay = delay, offset = offset, rotation = rotation, randomRot = randomRot, speed = speed,
            };
        }

        /// <summary>레이어 사양들로 VfxDefinition 에셋을 만들고 카탈로그 항목을 구성한다.</summary>
        private static VfxLibrary.Entry Composite(string folder, string id, params LayerSpec[] specs)
        {
            var entry = new VfxLibrary.Entry { id = id, layers = new List<VfxLayer>() };

            for (int i = 0; i < specs.Length; i++)
            {
                var spec = specs[i];
                string sheetPath = "Assets/Sprites/VFX/" + spec.sheet;
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
                if (texture == null)
                {
                    Debug.LogWarning($"[SandboxSceneBuilder] 시트를 찾지 못했습니다: {sheetPath}");
                    continue;
                }

                // 같은 (시트, 행) 조합은 정의 에셋을 재사용한다.
                string defName = $"Vfx_{System.IO.Path.GetFileNameWithoutExtension(spec.sheet)}_{spec.row}";
                string defPath = $"{folder}/{defName}.asset";

                var def = AssetDatabase.LoadAssetAtPath<VfxDefinition>(defPath);
                if (def == null)
                {
                    def = ScriptableObject.CreateInstance<VfxDefinition>();
                    AssetDatabase.CreateAsset(def, defPath);
                }

                def.vfxId = defName;
                def.sheet = texture;
                def.row = spec.row;
                def.startFrame = 0;
                def.frameCount = 0;
                def.fps = spec.fps;
                def.loop = spec.loop;
                def.pixelsPerUnit = spec.ppu;
                def.scale = 1f;              // 크기는 레이어에서 배율로 조절
                def.sortingOrder = 20;
                EditorUtility.SetDirty(def);

                entry.layers.Add(new VfxLayer
                {
                    definition = def,
                    offset = spec.offset,
                    delay = spec.delay,
                    scaleMultiplier = spec.scale,
                    rotation = spec.rotation,
                    randomRotation = spec.randomRot,
                    tint = Color.white,
                    speedMultiplier = spec.speed,
                });
            }

            return entry;
        }

        // ── 공격하는 적 ───────────────────────────────────────────

        /// <summary>
        /// 실제 Enemies 시스템(EnemyData + EnemyController + EnemyAI)으로 적을 만든다.
        /// 정적 허수아비와 달리 플레이어를 쫓아와 공격하므로 피격 연출을 확인할 수 있다.
        /// </summary>
        private static void CreateAttackerEnemy(Sprite sprite)
        {
            var data = EnsureAttackerEnemyData();

            var go = new GameObject("Enemy_Attacker");
            go.transform.position = new Vector2(-9f, 0f);
            go.transform.localScale = new Vector3(0.9f, 1f, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.75f, 0.2f, 0.35f); // 적 = 빨강 (팔레트 시스템.md)
            renderer.sortingOrder = 6;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 1f);
            collider.sharedMaterial = EnsureZeroFrictionMaterial(); // 벽 끼임 방지

            var entity = go.AddComponent<CombatEntity>();
            SetPrivateField(entity, "team", (int)TeamType.Enemies, isEnum: true);
            SetPrivateField(entity, "ownerPlayerId", -1);
            SetPrivateField(entity, "maxHp", 80f);
            SetPrivateField(entity, "autoReviveOnDeath", false);

            go.AddComponent<StatusEffectController>();
            go.AddComponent<StatusEffectVisual>();
            go.AddComponent<EnemyHealthBar>(); // 머리 위 체력바

            var controller = go.AddComponent<EnemyController>();
            SetPrivateFieldObject(controller, "data", data);

            var ai = go.AddComponent<EnemyAI>();
            var aiSo = new SerializedObject(ai);
            var mask = aiSo.FindProperty("obstacleMask");
            if (mask != null) mask.intValue = 1 << LayerMask.NameToLayer(GroundLayerName);
            aiSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 원거리 공격 적 — 멀리서 투사체를 쏜다. 피격과 투사체 회피를 테스트할 수 있다.
        /// </summary>
        private static void CreateRangedEnemy(Sprite sprite)
        {
            var projectilePrefab = EnsureProjectilePrefab(sprite);
            var data = EnsureRangedEnemyData(projectilePrefab);

            var go = new GameObject("Enemy_Ranged");
            go.transform.position = new Vector2(13f, 0f);
            go.transform.localScale = new Vector3(0.9f, 1.1f, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.55f, 0.35f, 0.8f); // 원거리 = 보라 (마법사 계열)
            renderer.sortingOrder = 6;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 1f);
            collider.sharedMaterial = EnsureZeroFrictionMaterial();

            var entity = go.AddComponent<CombatEntity>();
            SetPrivateField(entity, "team", (int)TeamType.Enemies, isEnum: true);
            SetPrivateField(entity, "ownerPlayerId", -1);
            SetPrivateField(entity, "maxHp", 60f);
            SetPrivateField(entity, "autoReviveOnDeath", false);

            go.AddComponent<StatusEffectController>();
            go.AddComponent<StatusEffectVisual>(); // 상태이상이 적에게도 보이도록
            go.AddComponent<EnemyHealthBar>();

            var controller = go.AddComponent<EnemyController>();
            SetPrivateFieldObject(controller, "data", data);

            var ai = go.AddComponent<EnemyAI>();
            var aiSo = new SerializedObject(ai);
            var mask = aiSo.FindProperty("obstacleMask");
            if (mask != null) mask.intValue = 1 << LayerMask.NameToLayer(GroundLayerName);

            aiSo.ApplyModifiedPropertiesWithoutUndo();

            // 원거리이므로 멀리서도 플레이어를 인지해야 한다.
            // 감지 거리는 EnemyAI 컴포넌트가 아니라 EnemyData.aiProfile이 소유한다
            // (적의 '성격'은 프리팹이 아니라 데이터에 있어야 적 1종 = 에셋 1개가 성립한다).
            var dataSo = new SerializedObject(data);
            var profile = dataSo.FindProperty("aiProfile");
            if (profile != null)
            {
                var detection = profile.FindPropertyRelative("detectionRange");
                if (detection != null) detection.floatValue = 16f;
                dataSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
        }

        /// <summary>투사체 프리팹을 만든다 (없으면 생성, 있으면 재사용).</summary>
        private static Projectile EnsureProjectilePrefab(Sprite sprite)
        {
            const string folder = "Assets/Settings/Prefabs";
            const string path = folder + "/Projectile_Test.prefab";
            Directory.CreateDirectory(folder);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var comp = existing.GetComponent<Projectile>();
                if (comp != null) return comp;
            }

            var go = new GameObject("Projectile");
            go.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.8f, 0.5f, 1f); // 보라 마법탄
            renderer.sortingOrder = 15;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;

            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1f, 1f);

            var projectile = go.AddComponent<Projectile>();
            var so = new SerializedObject(projectile);
            var mask = so.FindProperty("obstacleMask");
            if (mask != null) mask.intValue = 1 << LayerMask.NameToLayer(GroundLayerName);

            // 방향 고정 대신 회전시킨다 — 빙글빙글 도는 탄이 더 눈에 띈다.
            var rotate = so.FindProperty("rotateToDirection");
            if (rotate != null) rotate.boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return saved != null ? saved.GetComponent<Projectile>() : null;
        }

        /// <summary>원거리 적 데이터 — 멀리서 투사체를 쏜다.</summary>
        private static EnemyData EnsureRangedEnemyData(Projectile projectilePrefab)
        {
            const string folder = "Assets/Settings/Enemies";
            const string path = folder + "/Enemy_TestRanged.asset";
            Directory.CreateDirectory(folder);

            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, path);
            }

            var so = new SerializedObject(data);
            SetProp(so, "enemyId", "test.ranged");
            SetProp(so, "displayName", "테스트 저격수");
            SetProp(so, "maxHp", 60f);
            SetProp(so, "moveSpeed", 1.4f);   // 느리게 — 거리를 유지하는 편
            SetProp(so, "grade", (int)EnemyGrade.Special, isEnum: true);

            var attack = so.FindProperty("basicAttack");
            if (attack != null)
            {
                SetRelative(attack, "damage", 6f);
                SetRelative(attack, "range", 11f);     // 원거리 사거리
                SetRelative(attack, "cooldown", 1.6f);
                SetRelative(attack, "isRanged", true);
                SetRelative(attack, "projectileSpeed", 7f);
                SetRelative(attack, "muzzleForward", 0.8f);
                SetRelative(attack, "attackVfxId", VfxId.ProjectileImpact);

                var prefabProp = attack.FindPropertyRelative("projectilePrefab");
                if (prefabProp != null) prefabProp.objectReferenceValue = projectilePrefab;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        // SetRelative는 601줄에 이미 정의되어 있다 (전시장 배선을 추가하며 중복 작성했던 것을 제거).

        /// <summary>테스트용 적 데이터. 플레이어를 쫓아와 근접 공격한다.</summary>
        private static EnemyData EnsureAttackerEnemyData()
        {
            const string folder = "Assets/Settings/Enemies";
            const string path = folder + "/Enemy_TestAttacker.asset";
            Directory.CreateDirectory(folder);

            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, path);
            }

            var so = new SerializedObject(data);
            SetProp(so, "enemyId", "test.attacker");
            SetProp(so, "displayName", "테스트 추적자");
            SetProp(so, "maxHp", 80f);
            SetProp(so, "moveSpeed", 2.2f);

            // 기본 공격 — 사거리 안에 들어오면 때린다
            var attack = so.FindProperty("basicAttack");
            if (attack != null)
            {
                var dmg = attack.FindPropertyRelative("damage");
                if (dmg != null) dmg.floatValue = 8f;
                var range = attack.FindPropertyRelative("range");
                if (range != null) range.floatValue = 1.4f;
                var cd = attack.FindPropertyRelative("cooldown");
                if (cd != null) cd.floatValue = 1.2f;

                // 넉백 — 맞으면 밀려나는 느낌을 확인하기 위해
                var applyKb = attack.FindPropertyRelative("applyKnockback");
                if (applyKb != null) applyKb.boolValue = true;
                var kb = attack.FindPropertyRelative("knockback");
                if (kb != null)
                {
                    var force = kb.FindPropertyRelative("Force");
                    if (force != null) force.floatValue = 6f;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            return data;
        }

        /// <summary>UnityEngine.Object 참조형 private 필드 설정.</summary>
        private static void SetPrivateFieldObject(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[SandboxSceneBuilder] '{target.GetType().Name}'에 '{fieldName}' 필드가 없습니다.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>테스트용 상태이상 SO 4종을 생성한다 (혼란/공포/감전/중독).</summary>
        private static List<StatusEffectData> EnsureTestStatusEffects()
        {
            const string folder = "Assets/Settings/StatusEffects";
            Directory.CreateDirectory(folder);

            var specs = new (StatusEffectType type, string ko, float moveMul, float atkSpeedMul)[]
            {
                (StatusEffectType.Confusion, "혼란", 1f, 1f),
                (StatusEffectType.Fear,      "공포", 1f, 1f),
                (StatusEffectType.Shock,     "감전", 0.6f, 0.6f),
                (StatusEffectType.Poison,    "중독", 1f, 1f),
            };

            var result = new List<StatusEffectData>();

            foreach (var spec in specs)
            {
                string path = $"{folder}/{spec.type}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<StatusEffectData>(path);

                if (existing == null)
                {
                    existing = ScriptableObject.CreateInstance<StatusEffectData>();

                    // private 필드라 SerializedObject로 채운다.
                    var so = new SerializedObject(existing);
                    SetProp(so, "effectType", (int)spec.type, isEnum: true);
                    SetProp(so, "displayNameKo", spec.ko);
                    SetProp(so, "duration", 6f);           // TODO(밸런스): 문서 미정 — 테스트용 값
                    SetProp(so, "moveSpeedMultiplier", spec.moveMul);
                    SetProp(so, "attackSpeedMultiplier", spec.atkSpeedMul);
                    if (spec.type == StatusEffectType.Poison)
                    {
                        SetProp(so, "tickDamage", 2f);
                        SetProp(so, "tickInterval", 1f);
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();

                    AssetDatabase.CreateAsset(existing, path);
                }

                result.Add(existing);
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        private static void SetProp(SerializedObject so, string name, object value, bool isEnum = false)
        {
            var prop = so.FindProperty(name);
            if (prop == null) return;

            switch (value)
            {
                case int i when isEnum: prop.enumValueIndex = i; break;
                case int i: prop.intValue = i; break;
                case float f: prop.floatValue = f; break;
                case bool b: prop.boolValue = b; break;
                case string s: prop.stringValue = s; break;
            }
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
