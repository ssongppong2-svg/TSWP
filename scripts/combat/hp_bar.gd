class_name HpBar
extends MeshInstance3D
## 머리 위 체력바: 맞은 뒤 잠깐만 보인다 (보스·플레이어는 HUD 에서)

var f: Fighter
var mat: ShaderMaterial
var ghost := 1.0
var show_t := 0.0


static func attach(fighter: Fighter, width_px := 16) -> HpBar:
	var b := HpBar.new()
	b.f = fighter
	var q := QuadMesh.new()
	q.size = Vector2(width_px / 16.0, 3.0 / 16.0)
	b.mesh = q
	b.mat = ShaderMaterial.new()
	b.mat.shader = load("res://shaders/bar.gdshader")
	b.mat.set_shader_parameter("size_px", Vector2(width_px, 3))
	b.mat.set_shader_parameter("fill", Color("#e0504a") if fighter.team == 1 else Color("#6ad07a"))
	b.material_override = b.mat
	b.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	b.position = Vector3(0, fighter.body_h + 0.25, 0)
	b.visible = false
	fighter.add_child(b)
	fighter.damaged.connect(func(_f, _h, _a): b.show_t = 2.5)
	return b


func _process(dt: float) -> void:
	if f == null or f.dead:
		visible = false
		return
	show_t -= dt
	visible = show_t > 0.0
	var r := f.ratio()
	ghost = move_toward(ghost, r, dt * 0.8) if ghost > r else r
	mat.set_shader_parameter("ratio", r)
	mat.set_shader_parameter("ghost", ghost)
