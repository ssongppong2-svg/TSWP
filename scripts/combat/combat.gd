class_name Combat
extends RefCounted
## 범위 판정 도우미 (원 · 부채꼴) — 싸우는 것이 100명 안팎이라 거리 계산으로 충분하다

## 원 안의 싸우는 것들 (높이는 ±h)
static func in_radius(tree: SceneTree, c: Vector3, r: float, h := 2.0) -> Array:
	var out: Array = []
	for f in tree.get_nodes_in_group("fighters"):
		var fi := f as Fighter
		if fi == null or fi.dead:
			continue
		var d := fi.global_position - c
		if absf(d.y + fi.body_h * 0.5) > h + fi.body_h:
			continue
		d.y = 0.0
		if d.length() <= r + fi.body_r:
			out.append(fi)
	return out


## 부채꼴: c 에서 dir 방향 반각 half(도) 안
static func in_fan(tree: SceneTree, c: Vector3, dir: Vector3, r: float, half: float, h := 1.6) -> Array:
	var out: Array = []
	var cosh := cos(deg_to_rad(half))
	for fi in in_radius(tree, c, r, h):
		var d: Vector3 = (fi as Fighter).global_position - c
		d.y = 0.0
		if d.length() < 0.45 or d.normalized().dot(dir) >= cosh:
			out.append(fi)
	return out


## 맞은 방향으로 미는 힘
static func push(from: Vector3, to: Vector3, force: float, up := 0.3) -> Vector3:
	var d := to - from
	d.y = 0.0
	if d.length() < 0.01:
		d = Vector3(randf_range(-1, 1), 0, randf_range(-1, 1))
	return d.normalized() * force + Vector3(0, force * up, 0)


## 잠깐 멈춤 (타격감) — 실제 시간 기준
static func hitstop(tree: SceneTree, t := 0.05) -> void:
	if Engine.time_scale < 0.99:
		return
	Engine.time_scale = 0.05
	await tree.create_timer(t, true, false, true).timeout
	Engine.time_scale = 1.0
