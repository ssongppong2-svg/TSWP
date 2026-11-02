class_name Chest
extends StaticBody3D
## 보물상자 (E 로 열기): 금화 + 아이템 하나 (가끔 소비 아이템 덤).
##  · mimic: 열면 이빨을 드러내고 문다 + 거미가 튀어나온다 — 대신 희귀 이상 아이템을 뱉는다 (트롤 · 위험과 보상)

const ATLAS := "res://assets/sprites/loot.png"

var mimic := false
var solid := true                  # 측면 능선 위처럼 길을 막으면 안 되는 곳은 끈다
var opened := false
var reach := 1.5
var luck := 0.0
var region: Region
var mesh: MeshInstance3D
var _label: Label3D


func _ready() -> void:
	add_to_group("interact")
	add_to_group("chests")
	collision_layer = 1 if solid else 0
	var cs := CollisionShape3D.new()
	var b := BoxShape3D.new()
	b.size = Vector3(1.2, 0.8, 0.7)
	cs.shape = b
	cs.position.y = 0.4
	add_child(cs)
	mesh = BB.make(ATLAS, "chest_closed", {"unique": true, "up_normal": 0.8})
	add_child(mesh)
	add_child(BB.blob(0.8, 0.36, 0.9))
	_label = Label3D.new()
	_label.text = "[E] 열기"
	_label.font = PixelFont.get_font("small")
	_label.font_size = PixelFont.size_of("small")
	_label.pixel_size = 1.0 / 16.0
	_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_label.no_depth_test = true
	_label.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	_label.outline_modulate = Color(0.1, 0.08, 0.12)
	_label.outline_size = 3
	_label.modulate = Color("#ffe9a8")
	_label.position.y = 1.6
	_label.visible = false
	add_child(_label)


func _process(_dt: float) -> void:
	if opened:
		_label.visible = false
		return
	var near := false
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player and (f as Node3D).global_position.distance_to(global_position) < 2.4:
			near = true
			break
	_label.visible = near
	# 미믹은 가까이 가면 아주 살짝 들썩인다 (눈치 빠른 사람만 알아채게)
	if mimic and near:
		mesh.position.y = 1.0 / 16.0 if int(Time.get_ticks_msec() / 90) % 9 == 0 else 0.0


func interact(p: Node) -> void:
	if opened:
		return
	opened = true
	remove_from_group("interact")
	var parent := get_parent()
	var rng := RandomNumberGenerator.new()
	rng.randomize()
	var at := global_position + Vector3(0, 0.7, 0)
	if mimic:
		(mesh.material_override as ShaderMaterial).set_shader_parameter("region", BB.region_uv(ATLAS, "chest_mimic"))
		Audio.play("troll", global_position)
		Audio.play("spider_bite", global_position)
		if p is Player and (p as Player).local:
			Achievements.add(get_tree(), "mimic_bites")
		if p is Fighter:
			var pf := p as Fighter
			var push := Combat.push(global_position, pf.global_position, 9.0, 0.5)
			var h := Hit.make(15.0, null, push, "bite")
			h.team = 1
			pf.take(h)
		if region:
			for k in 2:
				var sp := global_position + Vector3(randf_range(-1.5, 1.5), 0.2, randf_range(1.0, 2.0))
				var e := Enemy.spawn(parent, "spider", sp, region.terrain, region.rig)
				region.enemy_spawned.emit(e)
		await get_tree().create_timer(0.6, false).timeout
		if not is_inside_tree():
			return
		ItemDrop.spawn(parent, Items.roll(rng, false, 1.5, "rare"), at, region.terrain if region else null)
		return
	(mesh.material_override as ShaderMaterial).set_shader_parameter("region", BB.region_uv(ATLAS, "chest_open"))
	Audio.play("chest", global_position)
	Particles.burst(parent, at, Color("#fff4a0"), Color("#f0a030"), 24, 5.0)
	Coin.burst(parent, at, rng.randi_range(3, 6), region.terrain if region else null)
	ItemDrop.spawn(parent, Items.roll(rng, false, luck), at, region.terrain if region else null)
	if rng.randf() < 0.4:
		ItemDrop.spawn(parent, Items.roll(rng, true), at, region.terrain if region else null)
