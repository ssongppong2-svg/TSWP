// Forest 프로토타입 씬 생성기 (에디터 전용) — Prototype v0.1.
// 메뉴 [TSWP > Forest 프로토타입 씬 만들기]로 플레이 가능한 방 5개짜리 맵을 만든다.
//
// 기존 [테스트 씬 만들기](SandboxSceneBuilder)는 조작감 확인용으로 남겨 둔다.
// 이 빌더는 "실제 게임 흐름"(방 입장 → 전투 → 클리어 → 문 → 다음 방 → 보스)을 검증한다.
//
// 설계: 방 배치·적 구성·보스 데이터를 전부 ScriptableObject로 만들어 둔다.
//       맵/보스가 늘어나면 에셋만 추가하고 이 코드는 손대지 않는다.
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
using TSWP.Core;
using TSWP.Enemies;
using TSWP.Jobs;
using TSWP.Map;
using TSWP.Player;
using TSWP.Puzzles;
using TSWP.StatusEffects;
using TSWP.UI;

namespace TSWP.EditorTools
{
    public static class ForestPrototypeBuilder
    {
        private const string ScenePath = "Assets/Scenes/Forest_Prototype.unity";
        private const string SettingsRoot = "Assets/Settings";
        private const string GroundLayerName = "Ground";

        /// <summary>방 하나의 가로 폭. 방끼리 이 간격으로 나란히 배치한다.</summary>
        private const float RoomWidth = 44f;

        private static Sprite _square;
        private static int _groundLayer;

        [MenuItem("TSWP/Forest 프로토타입 씬 만들기", priority = 1)]
        public static void BuildAndOpen()
        {
            EnsureSharedState();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = Color.white;

            // ── 공통 시스템 ──
            var player = CreatePlayer();
            CreateCamera(player);
            CreateManagers();
            CreatePostFx(player);
            var vfxLibrary = CreateVfx();
            CreateHud(player);
            CreateMapIntro();

            // ── 방 5개: Start → Battle → Puzzle → Boss → Reward ──
            var rooms = new List<RoomInstance>();
            rooms.Add(BuildStartRoom(0));
            rooms.Add(BuildBattleRoom(1));
            rooms.Add(BuildPuzzleRoom(2));
            rooms.Add(BuildBossRoom(3));
            rooms.Add(BuildRewardRoom(4));

            LinkRooms(rooms);
            CreateRoomFlow(rooms);

            // 플레이어를 시작 방에 배치
            player.transform.position = new Vector3(RoomOriginX(0), 1f, 0f);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log("[TSWP] Forest 프로토타입 씬을 만들었습니다.\n" +
                      "방 5개(Start → Battle → Puzzle → Boss → Reward). ▶ 재생 후 오른쪽으로 진행하세요.");
            EditorUtility.DisplayDialog("TSWP",
                "Forest 프로토타입 씬이 준비됐습니다.\n\n" +
                "▶ 재생 → 오른쪽으로 이동\n\n" +
                "Start → Battle(적 처치) → Puzzle(버튼) → Boss(Hatch Queen) → Reward\n\n" +
                "방을 클리어하면 문이 열립니다. E로 문을 통과하세요.", "확인");
        }

        // ── 방 배치 ───────────────────────────────────────────────

        /// <summary>
        /// 빌더가 공유하는 정적 상태(임시 스프라이트, 지형 레이어)를 준비한다.
        /// BuildAndOpen 경로와 외부 직접 호출 경로 양쪽에서 안전하게 쓰기 위한 것.
        /// </summary>
        private static void EnsureSharedState()
        {
            EnsureGroundLayer();
            _groundLayer = LayerMask.NameToLayer(GroundLayerName);
            if (_square == null) _square = EnsureSquareSprite();
        }

        private static float RoomOriginX(int index) => index * RoomWidth;

        /// <summary>방 하나의 공통 구조(바닥·벽·천장)를 만들고 루트를 돌려준다.</summary>
        private static GameObject CreateRoomShell(int index, string name, Color floorColor)
        {
            float x = RoomOriginX(index);

            var root = new GameObject($"Room_{index}_{name}");
            root.transform.position = new Vector3(x, 0f, 0f);

            // 바닥
            Block(root, "Floor", new Vector2(x, -1f), new Vector2(RoomWidth - 4f, 1f), floorColor);

            // 좌우 벽 — 방 경계
            Block(root, "Wall_Left", new Vector2(x - RoomWidth * 0.5f + 2f, 4f), new Vector2(1f, 12f),
                  new Color(0.18f, 0.22f, 0.18f));
            Block(root, "Wall_Right", new Vector2(x + RoomWidth * 0.5f - 2f, 4f), new Vector2(1f, 12f),
                  new Color(0.18f, 0.22f, 0.18f));

            return root;
        }

        /// <summary>Forest 분위기용 나무 더미 — 장식이자 지형지물.</summary>
        private static void TreeDummy(GameObject parent, Vector2 position, float height)
        {
            var trunk = Block(parent, "Tree_Trunk", position, new Vector2(0.8f, height),
                              new Color(0.32f, 0.22f, 0.14f), collide: false);
            trunk.GetComponent<SpriteRenderer>().sortingOrder = -2;

            var leaves = Block(parent, "Tree_Leaves",
                               new Vector2(position.x, position.y + height * 0.5f + 1.2f),
                               new Vector2(4f, 3f), new Color(0.16f, 0.42f, 0.22f), collide: false);
            leaves.GetComponent<SpriteRenderer>().sortingOrder = -1;
        }

