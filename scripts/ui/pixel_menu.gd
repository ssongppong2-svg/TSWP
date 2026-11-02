class_name PixelMenu
extends Control
## 도트 메뉴 (타이틀·일시정지·설정 공용). 480x270 UI 화면 위에 그린다.
##  항목: {"label", "type": button|slider|choice|toggle, "get": Callable, "set": Callable, "do": Callable, "options": [...]}
##  · 키보드: 위/아래(W/S·화살표) 고르기 · 좌/우(A/D) 값 바꾸기 · Enter/Space/E 실행 · Esc 뒤로
##  · 마우스: 올리면 고르기 · 클릭 실행 (슬라이더는 누른 자리로) · 휠로 값 바꾸기
##  · 패드: 십자키·왼스틱 / A 실행 / B 뒤로
## 입력은 주인이 handle(event, ui_pos) 로 넘겨준다 (UI 화면은 입력을 직접 받지 않는다)

signal back

const ROW_H := 16
const SLIDER_N := 10

var title := ""
var items: Array = []
var width := 200
var top := -1.0                    # 판 위쪽 y (음수면 화면 가운데)
var boxless := false               # 판 없이 글자만 (타이틀) — 고른 줄만 금테
var sel := 0
var _rows: Array = []              # [Control 줄, 값 Label, 선택 판]
var _box: Control
var _panel: NinePatchRect
var _arrow: TextureRect
var _t := 0.0
var _origin := Vector2.ZERO        # 첫 줄 왼쪽 위 (UI 좌표)
var _fade := 0.0


func _init(title_: String = "", items_: Array = [], width_ := 200) -> void:
	title = title_
	items = items_
	width = width_


func _ready() -> void:
	size = Vector2(PixelView.GW, PixelView.GH)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	process_mode = Node.PROCESS_MODE_ALWAYS
	build()


func build() -> void:
	for c in get_children():
		c.queue_free()
	_rows.clear()
	var head := 22 if title != "" else 6
	var h := head + items.size() * ROW_H + 8
	var x0 := roundf((PixelView.GW - width) / 2.0)
	var y0 := top if top >= 0.0 else roundf((PixelView.GH - h) / 2.0) + 18
	_box = Control.new()
	_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_box)
	_panel = UiKit.panel("panel", Vector2(x0, y0), Vector2(width, h))
	_panel.visible = not boxless
	_box.add_child(_panel)
	if title != "":
		var tl := PixelFont.label(title, "big", Color("#ffe09a"))
		tl.position = Vector2(x0, y0 + 4)
		tl.size = Vector2(width, 14)
		tl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		_box.add_child(tl)
		var line := ColorRect.new()
		line.color = Color("#5a4a6e")
		line.position = Vector2(x0 + 10, y0 + 19)
		line.size = Vector2(width - 20, 1)
		_box.add_child(line)
	_origin = Vector2(x0 + 6, y0 + head)
	for i in items.size():
		var it: Dictionary = items[i]
		var hi := UiKit.panel("panel_hi", _origin + Vector2(0, i * ROW_H), Vector2(width - 12, ROW_H))
		hi.visible = false
		_box.add_child(hi)
		var l := PixelFont.label(String(it["label"]), "big", Color("#fff4d6"))
		l.position = _origin + Vector2(14, i * ROW_H + 1)
		if boxless:
			l.add_theme_color_override("font_outline_color", Color("#140e1a"))
			l.add_theme_constant_override("outline_size", 4)
		_box.add_child(l)
		var v := PixelFont.label("", "big", Color("#ffd860"))
		v.position = _origin + Vector2(width - 12 - 96 - 6, i * ROW_H + 1)
		v.size = Vector2(96, 14)
		v.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
		_box.add_child(v)
		if String(it.get("type", "button")) == "button":
			l.size = Vector2(width - 40, 14)
		_rows.append([l, v, hi])
	_arrow = UiKit.icon("sel")
	_box.add_child(_arrow)
	sel = clampi(sel, 0, items.size() - 1)
	for k in items.size():
		if _selectable(sel):
			break
		sel = posmod(sel + 1, items.size())
	refresh()


func refresh() -> void:
	for i in items.size():
		var it: Dictionary = items[i]
		var v: Label = _rows[i][1]
		var tp := String(it.get("type", "button"))
		match tp:
			"slider":
				var x := clampf(float(it["get"].call()), 0.0, 1.0)
				var n := roundi(x * SLIDER_N)
				v.text = "■".repeat(n) + "□".repeat(SLIDER_N - n)
				v.add_theme_font_override("font", PixelFont.get_font("small"))
				v.add_theme_font_size_override("font_size", PixelFont.size_of("small"))
				v.position.y = _origin.y + i * ROW_H + 3
			"choice":
				var opts: Array = it["options"]
				v.text = "◀ %s ▶" % opts[clampi(int(it["get"].call()), 0, opts.size() - 1)]
			"toggle":
				v.text = "켬" if bool(it["get"].call()) else "끔"
				v.add_theme_color_override("font_color", Color("#9aff9a") if bool(it["get"].call()) else Color("#ff9a9a"))
			_:
				v.text = String(it.get("value", ""))
		var on := i == sel
		(_rows[i][2] as Control).visible = on
		var lc := Color("#ffffff") if on else Color("#d8ccb8")
		if tp == "info":
			lc = Color("#a89cb8")
		(_rows[i][0] as Label).add_theme_color_override("font_color", lc)
	_arrow.position = _origin + Vector2(4, sel * ROW_H + 5)


