class_name Hud
extends Control
## 도트 HUD (480x270 위에 그린다 · 부품은 UiKit 아틀라스)
##  왼쪽 위: 직업 초상(아이콘) · 이름 · 패시브(보석) · 체력바(숫자는 바 안, 막 깎인 부분은 하얗게 남았다 줄어듦)
##  그 옆: 스킬 칸 (스킬 그림 · 쿨타임은 위에서부터 걷힘 · 남은 초 · Q 키캡 · 다 차면 금테 번쩍)
##        · 소비 아이템 칸 (F · 개수) · 궁극기 칸 (R · 아래부터 차오르는 게이지, 가득 차면 금빛 깜빡)
##  그 아래: 장비 5칸 (희귀도 색 밑줄) · 새로 얻으면 왼쪽에 아이템 알림(이름·효과·위험)
##  오른쪽 위: 남은 부활(하트) · 금화 (늘면 톡 튐)
##  아래 가운데: 상황 힌트(키캡 + 설명, 잠깐 떴다 사라짐) · 보스 체력바 + 위험 게이지
##  문서(UI 시스템.md): 왼쪽 파티 정보 · 미니맵 · 우측 상단 알림 · 머리 위 이름/체력(Player 쪽)
## 배치: 덩어리(위젯)마다 기본 자리에서 얼마나 옮겼는지 Prefs.hud_layout 에 저장 — HudEditor 로 끌어서 옮긴다
##       숨긴 위젯은 Prefs.hud_hidden, 전체 투명도는 Prefs.ui_opacity

var player: Player
var party: Party
var boss: Queen

const HP_W := 92
const BOSS_W := 220

var _root: Control                 # HUD 전체 (타이틀·메뉴에서 숨김)
var _cur: Control                  # 지금 만드는 위젯 (만들기 함수들이 여기에 붙인다)
var widgets := {}                  # 이름 → 위젯 Control (기본 자리 = 0,0 · 옮긴 만큼 position)
## 위젯: 이름(편집기에 보이는) · 기본 자리 사각형 (480x270 기준)
const WIDGETS := {
	"status": {"name": "내 상태", "rect": Rect2(4, 4, 138, 57)},
	"skills": {"name": "스킬 · 소비 · 궁극기", "rect": Rect2(145, 4, 108, 47)},
	"lives": {"name": "부활 · 금화", "rect": Rect2(400, 4, 76, 34)},
	"minimap": {"name": "미니맵", "rect": Rect2(400, 41, 76, 58)},
	"feed": {"name": "알림", "rect": Rect2(326, 102, 150, 64)},
	"party": {"name": "파티", "rect": Rect2(4, 66, 100, 64)},
	"toast": {"name": "아이템 설명", "rect": Rect2(4, 168, 196, 68)},
	"boss": {"name": "보스", "rect": Rect2(122, 226, 236, 40)},
}
# 왼쪽 위
var portrait: TextureRect
var name_l: Label
var gem: TextureRect
var passive_l: Label
var hp_ghost: ColorRect
var hp_fill: ColorRect
var hp_shine: ColorRect
var hp_text: Label
var _ghost := 1.0
var _shown_job := ""
# 스킬 칸
var q_panel: NinePatchRect
var q_icon: TextureRect
var q_cover: ColorRect
var q_sec: Label
var q_flash: ColorRect
var q_name: Label
var _was_cd := false
# 소비 · 궁극기 · 장비
var c_icon: TextureRect
var c_n: Label
var u_panel: NinePatchRect
var u_icon: TextureRect
var u_fill: ColorRect
var _ult_full := false
var item_slots: Array = []         # [TextureRect 그림, ColorRect 밑줄]
var _bag_sig := ""
# 알림
var toast_box: Control
var _toast_t := 0.0
var _toast_q: Array = []
var _toast_len := 4.2
var msg_l: Label
var _msg_t := 0.0
var _ready_t := 0.0
var _qname_t := 0.0                # 직업을 바꾼 뒤 잠깐만 스킬 이름을 보여 준다
# 오른쪽 위
var rp: NinePatchRect
var hearts: Control
var coin: TextureRect
var gold_l: Label
var _max_lives := 3
var _lives_shown := -1
var _gold_shown := -1
var _gold_bump := 0.0
# 보스
var boss_panel: Control
var boss_name: Label
var boss_fill: ColorRect
var boss_ghost: ColorRect
var danger_fill: ColorRect
var _bghost := 1.0
# 가운데 글자 · 힌트
var center_l: Label
var _banner_t := 0.0
var hint_box: Control
var _hint_t := 0.0
var _hint_queue: Array = []
# 피격
var hurt_rect: TextureRect
var _hurt := 0.0


## 맞으면 화면 가장자리가 붉게 번쩍 (도트 디더 테두리)
func hurt_flash(k: float) -> void:
	_hurt = maxf(_hurt, k)


