class_name PixelView
extends Control
## 3D 세계를 480x270 저해상도로 그린 뒤 정수 배율로 확대해 보여 준다.
##  · 카메라는 CamRig 가 1/16 칸(=1픽셀) 단위로 끊어 움직이고, 남은 소수점만큼 화면을 밀어
##    도트가 흔들리지 않으면서도 부드럽게 따라간다 (서브픽셀 스크롤).
##  · 테두리 2픽셀을 더 그려 두었다가 잘라 낸다 (밀어도 빈칸이 안 보이게).

const GW := 480
const GH := 270
const MARGIN := 2

var vp: SubViewport
var screen: TextureRect
var frame: Control
var scale_k := 3
var sub_offset := Vector2.ZERO       # 카메라 리그가 채움: 화면 픽셀 단위 남은 소수점


func _ready() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	vp = SubViewport.new()
	vp.size = Vector2i(GW + MARGIN * 2, GH + MARGIN * 2)
	vp.own_world_3d = true
	vp.msaa_3d = Viewport.MSAA_DISABLED
	vp.scaling_3d_mode = Viewport.SCALING_3D_MODE_BILINEAR
	vp.canvas_item_default_texture_filter = Viewport.DEFAULT_CANVAS_ITEM_TEXTURE_FILTER_NEAREST
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	vp.positional_shadow_atlas_size = 2048
	vp.handle_input_locally = false
	add_child(vp)
	frame = Control.new()
	frame.clip_contents = true
	frame.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(frame)
	screen = TextureRect.new()
	screen.texture = vp.get_texture()
	screen.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	screen.stretch_mode = TextureRect.STRETCH_SCALE
	screen.mouse_filter = Control.MOUSE_FILTER_IGNORE
	frame.add_child(screen)
	resized.connect(_layout)
	_layout()


func _layout() -> void:
	var s := size
	if s.x <= 0:
		s = get_viewport_rect().size
	scale_k = maxi(1, mini(int(s.x / GW), int(s.y / GH)))
	var fs := Vector2(GW, GH) * scale_k
	frame.size = fs
	frame.position = ((s - fs) * 0.5).floor()
	screen.size = Vector2(vp.size) * scale_k


func _process(_dt: float) -> void:
	# 남은 소수점 밀기: 화면 픽셀(확대 전) 단위 → 확대한 실제 픽셀
	screen.position = (-Vector2(MARGIN, MARGIN) - sub_offset) * scale_k


## 화면 좌표 → 저해상도 화면 좌표 (마우스 조준용)
func to_low(p: Vector2) -> Vector2:
	return (p - frame.position) / float(scale_k) + sub_offset + Vector2(MARGIN, MARGIN)
