class_name Region
extends Node3D
## 지역 공통: 높이 칸 지형 + 장식 흩뿌리기 도우미. 지역마다 이것을 이어받아 _gen_cells·_decorate 를 채운다.
## 좌표: 생성은 세계 m 단위로 생각하고, 칸(Terrain.CELL = 0.5m)으로 바꿔 채운다.

const FLORA := "res://assets/sprites/flora.png"
const CAMP := "res://assets/sprites/camp.png"
const C := Terrain.CELL

enum G { GRASS, LUSH, DIRT, STONE, FOREST, SAND }

var WM := 64.0                   # 지역 크기 (m)
var HM := 52.0
var terrain: Terrain
var rng := RandomNumberGenerator.new()
var noise := FastNoiseLite.new()
var heights: Array = []
var mats: Array = []
var gw := 0                      # 칸 수
var gh := 0
var blocked := {}                # 물건이 차지한 칸 (Vector2i)
var spawn := Vector3(32, 0, 44)
var env_preset := "forest"
var music := "forest"
var ambience := "forest"
var lights: Array = []           # [OmniLight3D, 기본 세기, 흔들림] — 모닥불·등불 깜빡임
var side_zones: Array = []       # [Rect2(x, z, 폭, 깊이), 구간 선 z] — 들어가면 측면 시점
var exits: Array = []            # [Rect2, 다음 지역 이름, 도착 위치 Vector3]
var groups: Array = []           # 적 무리: {"at": Vector3, "kinds": [...], "r": 깨어나는 거리, "done": false}
var rig: CamRig
signal enemy_spawned(e: Enemy)


func build(seed_: int = 7) -> void:
	rng.seed = seed_
	noise.seed = seed_
	noise.frequency = 0.08
	gw = int(WM / C)
	gh = int(HM / C)
	heights.clear()
	mats.clear()
	for gz in gh:
		var hr: Array = []
		var mr: Array = []
		for gx in gw:
			var x := (gx + 0.5) * C
			var z := (gz + 0.5) * C
			hr.append(_level_at(x, z))
			mr.append(_mat_at(x, z))
		heights.append(hr)
		mats.append(mr)
	_post_cells()
	terrain = Terrain.new()
	add_child(terrain)
	terrain.build(heights, mats, load("res://assets/tex/ground.png"), load("res://assets/tex/wall.png"), 6)
	for gz in gh:
		for gx in gw:
			var m: int = mats[gz][gx]
			if m == G.DIRT or m == G.STONE:
				blocked[Vector2i(gx, gz)] = true
	_decorate()
	terrain.commit_shade()
	_bounds()


## 지역 가장자리 보이지 않는 벽 (출구는 벽 안쪽에 있어서 먼저 닿는다)
func _bounds() -> void:
	for r in [Rect2(-1, -1, WM + 2, 1), Rect2(-1, HM, WM + 2, 1), Rect2(-1, 0, 1, HM), Rect2(WM, 0, 1, HM)]:
		var b := StaticBody3D.new()
		var cs := CollisionShape3D.new()
		var bx := BoxShape3D.new()
		bx.size = Vector3(r.size.x, 30.0, r.size.y)
		cs.shape = bx
		b.add_child(cs)
		b.position = Vector3(r.position.x + r.size.x * 0.5, 10.0, r.position.y + r.size.y * 0.5)
		add_child(b)


# ---- 지역마다 채우는 것
func _level_at(_x: float, _z: float) -> int:
	return 0


func _mat_at(_x: float, _z: float) -> int:
	return G.GRASS


func _post_cells() -> void:
	pass


func _decorate() -> void:
	pass


## 지역마다 매 프레임 할 일 (보스방 진행 등)
func tick(_players: Array) -> void:
	pass


