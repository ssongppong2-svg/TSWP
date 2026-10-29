class_name Fx
extends Node3D
## 잠깐 나왔다 사라지는 효과 (베기 궤적 · 흙먼지 · 맞음 불꽃).
##  · flat: 땅에 눕혀 각도만큼 돌린 판 (베기처럼 방향이 있는 것)
##  · 아니면 빌보드 (먼지·불꽃)
##  · 마지막 프레임이 지나면 스스로 사라진다

const ATLAS := "res://assets/sprites/fx.png"
const PPU := 16.0

var mesh: MeshInstance3D
var mat: Material
var regions: Array[Vector4] = []
var fps := 16.0
var t := 0.0
var flat := false
var vel := Vector3.ZERO
static var _flat_shader: Shader


## 땅에 눕힌 효과. angle: 세계 y축 회전(라디안, 0 = +x 방향을 향함)
static func flat_at(parent: Node, pos: Vector3, angle: float, frames: Array, fps_: float = 16.0) -> Fx:
	var f := Fx.new()
	f.flat = true
	f.fps = fps_
	for n in frames:
		f.regions.append(BB.region_uv(ATLAS, n))
	var sz := BB.px_size(ATLAS, frames[0]) / PPU
	var q := QuadMesh.new()
	q.size = sz
	q.orientation = PlaneMesh.FACE_Y
	f.mesh = MeshInstance3D.new()
	f.mesh.mesh = q
	if _flat_shader == null:
		_flat_shader = load("res://shaders/fx_flat.gdshader")
	var m := ShaderMaterial.new()
	m.shader = _flat_shader
	m.set_shader_parameter("tex", BB.atlas(ATLAS)["tex"])
	m.set_shader_parameter("region", f.regions[0])
	f.mat = m
	f.mesh.material_override = m
	f.mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	f.add_child(f.mesh)
	parent.add_child(f)
	f.global_position = pos + Vector3(0, 0.05, 0)
	f.rotation.y = -angle
	return f


## 서 있는 효과 (빌보드)
static func pop(parent: Node, pos: Vector3, frames: Array, fps_: float = 14.0, velocity := Vector3.ZERO) -> Fx:
	var f := Fx.new()
	f.fps = fps_
	f.vel = velocity
	for n in frames:
		f.regions.append(BB.region_uv(ATLAS, n))
	f.mesh = BB.make(ATLAS, frames[0], {"unique": true, "up_normal": 1.0, "receive_shade": false})
	f.mat = f.mesh.material_override
	f.add_child(f.mesh)
	parent.add_child(f)
	f.global_position = pos
	return f


func _process(dt: float) -> void:
	t += dt
	var i := int(t * fps)
	if i >= regions.size():
		queue_free()
		return
	(mat as ShaderMaterial).set_shader_parameter("region", regions[i])
	position += vel * dt
