class_name Hud
extends Control
## 도트 HUD (480x270 위에 그린다 · 부품은 UiKit 아틀라스)
##  왼쪽 위: 직업 초상(아이콘) · 이름 · 패시브(보석) · 체력바(숫자는 바 안, 막 깎인 부분은 하얗게 남았다 줄어듦)
##  그 옆: 스킬 칸 (스킬 그림 · 쿨타임은 위에서부터 걷힘 · 남은 초 · Q 키캡 · 다 차면 금테 번쩍)
##        · 소비 아이템 칸 (F · 개수) · 궁극기 칸 (R · 아래부터 차오르는 게이지, 가득 차면 금빛 깜빡)
##  그 아래: 장비 5칸 (희귀도 색 밑줄) · 새로 얻으면 왼쪽에 아이템 알림(이름·효과·위험)
##  오른쪽 위: 남은 부활(하트) · 금화 (늘면 톡 튐)
##  아래 가운데: 상황 힌트(키캡 + 설명, 잠깐 떴다 사라짐) · 보스 체력바 + 위험 게이지

var player: Player
var party: Party
var boss: Queen

const HP_W := 92
const BOSS_W := 220

var _root: Control                 # HUD 전체 (타이틀·메뉴에서 숨김)
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
	_build_main()
	_build_skill()
	_build_extra()
	_build_right()
	_build_boss()
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


## 타이틀·메뉴에서 HUD 숨기기 (피격 테두리는 남김)
func set_shown(on: bool) -> void:
	_root.visible = on


# ------------------------------------------------------------------ 만들기
func _build_main() -> void:
	_root.add_child(UiKit.panel("panel", Vector2(4, 4), Vector2(138, 34)))
	_root.add_child(UiKit.panel("panel_dark", Vector2(7, 7), Vector2(28, 28)))
	portrait = UiKit.icon("job_warrior", Vector2(12, 12))
	_root.add_child(portrait)
	name_l = PixelFont.label("", "big", Color("#fff4d6"))
	name_l.position = Vector2(39, 5)
	_root.add_child(name_l)
	gem = UiKit.icon("gem", Vector2(70, 9))
	_root.add_child(gem)
	passive_l = PixelFont.label("", "small", Color("#b8e4ff"))
	passive_l.position = Vector2(79, 6)
	_root.add_child(passive_l)
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
	_root.add_child(hp_text)


func _build_skill() -> void:
	q_panel = UiKit.panel("panel", Vector2(145, 4), Vector2(34, 34))
	_root.add_child(q_panel)
	q_icon = UiKit.icon("skill_warrior", Vector2(153, 12))
	_root.add_child(q_icon)
	q_cover = _rect(Vector2(147, 6), Vector2(30, 30), Color(0.04, 0.03, 0.07, 0.72))
	q_sec = PixelFont.label("", "big", Color("#ffffff"), Color("#1a1420"))
	q_sec.position = Vector2(147, 13)
	q_sec.size = Vector2(30, 14)
	q_sec.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_root.add_child(q_sec)
	q_flash = _rect(Vector2(147, 6), Vector2(30, 30), Color(1, 0.95, 0.75, 0.0))
	var k := UiKit.key("Q")
	k.position = Vector2(169, 26)
	_root.add_child(k)
	q_name = PixelFont.label("", "small", Color(1, 1, 1, 0.75))
	q_name.position = Vector2(138, 41)
	q_name.size = Vector2(48, 10)
	q_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_root.add_child(q_name)


func _build_right() -> void:
	rp = UiKit.panel("panel", Vector2(PixelView.GW - 80, 4), Vector2(76, 34))
	_root.add_child(rp)
	hearts = Control.new()
	hearts.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_root.add_child(hearts)
	coin = UiKit.icon("coin", Vector2(0, 0))
	_root.add_child(coin)
	gold_l = PixelFont.label("0", "big", Color("#ffd860"))
	_root.add_child(gold_l)


## 소비 칸 · 궁극기 칸 · 장비 5칸 · 알림
func _build_extra() -> void:
	_root.add_child(UiKit.panel("panel", Vector2(182, 4), Vector2(34, 34)))
	c_icon = UiKit.icon("potion", Vector2(191, 13))
	c_icon.texture = null
	_root.add_child(c_icon)
	c_n = PixelFont.label("", "small", Color.WHITE, Color("#1a1420"))
	c_n.position = Vector2(186, 6)
	_root.add_child(c_n)
	var kf := UiKit.key("F")
	kf.position = Vector2(206, 26)
	_root.add_child(kf)
	u_panel = UiKit.panel("panel", Vector2(219, 4), Vector2(34, 34))
	_root.add_child(u_panel)
	u_fill = _rect(Vector2(221, 36), Vector2(30, 0), Color(0.35, 0.85, 1.0, 0.35))
	u_icon = UiKit.icon("ult_warrior", Vector2(227, 12))
	_root.add_child(u_icon)
	var kr := UiKit.key("R")
	kr.position = Vector2(243, 26)
	_root.add_child(kr)
	for i in Items.SLOTS:
		var x := 4 + i * 21
		_root.add_child(UiKit.panel("panel_dark", Vector2(x, 41), Vector2(20, 20)))
		var ic := TextureRect.new()
		ic.position = Vector2(x + 2, 43)
		ic.size = Vector2(16, 16)
		ic.mouse_filter = Control.MOUSE_FILTER_IGNORE
		ic.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		_root.add_child(ic)
		var ul := _rect(Vector2(x + 2, 58), Vector2(16, 1), Color(0, 0, 0, 0))
		item_slots.append([ic, ul])
	toast_box = Control.new()
	toast_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	toast_box.modulate.a = 0.0
	_root.add_child(toast_box)
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
	_toast_t = 4.2


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
	_root.add_child(boss_panel)
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
	return _rect_in(_root, p, s, c)


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
		toast_box.modulate.a = clampf(minf(_toast_t / 0.4, (4.2 - _toast_t) / 0.15), 0.0, 1.0)
		toast_box.position.x = 4.0 - roundf(maxf(0.0, 0.15 - (4.2 - _toast_t)) / 0.15 * 12.0)
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
