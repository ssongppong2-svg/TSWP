class_name HudEditor
extends Control
## HUD 배치 편집 (설정 → HUD 배치): 위젯을 마우스로 끌어 옮긴다. 방송 화면(웹캠·채팅 자리)에 맞추기용.
##  · 끌기: 왼쪽 버튼 · 고르기: 클릭 또는 Tab · 1px 옮기기: 방향키 (Shift = 8px)
##  · H: 고른 것 숨기기/보이기 · R: 고른 것 원래 자리 · 1~4: 배치 프리셋 · Esc/오른쪽 클릭: 저장하고 나가기
##  · 4px 격자에 붙고 화면 밖으로는 못 나간다
## 저장: Prefs.hud_layout (기본 자리에서 옮긴 만큼) · Prefs.hud_hidden

signal done

const PRESETS := [
	{"name": "기본", "layout": {}, "hidden": {}},
	# 웹캠을 오른쪽 아래에 두는 방송: 오른쪽 아래를 비운다 (보스바를 왼쪽으로)
	{"name": "방송: 캠 오른쪽 아래", "layout": {"boss": Vector2(-118, 0)}, "hidden": {}},
	# 웹캠을 왼쪽 위에 두는 방송: 내 상태·스킬은 아래로, 파티는 오른쪽, 보스·아이템 설명은 위 가운데
	{"name": "방송: 캠 왼쪽 위", "layout": {"status": Vector2(0, 204), "skills": Vector2(0, 218), "party": Vector2(376, 100),
		"boss": Vector2(0, -210), "toast": Vector2(140, -110)}, "hidden": {}},
	{"name": "최소 (미니맵·알림 숨김)", "layout": {}, "hidden": {"minimap": true, "feed": true, "party": true}},
]

var hud: Hud
var sel := ""
var _drag := false
var _grab := Vector2.ZERO
var _info: Label
var _t := 0.0


func _ready() -> void:
	size = Vector2(PixelView.GW, PixelView.GH)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	process_mode = Node.PROCESS_MODE_ALWAYS
	_info = PixelFont.label("", "small", Color("#fff4d6"))
	_info.position = Vector2(0, 118)
	_info.size = Vector2(PixelView.GW, 40)
	_info.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_info.add_theme_color_override("font_outline_color", Color("#140e1a"))
	_info.add_theme_constant_override("outline_size", 4)
	add_child(_info)
	sel = hud.WIDGETS.keys()[0]
	_refresh_info()


func _refresh_info() -> void:
	var nm := String(hud.WIDGETS[sel]["name"]) if sel != "" else ""
	var hid := bool(Prefs.hud_hidden.get(sel, false))
	_info.text = "HUD 배치 — 고른 것: %s%s\n끌어서 옮기기 · 방향키 1px(Shift 8px) · H 숨기기 · R 원래 자리\n1 기본  2 방송(캠 오른쪽 아래)  3 방송(캠 왼쪽 위)  4 최소   ·   Esc 저장하고 나가기" % [nm, "  (숨김)" if hid else ""]


func _process(dt: float) -> void:
	_t += dt
	queue_redraw()


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color(0.05, 0.04, 0.08, 0.35))
	for n in hud.WIDGETS:
		var r := hud.widget_rect(n)
		var hid := bool(Prefs.hud_hidden.get(n, false))
		var on: bool = n == sel
		var col := Color("#ffd860") if on else (Color(1, 1, 1, 0.35) if hid else Color("#8ad8ff"))
		if on and int(_t * 4.0) % 2 == 0:
			col = Color("#fff4b0")
		draw_rect(r, Color(col.r, col.g, col.b, 0.12 if not hid else 0.05))
		draw_rect(r.grow(-0.5), col, false, 1.0)
		var f := PixelFont.get_font("small")
		draw_string(f, r.position + Vector2(3, 10), String(hud.WIDGETS[n]["name"]) + (" (숨김)" if hid else ""), HORIZONTAL_ALIGNMENT_LEFT, -1, PixelFont.size_of("small"), col)


func _move_sel(d: Vector2) -> void:
	if sel == "":
		return
	var cur: Vector2 = Prefs.hud_layout.get(sel, Vector2.ZERO)
	var nxt := cur + d
	var base: Rect2 = hud.WIDGETS[sel]["rect"]
	# 화면 밖으로 못 나가게
	nxt.x = clampf(nxt.x, -base.position.x, PixelView.GW - base.end.x)
	nxt.y = clampf(nxt.y, -base.position.y, PixelView.GH - base.end.y)
	Prefs.hud_layout[sel] = nxt
	hud.apply_layout()


func _pick(p: Vector2) -> String:
	# 작은 것부터 (겹치면 안쪽 것을 고르게)
	var best := ""
	var area := 1e9
	for n in hud.WIDGETS:
		var r := hud.widget_rect(n)
		if r.has_point(p) and r.get_area() < area:
			area = r.get_area()
			best = n
	return best


func handle(e: InputEvent, ui_pos: Vector2) -> bool:
	if e is InputEventMouseButton:
		var mb := e as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_LEFT:
			if mb.pressed:
				var n := _pick(ui_pos)
				if n != "":
					sel = n
					_drag = true
					_grab = ui_pos - (Prefs.hud_layout.get(sel, Vector2.ZERO) as Vector2)
					Audio.play("ui_click")
					_refresh_info()
			else:
				_drag = false
			return true
		if mb.button_index == MOUSE_BUTTON_RIGHT and mb.pressed:
			_finish()
			return true
	if e is InputEventMouseMotion and _drag and sel != "":
		var want := ui_pos - _grab
		want = (want / 4.0).round() * 4.0
		var cur: Vector2 = Prefs.hud_layout.get(sel, Vector2.ZERO)
		_move_sel(want - cur)
		return true
	if e is InputEventKey and e.pressed:
		var k := e as InputEventKey
		var step := 8.0 if k.shift_pressed else 1.0
		match k.physical_keycode:
			KEY_LEFT, KEY_A:
				_move_sel(Vector2(-step, 0))
			KEY_RIGHT, KEY_D:
				_move_sel(Vector2(step, 0))
			KEY_UP, KEY_W:
				_move_sel(Vector2(0, -step))
			KEY_DOWN, KEY_S:
				_move_sel(Vector2(0, step))
			KEY_TAB:
				var ks: Array = hud.WIDGETS.keys()
				sel = ks[(ks.find(sel) + 1) % ks.size()]
				Audio.play("ui_hover")
			KEY_H:
				Prefs.hud_hidden[sel] = not bool(Prefs.hud_hidden.get(sel, false))
				hud.apply_layout()
				Audio.play("ui_toggle")
			KEY_R:
				Prefs.hud_layout.erase(sel)
				hud.apply_layout()
				Audio.play("ui_back")
			KEY_1, KEY_2, KEY_3, KEY_4:
				var pr: Dictionary = PRESETS[k.physical_keycode - KEY_1]
				Prefs.hud_layout = (pr["layout"] as Dictionary).duplicate()
				Prefs.hud_hidden = (pr["hidden"] as Dictionary).duplicate()
				hud.apply_layout()
				Audio.play("ui_confirm")
				get_tree().call_group("hud", "notify", "배치: " + String(pr["name"]), Color("#ffd860"))
			KEY_ESCAPE, KEY_ENTER:
				_finish()
		_refresh_info()
		return true
	return e is InputEventMouse


func _finish() -> void:
	Prefs.save_prefs()
	Audio.play("ui_confirm")
	done.emit()
	queue_free()