        private static GameObject Block(GameObject parent, string name, Vector2 position, Vector2 size,
                                        Color color, bool collide = true, bool isGround = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = color;
            sr.sortingOrder = 0;

            if (collide)
            {
                go.AddComponent<BoxCollider2D>();
                if (isGround) go.layer = _groundLayer;
            }

            return go;
        }

        private static SpawnPoint SpawnAt(GameObject parent, Vector2 position)
        {
            var go = new GameObject("SpawnPoint");
            go.transform.SetParent(parent.transform);
            go.transform.position = position;
            return go.AddComponent<SpawnPoint>();
        }

        private static Transform MarkerAt(GameObject parent, string name, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.position = position;
            return go.transform;
        }

        /// <summary>방 오른쪽 끝에 다음 방으로 가는 문을 만든다.</summary>
        private static RoomDoor CreateExitDoor(GameObject parent, int roomIndex)
        {
            float x = RoomOriginX(roomIndex) + RoomWidth * 0.5f - 3.5f;

            var go = new GameObject("Exit_Door");
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(x, 0.5f, 0f);
            go.transform.localScale = new Vector3(1.4f, 3f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.sortingOrder = 3;

            // 상호작용 범위
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.6f, 1.2f);

            var door = go.AddComponent<RoomDoor>();

            // 잠김/열림 시각 표현 — 색이 다른 두 자식으로 구분한다.
            var locked = Block(go, "LockedVisual", go.transform.position, Vector2.one,
                               new Color(0.45f, 0.16f, 0.16f), collide: false);
            locked.transform.SetParent(go.transform);
            locked.transform.localPosition = Vector3.zero;
            locked.transform.localScale = Vector3.one;
            locked.GetComponent<SpriteRenderer>().sortingOrder = 4;

            var open = Block(go, "OpenVisual", go.transform.position, Vector2.one,
                             new Color(0.2f, 0.6f, 0.35f), collide: false);
            open.transform.SetParent(go.transform);
            open.transform.localPosition = Vector3.zero;
            open.transform.localScale = Vector3.one;
            open.GetComponent<SpriteRenderer>().sortingOrder = 4;

            // 문 자체의 스프라이트는 자식이 대신하므로 숨긴다
            sr.enabled = false;

            var so = new SerializedObject(door);
            SetObj(so, "lockedVisual", locked);
            SetObj(so, "openVisual", open);
            so.ApplyModifiedPropertiesWithoutUndo();

            return door;
        }

        // ── 방 5종 ────────────────────────────────────────────────

        private static RoomInstance BuildStartRoom(int index)
        {
            var root = CreateRoomShell(index, "Start", new Color(0.24f, 0.30f, 0.24f));
            float x = RoomOriginX(index);

            TreeDummy(root, new Vector2(x - 12f, 2f), 5f);
            TreeDummy(root, new Vector2(x + 10f, 2.5f), 6f);

            var spawn = MarkerAt(root, "PlayerSpawn", new Vector2(x, 1f));
            var door = CreateExitDoor(root, index);

            var definition = EnsureRoomDefinition("room.forest.start", RoomType.Start, null, null);
            return AttachRoom(root, definition, spawn, new List<SpawnPoint>(), door);
        }

        private static RoomInstance BuildBattleRoom(int index)
        {
            var root = CreateRoomShell(index, "Battle", new Color(0.26f, 0.30f, 0.26f));
            float x = RoomOriginX(index);

            TreeDummy(root, new Vector2(x - 14f, 2f), 5f);
            TreeDummy(root, new Vector2(x + 13f, 2f), 5.5f);

            // 발판 — 전투 중 높이 활용
            Block(root, "Platform_A", new Vector2(x - 6f, 2.2f), new Vector2(5f, 0.5f), new Color(0.34f, 0.40f, 0.32f));
            Block(root, "Platform_B", new Vector2(x + 6f, 3.4f), new Vector2(5f, 0.5f), new Color(0.34f, 0.40f, 0.32f));

            var spawn = MarkerAt(root, "PlayerSpawn", new Vector2(x - 16f, 1f));

            var spawns = new List<SpawnPoint>
            {
                SpawnAt(root, new Vector2(x + 2f, 1f)),
                SpawnAt(root, new Vector2(x + 8f, 1f)),
                SpawnAt(root, new Vector2(x + 6f, 4.5f)),
            };

            var door = CreateExitDoor(root, index);

            var encounter = EnsureBattleEncounter();
            var definition = EnsureRoomDefinition("room.forest.battle", RoomType.NormalCombat, encounter, null);
            return AttachRoom(root, definition, spawn, spawns, door);
        }

