class_name NestMap
extends Region
## 거미 여왕의 둥지 (보스 01 방): 둘레가 높은 절벽으로 막힌 어두운 원형 공터.
##  · 남쪽 입구로 들어와 가운데로 가면 여왕이 내려온다
##  · 껍질 단계마다 돌기둥 4개가 솟는다 (Pillar)
##  · 여왕이 쓰러지면 2막: 거미 떼 + 양쪽 레버를 거의 동시에 → 진짜 승리 (왕관)

signal boss_started(q: Queen)
signal victory

const CENTER := Vector2(18.0, 15.0)

var queen: Queen
var pillars: Array = []
var levers: Array = []
var started := false
var act2 := false
var won := false
var swarm: Array = []


func _init() -> void:
	WM = 36.0
	HM = 34.0
	spawn = Vector3(18.0, 0, 30.5)
	env_preset = "nest"
	music = "nest"
	ambience = "nest"
	exits = [[Rect2(14.0, 33.2, 8.0, 1.0), "forest", Vector3(33.5, 0, 2.0)]]


func _level_at(x: float, z: float) -> int:
	var n := noise.get_noise_2d(x * 1.3, z * 1.3)
	var d := Vector2((x - CENTER.x) / 1.1, z - CENTER.y).length()
	# 남쪽 입구 골짜기
	if z > CENTER.y + 8.0 and absf(x - 18.0) < 3.2 + n * 1.2:
		return 0
	if d > 14.5 + n * 2.0:
		return 6
	if d > 13.0 + n * 2.0:
		return 3
	return 0


func _mat_at(x: float, z: float) -> int:
	var n := noise.get_noise_2d(x * 0.8, z * 0.8)
	var d := Vector2(x - CENTER.x, z - CENTER.y).length()
	if _level_at(x, z) >= 3:
		return G.FOREST
	if d < 6.0 + n * 2.0:
		return G.STONE
	if n > 0.2:
		return G.LUSH
	return G.DIRT if n < -0.15 else G.GRASS


func _decorate() -> void:
	# 둘레 숲 (높은 곳에)
	scatter_trees(1400, 0.0)
	scatter_props([["rock1", 6], ["rock0", 4], ["mush0", 10], ["stump0", 3]])
	scatter_grass(0.6, 0.6)
	# 보라빛 버섯 (어두운 둥지의 광원) + 보라 반딧불
	for k in 10:
		var a := k * TAU / 10.0 + rng.randf() * 0.3
		glow_patch(Vector3(CENTER.x + cos(a) * 12.0, 0, CENTER.y + sin(a) * 10.5), 1)
	fireflies(8, Rect2(CENTER.x - 12, CENTER.y - 10, 24, 20), Color("#d0a0ff"))
	# 돌기둥 4개 (처음엔 땅속)
	for k in 4:
		var a := PI * 0.25 + k * PI * 0.5
		var pl := Pillar.new()
		add_child(pl)
		pl.position = Vector3(CENTER.x + cos(a) * 6.5, -3.2, CENTER.y + sin(a) * 5.5)
		pillars.append(pl)


func raise_pillars(on: bool) -> void:
	for p in pillars:
		(p as Pillar).set_up(on)


## 매 프레임: 보스 시작 · 2막 진행
func tick(players: Array) -> void:
	if not started:
		for p in players:
			if (p as Node3D).global_position.z < CENTER.y + 8.0:
				_start_boss()
				break
	if act2 and not won:
		var alive := swarm.filter(func(e): return is_instance_valid(e) and not (e as Fighter).dead).size()
		var on := levers.filter(func(l): return (l as Lever).on).size()
		if alive == 0 and on == levers.size() and levers.size() > 0:
			_win()


func _start_boss() -> void:
	started = true
	var at := Vector3(CENTER.x, 0, CENTER.y - 2.0)
	queen = Queen.new()
	queen.terrain = terrain
	queen.rig = rig
	queen.arena = self
	add_child(queen)
	queen.global_position = at + Vector3(0, 0.2, 0)
	queen.defeated.connect(_on_queen_down)
	Audio.music("boss", 0.6)
	boss_started.emit(queen)


## 2막: 거미 떼 + 레버 두 개 (각각 4초 동안 켜짐 — 둘이 동시에 켜져 있어야 한다)
func _on_queen_down(_q: Queen) -> void:
	act2 = true
	Audio.stinger("st_phase")
	await get_tree().create_timer(1.6, false).timeout
	for k in 6:
		var a := k * TAU / 6.0
		var p := Vector3(CENTER.x + cos(a) * 8.0, 0.3, CENTER.y + sin(a) * 7.0)
		var e := Enemy.spawn(self, "spider", p, terrain, rig)
		swarm.append(e)
		get_tree().call_group("party", "on_spawn", e)
	Audio.play("spawn", Vector3(CENTER.x, 0, CENTER.y))
	for x in [CENTER.x - 9.0, CENTER.x + 9.0]:
		var l := Lever.new()
		add_child(l)
		l.position = Vector3(x, terrain.height_at(Vector3(x, 0, CENTER.y + 2.0)), CENTER.y + 2.0)
		levers.append(l)


func _win() -> void:
	won = true
	Audio.play("q_open", Vector3(CENTER.x, 0, CENTER.y))
	Audio.stinger("st_win")
	Audio.music("crown", 2.0)
	# 왕관 (보상) — 빛나며 튀어나온다
	var c := BB.make(BossBits.ATLAS, "crown", {"emission": 0.6, "receive_shade": false})
	add_child(c)
	var at := queen.global_position if queen else Vector3(CENTER.x, 0, CENTER.y)
	c.position = at + Vector3(0, 0.1, 1.0)
	var l := OmniLight3D.new()
	l.light_color = Color("#ffe080")
	l.light_energy = 2.5
	l.omni_range = 5.0
	l.position = c.position + Vector3(0, 1.0, 0)
	add_child(l)
	var tw := c.create_tween().set_loops()
	tw.tween_property(c, "position:y", c.position.y + 0.4, 0.6).set_trans(Tween.TRANS_SINE)
	tw.tween_property(c, "position:y", c.position.y, 0.6).set_trans(Tween.TRANS_SINE)
	victory.emit()
