# TSWP 코드 아키텍처 계약 (v1.0)

이 문서는 TSWP Unity 뼈대 코드의 **구속력 있는 계약**이다.
모든 시스템 코드는 이 문서의 네임스페이스·타입 시그니처·규칙을 그대로 따른다.
설계 근거는 저장소 루트의 각 `*.md` 설계 문서다. (게임 성경.md이 최우선)

---

## 1. 프로젝트 전제

- Unity 6 LTS, 2D URP, 픽셀아트 (Pixel Perfect Camera, PPU 16 권장 — 도트 시스템.md)
- 2D 횡스크롤 협동 액션 로그라이크, 최대 8인 멀티플레이 (호스트 권위)
- **뼈대 단계에서는 외부 패키지 참조 금지**: `UnityEngine` + `System`만 사용.
  - 네트워킹(NGO/Steamworks), 보이스(Vivox), 새 Input System은 **TODO 주석 + 추상화 시임**으로만 남긴다.
  - 입력은 우선 레거시 `UnityEngine.Input`으로 구현하고 `IPlayerInput` 추상화 뒤에 둔다 (키 리바인딩 요구 → 추후 Input System 교체).
- 에디터 전용 코드는 반드시 `#if UNITY_EDITOR`로 감싼다.

## 2. 폴더 ↔ 네임스페이스 (1:1 고정)

| 폴더 (Assets/Scripts/) | 네임스페이스 | 담당 |
|---|---|---|
| Core/ | TSWP.Core | 게임 흐름 FSM, 런 관리, 공유 부활, 규칙 상수, 이벤트 버스, 스탯, 유틸 |
| Combat/ | TSWP.Combat | 피해 파이프라인, CombatEntity, 구조물, 환경 해저드 |
| StatusEffects/ | TSWP.StatusEffects | 상태이상 16종, 컨트롤러, 시너지 |
| Jobs/ | TSWP.Jobs | 직업 정의, 기본공격/스킬/패시브, 스킬 캐스터 |
| Items/ | TSWP.Items | 아이템 정의, 장착(5슬롯), 효과 모듈, 드롭/루트 테이블 |
| Player/ | TSWP.Player | 플레이어 이동/입력/상호작용/스탯 조립 |
| Map/ | TSWP.Map | 절차 맵 생성(Branch&Merge DAG), 방/바이옴, 구조물 배치 |
| Enemies/ | TSWP.Enemies | 적 데이터/AI(6요소 컨텍스트)/스폰/조합 |
| Bosses/ | TSWP.Bosses | 보스 15종 데이터, 7단계 전투 FSM, 패턴 선택기, 광폭화 |
| Puzzles/ | TSWP.Puzzles | 퍼즐 FSM, 요소(버튼/레버/발판/상자/폭탄), 트롤 결과, 리커버리 |
| UI/ | TSWP.UI | HUD 뷰모델, 알림, 미니맵, 보스 UI, 핑 UI, 설정 |
| Meta/ | TSWP.Meta | 업적, 칭호, 플레이어 정체성(Steam 닉네임+칭호) |
| Online/ | TSWP.Online | 로비/세션/재접속/채팅/보이스 설정 (전부 네트워크 도입 전 스텁) |
| Art/ | TSWP.Art | 팔레트/색상 규칙 SO, 아트 규격, CharacterVisual |
| Sandbox/ | TSWP.Sandbox | **임시** — 프로토타입 검증용 (허수아비/추적 카메라/IMGUI HUD). 실제 시스템 완성 시 삭제 |
| Editor/ | TSWP.EditorTools | 에디터 전용 도구. 테스트 씬 자동 생성기 등 (Editor 폴더 = 에디터 어셈블리) |

단일 어셈블리(asmdef 없음)이므로 네임스페이스 간 상호 참조는 자유다.
단, **타입 정의 위치는 아래 §4의 소유권 표를 따른다** (중복 정의 금지).