func _process(dt: float) -> void:
	_t += dt
	if _arrow:
		_arrow.position.x = _origin.x + 4 + (1.0 if int(_t * 3.0) % 2 == 0 else 0.0)
	_fade = minf(1.0, _fade + dt * 6.0)
	modulate.a = _fade


func _selectable(i: int) -> bool:
	return String(items[i].get("type", "button")) != "info"


func _move(d: int) -> void:
	if items.is_empty():
		return
	for k in items.size():
		sel = posmod(sel + d, items.size())
		if _selectable(sel):
			break
	Audio.play("ui_hover")
	refresh()


func _adjust(d: int) -> bool:
	var it: Dictionary = items[sel]
	match String(it.get("type", "button")):
		"slider":
			var x := clampf(float(it["get"].call()) + d * 0.1, 0.0, 1.0)
			it["set"].call(snappedf(x, 0.1))
		"choice":
			var opts: Array = it["options"]
			it["set"].call(posmod(int(it["get"].call()) + d, opts.size()))
		"toggle":
			it["set"].call(not bool(it["get"].call()))
		_:
			return false
	Audio.play("ui_tick")
	refresh()
	return true


func _activate() -> void:
	var it: Dictionary = items[sel]
	match String(it.get("type", "button")):
		"button":
			Audio.play("ui_confirm")
			if it.has("do"):
				it["do"].call()
		"toggle", "choice":
			_adjust(1)
		"slider":
			pass


## 주인이 넘겨주는 입력. 처리했으면 true
func handle(e: InputEvent, ui_pos: Vector2) -> bool:
	if not is_visible_in_tree() or items.is_empty():
		return false
	if e is InputEventMouseMotion:
		var i := _row_at(ui_pos)
		if i >= 0 and i != sel:
			sel = i
			Audio.play("ui_hover")
			refresh()
		return i >= 0
	if e is InputEventMouseButton and e.pressed:
		var mb := e as InputEventMouseButton
		var i := _row_at(ui_pos)
		if mb.button_index == MOUSE_BUTTON_WHEEL_UP or mb.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			if i >= 0:
				sel = i
				_adjust(1 if mb.button_index == MOUSE_BUTTON_WHEEL_UP else -1)
			return true
		if mb.button_index == MOUSE_BUTTON_RIGHT:
			Audio.play("ui_back")
			back.emit()
			return true
		if mb.button_index != MOUSE_BUTTON_LEFT or i < 0:
			return false
		sel = i
		var it: Dictionary = items[sel]
		var tp := String(it.get("type", "button"))
		if tp == "slider":
			var v: Label = _rows[i][1]
			var x := (ui_pos.x - v.position.x - (v.size.x - _slider_w())) / _slider_w()
			if x >= -0.1 and x <= 1.1:
				it["set"].call(clampf(snappedf(x + 0.05, 0.1), 0.0, 1.0))
				Audio.play("ui_tick")
			refresh()
		elif tp == "choice":
			var v2: Label = _rows[i][1]
			_adjust(-1 if ui_pos.x < v2.position.x + v2.size.x - 40 else 1)
		else:
			_activate()
		return true
	if e.is_echo() and not (e.is_action("move_up") or e.is_action("move_down") or e.is_action("move_left") or e.is_action("move_right")):
		return false
	if e.is_action_pressed("move_up", true) or e.is_action_pressed("ui_up", true):
		_move(-1)
		return true
	if e.is_action_pressed("move_down", true) or e.is_action_pressed("ui_down", true):
		_move(1)
		return true
	if e.is_action_pressed("move_left", true) or e.is_action_pressed("ui_left", true):
		return _adjust(-1)
	if e.is_action_pressed("move_right", true) or e.is_action_pressed("ui_right", true):
		return _adjust(1)
	if e.is_action_pressed("ui_accept") or e.is_action_pressed("roll") or e.is_action_pressed("interact"):
		_activate()
		return true
	if e.is_action_pressed("menu") or e.is_action_pressed("ui_cancel"):
		Audio.play("ui_back")
		back.emit()
		return true
	return false


func _slider_w() -> float:
	var f := PixelFont.get_font("small")
	return f.get_string_size("■".repeat(SLIDER_N), HORIZONTAL_ALIGNMENT_LEFT, -1, PixelFont.size_of("small")).x


func _row_at(p: Vector2) -> int:
	var r := p - _origin
	if r.x < 0 or r.x > width - 12 or r.y < 0:
		return -1
	var i := int(r.y / ROW_H)
	return i if i < items.size() and _selectable(i) else -1
