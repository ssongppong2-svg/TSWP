class_name Terrain
extends Node3D
## 높이 칸 지형.
##  heights[z][x] : 층 (0,1,2 …)  · 1층 = LEVEL_H m
##  mats[z][x]    : 윗면 재질 번호 (DB.GROUND 순서)
##  칸 하나 = CELL m (0.5m = 화면 8픽셀 — 절벽 윤곽이 덜 각져 보이게)
## 윗면은 칸마다 사각형, 낮은 이웃 쪽으로 벽을 세운다. 충돌은 같은 면으로.

const LEVEL_H := 1.0
const CELL := 0.5

var w := 0
var h := 0
var heights: Array = []
var mats: Array = []
var mesh_inst: MeshInstance3D
var body: StaticBody3D
var material: ShaderMaterial
var map_img: Image
const SHADE_PX := 16                 # 그늘 지도: 1m 당 16칸 (도트 1개 = 1칸)
var shade_img: Image                 # 나무·바위 밑 그늘, 절벽 밑 어둠 (구워 둔다 → 도트 경계가 또렷)
var shade_tex: ImageTexture


func build(hts: Array, ms: Array, ground_tex: Texture2D, wall_tex: Texture2D, n_mats: int) -> void:
	heights = hts
	mats = ms
	h = hts.size()
	w = (hts[0] as Array).size()
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for z in h:
		for x in w:
			var y := level(x, z) * LEVEL_H
			_quad_top(st, x * CELL, z * CELL, y)
			# 네 방향 벽
			for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
				var nx: int = x + d.x
				var nz: int = z + d.y
				var ny := (level(nx, nz) if inside(nx, nz) else -2) * LEVEL_H
				if ny < y:
					_wall(st, x * CELL, z * CELL, d, ny, y)
	var mesh := st.commit()
	mesh_inst = MeshInstance3D.new()
	mesh_inst.mesh = mesh
	material = ShaderMaterial.new()
	material.shader = preload("res://shaders/terrain.gdshader")
	material.set_shader_parameter("ground", ground_tex)
	material.set_shader_parameter("wall_tex", wall_tex)
	material.set_shader_parameter("n_mats", float(n_mats))
	material.set_shader_parameter("grid_size", Vector2(w, h))
	material.set_shader_parameter("cell", CELL)
	# 칸 정보 텍스처: R = 재질, G = 높이(층) — 셰이더가 재질 경계 흔들기·절벽 가장자리 테두리에 쓴다
	map_img = Image.create(w, h, false, Image.FORMAT_RG8)
	for z in h:
		for x in w:
			map_img.set_pixel(x, z, Color(float(mats[z][x]) / 255.0, float(level(x, z)) / 255.0, 0))
	material.set_shader_parameter("map", ImageTexture.create_from_image(map_img))
	shade_img = Image.create(int(w * CELL * SHADE_PX), int(h * CELL * SHADE_PX), false, Image.FORMAT_L8)
	_bake_cliff_shade()
	commit_shade()
	mesh_inst.material_override = material
	mesh_inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	add_child(mesh_inst)
	body = StaticBody3D.new()
	body.collision_layer = 1
	var cs := CollisionShape3D.new()
	cs.shape = mesh.create_trimesh_shape()
	body.add_child(cs)
	add_child(body)


## 측면 단면 뚜껑: 구간 선(z = lane, 카메라 쪽 면)을 따라 땅 속을 막는 벽.
## 지형과 같은 재질이라 꼭대기 잔디·바위 무늬가 그대로 이어진다. yaw 0 (세계 z) 구간만.
var cut_mesh: MeshInstance3D


func make_cut(lane: float) -> void:
	clear_cut()
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var gz := clampi(int(floor(lane / CELL)), 0, h - 1)
	var zf := lane - 0.005
	for x in w:
		var top := level(x, gz) * LEVEL_H
		var x0 := x * CELL
		var x1 := x0 + CELL
		var a := Vector3(x0, -12.0, zf)
		var b := Vector3(x1, -12.0, zf)
		var c := Vector3(x1, top, zf)
		var e := Vector3(x0, top, zf)
		st.set_uv2(Vector2(-12.0, top))
		st.set_normal(Vector3(0, 0, 1))
		st.add_vertex(a)
		st.add_vertex(c)
		st.add_vertex(b)
		st.add_vertex(a)
		st.add_vertex(e)
		st.add_vertex(c)
	cut_mesh = MeshInstance3D.new()
	cut_mesh.mesh = st.commit()
	cut_mesh.material_override = material
	cut_mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(cut_mesh)


func clear_cut() -> void:
	if cut_mesh and is_instance_valid(cut_mesh):
		cut_mesh.queue_free()
	cut_mesh = null


## 그늘 한 덩이 (타원). p: 세계 위치(가운데), rx·rz: 반지름(m), k: 진하기 0~1. 가장자리는 부드럽게.
func blob(p: Vector3, rx: float, rz: float, k := 0.8) -> void:
	var cx := p.x * SHADE_PX
	var cz := p.z * SHADE_PX
	var ax := rx * SHADE_PX
	var az := rz * SHADE_PX
	var x0 := maxi(0, int(cx - ax - 1))
	var x1 := mini(shade_img.get_width() - 1, int(cx + ax + 1))
	var z0 := maxi(0, int(cz - az - 1))
	var z1 := mini(shade_img.get_height() - 1, int(cz + az + 1))
	for z in range(z0, z1 + 1):
		for x in range(x0, x1 + 1):
			var dx := (x + 0.5 - cx) / ax
			var dz := (z + 0.5 - cz) / az
			var d := sqrt(dx * dx + dz * dz)
			if d >= 1.0:
				continue
			var v := k * clampf((1.0 - d) / 0.35, 0.0, 1.0)
			var old := shade_img.get_pixel(x, z).r
			if v > old:
				shade_img.set_pixel(x, z, Color(v, v, v))