## 3. 코딩 규칙

1. 주석·문서화는 **한국어**, 코드 식별자는 영어. 각 파일 상단에 근거 문서를 적는다.
   예: `// 근거: 전투 시스템.md — 아군 피해 기본 50%`
2. 밸런스 수치가 문서에 **명시된 경우** → `GameRules` 상수 또는 SO 기본값으로 반영.
   문서에 **미정인 경우** → `[SerializeField]` 필드 + `// TODO(밸런스): 문서 미정` 주석.
3. ScriptableObject에는 `[CreateAssetMenu(menuName = "TSWP/...")]`를 붙인다.
4. 매니저 싱글턴은 `Instance` 정적 프로퍼티 + `Awake` 등록 패턴(간단형)으로 통일. DontDestroyOnLoad는 GameFlowManager만.
5. 게임플레이 → UI/메타 통지는 반드시 `TSWP.Core.GameEvents` 경유 (UI가 게임 로직을 직접 참조 금지).
6. 팀/아군 판정은 레이어가 아닌 `TeamType` 필드 비교 (아군사격이 항시 존재하기 때문).
7. 문서 간 미해소 지점(예: 즉시부활 vs E키 팀원부활)은 양쪽 수용 가능한 구조로 만들고 `// NOTE(기획 확인 필요)` 주석을 남긴다.
8. `Update` 폴링보다 C# event 구독을 우선한다.
9. 네트워크 동기화가 필요한 상태에는 `// SYNC: 호스트 권위, 추후 NGO NetworkVariable` 주석을 남긴다.

## 4. 공유 타입 소유권과 고정 시그니처

아래 타입들은 **정확히 이 위치·이 시그니처**로만 존재한다. 다른 폴더에서 유사 타입 재정의 금지.

### TSWP.Core (이미 작성됨 — 수정 금지, 참조만)

```csharp
public enum TeamType { Players, Enemies, Neutral }
public enum Difficulty { SuperCoward, Human, God, Meme }
public enum GameFlowState { MainMenu, Lobby, Starting, Tutorial, StartItemDrop, Exploration, BossFight, GameOver, Results, AfterParty }
public enum PingType { Danger, Move, Item, Rally, Help }          // 핑은 이 5종, 여기 한 곳에만 정의
public static class GameRules { /* MaxPlayers=8, 부활=인원×3, 시작아이템=floor(인원×3/5), 장착슬롯 5, 아군피해 0.5f, 기본치명타 0, 보스드롭 3~4, 보스 15종 등 */ }
public static class GameEvents { /* 정적 C# event 허브 — 원시타입/ID 페이로드 위주 */ }
public class CooldownTimer { /* Tick(dt), TryUse(), Remaining, IsReady */ }
public enum StatType { MaxHealth, AttackPower, Defense, MoveSpeed, AttackSpeed, CritChance, Range, CooldownReduction }
public class StatCollection { /* base값 + modifier 스택(가산/승산), GetValue(StatType) */ }
public class SharedReviveSystem { /* remaining, TryConsume(), 초기화(playerCount) */ }
public class PlayerMatchStats { /* damageDealt, healingDone, kills, deaths, rescues, itemsAcquired, trollScore, pingsUsed */ }
public class MatchResult { /* mvpPlayerId, perPlayerStats, playTime, clearedBossIds, acquiredItemIds */ }
public class EmoteData : ScriptableObject { /* emoteId, displayName, sprite, isUnlockable */ }
public class GameFlowManager : MonoBehaviour { /* State, ChangeState(GameFlowState) */ }
public class RunManager : MonoBehaviour { /* 시드, 스테이지 1~15, ReviveSystem, 통계 수집 */ }
```

### TSWP.Combat (IDamageable / DamageInfo / FriendlyFireRule / KnockbackInfo / HazardType은 이미 작성됨)

