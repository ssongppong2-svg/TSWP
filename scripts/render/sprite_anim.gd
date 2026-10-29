class_name SpriteAnim
extends Node
## 빌보드 그림 돌리기 (모닥불·깃발처럼 계속 도는 것). 시작 프레임은 자리마다 달라서 다 같이 움직이지 않는다.

var mat: ShaderMaterial
var regions: Array[Vector4] = []
var fps := 8.0
var t := 0.0


func setup(mesh: MeshInstance3D, atlas: String, frames: Array, fps_: float) -> void:
	mat = mesh.material_override
	fps = fps_
	for f in frames:
		regions.append(BB.region_uv(atlas, f))
	t = randf() * 10.0


func _process(dt: float) -> void:
	if regions.is_empty():
		return
	t += dt
	mat.set_shader_parameter("region", regions[int(t * fps) % regions.size()])
