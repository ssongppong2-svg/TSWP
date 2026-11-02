class_name EmoteWheel
extends Control
## 이모트 휠 (T 누르고 있는 동안): 마우스 방향 또는 1~8 로 고르고, T 를 놓으면 머리 위에 띄운다

var sel := -1
var center := Vector2(240, 135)
var _icons: Array = []


func _ready() -> void:
	size = Vector2(PixelView.GW, PixelView.GH)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	process_mode = Node.PROCESS_MODE_ALWAYS
	for i in Emote.IDS.size():
		var a := -PI / 2 + i * TAU / Emote.IDS.size()
		var p := center + Vector2(cos(a), sin(a)) * 38.0
		var ic := UiKit.icon("emote_" + String(Emote.IDS[i]), (p - Vector2(8, 8)).round())
		add_child(ic)
		var l := PixelFont.label("%d" % (i + 1), "small", Color(1, 1, 1, 0.7))
		l.position = (p + Vector2(6, 2)).round()
		add_child(l)
		_icons.append(ic)


func pick_dir(v: Vector2) -> void:
	if v.length() < 8.0:
		return
	var a := fposmod(v.angle() + PI / 2 + PI / Emote.IDS.size(), TAU)
	var i := int(a / (TAU / Emote.IDS.size())) % Emote.IDS.size()
	if i != sel:
		sel = i
		Audio.play("ui_hover")


func _process(_dt: float) -> void:
	queue_redraw()


func _draw() -> void:
	draw_circle(center, 54.0, Color(0.05, 0.04, 0.08, 0.55))
	draw_arc(center, 54.0, 0, TAU, 48, Color("#5a4a6e"), 1.0)
	if sel >= 0:
		var ic := _icons[sel] as Control
		draw_rect(Rect2(ic.position - Vector2(2, 2), Vector2(20, 20)), Color("#ffd860"), false, 1.0)
		var f := PixelFont.get_font("small")
		var nm := String(Emote.NAMES[Emote.IDS[sel]])
		var w := f.get_string_size(nm, HORIZONTAL_ALIGNMENT_LEFT, -1, PixelFont.size_of("small")).x
		draw_string(f, center + Vector2(-w / 2.0, 4), nm, HORIZONTAL_ALIGNMENT_LEFT, -1, PixelFont.size_of("small"), Color("#fff4d6"))


func chosen() -> String:
	return String(Emote.IDS[sel]) if sel >= 0 else ""
