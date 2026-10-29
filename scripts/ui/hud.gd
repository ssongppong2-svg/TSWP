class_name Hud
extends Control
## 도트 HUD (480x270 위에 그린다)
##  왼쪽 위: 직업 이름 · 체력바(막 깎인 부분은 하얗게 남았다 줄어듦) · Q 스킬 칸(쿨타임은 아래부터 차오름) · 패시브 이름
##  오른쪽 위: 남은 부활(하트) · 금화

var player: Player
var party: Party
var boss: Queen
var boss_panel: Control
var boss_name: Label
var boss_fill: ColorRect
var boss_ghost: ColorRect
var danger_fill: ColorRect
var danger_l: Label
var _bghost := 1.0
var _banner_t := 0.0
const BOSS_W := 220
var name_l: Label
var hp_back: ColorRect
var hp_ghost: ColorRect
var hp_fill: ColorRect
var hp_text: Label
var q_box: ColorRect
var q_fill: ColorRect
var q_text: Label
var q_name: Label
var passive_l: Label
var lives_l: Label
var gold_l: Label
var center_l: Label
var _ghost := 1.0
const HP_W := 72


func _ready() -> void:
	size = Vector2(PixelView.GW, PixelView.GH)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	# 바탕 판 (반투명)
	var panel := ColorRect.new()
	panel.color = Color(0.08, 0.07, 0.1, 0.55)
	panel.position = Vector2(4, 4)
	panel.size = Vector2(HP_W + 34, 30)
	add_child(panel)
	name_l = PixelFont.label("", "small", Color("#fff4d6"))
	name_l.position = Vector2(8, 5)
	add_child(name_l)
	hp_back = _rect(Vector2(8, 17), Vector2(HP_W + 2, 7), Color("#1a1620"))
	hp_ghost = _rect(Vector2(9, 18), Vector2(HP_W, 5), Color("#fff2d8"))
	hp_fill = _rect(Vector2(9, 18), Vector2(HP_W, 5), Color("#6ad07a"))
	_rect(Vector2(9, 18), Vector2(HP_W, 1), Color(1, 1, 1, 0.25))       # 위쪽 빛줄
	hp_text = PixelFont.label("", "small", Color.WHITE)
	hp_text.position = Vector2(9, 23)
	add_child(hp_text)
	# Q 칸
	q_box = _rect(Vector2(HP_W + 14, 8), Vector2(20, 20), Color("#2a2434"))
	_rect(Vector2(HP_W + 15, 9), Vector2(18, 18), Color("#4a405e"))
	q_fill = _rect(Vector2(HP_W + 15, 9), Vector2(18, 18), Color(0.05, 0.04, 0.08, 0.75))
	q_text = PixelFont.label("Q", "big", Color("#ffe9a8"))
	q_text.position = Vector2(HP_W + 20, 10)
	add_child(q_text)
	q_name = PixelFont.label("", "small", Color(1, 1, 1, 0.8))
	q_name.position = Vector2(8, 36)
	add_child(q_name)
	passive_l = PixelFont.label("", "small", Color(0.8, 0.9, 1.0, 0.7))
	passive_l.position = Vector2(8, 47)
	add_child(passive_l)
	# 오른쪽 위
	lives_l = PixelFont.label("", "big", Color("#ff7a6a"))
	lives_l.position = Vector2(PixelView.GW - 92, 6)
	lives_l.size = Vector2(86, 14)
	lives_l.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	add_child(lives_l)
	gold_l = PixelFont.label("", "big", Color("#ffd860"))
	gold_l.position = Vector2(PixelView.GW - 92, 20)
	gold_l.size = Vector2(86, 14)
	gold_l.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	add_child(gold_l)
	_build_boss()
	center_l = PixelFont.label("", "big", Color("#fff4d6"))
	center_l.position = Vector2(0, 110)
	center_l.size = Vector2(PixelView.GW, 30)
	center_l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_child(center_l)


