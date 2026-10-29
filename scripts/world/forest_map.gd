class_name ForestMap
extends Region
## 숲 지역 (캠프 앞 숲길).
##  · 남쪽 낮은 풀밭 → 흙길이 굽이쳐 북쪽 언덕(4층 절벽)으로 계단을 타고 올라간다
##  · 가장자리는 빽빽한 숲, 가운데는 트인 풀밭과 꽃, 돌바닥 공터


## 측면 구간: 절벽 앞 바위 능선 (x 42~62, 구간 선 z 26). 1m 마다 층 — 점프로 오르내리고 가운데 웅덩이 하나
const RIDGE := Rect2(42.0, 24.5, 20.0, 3.0)
const RIDGE_LV := [0, 1, 1, 2, 2, 1, 1, 0, 0, 1, 2, 3, 3, 2, 2, 1, 1, 2, 2, 1]


func _init() -> void:
	WM = 64.0
	HM = 52.0
	spawn = Vector3(29, 0, 49)
	side_zones = [[Rect2(41.6, 23.6, 21.0, 4.8), 26.0]]
	exits = [[Rect2(22.0, 51.2, 14.0, 1.0), "camp", Vector3(20.0, 0, 1.6)]]
	# 적 무리 (적 시스템.md: 역할을 섞는다 — 근접 + 원거리, 빠른 것 + 느린 것)
	groups = [
		{"at": Vector3(24, 0, 38), "kinds": ["acorn", "acorn", "acorn"], "r": 9.0, "done": false},
		{"at": Vector3(40, 0, 33), "kinds": ["acorn", "shroom", "acorn"], "r": 9.0, "done": false},
		{"at": Vector3(14, 0, 44), "kinds": ["spider", "spider"], "r": 8.0, "done": false},
		{"at": Vector3(52, 0, 40), "kinds": ["shroom", "shroom", "acorn", "spider"], "r": 10.0, "done": false},
		{"at": Vector3(50, 0, 26), "kinds": ["acorn", "spider"], "r": 7.0, "done": false},
		{"at": Vector3(20, 0, 28), "kinds": ["spider", "shroom", "acorn", "acorn"], "r": 9.0, "done": false},
		{"at": Vector3(30, 4, 10), "kinds": ["acorn", "shroom", "shroom", "spider", "acorn"], "r": 10.0, "done": false},
	]


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
	if Vector2(x - 40, z - 32).length() < 4.6 + n * 1.5:
		m = G.STONE
	return m


func _post_cells() -> void:
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


func _decorate() -> void:
	# 능선 둘레는 비워 둔다 (측면에서 앞을 가리지 않게)
	for gz in range(int((RIDGE.position.y - 2.0) / C), int((RIDGE.end.y + 2.5) / C)):
		for gx in range(int((RIDGE.position.x - 1.0) / C), mini(gw, int((RIDGE.end.x + 1.0) / C))):
			blocked[Vector2i(gx, gz)] = true
	scatter_trees(2600)
	scatter_props([["rock0", 6], ["rock1", 10], ["rock2", 4], ["stump0", 4], ["log0", 3], ["bush_light", 6], ["bush1", 6]])
	scatter_grass()