        private static RoomInstance BuildPuzzleRoom(int index)
        {
            var root = CreateRoomShell(index, "Puzzle", new Color(0.24f, 0.28f, 0.30f));
            float x = RoomOriginX(index);

            TreeDummy(root, new Vector2(x - 13f, 2f), 5f);

            // 점프 발판 — 높은 곳의 버튼으로 올라간다
            var pad = Block(root, "JumpPad", new Vector2(x - 4f, -0.2f), new Vector2(2.5f, 0.4f),
                            new Color(0.35f, 0.65f, 0.85f));
            var padTrigger = pad.AddComponent<BoxCollider2D>();
            padTrigger.isTrigger = true;
            padTrigger.size = new Vector2(1f, 3f);
            padTrigger.offset = new Vector2(0f, 1.5f);
            pad.AddComponent<JumpPlatform>();

            Block(root, "HighLedge", new Vector2(x - 4f, 5.5f), new Vector2(5f, 0.5f), new Color(0.34f, 0.40f, 0.32f));

            // 협동 버튼 2개 — 동시에 눌러야 한다
            var puzzleRoot = new GameObject("CoopButtonPuzzle");
            puzzleRoot.transform.SetParent(root.transform);

            var buttonA = CreateButton(puzzleRoot, new Vector2(x - 4f, 6.2f), "Button_High");
            var buttonB = CreateButton(puzzleRoot, new Vector2(x + 7f, 0.2f), "Button_Low");

            var controller = puzzleRoot.AddComponent<ButtonPuzzleController>();
            var pso = new SerializedObject(controller);
            var buttonList = pso.FindProperty("buttons");
            if (buttonList != null)
            {
                buttonList.arraySize = 2;
                buttonList.GetArrayElementAtIndex(0).objectReferenceValue = buttonA;
                buttonList.GetArrayElementAtIndex(1).objectReferenceValue = buttonB;
            }
            SetObj(pso, "definition", EnsureCoopButtonPuzzleDefinition());
            pso.ApplyModifiedPropertiesWithoutUndo();

            var spawn = MarkerAt(root, "PlayerSpawn", new Vector2(x - 16f, 1f));
            var door = CreateExitDoor(root, index);

            var definition = EnsureRoomDefinition("room.forest.puzzle", RoomType.Puzzle, null, "puzzle.forest.coopbutton");
            return AttachRoom(root, definition, spawn, new List<SpawnPoint>(), door);
        }

        private static PuzzleButton CreateButton(GameObject parent, Vector2 position, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.2f, 0.5f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = new Color(0.85f, 0.75f, 0.25f);
            sr.sortingOrder = 3;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.6f, 2.5f);

            return go.AddComponent<PuzzleButton>();
        }

        private static RoomInstance BuildBossRoom(int index)
        {
            var root = CreateRoomShell(index, "Boss", new Color(0.22f, 0.24f, 0.22f));
            float x = RoomOriginX(index);

            TreeDummy(root, new Vector2(x - 16f, 3f), 7f);
            TreeDummy(root, new Vector2(x + 16f, 3f), 7f);

            // 양쪽 점프 발판 — 보스 패턴 회피와 약점 접근용
            Block(root, "Ledge_Left", new Vector2(x - 12f, 4f), new Vector2(6f, 0.5f), new Color(0.32f, 0.36f, 0.30f));
            Block(root, "Ledge_Right", new Vector2(x + 12f, 4f), new Vector2(6f, 0.5f), new Color(0.32f, 0.36f, 0.30f));

            var spawn = MarkerAt(root, "PlayerSpawn", new Vector2(x - 16f, 1f));
            var bossSpawn = SpawnAt(root, new Vector2(x + 6f, 1.5f));

            var door = CreateExitDoor(root, index);

            var bossData = EnsureHatchQueenData();
            var definition = EnsureRoomDefinition("room.forest.boss", RoomType.Boss, null, null, bossId: "boss.hatchqueen");

            // 보스 실체 — EnemyData 기반 몸통에 BossController를 얹는다.
            CreateHatchQueen(root, new Vector2(x + 6f, 1.5f), bossData);

            return AttachRoom(root, definition, spawn, new List<SpawnPoint> { bossSpawn }, door);
        }