func _make_hurt() -> void:
	var w := PixelView.GW
	var h := PixelView.GH
	var img := Image.create(w, h, false, Image.FORMAT_RGBA8)
	var bayer := [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5]
	for y in h:
		for x in w:
			var e := minf(minf(x, w - 1 - x) / 40.0, minf(y, h - 1 - y) / 26.0)
			var k := clampf(1.0 - e, 0.0, 1.0)
			var th: float = (float(bayer[(y % 4) * 4 + (x % 4)]) + 0.5) / 16.0
			if k * k > th:
				img.set_pixel(x, y, Color(0.85, 0.08, 0.1, 0.55))
	hurt_rect = TextureRect.new()
	hurt_rect.texture = ImageTexture.create_from_image(img)
	hurt_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hurt_rect.modulate.a = 0.0
	add_child(hurt_rect)


func _ready() -> void:
	add_to_group("hud")
	size = Vector2(PixelView.GW, PixelView.GH)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	_make_hurt()
	_root = Control.new()
	_root.size = size
	_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_root)
	_begin("status")
	_build_main()
	_begin("skills")
	_build_skill()
	_build_extra()
	_begin("lives")
	_build_right()
	_begin("boss")
	_build_boss()
	_begin("minimap")
	_build_minimap()
	_begin("feed")
	_build_feed()
	_begin("party")
	_build_party()
	_cur = _root
	apply_layout()
	center_l = PixelFont.label("", "big", Color("#fff4d6"))
	center_l.position = Vector2(0, 104)
	center_l.size = Vector2(PixelView.GW, 30)
	center_l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	center_l.add_theme_color_override("font_outline_color", Color("#1a1420"))
	center_l.add_theme_constant_override("outline_size", 4)
	_root.add_child(center_l)
	hint_box = Control.new()
	hint_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	hint_box.modulate.a = 0.0
	_root.add_child(hint_box)


func _begin(n: String) -> void:
	var w := Control.new()
	w.name = n
	w.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_root.add_child(w)
	widgets[n] = w
	_cur = w


## 저장된 배치·숨김·투명도 적용 (편집기·설정에서 부른다)
func apply_layout() -> void:
	for n in widgets:
		var off: Variant = Prefs.hud_layout.get(n, Vector2.ZERO)
		(widgets[n] as Control).position = off if off is Vector2 else Vector2.ZERO
		(widgets[n] as Control).visible = not bool(Prefs.hud_hidden.get(n, false))
	_root.modulate.a = Prefs.ui_opacity


## 위젯이 지금 차지한 사각형 (편집기용)
func widget_rect(n: String) -> Rect2:
	var r: Rect2 = WIDGETS[n]["rect"]
	return Rect2(r.position + (widgets[n] as Control).position, r.size)


## 타이틀·메뉴에서 HUD 숨기기 (피격 테두리는 남김)
func set_shown(on: bool) -> void:
	_root.visible = on


# ------------------------------------------------------------------ 만들기
func _build_main() -> void:
	_cur.add_child(UiKit.panel("panel", Vector2(4, 4), Vector2(138, 34)))
	_cur.add_child(UiKit.panel("panel_dark", Vector2(7, 7), Vector2(28, 28)))
	portrait = UiKit.icon("job_warrior", Vector2(12, 12))
	_cur.add_child(portrait)
	name_l = PixelFont.label("", "big", Color("#fff4d6"))
	name_l.position = Vector2(39, 5)
	_cur.add_child(name_l)
	gem = UiKit.icon("gem", Vector2(70, 9))
	_cur.add_child(gem)
	passive_l = PixelFont.label("", "small", Color("#b8e4ff"))
	passive_l.position = Vector2(79, 6)
	_cur.add_child(passive_l)
	# 체력바: 테두리 · 바탕 · 잔상 · 채움 · 윗빛 · 아랫그늘 · 숫자
	_rect(Vector2(38, 20), Vector2(HP_W + 2, 13), Color("#0e0b12"))
	_rect(Vector2(39, 21), Vector2(HP_W, 11), Color("#2a2030"))
	hp_ghost = _rect(Vector2(39, 21), Vector2(HP_W, 11), Color("#fff2d8"))
	hp_fill = _rect(Vector2(39, 21), Vector2(HP_W, 11), Color("#5cc870"))
	hp_shine = _rect(Vector2(39, 21), Vector2(HP_W, 2), Color(1, 1, 1, 0.28))
	_rect(Vector2(39, 30), Vector2(HP_W, 2), Color(0, 0, 0, 0.22))
	hp_text = PixelFont.label("", "small", Color.WHITE, Color("#1a1420"))
	hp_text.position = Vector2(39, 21)
	hp_text.size = Vector2(HP_W, 11)
	hp_text.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_cur.add_child(hp_text)


func _build_skill() -> void:
	q_panel = UiKit.panel("panel", Vector2(145, 4), Vector2(34, 34))
	_cur.add_child(q_panel)
	q_icon = UiKit.icon("skill_warrior", Vector2(153, 12))
	_cur.add_child(q_icon)
	q_cover = _rect(Vector2(147, 6), Vector2(30, 30), Color(0.04, 0.03, 0.07, 0.72))
	q_sec = PixelFont.label("", "big", Color("#ffffff"), Color("#1a1420"))
	q_sec.position = Vector2(147, 13)
	q_sec.size = Vector2(30, 14)
	q_sec.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_cur.add_child(q_sec)
	q_flash = _rect(Vector2(147, 6), Vector2(30, 30), Color(1, 0.95, 0.75, 0.0))
	var k := UiKit.key("Q")
	k.position = Vector2(169, 26)
	_cur.add_child(k)
	q_name = PixelFont.label("", "small", Color(1, 1, 1, 0.75))
	q_name.position = Vector2(138, 41)
	q_name.size = Vector2(48, 10)
	q_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_cur.add_child(q_name)