## 보스 체력바 (아래 가운데) + 위험 게이지
func _build_boss() -> void:
	boss_panel = Control.new()
	boss_panel.position = Vector2((PixelView.GW - BOSS_W) / 2.0, PixelView.GH - 46)
	boss_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(boss_panel)
	boss_name = PixelFont.label("", "big", Color("#f0d8ff"))
	boss_name.size = Vector2(BOSS_W, 14)
	boss_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	boss_panel.add_child(boss_name)
	_rect_in(boss_panel, Vector2(0, 15), Vector2(BOSS_W + 2, 8), Color("#140e1a"))
	boss_ghost = _rect_in(boss_panel, Vector2(1, 16), Vector2(BOSS_W, 6), Color("#fff2d8"))
	boss_fill = _rect_in(boss_panel, Vector2(1, 16), Vector2(BOSS_W, 6), Color("#b04ae0"))
	_rect_in(boss_panel, Vector2(1, 16), Vector2(BOSS_W, 1), Color(1, 1, 1, 0.25))
	_rect_in(boss_panel, Vector2(40, 25), Vector2(BOSS_W - 78, 4), Color("#140e1a"))
	danger_fill = _rect_in(boss_panel, Vector2(41, 26), Vector2(0, 2), Color("#9aff5a"))
	danger_l = PixelFont.label("위험", "small", Color("#c8ff9a"))
	danger_l.position = Vector2(14, 21)
	boss_panel.add_child(danger_l)
	boss_panel.visible = false


func _rect_in(parent: Control, p: Vector2, s: Vector2, c: Color) -> ColorRect:
	var r := ColorRect.new()
	r.position = p
	r.size = s
	r.color = c
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	parent.add_child(r)
	return r


func banner(text: String, t: float) -> void:
	center_l.text = text
	_banner_t = t


func _rect(p: Vector2, s: Vector2, c: Color) -> ColorRect:
	var r := ColorRect.new()
	r.position = p
	r.size = s
	r.color = c
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(r)
	return r


func _process(dt: float) -> void:
	if player == null or not is_instance_valid(player):
		return
	var J := player.J
	name_l.text = "%s" % J["name"]
	var r := player.ratio()
	_ghost = move_toward(_ghost, r, dt * 0.7) if _ghost > r else r
	hp_fill.size.x = roundf(HP_W * r)
	hp_ghost.size.x = roundf(HP_W * _ghost)
	hp_fill.color = Color("#6ad07a") if r > 0.5 else (Color("#e8c050") if r > 0.25 else Color("#e0504a"))
	hp_text.text = "%d / %d" % [ceili(player.hp), roundi(player.max_hp())]
	var s: Dictionary = J["skill"]
	var cd := player.skill_cd / float(s["cd"])
	q_fill.size.y = roundf(18.0 * cd)
	q_text.modulate = Color(1, 1, 1, 0.45 if cd > 0.0 else 1.0)
	q_name.text = "Q·우클릭  %s%s" % [s["name"], ("  %.1f" % player.skill_cd) if player.skill_cd > 0.0 else ""]
	passive_l.text = "◆ %s" % J["passive"]["name"]
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
		danger_fill.size.x = roundf((BOSS_W - 80) * boss.danger)
		var blink := boss.danger > 0.8 and int(Time.get_ticks_msec() / 150) % 2 == 0
		danger_fill.color = Color("#ffffff") if blink else Color("#9aff5a")
	if party:
		lives_l.text = "♥ %d" % party.lives
		gold_l.text = "● %d" % party.gold
	if _banner_t > 0.0:
		_banner_t -= dt
		if _banner_t <= 0.0:
			center_l.text = ""
		return
	if party:
		if party.over:
			center_l.text = "전멸...  (1 키: 캠프로)"
		elif player.dead:
			center_l.text = "쓰러졌다!  곧 일어난다"
		else:
			center_l.text = ""
