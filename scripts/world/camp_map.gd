class_name CampMap
extends Region
## 캠프: 모험 떠나기 전 모이는 곳 (해 질 녘).
##  · 가운데 돌바닥 광장에 모닥불 · 둘레에 통나무 의자
##  · 천막 넷 · 등불 기둥 · 짐수레 · 상자/통 · 장작 · 냄비
##  · 북쪽으로 흙길이 숲으로 이어지고 입구에 깃발과 표지판
##  · 둘레는 빽빽한 숲 + 동쪽은 한 단 높은 둔덕

const FIRE := Vector2(20.0, 16.0)


func _init() -> void:
	WM = 40.0
	HM = 32.0
	spawn = Vector3(20.0, 0, 19.5)
	env_preset = "camp"
	exits = [[Rect2(16.0, -0.6, 8.0, 1.0), "forest", Vector3(29.0, 0, 49.0)]]
	music = "camp"
	ambience = "camp"


func _level_at(x: float, z: float) -> int:
	var n := noise.get_noise_2d(x, z)
	# 동쪽 둔덕 (2층) — 광장 쪽은 깎인 절벽
	if x > 32.0 + n * 3.0 and z > 6.0 and z < 26.0 + n * 3.0:
		return 2
	return 0


func _mat_at(x: float, z: float) -> int:
	var n := noise.get_noise_2d(x, z)
	var m := G.GRASS
	if noise.get_noise_2d(x * 0.6 + 40, z * 0.6) > 0.2:
		m = G.LUSH
	var edge_d := minf(minf(x, WM - x), minf(z, HM - z))
	if edge_d < 4.0 + n * 2.5:
		m = G.FOREST
	var d := Vector2(x, z).distance_to(FIRE)
	if d < 6.2 + n * 1.2:
		m = G.STONE
	elif d < 7.6 + n * 1.5:
		m = G.DIRT
	return m


func _post_cells() -> void:
	# 북쪽 숲으로 가는 길 · 천막 앞 오솔길
	paint_path([Vector2(20, 9), Vector2(19.2, 5), Vector2(20.5, 2), Vector2(20, -1)], 2.6)
	paint_path([Vector2(14, 16), Vector2(9, 18), Vector2(6, 21)], 1.4)
	paint_path([Vector2(25, 21), Vector2(28, 24)], 1.6)


func _decorate() -> void:
	var fx := FIRE.x
	var fz := FIRE.y
	# ---- 모닥불 + 불빛
	camp_prop(fx, fz, "fire0", {"frames": ["fire0", "fire1", "fire2", "fire3"], "fps": 9.0, "emission": 0.9, "shade": 0.0, "solid": 0.5})
	warm_light(Vector3(fx, 1.0, fz + 0.2), 3.2, 9.0, Color("#ffa850"), 0.14)
	camp_prop(fx + 1.7, fz - 0.9, "pot", {"shade": 0.5})
	# ---- 통나무 의자 (불 둘레)
	camp_prop(fx, fz - 2.5, "bench", {"box": Vector3(1.8, 0.6, 0.5)})
	camp_prop(fx - 0.2, fz + 2.6, "bench", {"box": Vector3(1.8, 0.6, 0.5), "flip": true})
	camp_prop(fx - 3.1, fz + 0.2, "bench", {"box": Vector3(1.8, 0.6, 0.5)})
	camp_prop(fx + 3.3, fz + 0.4, "bench", {"box": Vector3(1.8, 0.6, 0.5), "flip": true})
	# ---- 천막
	camp_prop(11.5, 11.0, "tent_red", {"box": Vector3(3.2, 2.0, 1.4), "shade": 0.85, "block": 6})
	camp_prop(28.0, 10.0, "tent_cream", {"box": Vector3(3.2, 2.0, 1.4), "shade": 0.85, "block": 6})
	camp_prop(9.0, 20.5, "tent_blue", {"box": Vector3(2.8, 2.0, 1.4), "shade": 0.85, "block": 6, "flip": true})
	camp_prop(29.0, 28.5, "tent_red", {"box": Vector3(3.2, 2.0, 1.4), "shade": 0.85, "block": 6, "flip": true})
	# ---- 짐: 수레·상자·통·장작
	camp_prop(26.5, 24.5, "cart", {"box": Vector3(2.6, 1.4, 1.0), "shade": 0.8, "block": 4})
	camp_prop(24.4, 23.2, "crate", {"box": Vector3(1.0, 1.0, 0.8)})
	camp_prop(23.6, 24.4, "crate", {"box": Vector3(1.0, 1.0, 0.8)})
	camp_prop(29.6, 22.6, "barrel", {"solid": 0.45})
	camp_prop(30.4, 23.6, "barrel", {"solid": 0.45})
	camp_prop(13.2, 23.0, "woodpile", {"box": Vector3(1.8, 1.0, 0.8)})
	camp_prop(7.5, 12.5, "crate", {"box": Vector3(1.0, 1.0, 0.8)})
	camp_prop(15.0, 9.5, "barrel", {"solid": 0.45})
	# ---- 등불 기둥 (불빛)
	for lp in [Vector2(16.5, 10.0), Vector2(24.0, 9.8), Vector2(14.6, 21.6), Vector2(26.2, 19.6)]:
		camp_prop(lp.x, lp.y, "lantern0", {"solid": 0.2, "emission": 0.6, "shade": 0.4})
		warm_light(Vector3(lp.x + 0.2, 2.0, lp.y + 0.1), 1.5, 5.0, Color("#ffc070"), 0.05)
	# ---- 입구: 깃발 둘 + 표지판
	camp_prop(17.8, 6.2, "flag0", {"frames": ["flag0", "flag1", "flag2", "flag3"], "fps": 6.0, "solid": 0.15, "shade": 0.4})
	camp_prop(22.4, 6.0, "flag0", {"frames": ["flag0", "flag1", "flag2", "flag3"], "fps": 6.0, "solid": 0.15, "shade": 0.4, "flip": true})
	camp_prop(23.6, 4.4, "sign", {"solid": 0.2, "shade": 0.5})
	# ---- 숲·풀
	scatter_trees(1600, 0.02)
	scatter_props([["rock1", 5], ["stump0", 3], ["bush_light", 4], ["bush1", 3], ["log0", 2]])
	scatter_grass(0.9, 0.2)
