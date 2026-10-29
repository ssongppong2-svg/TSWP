class_name DemoMap
extends Node3D
## 그림 확인용 숲 지역 (캠프 앞 숲길).
##  · 남쪽 낮은 풀밭 → 흙길이 굽이쳐 북쪽 언덕(4층 절벽)으로 계단을 타고 올라간다
##  · 가장자리는 빽빽한 숲, 가운데는 트인 풀밭과 꽃, 돌바닥 공터
##  · 장식은 땅 재질과 높이에 맞춰 흩뿌린다 (풀 다발·꽃은 멀티메시 한 덩어리)
## 좌표: 생성은 세계 m 단위로 생각하고, 칸(Terrain.CELL = 0.5m)으로 바꿔 채운다.

const FLORA := "res://assets/sprites/flora.png"
const WM := 64.0                 # 지역 크기 (m)
const HM := 52.0
const C := Terrain.CELL

enum G { GRASS, LUSH, DIRT, STONE, FOREST, SAND }

var terrain: Terrain
var rng := RandomNumberGenerator.new()
var noise := FastNoiseLite.new()
var heights: Array = []
var mats: Array = []
var gw := 0                      # 칸 수
var gh := 0
var blocked := {}                # 나무 등이 차지한 칸 (Vector2i)
var spawn := Vector3(32, 0, 44)


func build(seed_: int = 7) -> void:
	rng.seed = seed_
	noise.seed = seed_
	noise.frequency = 0.08
	gw = int(WM / C)
	gh = int(HM / C)
	_gen_cells()
	terrain = Terrain.new()
	add_child(terrain)
	terrain.build(heights, mats, load("res://assets/tex/ground.png"), load("res://assets/tex/wall.png"), 6)
	_decorate()


# ------------------------------------------------------------------ 칸
func _path_x(z: float) -> float:
	return 32.0 + sin(z * 0.11) * 7.0 + sin(z * 0.27 + 1.0) * 2.0


## 세계 위치(m)의 층
func _level_at(x: float, z: float) -> int:
	var n := noise.get_noise_2d(x, z)
	var lvl := 0
	var edge := 17.0 + n * 6.0 + noise.get_noise_2d(x * 3.0, 50) * 1.2
	if z < edge:
		lvl = 4
	if z < edge - 7.0 + noise.get_noise_2d(x * 2.0, 99) * 4.0 and absf(x - 20.0) < 12.0 + n * 3.0:
		lvl = 7
	# 서쪽 작은 둔덕
	var d := Vector2(x - 10, z - 34).length() + n * 4.0
	if d < 6.0:
		lvl = 2
	return lvl


func _mat_at(x: float, z: float) -> int:
	var n := noise.get_noise_2d(x, z)
	var m := G.GRASS
	if noise.get_noise_2d(x * 0.6 + 40, z * 0.6) > 0.18:
		m = G.LUSH
	var edge_d := minf(minf(x, WM - x), minf(z, HM - z))
	if edge_d < 5.0 + n * 2.0 or noise.get_noise_2d(x * 0.5 + 200, z * 0.5) > 0.42:
		m = G.FOREST
	if absf(x - _path_x(z)) < 1.6 + noise.get_noise_2d(x * 3.0, z * 3.0) * 0.8:
		m = G.DIRT
	if Vector2(x - 40, z - 32).length() < 4.6 + n * 1.5:
		m = G.STONE
	return m


func _gen_cells() -> void:
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
	# 흙길이 절벽을 지나는 곳은 계단으로: 남→북으로 갈수록 한 층씩만 오르게 (계단 한 단 = 1m 깊이)
	var step_cells := int(1.0 / C)
	for pass_ in 8:
		for gz in range(gh - 2, -1, -1):
			var px := int(round(_path_x((gz + 0.5) * C) / C))
			for gx in range(px - 4, px + 5):
				if gx < 0 or gx >= gw:
					continue
				var below: int = heights[mini(gz + step_cells, gh - 1)][gx]
				if (heights[gz][gx] as int) > below + 1:
					heights[gz][gx] = below + 1
				# 계단 한 단 안은 같은 높이
				if (heights[gz][gx] as int) > (heights[gz + 1][gx] as int) and gz % step_cells != 0:
					heights[gz][gx] = heights[gz + 1][gx]


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


# ------------------------------------------------------------------ 장식
## 그림 폭에 맞춘 그늘 (해가 왼쪽 위라 오른쪽 아래로 살짝 밀린다)
func _shade_under(p: Vector3, sprite: String, k: float) -> void:
	var sz := BB.px_size(FLORA, sprite) / BB.PPU
	var rx := sz.x * 0.44
	var tall := sz.y > sz.x * 0.9
	var rz := rx * (0.42 if tall else 0.5)
	terrain.blob(p + Vector3(rx * 0.12, 0, rz * 0.25 + (0.25 if tall else 0.05)), rx, rz, k)


