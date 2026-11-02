class_name PlatePuzzle
extends Node3D
## 협동 퍼즐 (퍼즐 시스템.md · 발판 퍼즐): 발판 둘을 동시에 눌러야 철창이 열린다.
##  · 누르는 것: 사람 · 적 · 들어서 던진 것 · 밀 수 있는 상자 (혼자서도 상자로 풀 수 있게 — 오래 걸릴 뿐)
##  · 옆에 '누르지 마시오' 버튼 (트롤 퍼즐: 누르면 몬스터가 나오거나… 가끔 금화)
## 놓는 쪽(지역)이 plates/cage 자리를 정한다

const LOOT := "res://assets/sprites/loot.png"

var region: Region
var plate_pos: Array = []          # [Vector3, Vector3]
var cage_pos := Vector3.ZERO
var solved := false
var _plates: Array = []            # MeshInstance3D (바닥 그림)
var _down: Array = [false, false]
var _cage: MeshInstance3D
var _cage_body: StaticBody3D
var _label: Label3D


func _ready() -> void:
	add_to_group("puzzles")
	for p in plate_pos:
		var m := _flat("plate_up", p)
		_plates.append(m)
	_cage = BB.make(LOOT, "cage", {"unique": true, "up_normal": 0.7})
	_cage.position = cage_pos
	add_child(_cage)
	_cage_body = StaticBody3D.new()
	var cs := CollisionShape3D.new()
	var b := BoxShape3D.new()
	b.size = Vector3(1.8, 1.6, 0.9)
	cs.shape = b
	cs.position.y = 0.8
	_cage_body.add_child(cs)
	_cage_body.position = cage_pos
	add_child(_cage_body)
	_label = Label3D.new()
	_label.text = "발판 두 개를 동시에!"
	_label.font = PixelFont.get_font("small")
	_label.font_size = PixelFont.size_of("small")
	_label.pixel_size = 1.0 / 16.0
	_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_label.no_depth_test = true
	_label.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	_label.outline_modulate = Color(0.1, 0.08, 0.12)
	_label.outline_size = 3
	_label.modulate = Color("#ffe9a8")
	_label.position = cage_pos + Vector3(0, 2.5, 0)
	add_child(_label)


func _flat(nm: String, at: Vector3) -> MeshInstance3D:
	var q := QuadMesh.new()
	q.size = BB.px_size(LOOT, nm) / 16.0
	q.orientation = PlaneMesh.FACE_Y
	var mi := MeshInstance3D.new()
	mi.mesh = q
	var m := ShaderMaterial.new()
	m.shader = load("res://shaders/fx_flat.gdshader")
	m.set_shader_parameter("tex", BB.atlas(LOOT)["tex"])
	m.set_shader_parameter("region", BB.region_uv(LOOT, nm))
	mi.material_override = m
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mi.position = at + Vector3(0, 0.03, 0)
	add_child(mi)
	return mi


func _pressed(at: Vector3) -> bool:
	for f in get_tree().get_nodes_in_group("fighters"):
		var o := f as Fighter
		if not o.dead and not o.held and Vector2(o.global_position.x - at.x, o.global_position.z - at.z).length() < 0.6 and absf(o.global_position.y - at.y) < 0.8:
			return true
	for c in get_tree().get_nodes_in_group("crates"):
		if Vector2((c as Node3D).global_position.x - at.x, (c as Node3D).global_position.z - at.z).length() < 0.6:
			return true
	return false


func _process(_dt: float) -> void:
	if solved:
		return
	var all := true
	for i in _plates.size():
		var d := _pressed(plate_pos[i])
		if d != _down[i]:
			_down[i] = d
			((_plates[i] as MeshInstance3D).material_override as ShaderMaterial).set_shader_parameter("region", BB.region_uv(LOOT, "plate_down" if d else "plate_up"))
			Audio.play("plate", plate_pos[i])
		all = all and d
	if all:
		_solve()


func _solve() -> void:
	solved = true
	_label.text = ""
	Audio.play("gate_open", cage_pos)
	Audio.stinger("st_puzzle")
	(_cage.material_override as ShaderMaterial).set_shader_parameter("region", BB.region_uv(LOOT, "cage_open"))
	_cage_body.queue_free()
	var rng := RandomNumberGenerator.new()
	rng.randomize()
	ItemDrop.spawn(region, Items.roll(rng, false, 1.5, "rare"), global_position + cage_pos + Vector3(0, 1.0, 0), region.terrain, Vector3(0, 0, 2))
	Coin.burst(region, global_position + cage_pos + Vector3(0, 0.8, 0), 10, region.terrain)
	get_tree().call_group("hud", "notify", "협동 퍼즐 해결! 철창이 열렸다", Color("#9aff9a"))
	get_tree().call_group("hud", "banner", "와, 잘했다!", 2.0)
	Achievements.add(get_tree(), "puzzles")