func _build_right() -> void:
	rp = UiKit.panel("panel", Vector2(PixelView.GW - 80, 4), Vector2(76, 34))
	_cur.add_child(rp)
	hearts = Control.new()
	hearts.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_cur.add_child(hearts)
	coin = UiKit.icon("coin", Vector2(0, 0))
	_cur.add_child(coin)
	gold_l = PixelFont.label("0", "big", Color("#ffd860"))
	_cur.add_child(gold_l)


## 소비 칸 · 궁극기 칸 · 장비 5칸 · 알림
func _build_extra() -> void:
	_cur.add_child(UiKit.panel("panel", Vector2(182, 4), Vector2(34, 34)))
	c_icon = UiKit.icon("potion", Vector2(191, 13))
	c_icon.texture = null
	_cur.add_child(c_icon)
	c_n = PixelFont.label("", "small", Color.WHITE, Color("#1a1420"))
	c_n.position = Vector2(186, 6)
	_cur.add_child(c_n)
	var kf := UiKit.key("F")
	kf.position = Vector2(206, 26)
	_cur.add_child(kf)
	u_panel = UiKit.panel("panel", Vector2(219, 4), Vector2(34, 34))
	_cur.add_child(u_panel)
	u_fill = _rect(Vector2(221, 36), Vector2(30, 0), Color(0.35, 0.85, 1.0, 0.35))
	u_icon = UiKit.icon("ult_warrior", Vector2(227, 12))
	_cur.add_child(u_icon)
	var kr := UiKit.key("R")
	kr.position = Vector2(243, 26)
	_cur.add_child(kr)
	for i in Items.SLOTS:
		var x := 4 + i * 21
		_cur.add_child(UiKit.panel("panel_dark", Vector2(x, 41), Vector2(20, 20)))
		var ic := TextureRect.new()
		ic.position = Vector2(x + 2, 43)
		ic.size = Vector2(16, 16)
		ic.mouse_filter = Control.MOUSE_FILTER_IGNORE
		ic.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		_cur.add_child(ic)
		var ul := _rect(Vector2(x + 2, 58), Vector2(16, 1), Color(0, 0, 0, 0))
		item_slots.append([ic, ul])
	var keep := _cur
	_begin("toast")
	toast_box = Control.new()
	toast_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	toast_box.modulate.a = 0.0
	_cur.add_child(toast_box)
	_cur = keep
	msg_l = PixelFont.label("", "big", Color.WHITE)
	msg_l.position = Vector2(0, 78)
	msg_l.size = Vector2(PixelView.GW, 14)
	msg_l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	msg_l.add_theme_color_override("font_outline_color", Color("#1a1420"))
	msg_l.add_theme_constant_override("outline_size", 4)
	_root.add_child(msg_l)


static func _item_tex(id: String) -> AtlasTexture:
	var a := BB.atlas(ItemDrop.ATLAS)
	var r: Array = a["rects"].get(id, [0, 0, 16, 16])
	var t := AtlasTexture.new()
	t.atlas = a["tex"]
	t.region = Rect2(r[0], r[1], r[2], r[3])
	return t


## 아이템을 얻으면 왼쪽에 잠깐: 그림 · 이름(희귀도 색) · 등급·종류 · 효과 · 위험
func toast_item(id: String) -> void:
	_toast_q.append(id)
	if _toast_q.size() == 1:
		_show_toast()


func _show_toast() -> void:
	for c in toast_box.get_children():
		c.queue_free()
	if _toast_q.is_empty():
		return
	var id: String = _toast_q[0]
	var it := Items.get_item(id)
	var w := 196.0
	var f := PixelFont.get_font("small")
	var fs := PixelFont.size_of("small")
	var desc := String(it.get("desc", ""))
	var risk := String(it.get("risk", ""))
	if risk != "":
		risk = "위험: " + risk
	var dn := _lines(f, fs, desc, w - 12.0)
	var rn := 0 if risk == "" else _lines(f, fs, risk, w - 12.0)
	var h := 31.0 + (dn + rn) * 11.0 + 5.0
	toast_box.position = Vector2(4, PixelView.GH - 34 - h)      # 왼쪽 아래 (가운데 힌트·보스바와 겹치지 않게)
	toast_box.add_child(UiKit.panel("panel", Vector2.ZERO, Vector2(w, h)))
	var slot := UiKit.panel("panel_dark", Vector2(5, 5), Vector2(22, 22))
	toast_box.add_child(slot)
	var ic := TextureRect.new()
	ic.texture = _item_tex(id)
	ic.position = Vector2(8, 8)
	ic.mouse_filter = Control.MOUSE_FILTER_IGNORE
	toast_box.add_child(ic)
	var nl := PixelFont.label(String(it.get("name", id)), "big", Items.color(id))
	nl.position = Vector2(31, 3)
	toast_box.add_child(nl)
	var ll := PixelFont.label(Items.line(id), "small", Color(1, 1, 1, 0.6))
	ll.position = Vector2(31, 16)
	toast_box.add_child(ll)
	var dl := PixelFont.label(desc, "small", Color("#fff4d6"))
	dl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART          # 줄바꿈을 먼저 켜야 폭이 글 길이로 늘어나지 않는다
	dl.position = Vector2(6, 30)
	dl.size = Vector2(w - 12, dn * 11)
	toast_box.add_child(dl)
	if risk != "":
		var rl := PixelFont.label(risk, "small", Color("#ff8a7a"))
		rl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		rl.position = Vector2(6, 30 + dn * 11)
		rl.size = Vector2(w - 12, rn * 11)
		toast_box.add_child(rl)
	_toast_len = 4.2 if _toast_q.size() <= 1 else 2.4      # 줄 서 있으면 빨리 넘긴다
	_toast_t = _toast_len


