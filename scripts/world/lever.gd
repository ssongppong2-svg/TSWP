class_name Lever
extends Node3D
## 레버 (E 로 당김). 당기면 hold 초 동안 켜져 있다가 돌아간다 → 둘을 거의 동시에 켜야 하는 협동 퍼즐에 쓴다

signal pulled(l: Lever)

var on := false
var hold := 4.0
var t := 0.0
var sprite: MeshInstance3D
var reach := 1.4
var label: Label3D


func _ready() -> void:
	add_to_group("interact")
	sprite = BB.make(BossBits.ATLAS, "lever_off", {"unique": true})
	add_child(sprite)
	label = Label3D.new()
	label.text = "E"
	label.font = PixelFont.get_font("small")
	label.font_size = PixelFont.size_of("small")
	label.pixel_size = 1.0 / 16.0
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.no_depth_test = true
	label.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	label.outline_size = 2
	label.position.y = 1.7
	add_child(label)


func interact(_p: Node) -> void:
	on = true
	t = hold
	Audio.play("lever", global_position)
	(sprite.material_override as ShaderMaterial).set_shader_parameter("region", BB.region_uv(BossBits.ATLAS, "lever_on"))
	pulled.emit(self)


func _process(dt: float) -> void:
	label.visible = not on and int(Time.get_ticks_msec() / 400) % 2 == 0
	if on:
		t -= dt
		if t <= 0.0:
			on = false
			(sprite.material_override as ShaderMaterial).set_shader_parameter("region", BB.region_uv(BossBits.ATLAS, "lever_off"))
			Audio.play("lever", global_position, -6.0)