```csharp
public interface IDamageable { void TakeDamage(in DamageInfo info); }
public struct DamageInfo { /* baseDamage, itemBonus, skillBonus, miscBonus, TotalDamage, isCritical, isExplosive, knockback(KnockbackInfo?), statusEffects(List<StatusEffectData>), source(CombatEntity), friendlyFireOverride(FriendlyFireRule?) */ }
public struct FriendlyFireRule { FriendlyFireMode mode; float value; }   // DefaultPercent | CurrentHpPercent | Custom
public struct KnockbackInfo { Vector2 direction; float force; float stunDuration; }
public enum HazardType { Lava, Poison, Spike, FallingRock, Explosion, Ice, FallDeath }  // 환경 해저드는 여기 한 곳
// ↓ Combat 담당자가 작성
public class CombatEntity : MonoBehaviour, IDamageable
{
    // CurrentHp, MaxHp, Team(TeamType), IsKnockbackImmune, IsInvincible
    // event Action<DamageInfo> Damaged; event Action<CombatEntity> Died;
}
public static class DamageSystem { /* 단일 진입점 Apply(target, info): 무적→아군판정(50%/오버라이드)→구조물 폭발판정→HP차감→넉백→상태이상 위임 */ }
public class Structure : CombatEntity { /* bombOnlyDestructible, architectBuilt 예외 */ }
public class EnvironmentHazard : MonoBehaviour { /* HazardType, 진영 무관 피해 */ }
```

### TSWP.StatusEffects

```csharp
public enum StatusEffectType { Burn, Poison, Freeze, Shock, Bleed, Fear, Confusion, Silence, Slow, Root, Stun, Weak, Vulnerable, HealBlock, Knockback, Launch }  // 16종 고정
public class StatusEffectData : ScriptableObject { /* effectType, duration, tickDamage, tickInterval, 각종 배율, blocks* 플래그, breaksOnDamage, ccPriority, canSpread, canAffectAllies, icon */ }
public class StatusEffectController : MonoBehaviour
{
    // ApplyEffect(StatusEffectData data, GameObject source) : bool  — 면역 체크, 동종=지속시간 갱신(중첩 금지)
    // RemoveEffect(StatusEffectType), HasEffect(StatusEffectType)
    // CanMove / CanAttack / CanUseSkill / CanBeHealed : bool  — 이동/공격/스킬/회복 시스템은 이 질의만 사용
    // MoveInputModifier(Vector2 입력) : Vector2 — 공포(강제 반전 이동)/혼란(입력 반전) 적용 지점
    // OnDamageTaken() — 빙결 즉시 해제 훅 / OnMoved(distance) — 출혈 훅
}
public class StatusSynergyRule : ScriptableObject { /* trigger, catalyst, result — 감전+물, 화상+기름, 빙결+폭탄 */ }
```

### TSWP.Jobs

```csharp
public class JobDefinition : ScriptableObject { /* jobId(string), displayName, difficulty(1~5), roles(JobRole[]), jobColor(Color), basicAttack(BasicAttackProfile), activeSkill(ActiveSkillDefinition), passive(PassiveDefinition), icon */ }
public enum JobRole { Melee, Ranged, Support, Defense, Utility, Deployable, Special }
public class BasicAttackProfile { /* attackType, range, attackSpeed, damage — 직렬화 클래스 */ }
public class ActiveSkillDefinition : ScriptableObject { /* skillId, cooldown(>0 강제 — OnValidate), grantsInvincibility, invincibilityDuration, friendlyFireOverride, isExplosive */ }
public class PassiveDefinition : ScriptableObject { /* passiveId, description — 효과 로직은 IPassiveBehaviour 전략 */ }
public class SkillCaster : MonoBehaviour { /* Q 입력 → 쿨타임 검사(CooldownTimer) → 발동, 침묵 시 차단(StatusEffectController.CanUseSkill) */ }
```
알려진 직업 jobId(팔레트 시스템.md): `warrior, bomber, doctor, shieldbearer, archer, mage, architect, psycho`
— **enum으로 만들지 말 것**(데이터 주도), 색상 매핑은 Art의 JobColorConfig가 jobId 문자열 키로 보관.

