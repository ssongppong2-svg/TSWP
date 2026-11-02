class_name ItemDrop
extends Node3D
## 바닥에 떨어진 아이템: 톡 튀어나와 떨어진 뒤 둥실 떠 있다.
##  · 희귀 이상은 희귀도 색 빛기둥 + 은은한 빛 — 멀리서도 보인다
##  · 가까이 가면 이름(희귀도 색)과 [E] 줍기 — 누구든 먼저 줍는 사람이 임자 (가로채기 트롤)

const ATLAS := "res://assets/sprites/items.png"

var id := ""
var vel := Vector3.ZERO
var terrain: Terrain
var reach := 1.3
var t := 0.0
var _landed := false
var _ground := 0.0
var icon: MeshInstance3D
var label: Label3D
var beam: MeshInstance3D
var _near := false


static func spawn(parent: Node, id_: String, at: Vector3, terrain_: Terrain, push := Vector3.ZERO) -> ItemDrop:
	var d := ItemDrop.new()
	d.id = id_
	d.terrain = terrain_
	d.vel = Vector3(randf_range(-1.2, 1.2), 6.0, randf_range(-1.0, 1.0)) + push
	parent.add_child(d)
	d.global_position = at
	return d


func _ready() -> void:
	add_to_group("interact")
	add_to_group("loot")
	var col := Items.color(id)
	var rar := String(Items.get_item(id).get("rarity", "common"))
	icon = BB.make(ATLAS, id, {"unique": true, "center": true, "receive_shade": false, "up_normal": 0.9, "emission": 0.15})
	add_child(icon)
	add_child(BB.blob(0.28, 0.14, 0.7))
	if Items.RARITY_ORDER.find(rar) >= 2:
		# 빛기둥 (위로 갈수록 옅어짐) + 빛
		var q := QuadMesh.new()
		q.size = Vector2(0.18, 2.6)
		beam = MeshInstance3D.new()
		beam.mesh = q
		var m := StandardMaterial3D.new()
		m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		m.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
		m.billboard_mode = BaseMaterial3D.BILLBOARD_FIXED_Y
		m.albedo_color = Color(col.r, col.g, col.b, 0.32)
		m.albedo_texture = _beam_tex()
		m.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
		beam.material_override = m
		beam.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		beam.position.y = 1.3
		add_child(beam)
		var l := OmniLight3D.new()
		l.light_color = col
		l.light_energy = 0.7
		l.omni_range = 2.2
		l.position.y = 0.6
		add_child(l)
	label = Label3D.new()
	label.text = "[E] %s" % Items.get_item(id).get("name", id)
	label.font = PixelFont.get_font("small")
	label.font_size = PixelFont.size_of("small")
	label.pixel_size = 1.0 / 16.0
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.no_depth_test = true
	label.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	label.modulate = col
	label.outline_modulate = Color(0.1, 0.08, 0.12)
	label.outline_size = 3
	label.render_priority = 6
	label.outline_render_priority = 5
	label.position.y = 1.15
	label.visible = false
	add_child(label)


static var _btex: Texture2D
static func _beam_tex() -> Texture2D:
	if _btex == null:
		var img := Image.create(1, 16, false, Image.FORMAT_RGBA8)
		for y in 16:
			# 위쪽(0) 투명 → 아래 진하게, 4단 계단
			img.set_pixel(0, y, Color(1, 1, 1, floorf(float(y) / 16.0 * 4.0) / 4.0 + 0.1))
		_btex = ImageTexture.create_from_image(img)
	return _btex


func _physics_process(dt: float) -> void:
	t += dt
	if not _landed:
		vel.y -= 20.0 * dt
		var p := global_position + vel * dt
		var g := terrain.height_at(p) if terrain else 0.0
		if p.y < g + 0.35 and vel.y < 0.0:
			p.y = g + 0.35
			if absf(vel.y) < 2.5:
				_landed = true
				_ground = g
			vel.y = absf(vel.y) * 0.4
			vel.x *= 0.5
			vel.z *= 0.5
		global_position = p
		return
	# 둥실 (1px 단위로)
	var bob := floorf((sin(t * 3.0) * 0.5 + 0.5) * 3.0) / 16.0
	icon.position.y = 0.05 + bob
	# 가까운 플레이어가 있으면 이름 보이기
	_near = false
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player and not (f as Player).dead and (f as Node3D).global_position.distance_to(global_position) < 2.4:
			_near = true
			break
	label.visible = _near
	if beam:
		beam.visible = not _near


func interact(p: Node) -> void:
	if not _landed and t < 0.3:
		return
	if p is Player:
		(p as Player).pick_up(id)
		queue_free()