## 적 무리: 플레이어가 가까이 오면 나타난다 (화면 가장자리 쪽에서 — 갑자기 코앞에 생기지 않게)
func spawn_check(players: Array) -> void:
	for g in groups:
		if g["done"]:
			continue
		for p in players:
			var pl := p as Node3D
			if pl.global_position.distance_to(g["at"]) < float(g["r"]):
				g["done"] = true
				var i := 0
				for k in g["kinds"]:
					var ang := TAU * i / float((g["kinds"] as Array).size()) + rng.randf() * 0.5
					var pos: Vector3 = g["at"] + Vector3(cos(ang), 0, sin(ang)) * rng.randf_range(1.0, 2.6)
					pos.y = terrain.height_at(pos) + 0.2
					var e := Enemy.spawn(self, k, pos, terrain, rig)
					enemy_spawned.emit(e)
					i += 1
				Audio.play("spawn", g["at"])
				break


func _process(_dt: float) -> void:
	# 불빛 깜빡임 (겹친 사인파 — 규칙적으로 안 보이게)
	var t := Time.get_ticks_msec() / 1000.0
	for l in lights:
		var o: OmniLight3D = l[0]
		var f: float = l[2]
		var k := 1.0 + (sin(t * 9.1 + o.position.x) * 0.5 + sin(t * 13.7 + o.position.z) * 0.3 + sin(t * 4.3) * 0.2) * f
		o.light_energy = float(l[1]) * k


# ------------------------------------------------------------------ 칸 도우미
## 흙길: 점들을 잇는 굽은 띠를 칸 재질로 칠한다 (폭 m)
func paint_path(pts: Array, width: float, mat := G.DIRT) -> void:
	for i in pts.size() - 1:
		var a: Vector2 = pts[i]
		var b: Vector2 = pts[i + 1]
		var n := int(a.distance_to(b) / (C * 0.5)) + 1
		for k in n + 1:
			var p := a.lerp(b, float(k) / n)
			var r := width * 0.5 + noise.get_noise_2d(p.x * 3.0, p.y * 3.0) * 0.5
			for gz in range(int((p.y - r) / C) - 1, int((p.y + r) / C) + 2):
				for gx in range(int((p.x - r) / C) - 1, int((p.x + r) / C) + 2):
					if gx < 0 or gz < 0 or gx >= gw or gz >= gh:
						continue
					if Vector2((gx + 0.5) * C, (gz + 0.5) * C).distance_to(p) <= r:
						mats[gz][gx] = mat


func _free(c: Vector2i, r: int) -> bool:
	for dz in range(-r, r + 1):
		for dx in range(-r, r + 1):
			if blocked.has(c + Vector2i(dx, dz)):
				return false
	return true


func _mark(c: Vector2i, r: int) -> void:
	for dz in range(-r, r + 1):
		for dx in range(-r, r + 1):
			blocked[c + Vector2i(dx, dz)] = true


## 칸 안 아무 자리
func _spot(c: Vector2i) -> Vector3:
	var p := Vector3((c.x + rng.randf_range(0.2, 0.8)) * C, 0, (c.y + rng.randf_range(0.2, 0.8)) * C)
	p.y = terrain.height_at(p)
	return p


## 주변 r칸이 모두 같은 높이인가 (절벽 끝에 걸치지 않게)
func _flat(c: Vector2i, r := 1) -> bool:
	var l := terrain.level(c.x, c.y)
	for dz in range(-r, r + 1):
		for dx in range(-r, r + 1):
			var n := c + Vector2i(dx, dz)
			if not terrain.inside(n.x, n.y) or terrain.level(n.x, n.y) != l:
				return false
	return true


func _rand_cell(margin := 1) -> Vector2i:
	return Vector2i(rng.randi_range(margin, gw - 1 - margin), rng.randi_range(margin, gh - 1 - margin))


func _cell(x: float, z: float) -> Vector2i:
	return terrain.cell_of(Vector3(x, 0, z))