### TSWP.Items

```csharp
public enum ItemRarity { Common, Uncommon, Rare, Epic, Legendary, Developer }  // 6단계 (팔레트 문서의 개발자 등급 포함)
public enum ItemType { Weapon, Armor, Accessory, Consumable, Relic }
[Flags] public enum AcquisitionMethod { None=0, StartingItem=1, NormalMonster=2, EliteMonster=4, Boss=8, Event=16, Shop=32, SecretRoom=64, SpecialObject=128 }
public enum StackingBehavior { EffectStack, DurationIncrease, DamageIncrease, MaxStackLimited }
public class ItemDefinition : ScriptableObject { /* itemCode, itemName, rarity, itemType, acquisition, stacking, maxStack, statModifiers(StatType/값 목록), effects(List<ItemEffect>), risk 서술+패널티, synergy, visual, flavorText */ }
public abstract class ItemEffect : ScriptableObject { /* OnEquip/OnUnequip/OnAttack/OnCrit/OnKill/OnDeath/OnTick 훅 — 위험요소도 음수 효과로 동일 파이프라인 */ }
public class ItemInstance { /* definition, stackCount, ownerPlayerId */ }
public class PlayerEquipment : MonoBehaviour { /* 슬롯 GameRules.EquipSlotCount(5), Equip 즉시 적용, SwapAndDrop→DroppedItem 스폰, UseConsumable */ }
public class DroppedItem : MonoBehaviour { /* 선착순 선점 — 픽업 판정은 단일 지점(호스트 권위 예정) */ }
public class ItemDropManager : MonoBehaviour { /* LootTable SO, 시작 아이템 GameRules.GetStartItemCount, 보스 3~4개 */ }
```

### TSWP.Player

```csharp
public interface IPlayerInput { /* MoveAxis, JumpPressed, RunHeld, AttackPressed, SkillPressed, InteractPressed, PingPressed, EmotePressed */ }
public class LegacyPlayerInput : IPlayerInput { /* UnityEngine.Input 기반. TODO: Input System 교체(리바인딩) */ }
public class PlayerController : MonoBehaviour { /* A/D 이동, Space 점프, LShift 달리기(스태미나 없음), 중력반전 대응(gravityScale 부호), StatusEffectController.MoveInputModifier 경유 */ }
public interface IInteractable { string PromptDescription { get; } bool CanInteract(PlayerController user); void Interact(PlayerController user); }
public class PlayerInteraction : MonoBehaviour { /* E키, OverlapCircle 근접 탐색, 최근접 IInteractable 실행 */ }
public class PlayerStats : MonoBehaviour { /* StatCollection 보유, CombatEntity와 연결, 아이템 modifier 적용 지점 */ }
```
퍼즐의 직업 상호작용은 능력 인터페이스로: `IPlatformBuilder, IWallBreaker, ITrapBlocker, IRangedActivator, IPoisonSupport` (Player 폴더에 정의, Jobs가 구현).

### TSWP.Map

```csharp
public enum RoomType { Start, NormalCombat, Elite, Event, Shop, Rest, Puzzle, Secret, BossPractice, Boss }
public enum BiomeType { Forest, Cave, Snowfield, Desert, Ruins, Castle, Volcano, Abyss }
public class MapGenerator { /* 순수 C#, System.Random(seed) 주입, Branch&Merge 레이어 DAG, 불변식: 모든 경로 보스 도달 + 분기≥1 */ }
public class MapGraph / RoomNode / RoomConnection { /* 스펙 필드 그대로 */ }
public class RoomManager : MonoBehaviour { /* 방 활성화/전환, 클리어 조건(전멸형/목표형) */ }
public class BiomeDefinition : ScriptableObject
public class StructureDefinition : ScriptableObject { /* StructureType, isDestructible, bombOnlyDestructible, isInteractable, architectBuildable */ }
public enum StructureType { WoodenCrate, BombCrate, Chest, Door, Lever, Button, PressurePlate, Ladder, MovingPlatform }
```