## 낱말 단위 줄바꿈했을 때 몇 줄인가
static func _lines(f: Font, fs: int, text: String, width: float) -> int:
	var n := 1
	var cur := 0.0
	var sp := f.get_string_size(" ", HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x
	for word in text.split(" "):
		var ww := f.get_string_size(word, HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x
		if cur > 0.0 and cur + sp + ww > width:
			n += 1
			cur = ww
		else:
			cur += (sp if cur > 0.0 else 0.0) + ww
	return n


## 짧은 글 알림 (가운데 위)
func toast_text(text: String, col := Color.WHITE) -> void:
	msg_l.text = text
	msg_l.add_theme_color_override("font_color", col)
	_msg_t = 2.2


## 보스 체력바 (아래 가운데) + 위험 게이지
func _build_boss() -> void:
	boss_panel = Control.new()
	boss_panel.position = Vector2((PixelView.GW - BOSS_W) / 2.0 - 8, PixelView.GH - 44)
	boss_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_cur.add_child(boss_panel)
	boss_panel.add_child(UiKit.panel("panel", Vector2(0, 0), Vector2(BOSS_W + 16, 40)))
	boss_name = PixelFont.label("", "big", Color("#f0d8ff"))
	boss_name.position = Vector2(0, 2)
	boss_name.size = Vector2(BOSS_W + 16, 14)
	boss_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	boss_panel.add_child(boss_name)
	_rect_in(boss_panel, Vector2(7, 17), Vector2(BOSS_W + 2, 9), Color("#0e0b12"))
	_rect_in(boss_panel, Vector2(8, 18), Vector2(BOSS_W, 7), Color("#2a2030"))
	boss_ghost = _rect_in(boss_panel, Vector2(8, 18), Vector2(BOSS_W, 7), Color("#fff2d8"))
	boss_fill = _rect_in(boss_panel, Vector2(8, 18), Vector2(BOSS_W, 7), Color("#b04ae0"))
	_rect_in(boss_panel, Vector2(8, 18), Vector2(BOSS_W, 2), Color(1, 1, 1, 0.25))
	var dl := PixelFont.label("위험", "small", Color("#c8ff9a"))
	dl.position = Vector2(10, 26)
	boss_panel.add_child(dl)
	_rect_in(boss_panel, Vector2(36, 29), Vector2(BOSS_W - 30, 5), Color("#0e0b12"))
	danger_fill = _rect_in(boss_panel, Vector2(37, 30), Vector2(0, 3), Color("#9aff5a"))
	boss_panel.visible = false


func _rect_in(parent: Control, p: Vector2, s: Vector2, c: Color) -> ColorRect:
	var r := ColorRect.new()
	r.position = p
	r.size = s
	r.color = c
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(r)
	return r


func _rect(p: Vector2, s: Vector2, c: Color) -> ColorRect:
	return _rect_in(_cur, p, s, c)


# ------------------------------------------------------------------ 바깥에서 부르는 것
func banner(text: String, t: float) -> void:
	center_l.text = text
	_banner_t = t


## 상황 힌트: pairs = [["WASD", "이동"], …]. 같은 게 줄 서 있으면 무시
func hint(pairs: Array, dur := 5.0) -> void:
	for h in _hint_queue:
		if h[0] == pairs:
			return
	_hint_queue.append([pairs, dur])
	if _hint_queue.size() == 1 and _hint_t <= 0.0:
		_next_hint()


func clear_hints() -> void:
	_hint_queue.clear()
	_hint_t = 0.0
	hint_box.modulate.a = 0.0


func _next_hint() -> void:
	for c in hint_box.get_children():
		c.queue_free()
	if _hint_queue.is_empty():
		return
	var h: Array = _hint_queue[0]
	var row := UiKit.hint_row(h[0])
	var w := row.size.x + 14
	hint_box.position = Vector2(roundf((PixelView.GW - w) / 2.0), PixelView.GH - 30)
	hint_box.add_child(UiKit.panel("panel_dark", Vector2.ZERO, Vector2(w, 23)))
	row.position = Vector2(7, 4)
	hint_box.add_child(row)
	_hint_t = float(h[1])


# ------------------------------------------------------------------ 매 프레임
func _process(dt: float) -> void:
	_hurt = move_toward(_hurt, 0.0, dt * 2.2)
	hurt_rect.modulate.a = _hurt if not Prefs.reduce_flash else _hurt * 0.4
	_tick_hint(dt)
	_tick_toast(dt)
	_feed_tick(dt)
	if _root.visible:
		_mm_tick(dt)
		_party_tick()
	if player == null or not is_instance_valid(player):
		return
	var J := player.J
	if _shown_job != player.job:
		_shown_job = player.job
		portrait.texture = UiKit.tex("job_" + player.job)
		q_icon.texture = UiKit.tex("skill_" + player.job)
		name_l.text = String(J["name"])
		var f := PixelFont.get_font("big")
		var nw := f.get_string_size(name_l.text, HORIZONTAL_ALIGNMENT_LEFT, -1, PixelFont.size_of("big")).x
		gem.position.x = roundf(39 + nw + 5)
		passive_l.position.x = gem.position.x + 9
		passive_l.text = String(J["passive"]["name"])
		q_name.text = String(J["skill"]["name"])
		_qname_t = 4.0
		u_icon.texture = UiKit.tex("ult_" + player.job)
	# 체력
	var r := player.ratio()
	_ghost = move_toward(_ghost, r, dt * 0.7) if _ghost > r else r
	hp_fill.size.x = roundf(HP_W * r)
	hp_shine.size.x = hp_fill.size.x
	hp_ghost.size.x = roundf(HP_W * _ghost)
	var low := r <= 0.25
	hp_fill.color = Color("#5cc870") if r > 0.5 else (Color("#e8c050") if not low else Color("#e0504a"))
	if low and int(Time.get_ticks_msec() / 220) % 2 == 0:
		hp_fill.color = Color("#ff8a7a")
	hp_text.text = "%d / %d" % [ceili(player.hp), roundi(player.max_hp())]
	# 스킬
	var s: Dictionary = J["skill"]
	var cd := clampf(player.skill_cd / float(s["cd"]), 0.0, 1.0)
	q_cover.size.y = roundf(30.0 * cd)
	q_sec.text = ("%d" % ceili(player.skill_cd)) if player.skill_cd > 0.0 else ""
	if _was_cd and cd <= 0.0:
		_ready_t = 0.35
		Audio.play("ui_tick")                   # 스킬 준비됨 (작게 딸깍)
	_was_cd = cd > 0.0
	_ready_t = maxf(0.0, _ready_t - dt)
	q_flash.color.a = _ready_t / 0.35 * 0.7
	q_panel.texture = UiKit.tex("panel_hi" if _ready_t > 0.0 else "panel")
	q_icon.modulate = Color(1, 1, 1, 1) if cd <= 0.0 else Color(0.6, 0.6, 0.7, 1)
	# 궁극기 게이지
	var uk := clampf(player.ult / DB.ULT_MAX, 0.0, 1.0)
	u_fill.size.y = roundf(30.0 * uk)
	u_fill.position.y = 36.0 - u_fill.size.y
	var full := uk >= 1.0
	if full and not _ult_full:
		Audio.play("ui_confirm")
	_ult_full = full
	var ublink := full and int(Time.get_ticks_msec() / 250) % 2 == 0
	u_panel.texture = UiKit.tex("panel_hi" if ublink else "panel")
	u_fill.color = Color(1.0, 0.85, 0.35, 0.45) if full else Color(0.35, 0.85, 1.0, 0.35)
	u_icon.modulate = Color.WHITE if full else Color(0.55, 0.55, 0.65)
	# 소비 · 장비 (바뀔 때만 다시 칠한다)
	if player.inv:
		var sig := "%s|%s|%d" % [",".join(player.inv.slots), player.inv.cons, player.inv.cons_n]
		if sig != _bag_sig:
			_bag_sig = sig
			c_icon.texture = _item_tex(player.inv.cons) if player.inv.cons != "" else null
			c_n.text = ("×%d" % player.inv.cons_n) if player.inv.cons_n > 1 else ""
			for i in Items.SLOTS:
				var ic: TextureRect = item_slots[i][0]
				var ul: ColorRect = item_slots[i][1]
				if i < player.inv.slots.size():
					ic.texture = _item_tex(player.inv.slots[i])
					ul.color = Items.color(player.inv.slots[i])
				else:
					ic.texture = null
					ul.color = Color(0, 0, 0, 0)
	_qname_t = maxf(0.0, _qname_t - dt)
	q_name.modulate.a = clampf(_qname_t / 0.6, 0.0, 1.0)
	# 오른쪽 위
	if party:
		_max_lives = maxi(_max_lives, party.lives)
		if _lives_shown != party.lives:
			_lives_shown = party.lives
			_draw_hearts()
		if _gold_shown != party.gold:
			if _gold_shown >= 0 and party.gold > _gold_shown:
				_gold_bump = 0.18
			_gold_shown = party.gold
			gold_l.text = str(party.gold)
		_gold_bump = maxf(0.0, _gold_bump - dt)
		var by := -2.0 if _gold_bump > 0.09 else (-1.0 if _gold_bump > 0.0 else 0.0)
		coin.position = Vector2(rp.position.x + 7, 23 + by)
		gold_l.position = Vector2(rp.position.x + 18, 19 + by)
	# 보스
	boss_panel.visible = boss != null and is_instance_valid(boss) and not boss.dead
	if boss_panel.visible:
		var br := boss.ratio()
		_bghost = move_toward(_bghost, br, dt * 0.5) if _bghost > br else br
		boss_fill.size.x = roundf(BOSS_W * br)
		boss_ghost.size.x = roundf(BOSS_W * _bghost)
		boss_fill.color = Color("#8a8a9a") if boss.shell else (Color("#e04a6a") if boss.enraged else Color("#b04ae0"))
		var tag := "  [껍질 — 기둥 위에서 등을 쳐라!]" if boss.shell else ("  [광폭화]" if boss.enraged else "")
		if boss.state == "stun":
			tag = "  [기절!]"
		boss_name.text = "부화의 여왕" + tag
		danger_fill.size.x = roundf((BOSS_W - 32) * boss.danger)
		var blink := boss.danger > 0.8 and int(Time.get_ticks_msec() / 150) % 2 == 0
		danger_fill.color = Color("#ffffff") if blink else Color("#9aff5a")
		hint_box.position.y = PixelView.GH - 74
	elif hint_box.position.y < PixelView.GH - 40:
		hint_box.position.y = PixelView.GH - 30
	# 가운데 글자
	if _banner_t > 0.0:
		_banner_t -= dt
		if _banner_t <= 0.0:
			center_l.text = ""
		return
	if party:
		if party.over:
			center_l.text = "전멸..."
		elif player.dead:
			center_l.text = "쓰러졌다!  곧 일어난다"
		else:
			center_l.text = ""


## 하트: 남은 수만큼 빨강, 쓴 만큼 빈 하트 (6개 넘으면 하트 하나 + ×N)
func _draw_hearts() -> void:
	for c in hearts.get_children():
		c.queue_free()
	var n := _max_lives
	var x0 := rp.position.x + 7
	if n <= 6:
		for i in n:
			hearts.add_child(UiKit.icon("heart" if i < party.lives else "heart_empty", Vector2(x0 + i * 10, 9)))
	else:
		hearts.add_child(UiKit.icon("heart", Vector2(x0, 9)))
		var l := PixelFont.label("× %d" % party.lives, "big", Color("#ff8a8a"))
		l.position = Vector2(x0 + 12, 5)
		hearts.add_child(l)


func _tick_toast(dt: float) -> void:
	if _toast_t > 0.0:
		_toast_t -= dt
		toast_box.modulate.a = clampf(minf(_toast_t / 0.4, (_toast_len - _toast_t) / 0.15), 0.0, 1.0)
		toast_box.position.x = 4.0 - roundf(maxf(0.0, 0.15 - (_toast_len - _toast_t)) / 0.15 * 12.0)
		if _toast_t <= 0.0:
			_toast_q.pop_front()
			toast_box.modulate.a = 0.0
			_show_toast()
	if _msg_t > 0.0:
		_msg_t -= dt
		msg_l.modulate.a = clampf(_msg_t / 0.4, 0.0, 1.0)
		if _msg_t <= 0.0:
			msg_l.text = ""


func _tick_hint(dt: float) -> void:
	if _hint_t > 0.0:
		_hint_t -= dt
		hint_box.modulate.a = minf(1.0, minf(_hint_t / 0.4, hint_box.modulate.a + dt * 4.0))
		if _hint_t <= 0.0:
			_hint_queue.pop_front()
			hint_box.modulate.a = 0.0
			_next_hint()


# ------------------------------------------------------------------ 미니맵 · 알림 · 파티 (UI 시스템.md)
var map: Region                    # main 이 지역을 불러올 때 넣는다
var mm_panel: NinePatchRect
var mm_tex: TextureRect
var mm_marks: Control
var _mm_base: Image                # 지역 색 (1m = 1px)
var _mm_seen: PackedByteArray      # 가 본 곳 (1m 칸)
var _mm_img: Image
var _mm_t := 0.0
var _mm_scale := 1.0
var _mm_map: Region
const MM_W := 70
const MM_H := 52
const MM_COL := {0: Color("#5e7a48"), 1: Color("#4e6a40"), 2: Color("#8a7050"), 3: Color("#7a766e"), 4: Color("#30483a"), 5: Color("#a89a74")}


func _build_minimap() -> void:
	mm_panel = UiKit.panel("panel", Vector2(400, 41), Vector2(76, 58))
	_cur.add_child(mm_panel)
	mm_tex = TextureRect.new()
	mm_tex.position = Vector2(403, 44)
	mm_tex.mouse_filter = Control.MOUSE_FILTER_IGNORE
	mm_tex.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	_cur.add_child(mm_tex)
	mm_marks = Control.new()
	mm_marks.position = mm_tex.position
	mm_marks.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_cur.add_child(mm_marks)


## 지역이 바뀌면: 바탕 색 그림 (재질 · 높이) — 안 가 본 곳은 어둡게 덮는다
func _mm_rebuild() -> void:
	_mm_map = map
	if map == null:
		return
	var w := int(map.WM)
	var h := int(map.HM)
	_mm_scale = minf(float(MM_W) / w, float(MM_H) / h)
	_mm_base = Image.create(w, h, false, Image.FORMAT_RGBA8)
	var per := int(round(1.0 / Region.C))
	for z in h:
		for x in w:
			var gz := mini(z * per, map.gh - 1)
			var gx := mini(x * per, map.gw - 1)
			var c: Color = MM_COL.get(int(map.mats[gz][gx]), Color("#5e7a48"))
			var lv := int(map.heights[gz][gx])
			c = c.lightened(clampf(lv * 0.05, 0.0, 0.35))
			# 높이가 바뀌는 곳(절벽)은 진한 테
			if gx + per < map.gw and int(map.heights[gz][gx + per]) != lv or gz + per < map.gh and int(map.heights[mini(gz + per, map.gh - 1)][gx]) != lv:
				c = c.darkened(0.35)
			_mm_base.set_pixel(x, z, c)
	_mm_seen = PackedByteArray()
	_mm_seen.resize(w * h)
	_mm_img = Image.create(w, h, false, Image.FORMAT_RGBA8)
	_mm_t = 0.0
	mm_tex.size = Vector2(w, h) * _mm_scale
	var off := (Vector2(MM_W, MM_H) - mm_tex.size) * 0.5
	mm_tex.position = Vector2(403, 44) + off.floor()
	mm_marks.position = mm_tex.position


func _mm_tick(dt: float) -> void:
	if map != _mm_map:
		_mm_rebuild()
	if map == null or _mm_base == null:
		return
	_mm_t -= dt
	var w := _mm_base.get_width()
	var h := _mm_base.get_height()
	var players: Array = []
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player:
			players.append(f)
	if _mm_t <= 0.0:
		_mm_t = 0.25
		# 둘레 9m 를 '가 봄' 으로
		for p in players:
			var pp := (p as Node3D).global_position
			for z in range(maxi(0, int(pp.z) - 9), mini(h, int(pp.z) + 10)):
				for x in range(maxi(0, int(pp.x) - 9), mini(w, int(pp.x) + 10)):
					if Vector2(x - pp.x, z - pp.z).length() < 9.0:
						_mm_seen[z * w + x] = 1
		for z in h:
			for x in w:
				_mm_img.set_pixel(x, z, _mm_base.get_pixel(x, z) if _mm_seen[z * w + x] == 1 else Color("#16121e"))
		var big := _mm_img.duplicate() as Image
		big.resize(int(w * _mm_scale), int(h * _mm_scale), Image.INTERPOLATE_NEAREST)
		mm_tex.texture = ImageTexture.create_from_image(big)
		_mm_marks_rebuild(players)


func _mm_dot(p: Vector3, col: Color, sz := 2) -> void:
	var d := ColorRect.new()
	d.color = col
	d.size = Vector2(sz, sz)
	d.position = (Vector2(p.x, p.z) * _mm_scale - Vector2(sz, sz) * 0.5).round()
	d.mouse_filter = Control.MOUSE_FILTER_IGNORE
	mm_marks.add_child(d)


func _mm_seen_at(p: Vector3) -> bool:
	var w := _mm_base.get_width()
	var x := clampi(int(p.x), 0, w - 1)
	var z := clampi(int(p.z), 0, _mm_base.get_height() - 1)
	return _mm_seen[z * w + x] == 1


func _mm_marks_rebuild(players: Array) -> void:
	for c in mm_marks.get_children():
		c.queue_free()
	# 출구(노랑) · 상점(금) · 상자(갈색) · 보스(빨강) — 가 본 곳만
	for ex in map.exits:
		var r: Rect2 = ex[0]
		_mm_dot(Vector3(clampf(r.get_center().x, 1, map.WM - 1), 0, clampf(r.get_center().y, 1, map.HM - 1)), Color("#ffe060"), 3)
	for n in get_tree().get_nodes_in_group("interact"):
		var np := (n as Node3D).global_position
		if not _mm_seen_at(np):
			continue
		if n is Chest:
			_mm_dot(np, Color("#c8884a"))
		elif n is ShopStand:
			_mm_dot(np, Color("#ffd860"))
	if boss and is_instance_valid(boss) and not boss.dead:
		_mm_dot(boss.global_position, Color("#ff4a4a"), 3)
	for pg in get_tree().get_nodes_in_group("pings"):
		_mm_dot((pg as Node3D).global_position, (pg as PingMark).col, 3)
	for p in players:
		var pl := p as Player
		_mm_dot(pl.global_position, Color(0.5, 0.5, 0.5) if pl.dead else pl.team_color, 3)


# ---- 알림 (우측 상단): 아이템 · 보스 등장 · 쓰러짐/부활 · 업적
var feed: Control
var _feed: Array = []              # [Control, 남은 시간]


func _build_feed() -> void:
	feed = Control.new()
	feed.position = Vector2(326, 102)
	feed.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_cur.add_child(feed)


func notify(text: String, col := Color("#fff4d6"), icon: Texture2D = null) -> void:
	var row := Control.new()
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var f := PixelFont.get_font("small")
	var tw := f.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, PixelFont.size_of("small")).x
	var w := minf(150.0, tw + (22.0 if icon else 10.0))
	row.add_child(UiKit.panel("panel_dark", Vector2(150 - w, 0), Vector2(w, 16)))
	var x := 150 - w + 5
	if icon:
		var ic := TextureRect.new()
		ic.texture = icon
		ic.position = Vector2(x - 1, 0)
		ic.mouse_filter = Control.MOUSE_FILTER_IGNORE
		ic.scale = Vector2(1, 1)
		row.add_child(ic)
		x += 15
	var l := PixelFont.label(text, "small", col)
	l.position = Vector2(x, 2)
	l.size = Vector2(w - 8, 12)
	l.clip_text = true
	row.add_child(l)
	feed.add_child(row)
	_feed.push_front([row, 3.6])
	while _feed.size() > 4:
		(_feed.pop_back()[0] as Control).queue_free()


func _feed_tick(dt: float) -> void:
	for i in range(_feed.size() - 1, -1, -1):
		var e: Array = _feed[i]
		e[1] = float(e[1]) - dt
		var row := e[0] as Control
		row.position.y = lerpf(row.position.y, i * 17.0, 1.0 - exp(-14.0 * dt))
		row.position.y = roundf(row.position.y)
		row.modulate.a = clampf(float(e[1]) / 0.4, 0.0, 1.0)
		if float(e[1]) <= 0.0:
			row.queue_free()
			_feed.remove_at(i)


# ---- 파티 (왼쪽): 여럿일 때만 — 직업 · 이름 · 체력 · 쓰러짐
var party_box: Control
var _party_sig := ""


func _build_party() -> void:
	party_box = Control.new()
	party_box.position = Vector2(4, 66)
	party_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_cur.add_child(party_box)


func _party_tick() -> void:
	var ps: Array = []
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player:
			ps.append(f)
	ps.sort_custom(func(a, b): return (a as Player).slot < (b as Player).slot)
	party_box.visible = ps.size() > 1
	if ps.size() <= 1:
		return
	var sig := ""
	for p in ps:
		sig += "%d%s" % [(p as Player).slot, (p as Player).job]
	if sig != _party_sig:
		_party_sig = sig
		for c in party_box.get_children():
			c.queue_free()
		for i in ps.size():
			var row := Control.new()
			row.position = Vector2(0, i * 16)
			row.mouse_filter = Control.MOUSE_FILTER_IGNORE
			row.add_child(UiKit.panel("panel_dark", Vector2.ZERO, Vector2(100, 15)))
			var col := ColorRect.new()
			col.color = (ps[i] as Player).team_color
			col.position = Vector2(2, 2)
			col.size = Vector2(2, 11)
			row.add_child(col)
			var nl := PixelFont.label("", "small", Color.WHITE)
			nl.position = Vector2(7, 1)
			nl.size = Vector2(52, 12)
			nl.clip_text = true
			nl.name = "N"
			row.add_child(nl)
			var bb := ColorRect.new()
			bb.color = Color("#2a2030")
			bb.position = Vector2(60, 6)
			bb.size = Vector2(36, 3)
			row.add_child(bb)
			var bf := ColorRect.new()
			bf.name = "B"
			bf.position = Vector2(60, 6)
			bf.size = Vector2(36, 3)
			row.add_child(bf)
			party_box.add_child(row)
	for i in mini(ps.size(), party_box.get_child_count()):
		var p := ps[i] as Player
		var row := party_box.get_child(i)
		var nl := row.get_node("N") as Label
		nl.text = ("✕ " if p.dead else "") + p.nick
		nl.add_theme_color_override("font_color", Color("#ff8a7a") if p.dead else Color("#fff4d6"))
		var bf := row.get_node("B") as ColorRect
		bf.size.x = roundf(36.0 * p.ratio())
		bf.color = Color("#5cc870") if p.ratio() > 0.4 else (Color("#e8c050") if p.ratio() > 0.2 else Color("#e0504a"))


# ---- 시청자 투표 (돌발 이벤트) — 화면 위 가운데
var vote_box: Control
func vote_update(choices: Array, votes: Array, t: float, live: bool) -> void:
	if vote_box == null:
		vote_box = Control.new()
		vote_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
		_root.add_child(vote_box)
	for c in vote_box.get_children():
		c.queue_free()
	if choices.is_empty():
		vote_box.visible = false
		return
	vote_box.visible = true
	var w := 236.0
	var h := 20.0 + choices.size() * 13.0 + 4.0
	vote_box.position = Vector2(roundf((PixelView.GW - w) / 2.0), 40)
	vote_box.add_child(UiKit.panel("panel", Vector2.ZERO, Vector2(w, h)))
	var tl := PixelFont.label("시청자 투표! %s  %d초" % ["채팅 !1 !2 !3" if live else "(가짜 시청자)", ceili(t)], "small", Color("#ffd860"))
	tl.position = Vector2(6, 3)
	vote_box.add_child(tl)
	var total := 0
	for v in votes:
		total += int(v)
	for i in choices.size():
		var y := 18.0 + i * 13.0
		var k := float(votes[i]) / maxf(1.0, float(total))
		var bar := ColorRect.new()
		bar.color = Color(1.0, 0.85, 0.4, 0.25)
		bar.position = Vector2(6, y)
		bar.size = Vector2(roundf((w - 12) * k), 11)
		vote_box.add_child(bar)
		var l := PixelFont.label("!%d  %s" % [i + 1, EventDirector.EVENTS[choices[i]][0]], "small", Color("#fff4d6"))
		l.position = Vector2(8, y)
		vote_box.add_child(l)
		var n := PixelFont.label("%d표" % int(votes[i]), "small", Color("#ffd860"))
		n.position = Vector2(w - 40, y)
		vote_box.add_child(n)
