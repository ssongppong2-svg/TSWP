class_name Firefly
extends Node3D
## 반딧불: 작은 실제 광원이 천천히 떠다니며 숨 쉬듯 깜빡인다 (주변 풀·나무가 노멀맵으로 반응)

var home := Vector3.ZERO
var ph := 0.0
var spd := 1.0
var light: OmniLight3D
var dot: MeshInstance3D
var col := Color("#d8ff80")


func _ready() -> void:
	ph = randf() * TAU
	spd = randf_range(0.5, 1.1)
	home = position
	light = OmniLight3D.new()
	light.light_color = col
	light.light_energy = 0.6
	light.omni_range = 2.0
	light.omni_attenuation = 1.6
	light.shadow_enabled = false
	add_child(light)
	dot = BB.make(Fx.ATLAS, "spark0", {"unique": true, "center": true, "emission": 2.0, "receive_shade": false, "up_normal": 1.0})
	(dot.material_override as ShaderMaterial).set_shader_parameter("tint", col)
	add_child(dot)


func _process(dt: float) -> void:
	ph += dt * spd
	position = home + Vector3(sin(ph * 0.7) * 1.6 + sin(ph * 1.9) * 0.4, 0.9 + sin(ph * 1.3) * 0.35, cos(ph * 0.53) * 1.2)
	var pulse := 0.5 + 0.5 * sin(ph * 3.1)
	light.light_energy = 0.15 + pulse * 0.55
	dot.visible = pulse > 0.25