### TSWP.Enemies

```csharp
public enum EnemyGrade { Normal, Special, Elite, MiniBoss, Boss }
[Flags] public enum EnemyRole { None=0, Melee=1, Ranged=2, Tank=4, Assassin=8, Healer=16, Summoner=32, Buffer=64, Debuffer=128, SelfDestruct=256 }
public class EnemyData : ScriptableObject { /* OnValidate: 역할 최소 1개 */ }
public class EnemyController : MonoBehaviour { /* CombatEntity 조합, DropOnDeath */ }
public class EnemyAI : MonoBehaviour { /* EnemyAIContext 6요소(거리/체력/시야/장애물/공격가능/아군위치) 유틸리티 AI 스켈레톤 */ }
public class SpawnManager : MonoBehaviour { /* 화면 밖/지정 지점, 플레이어 최소거리 */ }
public class EncounterComposition : ScriptableObject
public class DropTable : ScriptableObject
```

### TSWP.Bosses

```csharp
[Flags] public enum BossType { Combat=1, Puzzle=2, Environment=4, Psychological=8 }  // 1~3개 조합
public class BossData : ScriptableObject { /* 15종, 9필수요소(외형/BGM/일반공격/특수패턴/기믹/협동퍼즐/약점직업jobId/광폭화/처치연출), OnValidate: 패턴≥5·기믹≥1·퍼즐≥1·유형1~3 */ }
public enum BossFightPhase { Intro, NormalPattern, CoopPuzzle, PatternChange, Enrage, DeathCinematic, Reward }
public class BossController : MonoBehaviour { /* 7단계 FSM, 광폭화에 체력배율 필드 금지(문서 명시) */ }
public class BossPattern : ScriptableObject { /* 조건 기반 선택(무작위 금지), 최근 패턴 이력 반복 방지 */ }
public class BossPatternSelector { /* 조건 스코어링 */ }
public class EnrageConfig { /* 공속/이속 배율 + 신규·강화 패턴 — 체력 배율 없음 */ }
public interface IGimmick / ICoopPuzzle { /* 보스별 플러그인 */ }
```

### TSWP.Puzzles

```csharp
public enum PuzzleType { Button, Lever, PressurePlate, Jump, Bomb, Structure, Carry, Timed, BombRelay, Mixed }
public enum PuzzleState { Idle, Active, Solved, Failed, Recovering }
public class PuzzleDefinition : ScriptableObject { /* minPlayers 기본 2, soloSolvable, 제한시간, 보상, 실패 불이익, 리커버리, 실패 연출 */ }
public abstract class PuzzleController : MonoBehaviour { /* 공통 FSM — Failed→게임오버 전이 금지 */ }
public abstract class PuzzleElement : MonoBehaviour { /* 버튼/레버/발판/상자/운반물/폭탄 파생, C# event로 컨트롤러에 통지 */ }
public class TrollOutcome { /* 오조작→결과 매핑 (잘못 누름→몬스터 소환 등) */ }
public class RecoveryHandler { /* 대기/레버 재생성/버튼 초기화/우회 경로 — 소프트락 금지 불변식 */ }
```

### TSWP.UI / TSWP.Meta