## 절벽 밑(낮은 쪽) 바닥에 띠 모양 어둠 — 높이차가 클수록 넓게
func _bake_cliff_shade() -> void:
	for z in h:
		for x in w:
			var l := level(x, z)
			for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
				var nx: int = x + d.x
				var nz: int = z + d.y
				if not inside(nx, nz):
					continue
				var dl := level(nx, nz) - l
				if dl <= 0:
					continue
				# 이 칸(낮은 쪽)의 그 방향 가장자리를 따라 어둡게
				var cp := int(CELL * SHADE_PX)            # 칸 한 변의 그늘 칸 수
				# 절벽이 높을수록 넓고 진한 접지 그늘 (절벽 밑이 바닥에 붙어 보이게)
				var width := mini(cp, int(clampf(0.22 + dl * 0.1, 0.25, 0.5) * SHADE_PX))
				for i in cp:
					for j in width:
						var u: float = float(j) / float(width)
						var v := (1.0 - u * u) * 0.9
						var px: int
						var pz: int
						match d:
							Vector2i(1, 0):
								px = (x + 1) * cp - 1 - j
								pz = z * cp + i
							Vector2i(-1, 0):
								px = x * cp + j
								pz = z * cp + i
							Vector2i(0, 1):
								px = x * cp + i
								pz = (z + 1) * cp - 1 - j
							_:
								px = x * cp + i
								pz = z * cp + j
						if shade_img.get_pixel(px, pz).r < v:
							shade_img.set_pixel(px, pz, Color(v, v, v))


func commit_shade() -> void:
	if shade_tex == null:
		shade_tex = ImageTexture.create_from_image(shade_img)
	else:
		shade_tex.update(shade_img)
	material.set_shader_parameter("shade_map", shade_tex)
	RenderingServer.global_shader_parameter_set("shade_map", shade_tex)
	RenderingServer.global_shader_parameter_set("shade_area", Vector4(global_position.x, global_position.z, w * CELL, h * CELL))


func inside(x: int, z: int) -> bool:
	return x >= 0 and z >= 0 and x < w and z < h


func level(x: int, z: int) -> int:
	if not inside(x, z):
		return 0
	return int(heights[z][x])


## 세계 좌표 → 칸
func cell_of(p: Vector3) -> Vector2i:
	return Vector2i(int(floor(p.x / CELL)), int(floor(p.z / CELL)))


## 칸 → 세계 좌표 (칸 가운데, 높이 포함)
func center_of(c: Vector2i) -> Vector3:
	return Vector3((c.x + 0.5) * CELL, level(c.x, c.y) * LEVEL_H, (c.y + 0.5) * CELL)


## 세계 좌표의 땅 높이
func height_at(p: Vector3) -> float:
	var c := cell_of(p)
	return level(c.x, c.y) * LEVEL_H


func mat_at(p: Vector3) -> int:
	var c := cell_of(p)
	return int(mats[c.y][c.x]) if inside(c.x, c.y) else 0


func _quad_top(st: SurfaceTool, x: float, z: float, y: float) -> void:
	var a := Vector3(x, y, z)
	var b := Vector3(x + CELL, y, z)
	var c := Vector3(x + CELL, y, z + CELL)
	var d := Vector3(x, y, z + CELL)
	st.set_uv2(Vector2(y, y))
	st.set_normal(Vector3.UP)
	st.add_vertex(a)
	st.add_vertex(b)
	st.add_vertex(c)
	st.add_vertex(a)
	st.add_vertex(c)
	st.add_vertex(d)


func _wall(st: SurfaceTool, x: float, z: float, d: Vector2i, y0: float, y1: float) -> void:
	# 벽면 네 꼭짓점 (바깥을 향하게)
	var p0: Vector3
	var p1: Vector3
	var k := CELL
	match d:
		Vector2i(1, 0):
			p0 = Vector3(x + k, 0, z + k)
			p1 = Vector3(x + k, 0, z)
		Vector2i(-1, 0):
			p0 = Vector3(x, 0, z)
			p1 = Vector3(x, 0, z + k)
		Vector2i(0, 1):
			p0 = Vector3(x, 0, z + k)
			p1 = Vector3(x + k, 0, z + k)
		_:
			p0 = Vector3(x + k, 0, z)
			p1 = Vector3(x, 0, z)
	var a := p0 + Vector3(0, y0, 0)
	var b := p1 + Vector3(0, y0, 0)
	var c := p1 + Vector3(0, y1, 0)
	var e := p0 + Vector3(0, y1, 0)
	st.set_uv2(Vector2(y0, y1))
	st.set_normal(Vector3(d.x, 0, d.y))
	st.add_vertex(a)
	st.add_vertex(c)
	st.add_vertex(b)
	st.add_vertex(a)
	st.add_vertex(e)
	st.add_vertex(c)