        private static void CreateHatchQueen(GameObject parent, Vector2 position, TSWP.Bosses.BossData data)
        {
            var go = new GameObject("Boss_HatchQueen");
            go.transform.SetParent(parent.transform);
            go.transform.position = position;
            go.transform.localScale = new Vector3(3f, 2.6f, 1f); // 보스는 크게 (도트 시스템.md: 보스 64~128px)

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = new Color(0.45f, 0.15f, 0.35f); // 여왕거미 — 짙은 자주
            sr.sortingOrder = 8;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            col.sharedMaterial = EnsureZeroFrictionMaterial();

            var entity = go.AddComponent<CombatEntity>();
            SetPrivate(entity, "team", (int)TeamType.Enemies, isEnum: true);
            SetPrivate(entity, "ownerPlayerId", -1);
            SetPrivate(entity, "maxHp", 1850f);           // 요구사항 명시 수치
            SetPrivate(entity, "autoReviveOnDeath", false);
            SetPrivate(entity, "isKnockbackImmune", true); // 보스는 넉백 면역 (전투 시스템.md)

            go.AddComponent<StatusEffectController>();
            go.AddComponent<StatusEffectVisual>();

            var boss = go.AddComponent<TSWP.Bosses.BossController>();
            var so = new SerializedObject(boss);
            SetObj(so, "data", data);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static RoomInstance BuildRewardRoom(int index)
        {
            var root = CreateRoomShell(index, "Reward", new Color(0.28f, 0.28f, 0.22f));
            float x = RoomOriginX(index);

            TreeDummy(root, new Vector2(x - 10f, 2f), 5f);
            TreeDummy(root, new Vector2(x + 10f, 2f), 5f);

            // 보상 연출용 받침대
            Block(root, "Pedestal", new Vector2(x, 0f), new Vector2(2f, 1f), new Color(0.7f, 0.6f, 0.25f));

            var spawn = MarkerAt(root, "PlayerSpawn", new Vector2(x - 14f, 1f));

            var definition = EnsureRoomDefinition("room.forest.reward", RoomType.Reward, null, null);
            // 마지막 방 — 문 없음
            return AttachRoom(root, definition, spawn, new List<SpawnPoint>(), null);
        }

        // ── 방 조립 ───────────────────────────────────────────────

        private static RoomInstance AttachRoom(GameObject root, RoomDefinition definition, Transform playerSpawn,
                                               List<SpawnPoint> enemySpawns, RoomDoor door)
        {
            var room = root.AddComponent<RoomInstance>();

            var so = new SerializedObject(room);
            SetObj(so, "definition", definition);
            SetObj(so, "playerSpawnPoint", playerSpawn);

            var spawnList = so.FindProperty("enemySpawnPoints");
            if (spawnList != null)
            {
                spawnList.arraySize = enemySpawns.Count;
                for (int i = 0; i < enemySpawns.Count; i++)
                    spawnList.GetArrayElementAtIndex(i).objectReferenceValue = enemySpawns[i];
            }

            var doorList = so.FindProperty("doors");
            if (doorList != null)
            {
                doorList.arraySize = door != null ? 1 : 0;
                if (door != null) doorList.GetArrayElementAtIndex(0).objectReferenceValue = door;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return room;
        }

        /// <summary>각 방의 문이 다음 방을 가리키게 한다.</summary>
        private static void LinkRooms(List<RoomInstance> rooms)
        {
            for (int i = 0; i < rooms.Count - 1; i++)
            {
                var doorField = new SerializedObject(rooms[i]).FindProperty("doors");
                if (doorField == null || doorField.arraySize == 0) continue;

                var door = doorField.GetArrayElementAtIndex(0).objectReferenceValue as RoomDoor;
                if (door == null) continue;

                var dso = new SerializedObject(door);
                var target = dso.FindProperty("explicitTargetRoomId");
                if (target != null) target.intValue = i + 1;
                dso.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void CreateRoomFlow(List<RoomInstance> rooms)
        {
            var go = new GameObject("RoomFlowManager");
            var flow = go.AddComponent<RoomFlowManager>();

            var so = new SerializedObject(flow);

            var mode = so.FindProperty("mapSource");
            if (mode != null) mode.enumValueIndex = 0; // SceneAuthored

            var list = so.FindProperty("authoredRooms");
            if (list != null)
            {
                list.arraySize = rooms.Count;
                for (int i = 0; i < rooms.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = rooms[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── SO 에셋 ───────────────────────────────────────────────

        private static RoomDefinition EnsureRoomDefinition(string id, RoomType type, EncounterComposition encounter,
                                                           string puzzleId, string bossId = null)
        {
            string folder = SettingsRoot + "/Rooms";
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{id.Replace('.', '_')}.asset";

            var asset = AssetDatabase.LoadAssetAtPath<RoomDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RoomDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            SetStr(so, "roomDefinitionId", id);
            SetEnum(so, "roomType", (int)type);
            SetEnum(so, "biome", (int)BiomeType.Forest);
            SetObj(so, "encounter", encounter);
            if (!string.IsNullOrEmpty(puzzleId)) SetStr(so, "puzzleId", puzzleId);
            if (!string.IsNullOrEmpty(bossId)) SetStr(so, "bossId", bossId);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>전투 방 적 구성 — Melee 2 + Ranged 1 + Spider 2.</summary>
        private static EncounterComposition EnsureBattleEncounter()
        {
            string folder = SettingsRoot + "/Encounters";
            Directory.CreateDirectory(folder);
            const string path = SettingsRoot + "/Encounters/Encounter_Forest_Battle.asset";

            var asset = AssetDatabase.LoadAssetAtPath<EncounterComposition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EncounterComposition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var entries = new (EnemyData data, int count)[]
            {
                (EnsureMeleeDummy(), 2),
                (EnsureRangedDummy(), 1),
                (EnsureSpider(), 2),
            };

            var so = new SerializedObject(asset);
            var list = so.FindProperty("entries");
            if (list != null)
            {
                list.arraySize = entries.Length;
                for (int i = 0; i < entries.Length; i++)
                {
                    var e = list.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("enemy").objectReferenceValue = entries[i].data;
                    e.FindPropertyRelative("count").intValue = entries[i].count;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static EnemyData EnsureMeleeDummy() =>
            EnsureEnemy("enemy.forest.melee", "숲 근접병", hp: 60f, speed: 2.2f, damage: 8f,
                        range: 1.4f, cooldown: 1.2f, color: null, ranged: false);

        private static EnemyData EnsureRangedDummy() =>
            EnsureEnemy("enemy.forest.ranged", "숲 저격수", hp: 45f, speed: 1.4f, damage: 6f,
                        range: 11f, cooldown: 1.6f, color: null, ranged: true);

        /// <summary>Spider — 요구사항: 플레이어 추적 + 근접 공격만.</summary>
        private static EnemyData EnsureSpider() =>
            EnsureEnemy("enemy.forest.spider", "거미", hp: 35f, speed: 3.4f, damage: 5f,
                        range: 1.2f, cooldown: 0.9f, color: null, ranged: false);

        private static EnemyData EnsureEnemy(string id, string displayName, float hp, float speed, float damage,
                                             float range, float cooldown, Color? color, bool ranged)
        {
            string folder = SettingsRoot + "/Enemies";
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{id.Replace('.', '_')}.asset";

            var asset = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            SetStr(so, "enemyId", id);
            SetStr(so, "displayName", displayName);
            SetFloat(so, "maxHp", hp);
            SetFloat(so, "moveSpeed", speed);
            SetObj(so, "enemyPrefab", EnsureEnemyPrefab());

            var attack = so.FindProperty("basicAttack");
            if (attack != null)
            {
                SetRel(attack, "damage", damage);
                SetRel(attack, "range", range);
                SetRel(attack, "cooldown", cooldown);
                SetRel(attack, "isRanged", ranged);
                SetRel(attack, "applyKnockback", true);

                var kb = attack.FindPropertyRelative("knockback");
                if (kb != null)
                {
                    var force = kb.FindPropertyRelative("Force");
                    if (force != null) force.floatValue = 5f;
                }

                if (ranged)
                {
                    SetRel(attack, "projectileSpeed", 7f);
                    SetRel(attack, "muzzleForward", 0.8f);
                    var prefab = attack.FindPropertyRelative("projectilePrefab");
                    if (prefab != null) prefab.objectReferenceValue = EnsureProjectilePrefab();
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>적 몸통 프리팹 — EnemyData가 종류를 결정하므로 프리팹은 하나면 된다.</summary>
        private static GameObject EnsureEnemyPrefab()
        {
            string folder = SettingsRoot + "/Prefabs";
            Directory.CreateDirectory(folder);
            const string path = SettingsRoot + "/Prefabs/Enemy_Body.prefab";

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = new GameObject("Enemy");
            go.transform.localScale = new Vector3(0.9f, 1f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = new Color(0.75f, 0.25f, 0.3f);
            sr.sortingOrder = 6;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            col.sharedMaterial = EnsureZeroFrictionMaterial();

            var entity = go.AddComponent<CombatEntity>();
            SetPrivate(entity, "team", (int)TeamType.Enemies, isEnum: true);
            SetPrivate(entity, "ownerPlayerId", -1);
            SetPrivate(entity, "autoReviveOnDeath", false);

            go.AddComponent<StatusEffectController>();
            go.AddComponent<StatusEffectVisual>();
            go.AddComponent<EnemyHealthBar>();
            go.AddComponent<EnemyController>();

            var ai = go.AddComponent<EnemyAI>();
            var aiSo = new SerializedObject(ai);
            var mask = aiSo.FindProperty("obstacleMask");
            if (mask != null) mask.intValue = 1 << _groundLayer;
            aiSo.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        /// <summary>
        /// Hatch Queen 데이터를 만들거나 가져온다. 패턴 5종까지 함께 구성한다.
        /// 테스트 씬 빌더도 호출하므로 internal로 공개한다 — 보스 데이터가 없으면
        /// BossController가 스스로 비활성화되어 아무것도 보이지 않는다.
        /// </summary>
        internal static TSWP.Bosses.BossData EnsureHatchQueenData()
        {
            // 다른 빌더가 직접 부를 수 있으므로 공유 상태를 여기서도 보장한다.
            // (BuildAndOpen을 거치지 않으면 _square/_groundLayer가 비어 프리팹 생성이 깨진다)
            EnsureSharedState();

            string folder = SettingsRoot + "/Bosses";
            Directory.CreateDirectory(folder);
            const string path = SettingsRoot + "/Bosses/Boss_HatchQueen.asset";

            var asset = AssetDatabase.LoadAssetAtPath<TSWP.Bosses.BossData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TSWP.Bosses.BossData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            SetStr(so, "bossId", "boss.hatchqueen");
            SetStr(so, "displayName", "부화의 여왕");
            SetFloat(so, "baseMaxHp", 1850f); // 요구사항 명시 수치

            // 패턴 피해량의 기준값 — 각 패턴은 이 값을 ctx.Damage로 받아 쓴다.
            var basicAttack = so.FindProperty("basicAttack");
            if (basicAttack != null)
            {
                SetRel(basicAttack, "damage", 14f);  // TODO(밸런스): 문서 미정
                SetRel(basicAttack, "range", 2.5f);
                SetRel(basicAttack, "attackSpeed", 0.6f);
            }

            // 패턴 연결 — 보스가 실제로 움직이려면 이게 있어야 한다.
            var patterns = new List<TSWP.Bosses.BossPattern>
            {
                EnsureChargePattern(),
                EnsureWebPattern(),
                EnsureCocoonPattern(),
                EnsureSlamPattern(),
                EnsureSweepPattern(),
            };

            var patternList = so.FindProperty("patterns");
            if (patternList != null)
            {
                patternList.arraySize = patterns.Count;
                for (int i = 0; i < patterns.Count; i++)
                    patternList.GetArrayElementAtIndex(i).objectReferenceValue = patterns[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ── Hatch Queen 패턴 ──────────────────────────────────────
        // BossPattern(언제 쓸지) + BossPatternBehaviour(어떻게 실행할지) 2단 구조다.
        // 보스를 추가할 때는 이 함수들을 흉내내 에셋만 더 만들면 되고, 실행 코드는 건드리지 않는다.

        private static string BossFolder
        {
            get
            {
                string folder = SettingsRoot + "/Bosses/HatchQueen";
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        /// <summary>패턴 정의(조건·카테고리)와 실행 전략을 묶어 하나의 BossPattern 에셋을 만든다.</summary>
        private static TSWP.Bosses.BossPattern CreatePattern(
            string id, string displayName, TSWP.Bosses.BossPatternCategory category,
            TSWP.Bosses.BossPatternBehaviour behaviour, float telegraphSeconds, string telegraphVfx)
        {
            string path = $"{BossFolder}/Pattern_{id}.asset";

            var asset = AssetDatabase.LoadAssetAtPath<TSWP.Bosses.BossPattern>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TSWP.Bosses.BossPattern>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            SetStr(so, "patternId", id);
            SetStr(so, "displayName", displayName);
            SetEnum(so, "category", (int)category);
            SetObj(so, "behaviour", behaviour);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 예고 시간은 실행 전략이 소유한다 (모든 패턴 공통 계약).
            if (behaviour != null)
            {
                var bso = new SerializedObject(behaviour);
                SetFloat(bso, "telegraphSeconds", telegraphSeconds);
                if (!string.IsNullOrEmpty(telegraphVfx)) SetStr(bso, "telegraphVfxId", telegraphVfx);
                bso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(behaviour);
            }

            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static T EnsureBehaviour<T>(string fileName) where T : TSWP.Bosses.BossPatternBehaviour
        {
            string path = $"{BossFolder}/{fileName}.asset";

            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>돌진 — 플레이어를 향해 달려든다.</summary>
        private static TSWP.Bosses.BossPattern EnsureChargePattern()
        {
            var behaviour = EnsureBehaviour<TSWP.Bosses.ChargePatternBehaviour>("Behaviour_Charge");

            var so = new SerializedObject(behaviour);
            SetFloat(so, "chargeSpeed", 14f);
            SetFloat(so, "chargeSeconds", 1.1f);
            SetBool(so, "horizontalOnly", true);
            SetFloat(so, "hitRadius", 1.8f);
            SetFloat(so, "knockbackForce", 10f);
            SetFloat(so, "stunDuration", 0.3f);
            SetFloat(so, "recoverySeconds", 0.8f); // 돌진 후 빈틈 — 반격 기회
            SetStr(so, "impactVfxId", VfxId.HitCritical);
            so.ApplyModifiedPropertiesWithoutUndo();

            return CreatePattern("charge", "돌진", TSWP.Bosses.BossPatternCategory.Movement,
                                 behaviour, 0.8f, VfxId.StatusBurn);
        }

        /// <summary>거미줄 — 바닥에 장판을 깔아 이동을 방해한다.</summary>
        private static TSWP.Bosses.BossPattern EnsureWebPattern()
        {
            var behaviour = EnsureBehaviour<TSWP.Bosses.WebPatternBehaviour>("Behaviour_Web");

            var so = new SerializedObject(behaviour);
            SetInt(so, "fieldCount", 3);
            SetFloat(so, "fieldRadius", 2.8f);
            SetBool(so, "placeOnPlayers", true);
            SetFloat(so, "fieldDuration", 6f);
            SetFloat(so, "tickDamage", 2f);
            so.ApplyModifiedPropertiesWithoutUndo();

            return CreatePattern("web", "거미줄", TSWP.Bosses.BossPatternCategory.AreaAttack,
                                 behaviour, 0.7f, VfxId.StatusPoison);
        }

        /// <summary>고치 소환 — 부화하면 거미가 나온다.</summary>
        private static TSWP.Bosses.BossPattern EnsureCocoonPattern()
        {
            var behaviour = EnsureBehaviour<TSWP.Bosses.CocoonSpawnPatternBehaviour>("Behaviour_Cocoon");

            var so = new SerializedObject(behaviour);
            SetInt(so, "cocoonCount", 3);
            SetFloat(so, "spawnRadius", 4f);
            SetFloat(so, "hatchSeconds", 4f);
            SetInt(so, "maxAliveCocoons", 6);
            SetObj(so, "hatchEnemy", EnsureSpider());       // 부화 → 거미
            SetObj(so, "cocoonPrefab", EnsureCocoonPrefab());
            SetStr(so, "spawnVfxId", VfxId.Spawn);
            so.ApplyModifiedPropertiesWithoutUndo();

            return CreatePattern("cocoon", "고치 소환", TSWP.Bosses.BossPatternCategory.SpecialSkill,
                                 behaviour, 1.0f, VfxId.StatusCurse);
        }

        /// <summary>내려찍기 — 제자리 범위 공격. 근접한 플레이어를 벌한다.</summary>
        private static TSWP.Bosses.BossPattern EnsureSlamPattern()
        {
            var behaviour = EnsureBehaviour<TSWP.Bosses.AreaAttackPatternBehaviour>("Behaviour_Slam");

            // 피해량은 BossData.basicAttack에서 온다(ctx.Damage) — 패턴은 범위·넉백·경직으로 차별화한다.
            var so = new SerializedObject(behaviour);
            SetFloat(so, "radius", 4.5f);
            SetBool(so, "centerOnNearestPlayer", false); // 제자리 내려찍기
            SetFloat(so, "knockbackForce", 12f);
            SetFloat(so, "stunDuration", 0.35f);
            SetBool(so, "isExplosive", true);            // 구조물도 부순다
            SetFloat(so, "recoverySeconds", 0.7f);
            SetStr(so, "impactVfxId", VfxId.Explosion);
            so.ApplyModifiedPropertiesWithoutUndo();

            return CreatePattern("slam", "내려찍기", TSWP.Bosses.BossPatternCategory.AreaAttack,
                                 behaviour, 0.9f, VfxId.HitCritical);
        }

        /// <summary>휩쓸기 — 넓고 약한 공격. 기본 견제기.</summary>
        private static TSWP.Bosses.BossPattern EnsureSweepPattern()
        {
            var behaviour = EnsureBehaviour<TSWP.Bosses.AreaAttackPatternBehaviour>("Behaviour_Sweep");

            var so = new SerializedObject(behaviour);
            SetFloat(so, "radius", 3f);
            SetBool(so, "centerOnNearestPlayer", true); // 가까운 플레이어를 노린다
            SetFloat(so, "knockbackForce", 6f);
            SetFloat(so, "stunDuration", 0.1f);
            SetFloat(so, "recoverySeconds", 0.3f);
            SetStr(so, "impactVfxId", VfxId.HitNeutral);
            so.ApplyModifiedPropertiesWithoutUndo();

            return CreatePattern("sweep", "휩쓸기", TSWP.Bosses.BossPatternCategory.BasicAttack,
                                 behaviour, 0.5f, "");
        }

        /// <summary>고치 프리팹 — 때려 부수거나 놔두면 부화한다.</summary>
        private static GameObject EnsureCocoonPrefab()
        {
            const string path = SettingsRoot + "/Prefabs/Cocoon.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(SettingsRoot + "/Prefabs");

            var go = new GameObject("Cocoon");
            go.transform.localScale = new Vector3(0.7f, 0.9f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = new Color(0.85f, 0.82f, 0.7f); // 고치 — 옅은 미색
            sr.sortingOrder = 7;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            col.sharedMaterial = EnsureZeroFrictionMaterial();

            var entity = go.AddComponent<CombatEntity>();
            SetPrivate(entity, "team", (int)TeamType.Enemies, isEnum: true);
            SetPrivate(entity, "ownerPlayerId", -1);
            SetPrivate(entity, "maxHp", 25f);
            SetPrivate(entity, "autoReviveOnDeath", false);
            SetPrivate(entity, "isKnockbackImmune", true);

            go.AddComponent<StatusEffectController>();
            go.AddComponent<EnemyHealthBar>();
            go.AddComponent<TSWP.Bosses.BossCocoon>();

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        private static PuzzleDefinition EnsureCoopButtonPuzzleDefinition()
        {
            string folder = SettingsRoot + "/Puzzles";
            Directory.CreateDirectory(folder);
            const string path = SettingsRoot + "/Puzzles/Puzzle_Forest_CoopButton.asset";

            var asset = AssetDatabase.LoadAssetAtPath<PuzzleDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PuzzleDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            var so = new SerializedObject(asset);
            SetStr(so, "puzzleId", "puzzle.forest.coopbutton");
            SetStr(so, "displayName", "협동 버튼");
            SetEnum(so, "puzzleType", (int)PuzzleType.Button);

            // 프로토타입은 1인 테스트가 많다 — 최소 인원을 1로 두고 협동은 solo 허용으로 표현.
            var minPlayers = so.FindProperty("minPlayers");
            if (minPlayers != null) minPlayers.intValue = 1;
            var solo = so.FindProperty("soloSolvable");
            if (solo != null) solo.boolValue = true;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static MapIntroData EnsureForestIntro()
        {
            string folder = SettingsRoot + "/MapIntro";
            Directory.CreateDirectory(folder);
            const string path = SettingsRoot + "/MapIntro/MapIntro_Forest.asset";

            var asset = AssetDatabase.LoadAssetAtPath<MapIntroData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MapIntroData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.mapId = "forest";
            asset.title = "THE FOREST";
            asset.subtitle = "Map 01";
            asset.cameraMove = MapIntroCameraMove.PanRight;
            asset.cameraStartOffset = new Vector2(-6f, 2f);
            asset.cameraDistance = 10f;
            asset.titleHold = 3.5f;
            asset.fadeColor = new Color(0.04f, 0.05f, 0.08f, 1f);
            asset.subtitleColor = new Color(0.78f, 0.82f, 0.78f, 1f);

            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ── 공통 오브젝트 (SandboxSceneBuilder와 동일 구성) ────────

        private static GameObject CreatePlayer()
        {
            var go = new GameObject("Player");
            go.transform.position = new Vector3(0f, 1f, 0f);
            go.transform.localScale = new Vector3(0.8f, 1f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = new Color(0.35f, 0.6f, 0.95f);
            sr.sortingOrder = 10;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 1f);
            col.sharedMaterial = EnsureZeroFrictionMaterial();

            var entity = go.AddComponent<CombatEntity>();
            SetPrivate(entity, "team", (int)TeamType.Players, isEnum: true);
            SetPrivate(entity, "ownerPlayerId", 0);
            SetPrivate(entity, "maxHp", 100f);

            go.AddComponent<StatusEffectController>();
            go.AddComponent<StatusEffectVisual>();
            go.AddComponent<PlayerStats>();
            go.AddComponent<BasicAttacker>();

            var controller = go.AddComponent<PlayerController>();
            var so = new SerializedObject(controller);
            var mask = so.FindProperty("groundMask");
            if (mask != null) mask.intValue = (1 << _groundLayer) | (1 << 0);
            var offset = so.FindProperty("groundCheckOffset");
            if (offset != null) offset.vector2Value = new Vector2(0f, -0.5f);
            var radius = so.FindProperty("groundCheckRadius");
            if (radius != null) radius.floatValue = 0.15f;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<PlayerInteraction>();
            return go;
        }

        private static void CreateCamera(GameObject player)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 1.5f, -10f);

            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.backgroundColor = new Color(0.07f, 0.10f, 0.09f); // 숲 밤 분위기
            cam.clearFlags = CameraClearFlags.SolidColor;

            go.AddComponent<AudioListener>();

            var urp = go.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;

            go.AddComponent<CameraShake>();

            var follow = go.AddComponent<TSWP.Sandbox.SandboxCamera>();
            follow.SetTarget(player.transform);
        }

        private static void CreateManagers()
        {
            var go = new GameObject("GameManagers");
            go.AddComponent<GameFlowManager>();
            go.AddComponent<RunManager>();
            go.AddComponent<HitFeedback>();
            go.AddComponent<DamageNumberSpawner>();
            go.AddComponent<ProjectilePool>();
        }

        private static void CreatePostFx(GameObject player)
        {
            var go = new GameObject("StatusEffectPostFx");
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            go.AddComponent<StatusEffectPostFx>();
        }

        private static VfxLibrary CreateVfx()
        {
            // 이펙트 카탈로그는 SandboxSceneBuilder가 이미 만들어 둔 것을 재사용한다.
            const string path = SettingsRoot + "/VFX/VfxLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<VfxLibrary>(path);

            var go = new GameObject("VfxSpawner");
            var spawner = go.AddComponent<VfxSpawner>();

            if (library != null) spawner.SetLibrary(library);
            else Debug.LogWarning("[TSWP] VfxLibrary가 없습니다. [TSWP > 테스트 씬 만들기]를 한 번 실행하면 생성됩니다.");

            return library;
        }

        private static void CreateHud(GameObject player)
        {
            var go = new GameObject("GameplayHud");
            go.AddComponent<GameplayHud>();
            go.AddComponent<BossHealthBar>();
        }

        private static void CreateMapIntro()
        {
            var go = new GameObject("MapIntroManager");
            var manager = go.AddComponent<MapIntroManager>();
            manager.SetIntro(EnsureForestIntro());
        }

        // ── 공유 유틸 ─────────────────────────────────────────────

        private static PhysicsMaterial2D EnsureZeroFrictionMaterial()
        {
            const string path = "Assets/Settings/Physics/NoFriction.physicsMaterial2D";
            var existing = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var material = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Projectile EnsureProjectilePrefab()
        {
            const string path = SettingsRoot + "/Prefabs/Projectile_Test.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var comp = existing.GetComponent<Projectile>();
                if (comp != null) return comp;
            }

            Directory.CreateDirectory(SettingsRoot + "/Prefabs");

            var go = new GameObject("Projectile");
            go.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _square;
            sr.color = new Color(0.8f, 0.5f, 1f);
            sr.sortingOrder = 15;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1f, 1f);

            var projectile = go.AddComponent<Projectile>();
            var so = new SerializedObject(projectile);
            var mask = so.FindProperty("obstacleMask");
            if (mask != null) mask.intValue = 1 << _groundLayer;
            var rotate = so.FindProperty("rotateToDirection");
            if (rotate != null) rotate.boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved != null ? saved.GetComponent<Projectile>() : null;
        }

        private static Sprite EnsureSquareSprite()
        {
            const string path = "Assets/Sprites/Placeholder/square.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory("Assets/Sprites/Placeholder");

            var texture = new Texture2D(16, 16);
            var pixels = new Color32[16 * 16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureGroundLayer()
        {
            if (LayerMask.NameToLayer(GroundLayerName) != -1) return;

            var tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(element.stringValue)) continue;

                element.stringValue = GroundLayerName;
                tagManager.ApplyModifiedProperties();
                return;
            }

            Debug.LogError("[TSWP] 빈 레이어 슬롯이 없어 Ground 레이어를 만들지 못했습니다.");
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes)
                if (s.path == path) return;

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = updated;
        }

        // ── SerializedObject 헬퍼 ─────────────────────────────────

        private static void SetPrivate(Object target, string field, object value, bool isEnum = false)
        {
            var so = new SerializedObject(target);
            switch (value)
            {
                case int i when isEnum: SetEnum(so, field, i); break;
                case int i: SetInt(so, field, i); break;
                case float f: SetFloat(so, field, f); break;
                case bool b: SetBool(so, field, b); break;
                case string s: SetStr(so, field, s); break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStr(SerializedObject so, string field, string value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.stringValue = value;
        }

        private static void SetFloat(SerializedObject so, string field, float value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string field, int value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string field, bool value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.boolValue = value;
        }

        private static void SetEnum(SerializedObject so, string field, int index)
        {
            var p = so.FindProperty(field);
            if (p != null) p.enumValueIndex = index;
        }

        private static void SetObj(SerializedObject so, string field, Object value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        private static void SetRel(SerializedProperty parent, string field, object value)
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
    }
}
#endif