```csharp
// UI: 뷰모델은 GameEvents 구독만으로 갱신. 캔버스 3계층(HUD/월드/오버레이) 주석 명시.
public class HudModel / OverheadInfo / PartyMemberInfo / MinimapModel / BossUIModel / InteractionPrompt / SkillCooldownInfo
public class NotificationManager : MonoBehaviour  // 토스트 큐
public enum NotificationType { AchievementUnlocked, ItemAcquired, BossAppeared, PlayerDeath, PlayerRevived }
public class UISettings / AccessibilitySettings   // 직렬화 저장 데이터
// Meta:
public enum AchievementGrade { Common, Rare, Heroic, Legendary, Developer }
public enum AchievementCategory { Boss, Job, Coop, Troll, Exploration, Special }
public class AchievementData : ScriptableObject { /* targetCount, rewards */ }
public enum AchievementRewardType { Title, ProfileBorder, Emote, Skin }
public class AchievementManager : MonoBehaviour { /* GameEvents 카운터 구독 → 해금 → 보상 → 알림 */ }
public enum TitleColorType { Default, Legendary, Developer }   // 흰/금/보라 — 색값은 Art SO에
public enum TitleSource { Achievement, Event, Season, DeveloperEvent }
public class TitleData : ScriptableObject
public class PlayerIdentity { /* steamNickname 읽기전용, equippedTitleId, ownedTitleIds, 표시형식 "[칭호] 닉네임" */ }
```

### TSWP.Online (전부 스텁 — 외부 패키지 금지)

```csharp
public enum LobbyVisibility { Public, Private }
public enum ConnectionState { Connected, Disconnected, Reconnecting, Left }
public enum ChatChannel { All, System, ServerNotice }
public class LobbyPlayerState { /* playerId, steamId, displayName, selectedJobId, isReady, isHost */ }
public class LobbyManager : MonoBehaviour { /* 생성/참가(방코드)/준비/시작 — TODO(NGO+Steam) */ }
public class GameSessionManager : MonoBehaviour { /* GameFlowManager와 연동 */ }
public class ReconnectManager { /* steamId 키 스냅샷, 타임아웃 SerializeField(미정) */ }
public class PingBroadcaster { /* Core.PingType 사용 — 재정의 금지 */ }
public class VoiceChatConfig : ScriptableObject { /* OpenMic 고정, 거리감쇠 AnimationCurve, 벽차폐, 룸 에코 — TODO(Vivox) */ }
public class PlayerReport / ReportReason
```

### TSWP.Art

```csharp
public class ArtConfig : ScriptableObject { /* 1920x1080, 캐릭터 32, 보스 64/96/128, 소품 16, 타일 16/32, 12FPS, PPU 16 */ }
public class GamePalette : ScriptableObject { /* 48~64색, 의미 색상 매핑 */ }
public class RarityColorConfig : ScriptableObject { /* Items.ItemRarity → Color (회색/초록/파랑/보라/금색/무지개) */ }
public class JobColorConfig : ScriptableObject { /* jobId(string) → Color */ }
public class HealthBarColorConfig : ScriptableObject { /* 1.0초록/0.7연두/0.4노랑/0.2빨강/0.05깜빡임 */ }
public class EffectColorConfig / UIColorConfig / ShadowConfig : ScriptableObject
public class TitleColorConfig : ScriptableObject { /* Meta.TitleColorType → Color */ }
public class CharacterVisual : MonoBehaviour { /* flipX 좌우반전, 그림자(원형/알파 0.4~0.6/순수검정 금지) */ }
```

## 5. 중복 정의 금지 목록 (충돌 예방)

- `PingType` → Core 한 곳 (UI/Online은 참조만)
- `HazardType` → Combat 한 곳 (Map/Enemies/Bosses는 참조만)
- `ItemRarity` → Items 한 곳 (Art 색상 매핑은 참조만)
- 직업 식별 → `jobId` 문자열 (직업 enum 만들지 말 것)
- 부활/게임오버 판정 → Core.SharedReviveSystem 한 곳 (Combat/Map/Bosses는 호출만)
- `EmoteData` → Core 한 곳
- 퍼즐 타입 → Puzzles 한 곳 (Map/Bosses는 참조만)
- 결과 통계 → Core.PlayerMatchStats / MatchResult 한 곳 (UI/Online은 참조만)
