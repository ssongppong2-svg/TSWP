class_name DmgNum
extends Label3D
## 피해·회복 숫자 (도트 글꼴, 위로 튀었다 사라짐). 설정에서 끌 수 있다 (Prefs.damage_numbers)

const COLORS := {"enemy": Color("#fff4d6"), "player": Color("#ff6a5a"), "ally": Color("#ffb04a"), "heal": Color("#7af08a"), "crit": Color("#ffe14a")}

var vel := Vector3.ZERO
var t := 0.0
static var _recent := {}           # 대상 → [마지막 시각, 연속 개수] — 몰아치는 숫자가 겹치지 않게 엇갈려 쌓는다


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
	if kind == "crit":
		n.text += "!"
	if (amount >= 40.0 and kind != "heal") or kind == "crit":
		n.font = PixelFont.get_font("big")
		n.font_size = PixelFont.size_of("big")
		n.outline_size = 3
	at.get_parent().add_child(n)
	var now := Time.get_ticks_msec() / 1000.0
	var id := at.get_instance_id()
	var rc: Array = _recent.get(id, [-10.0, 0])
	var k := int(rc[1]) + 1 if now - float(rc[0]) < 0.35 else 0
	_recent[id] = [now, k]
	if _recent.size() > 64:
		_recent.clear()
	var side := -1.0 if k % 2 == 1 else 1.0
	n.global_position = at.global_position + Vector3(side * (0.15 + 0.22 * mini(k, 3)) * (1.0 if k > 0 else randf_range(-1.0, 1.0)), at.body_h + 0.3 + 0.28 * mini(k, 4), 0)
	n.vel = Vector3(side * randf_range(0.2, 0.7), 3.2, 0)


func _process(dt: float) -> void:
	t += dt
	vel.y -= 7.0 * dt
	position += vel * dt
	if t > 0.5:
		modulate.a = clampf(1.0 - (t - 0.5) / 0.3, 0.0, 1.0)
		outline_modulate.a = modulate.a
	if t > 0.8:
		queue_free()
