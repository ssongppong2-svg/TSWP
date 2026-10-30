class_name ForestMap
extends Region
## 숲 지역 (캠프 앞 숲길).
##  · 남쪽 낮은 풀밭 → 흙길이 굽이쳐 북쪽 언덕(4층 절벽)으로 돌계단을 타고 올라간다
##  · 기억할 자리(랜드마크): 서쪽 둔덕의 고대 나무 · 동쪽 돌 유적 공터(아치·기둥·수호상) · 남동쪽 연못 · 갈림길 표지판
##  · 길가 돌무더기가 진행 방향을 안내하고, 적 무리는 둘레가 트인 빈터에서 나온다


## 측면 구간: 절벽 앞 바위 능선 (x 42~62, 구간 선 z 26). 1m 마다 층 — 점프로 오르내리고 가운데 웅덩이 하나
const RIDGE := Rect2(42.0, 24.5, 20.0, 3.0)
const RIDGE_LV := [0, 1, 1, 2, 2, 1, 1, 0, 0, 1, 2, 3, 3, 2, 2, 1, 1, 2, 2, 1]


func _init() -> void:
	WM = 64.0
	HM = 52.0
	spawn = Vector3(29, 0, 49)
	side_zones = [[Rect2(41.6, 23.6, 21.0, 4.8), 26.0]]
	exits = [[Rect2(22.0, 51.2, 14.0, 1.0), "camp", Vector3(20.0, 0, 1.6)],
		[Rect2(29.0, -0.6, 10.0, 1.0), "nest", Vector3(18.0, 0, 30.5)]]
	# 적 무리 (적 시스템.md: 역할을 섞는다 — 근접 + 원거리, 빠른 것 + 느린 것)
	groups = [
		{"at": Vector3(24, 0, 38), "kinds": ["acorn", "acorn", "acorn"], "r": 9.0, "done": false},
		{"at": Vector3(40, 0, 33), "kinds": ["acorn", "shroom", "acorn"], "r": 9.0, "done": false},
		{"at": Vector3(14, 0, 45), "kinds": ["spider", "spider"], "r": 8.0, "done": false},
		{"at": Vector3(46, 0, 41), "kinds": ["shroom", "shroom", "acorn", "spider"], "r": 10.0, "done": false},
		{"at": Vector3(50, 0, 26), "kinds": ["acorn", "spider"], "r": 7.0, "done": false},
		{"at": Vector3(20, 0, 28), "kinds": ["spider", "shroom", "acorn", "acorn"], "r": 9.0, "done": false},
		{"at": Vector3(30, 4, 10), "kinds": ["acorn", "shroom", "shroom", "spider", "acorn"], "r": 10.0, "done": false},
	]


const RUINS := Vector2(40.0, 32.0)
const POND := Vector2(54.0, 45.0)
const GIANT := Vector2(10.0, 34.0)
var pond: PondSim


func _path_x(z: float) -> float:
	return 32.0 + sin(z * 0.11) * 7.0 + sin(z * 0.27 + 1.0) * 2.0


## 세계 위치(m)의 층
func _level_at(x: float, z: float) -> int:
	if RIDGE.has_point(Vector2(x, z)):
		return RIDGE_LV[clampi(int(x - RIDGE.position.x), 0, RIDGE_LV.size() - 1)]
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
	if Vector2(x - RUINS.x, z - RUINS.y).length() < 4.6 + n * 1.5:
		m = G.STONE
	# 연못 둘레 모래
	var pd := Vector2((x - POND.x) / 1.35, z - POND.y).length()
	if pd < 3.4 + n * 0.6:
		m = G.SAND
	# 적 무리 빈터: 둘레를 맨풀밭으로 (숲 바닥 칠 하지 않음)
	for g in groups:
		var ga: Vector3 = g["at"]
		if Vector2(x - ga.x, z - ga.z).length() < 4.5 and m == G.FOREST:
			m = G.GRASS
	return m


func _post_cells() -> void:
	# 흙길이 절벽을 지나는 곳은 계단으로: 남→북으로 갈수록 한 층씩만 오르게 (계단 한 단 = 1m 깊이)
	var step_cells := int(1.0 / C)
	var orig: Array = []
	for row in heights:
		orig.append((row as Array).duplicate())
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
	# 깎아 낸 칸 = 돌계단 (흙길과 다른 재질로 — 오를 수 있는 길이 한눈에 보이게)
	for gz in gh:
		for gx in gw:
			if int(heights[gz][gx]) != int(orig[gz][gx]):
				mats[gz][gx] = G.STONE