func _decorate() -> void:
	# 공터·길 주변은 비워 둔다
	for gz in gh:
		for gx in gw:
			var m: int = mats[gz][gx]
			if m == G.DIRT or m == G.STONE:
				blocked[Vector2i(gx, gz)] = true
	# ---- 나무: 숲 바닥에 빽빽하게, 풀밭엔 드문드문 (덤불·고사리를 둘레에 곁들여 덩어리로)
	var trees := ["tree0", "tree1", "tree2", "tree_light0", "pine0", "pine1"]
	var tree_spots: Array = []
	for i in 2600:
		var c := _rand_cell(2)
		var m: int = mats[c.y][c.x]
		var dense := m == G.FOREST
		if not dense and rng.randf() > 0.05:
			continue
		if not _flat(c, 2) or not _free(c, 5 if dense else 7):
			continue
		var name: String = trees[rng.randi_range(0, 3)] if rng.randf() > 0.22 else trees[rng.randi_range(4, 5)]
		var t := BB.make(FLORA, name, {"sway": 0.06, "receive_shade": false})
		t.position = _spot(c)
		if rng.randf() < 0.5:
			t.scale.x = -1.0
		add_child(t)
		_shade_under(t.position, name, 0.85)
		_mark(c, 2)
		tree_spots.append(c)
		# 나무 밑동 충돌
		var body := StaticBody3D.new()
		var cs := CollisionShape3D.new()
		var cyl := CylinderShape3D.new()
		cyl.radius = 0.35
		cyl.height = 2.0
		cs.shape = cyl
		cs.position.y = 1.0
		body.add_child(cs)
		body.position = t.position
		add_child(body)
	# ---- 나무 둘레: 덤불·고사리·버섯
	for tc in tree_spots:
		for k in rng.randi_range(0, 2):
			var c: Vector2i = tc + Vector2i(rng.randi_range(-6, 6), rng.randi_range(1, 6))
			if not terrain.inside(c.x, c.y) or not _flat(c) or blocked.has(c):
				continue
			var pick := rng.randf()
			var nm := "bush%d" % rng.randi_range(0, 2) if pick < 0.35 else ("fern%d" % rng.randi_range(0, 1) if pick < 0.85 else "mush0")
			_prop(c, nm)
	# ---- 트인 곳 소품: 바위·그루터기·통나무·밝은 덤불 (드문드문)
	var props := [["rock0", 6], ["rock1", 10], ["rock2", 4], ["stump0", 4], ["log0", 3], ["bush_light", 6], ["bush1", 6]]
	for pr in props:
		var placed := 0
		var tries := 0
		while placed < int(pr[1]) and tries < 600:
			tries += 1
			var c := _rand_cell(2)
			if not _flat(c) or not _free(c, 2):
				continue
			_prop(c, pr[0])
			placed += 1
	terrain.commit_shade()
	# ---- 풀 다발·꽃 (멀티메시) — 1m 칸마다 몇 개씩
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
				if rng.randf() < 0.55:
					var nm := "tuft%d" % rng.randi_range(0, 5) if m != G.FOREST else "tuft_dark%d" % rng.randi_range(0, 1)
					var p := Vector3(x + rng.randf(), 0, z + rng.randf())
					if terrain.mat_at(p) in [G.DIRT, G.STONE]:
						continue
					p.y = terrain.height_at(p)
					tufts.append([nm, p, rng.randf() < 0.5])
			# 꽃 무리 (노이즈가 높은 곳에 몰리게)
			if m != G.FOREST and noise.get_noise_2d(x * 1.3 + 500, z * 1.3) > 0.25 and rng.randf() < 0.6:
				var p2 := Vector3(x + rng.randf(), 0, z + rng.randf())
				p2.y = terrain.height_at(p2)
				var fn := "flower%d" % rng.randi_range(0, 2) if rng.randf() > 0.15 else "flower_y"
				flowers.append([fn, p2, rng.randf() < 0.5])
	add_child(BB.multi(FLORA, tufts, {"sway": 0.05, "push": 0.3}))
	add_child(BB.multi(FLORA, flowers, {"sway": 0.03, "push": 0.2}))


func _prop(c: Vector2i, nm: String) -> void:
	var soft := nm.begins_with("bush") or nm.begins_with("fern")
	var casts := not nm.begins_with("fern") and not nm.begins_with("mush")
	var s := BB.make(FLORA, nm, {"sway": 0.04 if soft else 0.0, "push": 0.12 if soft else 0.0, "receive_shade": not casts})
	s.position = _spot(c)
	if rng.randf() < 0.5:
		s.scale.x = -1.0
	add_child(s)
	if casts:
		_shade_under(s.position, nm, 0.75)
	if nm.begins_with("bush") or nm.begins_with("rock") or nm.begins_with("stump") or nm.begins_with("log"):
		_mark(c, 1)
