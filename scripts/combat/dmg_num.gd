class_name DmgNum
extends Label3D
## 피해·회복 숫자 (도트 글꼴, 위로 튀었다 사라짐). 설정에서 끌 수 있다 (Prefs.damage_numbers)

const COLORS := {"enemy": Color("#fff4d6"), "player": Color("#ff6a5a"), "ally": Color("#ffb04a"), "heal": Color("#7af08a"), "crit": Color("#ffe14a")}

var vel := Vector3.ZERO
var t := 0.0


static func pop(at: Fighter, amount: float, kind := "enemy") -> void:
	if not Prefs.damage_numbers or at == null or not at.is_inside_tree():
		return
	var n := DmgNum.new()
	n.text = ("+%d" if kind == "heal" else "%d") % maxi(1, int(round(amount)))
	n.font = PixelFont.get_font("small")
	n.font_size = PixelFont.size_of("small")
	n.pixel_size = 1.0 / 16.0
	n.modulate = COLORS.get(kind, Color.WHITE)
	n.outline_modulate = Color(0.1, 0.08, 0.12)
	n.outline_size = 2
	n.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	n.no_depth_test = true
	n.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	n.render_priority = 10
	n.outline_render_priority = 9
	n.double_sided = true
	at.get_parent().add_child(n)
	n.global_position = at.global_position + Vector3(randf_range(-0.25, 0.25), at.body_h + 0.3, 0)
	n.vel = Vector3(randf_range(-0.6, 0.6), 3.2, 0)


func _process(dt: float) -> void:
	t += dt
	vel.y -= 7.0 * dt
	position += vel * dt
	if t > 0.5:
		modulate.a = clampf(1.0 - (t - 0.5) / 0.3, 0.0, 1.0)
		outline_modulate.a = modulate.a
	if t > 0.8:
		queue_free()