func _decorate() -> void:
	# 능선 둘레는 비워 둔다 (측면에서 앞을 가리지 않게)
	for gz in range(int((RIDGE.position.y - 2.0) / C), int((RIDGE.end.y + 2.5) / C)):
		for gx in range(int((RIDGE.position.x - 1.0) / C), mini(gw, int((RIDGE.end.x + 1.0) / C))):
			blocked[Vector2i(gx, gz)] = true
	_landmarks()
	# 적 무리 빈터: 나무·소품을 두지 않아 싸울 자리(회피 공간)가 트여 보이게
	for g in groups:
		var ga: Vector3 = g["at"]
		_clear(Vector2(ga.x, ga.z), 5.0)
	scatter_trees(2600)
	scatter_props([["rock0", 4], ["rock1", 6], ["rock2", 3], ["stump0", 3], ["log0", 2], ["bush_light", 4], ["bush1", 5]])
	scatter_grass()
	# 광원: 짙은 숲 바닥의 빛나는 버섯 · 연못가와 유적의 반딧불
	var placed := 0
	for i in 400:
		if placed >= 14:
			break
		var c := _rand_cell(4)
		if int(mats[c.y][c.x]) == G.FOREST and _flat(c) and not blocked.has(c):
			glow_patch(_spot(c), 0 if rng.randf() < 0.7 else 1)
			_mark(c, 3)
			placed += 1
	fireflies(8, Rect2(POND.x - 6, POND.y - 5, 12, 9))
	fireflies(6, Rect2(RUINS.x - 6, RUINS.y - 5, 12, 10))
	fireflies(6, Rect2(GIANT.x - 5, GIANT.y - 5, 10, 10))


## 자리를 비워 둔다 (나무·소품 안 놓음)
func _clear(at: Vector2, r: float) -> void:
	for gz in range(int((at.y - r) / C), int((at.y + r) / C) + 1):
		for gx in range(int((at.x - r) / C), int((at.x + r) / C) + 1):
			if Vector2((gx + 0.5) * C - at.x, (gz + 0.5) * C - at.y).length() < r:
				blocked[Vector2i(gx, gz)] = true


func _landmarks() -> void:
	# ---- 고대 나무 (서쪽 둔덕 꼭대기 — 멀리서도 보이는 기준점)
	var gp := Vector3(GIANT.x, 0, GIANT.y)
	gp.y = terrain.height_at(gp)
	var gt := tree(gp, "giant_tree", false, LANDMARKS)
	gt.material_override.set_shader_parameter("sway", 0.03)
	_clear(GIANT, 4.0)
	# ---- 돌 유적 공터 (동쪽 돌바닥): 무너진 아치 · 부서진 기둥 · 수호상
	# 아치: 가운데는 지나갈 수 있고 두 기둥만 막힌다
	var ap := Vector3(RUINS.x, 0, RUINS.y - 4.4)
	land(ap, "arch", false)
	shade_under(LANDMARKS, ap, "arch", 0.7)
	solid_box(Vector3(ap.x - 1.45, terrain.height_at(ap), ap.z), Vector3(0.8, 3.0, 0.6))
	solid_box(Vector3(ap.x + 1.45, terrain.height_at(ap), ap.z), Vector3(0.8, 3.0, 0.6))
	land(Vector3(RUINS.x - 5.0, 0, RUINS.y - 1.0), "pillar0", true)
	land(Vector3(RUINS.x + 5.2, 0, RUINS.y + 1.2), "pillar1", true)
	land(Vector3(RUINS.x + 0.2, 0, RUINS.y + 0.6), "statue", true)
	_clear(RUINS, 6.5)
	# ---- 연못 (남동쪽): 물 + 갈대
	var q := QuadMesh.new()
	q.size = Vector2(7.6, 5.2)
	q.orientation = PlaneMesh.FACE_Y
	var w := MeshInstance3D.new()
	w.mesh = q
	var wm := ShaderMaterial.new()
	wm.shader = load("res://shaders/water.gdshader")
	wm.set_shader_parameter("size_px", Vector2(7.6, 5.2) * 16.0)
	w.material_override = wm
	w.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	w.position = Vector3(POND.x, terrain.height_at(Vector3(POND.x, 0, POND.y)) + 0.03, POND.y)
	add_child(w)
	solid_box(w.position - Vector3(0, 0.03, 0), Vector3(6.4, 1.0, 4.0))
	for k in 7:
		var a := k * TAU / 7.0 + 0.3
		land(Vector3(POND.x + cos(a) * 4.2, 0, POND.y + sin(a) * 2.9), "reeds%d" % (k % 2), false)
	_clear(POND, 5.0)
	Audio.loop_at(w, "water_loop")
	if Prefs.quality >= 1:
		pond = PondSim.new()
		add_child(pond)
		pond.setup(wm, Rect2(POND.x - 3.8, POND.y - 2.6, 7.6, 5.2))
		wm.set_shader_parameter("has_ripple", true)
	# ---- 갈림길 표지판 (공터로 가는 길과 북쪽 계단 사이)
	var sz := 30.0
	land(Vector3(_path_x(sz) - 2.2, 0, sz), "sign2", true)
	# ---- 길가 돌무더기: 6m 마다 길 양옆 번갈아 (어디로 가야 하는지)
	var side := 1.0
	var z := 47.0
	while z > 2.0:
		var p := Vector3(_path_x(z) + side * 2.3, 0, z)
		var c := terrain.cell_of(p)
		if terrain.inside(c.x, c.y) and _flat(c) and not int(mats[c.y][c.x]) in [G.DIRT, G.STONE]:
			land(p, "cairn%d" % (int(z) % 3), false)
		side = -side
		z -= 9.0
	# ---- 북쪽 출구(둥지 쪽) 양옆 부서진 기둥 — 길 끝이 어딘지 보이게
	land(Vector3(_path_x(1.5) - 2.6, 0, 2.0), "pillar0", true)
	land(Vector3(_path_x(1.5) + 2.6, 0, 2.0), "pillar1", true)
