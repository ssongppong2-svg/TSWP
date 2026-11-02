class_name Hud
extends Control
## 도트 HUD (480x270 위에 그린다 · 부품은 UiKit 아틀라스)
##  왼쪽 위: 직업 초상(아이콘) · 이름 · 패시브(보석) · 체력바(숫자는 바 안, 막 깎인 부분은 하얗게 남았다 줄어듦)
##  그 옆: 스킬 칸 (스킬 그림 · 쿨타임은 위에서부터 걷힘 · 남은 초 · Q 키캡 · 다 차면 금테 번쩍)
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


func _tick_hint(dt: float) -> void:
	if _hint_t > 0.0:
		_hint_t -= dt
		hint_box.modulate.a = minf(1.0, minf(_hint_t / 0.4, hint_box.modulate.a + dt * 4.0))
		if _hint_t <= 0.0:
			_hint_queue.pop_front()
			hint_box.modulate.a = 0.0
			_next_hint()