# ------------------------------------------------------------------ 장식 도우미
## 그림 폭에 맞춘 그늘 (해가 왼쪽 위라 오른쪽 아래로 살짝 밀린다)
func shade_under(atlas: String, p: Vector3, sprite: String, k: float) -> void:
	var sz := BB.px_size(atlas, sprite) / BB.PPU
	var rx := sz.x * 0.44
	var tall := sz.y > sz.x * 0.9
	var rz := rx * (0.42 if tall else 0.5)
	terrain.blob(p + Vector3(rx * 0.12, 0, rz * 0.25 + (0.25 if tall else 0.05)), rx, rz, k)


## 단단한 물건의 발밑 충돌 (원기둥)
func solid(p: Vector3, r: float, hgt := 2.0) -> void:
	var body := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var cyl := CylinderShape3D.new()
	cyl.radius = r
	cyl.height = hgt
	cs.shape = cyl
	cs.position.y = hgt * 0.5
	body.add_child(cs)
	body.position = p
	add_child(body)


## 상자 모양 충돌
func solid_box(p: Vector3, size: Vector3) -> void:
	var body := StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var bx := BoxShape3D.new()
	bx.size = size
	cs.shape = bx
	cs.position.y = size.y * 0.5
	body.add_child(cs)
	body.position = p
	add_child(body)


## 나무 한 그루 (그늘·충돌 포함)
func tree(p: Vector3, name: String, flip := false) -> MeshInstance3D:
	var t := BB.make(FLORA, name, {"sway": 0.06, "receive_shade": false, "see_through": true})
	t.position = p
	if flip:
		t.scale.x = -1.0
	add_child(t)
	shade_under(FLORA, p, name, 0.85)
	solid(p, 0.35)
	return t


## 소품 하나 (식물 아틀라스). 덤불·바위는 자리를 막는다
func prop(c: Vector2i, nm: String) -> MeshInstance3D:
	var soft := nm.begins_with("bush") or nm.begins_with("fern")
	var casts := not nm.begins_with("fern") and not nm.begins_with("mush")
	var s := BB.make(FLORA, nm, {"sway": 0.04 if soft else 0.0, "push": 0.12 if soft else 0.0, "receive_shade": not casts})
	s.position = _spot(c)
	if rng.randf() < 0.5:
		s.scale.x = -1.0
	add_child(s)
	if casts:
		shade_under(FLORA, s.position, nm, 0.75)
	if nm.begins_with("bush") or nm.begins_with("rock") or nm.begins_with("stump") or nm.begins_with("log"):
		_mark(c, 1)
	return s


## 캠프 소품 하나 (세계 위치 x,z). opts: flip, solid(반지름), box(Vector3), shade(진하기), emission
func camp_prop(x: float, z: float, nm: String, opts := {}) -> MeshInstance3D:
	var p := Vector3(x, 0, z)
	p.y = terrain.height_at(p)
	var big := BB.px_size(CAMP, nm).y > 28.0
	var s := BB.make(CAMP, nm, {"receive_shade": false, "emission": float(opts.get("emission", 0.0)), "unique": opts.has("frames"), "see_through": big})
	s.position = p
	if opts.get("flip", false):
		s.scale.x = -1.0
	add_child(s)
	if float(opts.get("shade", 0.7)) > 0.0:
		shade_under(CAMP, p, nm, float(opts.get("shade", 0.7)))
	if opts.has("solid"):
		solid(p, float(opts["solid"]))
	if opts.has("box"):
		solid_box(p, opts["box"])
	if opts.has("frames"):
		var an := SpriteAnim.new()
		an.setup(s, CAMP, opts["frames"], float(opts.get("fps", 8.0)))
		add_child(an)
	var c := terrain.cell_of(p)
	_mark(c, int(opts.get("block", 2)))
	return s


## 따뜻한 불빛 (모닥불·등불)
func warm_light(p: Vector3, energy: float, rng_m: float, col := Color("#ffb060"), flicker := 0.12) -> OmniLight3D:
	var o := OmniLight3D.new()
	o.position = p
	o.light_color = col
	o.light_energy = energy
	o.omni_range = rng_m
	o.omni_attenuation = 1.2
	o.shadow_enabled = false
	add_child(o)
	lights.append([o, energy, flicker])
	return o


