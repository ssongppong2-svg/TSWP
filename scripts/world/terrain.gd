class_name Terrain
extends Node3D
## 높이 칸 지형.
##  heights[z][x] : 층 (0,1,2 …)  · 1층 = LEVEL_H m
##  mats[z][x]    : 윗면 재질 번호 (DB.GROUND 순서)
## 윗면은 칸마다 사각형, 낮은 이웃 쪽으로 벽을 세운다. 충돌은 같은 면으로.

const LEVEL_H := 1.0

var w := 0
var h := 0
var heights: Array = []
var mats: Array = []
var mesh_inst: MeshInstance3D
var body: StaticBody3D
var material: ShaderMaterial
var map_img: Image


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
			_quad_top(st, x, z, y)
			# 네 방향 벽
			for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
				var nx: int = x + d.x
				var nz: int = z + d.y
				var ny := (level(nx, nz) if inside(nx, nz) else -2) * LEVEL_H
				if ny < y:
					_wall(st, x, z, d, ny, y)
	st.generate_normals()
	var mesh := st.commit()
	mesh_inst = MeshInstance3D.new()
	mesh_inst.mesh = mesh
	material = ShaderMaterial.new()
	material.shader = preload("res://shaders/terrain.gdshader")
	material.set_shader_parameter("ground", ground_tex)
	material.set_shader_parameter("wall_tex", wall_tex)
	material.set_shader_parameter("n_mats", float(n_mats))
	material.set_shader_parameter("map_size", Vector2(w, h))
	map_img = Image.create(w, h, false, Image.FORMAT_R8)
	for z in h:
		for x in w:
			map_img.set_pixel(x, z, Color(float(mats[z][x]) / 255.0, 0, 0))
	material.set_shader_parameter("map", ImageTexture.create_from_image(map_img))
	mesh_inst.material_override = material
	mesh_inst.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	add_child(mesh_inst)
	body = StaticBody3D.new()
	body.collision_layer = 1
	var cs := CollisionShape3D.new()
	cs.shape = mesh.create_trimesh_shape()
	body.add_child(cs)
	add_child(body)


func inside(x: int, z: int) -> bool:
	return x >= 0 and z >= 0 and x < w and z < h


func level(x: int, z: int) -> int:
	if not inside(x, z):
		return 0
	return int(heights[z][x])


## 세계 좌표의 땅 높이
func height_at(p: Vector3) -> float:
	return level(int(floor(p.x)), int(floor(p.z))) * LEVEL_H


func mat_at(p: Vector3) -> int:
	var x := int(floor(p.x))
	var z := int(floor(p.z))
	return int(mats[z][x]) if inside(x, z) else 0


func _quad_top(st: SurfaceTool, x: int, z: int, y: float) -> void:
	var a := Vector3(x, y, z)
	var b := Vector3(x + 1, y, z)
	var c := Vector3(x + 1, y, z + 1)
	var d := Vector3(x, y, z + 1)
	st.set_uv2(Vector2(y, y))
	st.add_vertex(a)
	st.add_vertex(b)
	st.add_vertex(c)
	st.add_vertex(a)
	st.add_vertex(c)
	st.add_vertex(d)


func _wall(st: SurfaceTool, x: int, z: int, d: Vector2i, y0: float, y1: float) -> void:
	# 벽면 네 꼭짓점 (바깥을 향하게)
	var p0: Vector3
	var p1: Vector3
	match d:
		Vector2i(1, 0):
			p0 = Vector3(x + 1, 0, z + 1)
			p1 = Vector3(x + 1, 0, z)
		Vector2i(-1, 0):
			p0 = Vector3(x, 0, z)
			p1 = Vector3(x, 0, z + 1)
		Vector2i(0, 1):
			p0 = Vector3(x, 0, z + 1)
			p1 = Vector3(x + 1, 0, z + 1)
		_:
			p0 = Vector3(x + 1, 0, z)
			p1 = Vector3(x, 0, z)
	var a := p0 + Vector3(0, y0, 0)
	var b := p1 + Vector3(0, y0, 0)
	var c := p1 + Vector3(0, y1, 0)
	var e := p0 + Vector3(0, y1, 0)
	st.set_uv2(Vector2(y0, y1))
	st.add_vertex(a)
	st.add_vertex(c)
	st.add_vertex(b)
	st.add_vertex(a)
	st.add_vertex(e)
	st.add_vertex(c)
