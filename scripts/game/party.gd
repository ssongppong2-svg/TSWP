class_name Party
extends Node
## 파티 공유 상태: 부활 횟수(인원 × 3) · 금화 · 기록(처치·피해·회복·사망 — 결과 화면용, 파티 번호별)
## 근거: 게임 시작과 선택, 직업, 플레이.md / 방 시스템.md

signal changed
signal game_over

var lives := 3
var gold := 0
var stats := {}                    # 파티 번호 → {"kills", "deaths", "dmg", "heal", "ff", "items", "throws"} (결과 화면 · MVP)
var over := false
## 파티 경험치 · 레벨 (전투 시스템.md: 처치 → 경험치. 아이템 시스템.md: 레벨보다 아이템이 중요 — 그래서 레벨 보너스는 작게)
##  레벨마다: 공격력 +4% · 최대 체력 +5% · 체력 30% 회복. 짝수 레벨마다 보상 아이템 하나 (먼저 줍는 사람이 임자)
var xp := 0
var level := 1
signal leveled(lv: int)
var bags := {}                     # 파티 번호 → Inventory (지역을 옮겨도 가방이 이어지게)
var rng := RandomNumberGenerator.new()
const DROP_ITEM := 0.07            # 적 처치 시 장비가 떨어질 확률
const DROP_CONS := 0.09            # 소비 아이템


func _enter_tree() -> void:
	add_to_group("party")


## 새 판: 부활 횟수 · 금화 · 기록을 처음으로
func setup(player_count: int) -> void:
	lives = player_count * 3
	gold = 0
	stats.clear()
	xp = 0
	level = 1
	for b in bags.values():
		(b as Inventory).clear()
	rng.randomize()
	over = false
	changed.emit()


## 부활 1회 사용. 남은 게 없으면 false (그 플레이어는 관전)
func use_life(p: Node) -> bool:
	_stat(p, "deaths", 1)
	if lives <= 0:
		_check_wipe()
		return false
	lives -= 1
	changed.emit()
	return true


func _check_wipe() -> void:
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player and not (f as Player).dead:
			return
	if not over:
		over = true
		game_over.emit()


func inv_of(slot: int) -> Inventory:
	if not bags.has(slot):
		bags[slot] = Inventory.new()
	return bags[slot]


func add_gold(n: int) -> void:
	gold += n
	changed.emit()


func _stat(p: Node, key: String, n: float) -> void:
	if p == null:
		return
	# 지역을 옮기면 Player 노드가 새로 생기므로 노드 대신 파티 번호(slot)로 모은다
	var id: Variant = p.get("slot") if p.get("slot") != null else p.get_instance_id()
	if not stats.has(id):
		stats[id] = blank()
	stats[id][key] = stats[id].get(key, 0) + n


static func blank() -> Dictionary:
	return {"kills": 0, "deaths": 0, "dmg": 0.0, "heal": 0.0, "ff": 0.0, "items": 0, "throws": 0, "saves": 0}


## 다른 곳(Fighter.take · Player)에서 기록 (group "party")
func record(p: Node, key: String, n: float) -> void:
	_stat(p, key, n)


func on_enemy_killed(e: Enemy, by: Node) -> void:
	# 금화는 실제 동전이 튀어나와 플레이어가 주울 때 더해진다 (Coin)
	if e.is_inside_tree():
		Coin.burst(e.get_parent(), e.global_position, int(e.E.get("gold", 1)), e.terrain)
	add_xp(int(ceilf(e.base_max_hp / 4.0)) * (3 if e.elite else 1), e)
	if by is Player:
		_stat(by, "kills", 1)
		(by as Player).on_kill(e)
		if (by as Player).local:
			Achievements.add(get_tree(), "kills")
	# 아이템 떨어뜨리기 (적 시스템.md: 일반 몬스터도 낮은 확률로)
	if e.is_inside_tree() and (e.elite or bool(e.E.get("loot", false))):
		# 엘리트·황금 거미: 좋은 것을 꼭 하나
		ItemDrop.spawn(e.get_parent(), Items.roll(rng, false, 1.0, "uncommon"), e.global_position + Vector3(0, 0.5, 0), e.terrain)
	elif e.is_inside_tree():
		if rng.randf() < DROP_ITEM * Diff.LOOT[Diff.level()]:
			ItemDrop.spawn(e.get_parent(), Items.roll(rng), e.global_position + Vector3(0, 0.5, 0), e.terrain)
		elif rng.randf() < DROP_CONS:
			ItemDrop.spawn(e.get_parent(), Items.roll(rng, true), e.global_position + Vector3(0, 0.5, 0), e.terrain)


## 무리 밖에서 생긴 적(고치에서 부화 등)도 처치 보상 연결
func on_spawn(e: Enemy) -> void:
	if not e.killed.is_connected(on_enemy_killed):
		e.killed.connect(on_enemy_killed)


func xp_need() -> int:
	return 40 + (level - 1) * 30


func add_xp(n: int, at: Node3D = null) -> void:
	xp += n
	while xp >= xp_need():
		xp -= xp_need()
		level += 1
		_level_up(at)
	changed.emit()


func _level_up(at: Node3D) -> void:
	leveled.emit(level)
	get_tree().call_group("hud", "banner", "레벨 업!  Lv.%d" % level, 2.0)
	get_tree().call_group("hud", "notify", "Lv.%d — 공격력 +4%% · 최대 체력 +5%% · 체력 회복%s" % [level, " · 보상 아이템!" if level % 2 == 0 else ""], Color("#9affc0"))
	Audio.stinger("st_item")
	var first: Player = null
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player:
			var p := f as Player
			p.apply_level(level)
			if p.dead:
				continue
			var h := Hit.make(p.max_hp() * 0.3, p, Vector3.ZERO, "item")
			h.heal = true
			p.take(h)
			Fx.pop(p.get_parent(), p.global_position + Vector3(0, 0.05, 0), ["kpop0", "kpop1", "kpop2", "kpop3"], 12.0)
			Particles.burst(p.get_parent(), p.center(), Color("#d0ffe0"), Color("#5ad08a"), 16, 4.0)
			if first == null or p.local:
				first = p
	if level % 2 == 0 and first and first.is_inside_tree():
		ItemDrop.spawn(first.get_parent(), Items.roll(rng, false, 0.6, "uncommon"), first.global_position + Vector3(0, 1.2, 0), first.terrain, Vector3(randf_range(-2, 2), 0, -2))