## 풀 다발·꽃 (멀티메시) — 1m 칸마다 몇 개씩. skip(m) 이 참이면 그 칸은 건너뛴다
func scatter_grass(density := 1.0, flower_k := 0.25) -> void:
	var tufts: Array = []
	var flowers: Array = []
	for z in int(HM):
		for x in int(WM):
			var c := terrain.cell_of(Vector3(x + 0.5, 0, z + 0.5))
			var m: int = mats[c.y][c.x]
			if m == G.DIRT or m == G.STONE:
				continue
			var n_t := 3 if m == G.LUSH else (2 if m == G.GRASS else 1)
			for k in n_t:
				if rng.randf() < 0.55 * density:
					var nm := "tuft%d" % rng.randi_range(0, 5) if m != G.FOREST else "tuft_dark%d" % rng.randi_range(0, 1)
					var p := Vector3(x + rng.randf(), 0, z + rng.randf())
					if terrain.mat_at(p) in [G.DIRT, G.STONE]:
						continue
					p.y = terrain.height_at(p)
					tufts.append([nm, p, rng.randf() < 0.5])
			# 꽃 무리 (노이즈가 높은 곳에 몰리게)
			if m != G.FOREST and noise.get_noise_2d(x * 1.3 + 500, z * 1.3) > flower_k and rng.randf() < 0.6:
				var p2 := Vector3(x + rng.randf(), 0, z + rng.randf())
				if terrain.mat_at(p2) in [G.DIRT, G.STONE]:
					continue
				p2.y = terrain.height_at(p2)
				var fn := "flower%d" % rng.randi_range(0, 2) if rng.randf() > 0.15 else "flower_y"
				flowers.append([fn, p2, rng.randf() < 0.5])
	if not tufts.is_empty():
		add_child(BB.multi(FLORA, tufts, {"sway": 0.05, "push": 0.3}))
	if not flowers.is_empty():
		add_child(BB.multi(FLORA, flowers, {"sway": 0.03, "push": 0.2}))


## 숲: 숲 바닥 칸에 빽빽하게, 풀밭엔 드문드문 + 나무 둘레 덤불·고사리
func scatter_trees(tries: int, sparse_k := 0.05) -> void:
	var names := ["tree0", "tree1", "tree2", "tree_light0", "pine0", "pine1"]
	var spots: Array = []
	for i in tries:
		var c := _rand_cell(2)
		var m: int = mats[c.y][c.x]
		var dense := m == G.FOREST
		if not dense and rng.randf() > sparse_k:
			continue
		if not _flat(c, 2) or not _free(c, 5 if dense else 7):
			continue
		var nm: String = names[rng.randi_range(0, 3)] if rng.randf() > 0.22 else names[rng.randi_range(4, 5)]
		tree(_spot(c), nm, rng.randf() < 0.5)
		_mark(c, 2)
		spots.append(c)
	for tc in spots:
		for k in rng.randi_range(0, 2):
			var c: Vector2i = tc + Vector2i(rng.randi_range(-6, 6), rng.randi_range(1, 6))
			if not terrain.inside(c.x, c.y) or not _flat(c) or blocked.has(c):
				continue
			var pick := rng.randf()
			var nm := "bush%d" % rng.randi_range(0, 2) if pick < 0.35 else ("fern%d" % rng.randi_range(0, 1) if pick < 0.85 else "mush0")
			prop(c, nm)


func scatter_props(list: Array) -> void:
	for pr in list:
		var placed := 0
		var tries := 0
		while placed < int(pr[1]) and tries < 600:
			tries += 1
			var c := _rand_cell(2)
			if not _flat(c) or not _free(c, 2):
				continue
			prop(c, pr[0])
			placed += 1
