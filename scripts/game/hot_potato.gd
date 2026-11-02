class_name HotPotato
extends Node3D
## 뜨거운 감자: 머리 위에 붙은 폭탄 (9초). 다른 사람·적에게 부딪히면 넘어간다 (0.6초 동안은 다시 못 받음).
## 터지면 들고 있던 쪽이 크게 아프고 하늘로 — 둘레도 휘말린다

var holder: Fighter
var t := 9.0
var _cool := 0.0
var _last: Fighter
var mesh: MeshInstance3D
var label: Label3D


static func attach(f: Fighter) -> HotPotato:
	var h := HotPotato.new()
	h.holder = f
	f.get_parent().add_child(h)
	return h


func _ready() -> void:
	mesh = BB.make(Fx.ATLAS, "bomb_big", {"unique": true, "receive_shade": false, "emission": 0.3, "up_normal": 1.0})
	add_child(mesh)
	label = Label3D.new()
	label.font = PixelFont.get_font("big")
	label.font_size = PixelFont.size_of("big")
	label.pixel_size = 1.0 / 16.0
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.no_depth_test = true
	label.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	label.outline_modulate = Color(0.1, 0.08, 0.12)
	label.outline_size = 4
	label.position.y = 1.1
	add_child(label)
	Audio.play("fuse", global_position)
	_say()


func _say() -> void:
	var nm := (holder as Player).nick if holder is Player else String((holder as Enemy).E["name"]) if holder is Enemy else "?"
	get_tree().call_group("hud", "notify", "뜨거운 감자 → %s" % nm, Color("#ff9a5a"))


func _process(dt: float) -> void:
	if holder == null or not is_instance_valid(holder) or holder.dead:
		_pass_to_nearest()
		if holder == null:
			queue_free()
			return
	t -= dt
	_cool -= dt
	global_position = holder.global_position + Vector3(0, holder.body_h + 0.5, 0)
	label.text = "%d" % ceili(t)
	label.modulate = Color("#ffffff") if int(t * (8.0 if t < 3.0 else 3.0)) % 2 == 0 else Color("#ff5a3a")
	(mesh.material_override as ShaderMaterial).set_shader_parameter("flash", 0.5 if t < 3.0 and int(t * 10.0) % 2 == 0 else 0.0)
	if _cool <= 0.0:
		for f in Combat.in_radius(get_tree(), holder.global_position, 0.7, 1.2):
			var o := f as Fighter
			if o != holder and o != _last and not (o is Queen):
				_last = holder
				holder = o
				_cool = 0.6
				Audio.play("troll", global_position, -4.0)
				_say()
				break
	if t <= 0.0:
		_boom()


func _pass_to_nearest() -> void:
	var best: Fighter = null
	var bd := 6.0
	for f in get_tree().get_nodes_in_group("fighters"):
		var o := f as Fighter
		if not o.dead and not (o is Queen) and o.global_position.distance_to(global_position) < bd:
			bd = o.global_position.distance_to(global_position)
			best = o
	holder = best


func _boom() -> void:
	var at := holder.global_position
	Audio.play("explode_l", at)
	Fx.pop(get_parent(), at + Vector3(0, 0.6, 0), ["boom0", "boom1", "boom2", "boom3", "boom4", "boom5"], 16.0)
	Particles.burst(get_parent(), at + Vector3(0, 0.5, 0), Color("#fff0a0"), Color("#e05a2a"), 34, 7.0)
	var h := Hit.make(holder.max_hp() * 0.35, null, Vector3(0, 14.0, 0), "bomb")
	h.explosive = true
	holder.take(h)
	for f in Combat.in_radius(get_tree(), at, 2.2):
		if f != holder:
			var h2 := Hit.make(12.0, null, Combat.push(at, (f as Node3D).global_position, 9.0, 0.6), "bomb")
			h2.explosive = true
			(f as Fighter).take(h2)
	get_tree().call_group("hud", "notify", "펑!!", Color("#ff6a3a"))
	var rg := get_tree().get_first_node_in_group("game")
	if rg and rg.rig:
		rg.rig.shake(6.0)
	queue_free()
