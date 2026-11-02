class_name ResultBoard
extends Control
## 결과 화면 (게임 시작과 선택, 직업, 플레이.md · 온라인 시스템.md)
##  · 위: 승리/전멸 · 난이도 · 시간 · 금화
##  · 상 카드: MVP · 가장 많은 피해 · 회복 · 처치 · 트롤왕(아군 피해) · 구조 · 수집 · 많이 쓰러짐
##  · 표: 파티원별 기록
##  · 아래: 이번 판에 연 업적
## members: [[slot, 이름, 색], …]  stats: Party.stats

const COLS := [["처치", "kills"], ["피해", "dmg"], ["회복", "heal"], ["팀킬", "ff"], ["구조", "saves"], ["아이템", "items"], ["쓰러짐", "deaths"]]
## 상: [제목, 기록, 색, 단위]
const AWARDS := [
	["가장 많은 피해", "dmg", "#ff9a6a", ""],
	["가장 많은 회복", "heal", "#7aff9a", ""],
	["가장 많은 처치", "kills", "#ffd860", "마리"],
	["트롤왕", "ff", "#ff7ad0", "팀킬"],
	["구조왕", "saves", "#8ad8ff", "번"],
	["수집왕", "items", "#c8a0ff", "개"],
	["오뚝이", "deaths", "#c8c8d0", "번 쓰러짐"],
]

var win := false
var members: Array = []
var stats := {}
var sub := ""
var new_ach: Array = []
var _t := 0.0
var _cards: Array = []
var _popped := 0


func _ready() -> void:
	size = Vector2(PixelView.GW, PixelView.GH)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	process_mode = Node.PROCESS_MODE_ALWAYS
	var W := PixelView.GW
	var head := PixelFont.label("승리!  부화의 여왕을 쓰러뜨렸다" if win else "전멸했다…", "big", Color("#ffe09a") if win else Color("#ff9a8a"))
	head.position = Vector2(0, 8)
	head.size = Vector2(W, 14)
	head.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_child(head)
	var sl := PixelFont.label(sub, "small", Color("#d8ccb8"))
	sl.position = Vector2(0, 24)
	sl.size = Vector2(W, 10)
	sl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_child(sl)
	_build_cards()
	_build_table()


func _val(slot: Variant, key: String) -> float:
	return float((stats.get(slot, {}) as Dictionary).get(key, 0))


## MVP 점수: 피해 + 회복(×1.2) + 처치·구조 가산 − 쓰러짐 감점
func _score(slot: Variant) -> float:
	return _val(slot, "dmg") + _val(slot, "heal") * 1.2 + _val(slot, "kills") * 12.0 + _val(slot, "saves") * 40.0 \
		+ _val(slot, "items") * 4.0 - _val(slot, "deaths") * 25.0


func _best(key: String) -> Array:
	var best: Array = []
	var bv := 0.0
	for m in members:
		var v := _val(m[0], key) if key != "mvp" else _score(m[0])
		if best.is_empty() or v > bv:
			best = m
			bv = v
	return [best, bv]


func _build_cards() -> void:
	var cards: Array = []
	var mvp := _best("mvp")
	cards.append(["MVP", mvp[0], "#ffc040", "%d점" % maxi(0, roundi(mvp[1]))])
	for a in AWARDS:
		if cards.size() >= 5:
			break
		var b := _best(String(a[1]))
		if float(b[1]) <= 0.0:
			continue
		cards.append([a[0], b[0], a[2], ("%d" % roundi(b[1])) + ((" " + String(a[3])) if String(a[3]) != "" else "")])
	var cw := 88
	var gap := 4
	var total := cards.size() * cw + (cards.size() - 1) * gap
	var x := roundf((PixelView.GW - total) / 2.0)
	for i in cards.size():
		var c: Array = cards[i]
		var at := Vector2(x + i * (cw + gap), 38)
		var p := UiKit.panel("panel_hi" if i == 0 else "panel", at, Vector2(cw, 42))
		add_child(p)
		var t := PixelFont.label(String(c[0]), "small", Color(String(c[2])))
		t.position = at + Vector2(0, 4)
		t.size = Vector2(cw, 10)
		t.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		add_child(t)
		var who: Array = c[1]
		var n := PixelFont.label(String(who[1]) if not who.is_empty() else "-", "big", who[2] if not who.is_empty() else Color.WHITE)
		n.position = at + Vector2(2, 14)
		n.size = Vector2(cw - 4, 14)
		n.clip_text = true
		n.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		add_child(n)
		var v := PixelFont.label(String(c[3]), "small", Color("#a89cb8"))
		v.position = at + Vector2(0, 29)
		v.size = Vector2(cw, 10)
		v.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		add_child(v)
		p.modulate.a = 0.0
		for l in [t, n, v]:
			(l as Control).modulate.a = 0.0
		_cards.append([p, t, n, v])


func _build_table() -> void:
	var W := PixelView.GW
	var x0 := 40.0
	var tw := W - 80.0
	var rows := members.size()
	var h := 16 + rows * 12 + 4
	var p := UiKit.panel("panel", Vector2(x0, 86), Vector2(tw, h))
	add_child(p)
	var name_w := 96.0
	var cw := (tw - name_w - 12.0) / COLS.size()
	for j in COLS.size():
		var hl := PixelFont.label(String(COLS[j][0]), "small", Color("#ffd860"))
		hl.position = Vector2(x0 + 6 + name_w + j * cw, 90)
		hl.size = Vector2(cw, 10)
		hl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		add_child(hl)
	var line := ColorRect.new()
	line.color = Color("#5a4a6e")
	line.position = Vector2(x0 + 6, 101)
	line.size = Vector2(tw - 12, 1)
	add_child(line)
	for i in rows:
		var m: Array = members[i]
		var y := 104 + i * 12
		var nl := PixelFont.label(String(m[1]), "small", m[2])
		nl.position = Vector2(x0 + 8, y)
		nl.size = Vector2(name_w - 4, 10)
		nl.clip_text = true
		add_child(nl)
		for j in COLS.size():
			var key := String(COLS[j][1])
			var v := _val(m[0], key)
			var best := members.size() > 1 and v > 0.0 and is_equal_approx(v, float(_best(key)[1]))
			var vl := PixelFont.label("%d" % roundi(v), "small", Color("#fff4d6") if best else Color("#a89cb8"))
			vl.position = Vector2(x0 + 6 + name_w + j * cw, y)
			vl.size = Vector2(cw, 10)
			vl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
			add_child(vl)
	if not new_ach.is_empty():
		var al := PixelFont.label("새 업적: " + ", ".join(new_ach), "small", Color("#ffc040"))
		al.position = Vector2(x0, 86 + h + 4)
		al.size = Vector2(tw, 10)
		al.clip_text = true
		al.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		add_child(al)


## 상 카드가 하나씩 '착' 붙는다
func _process(dt: float) -> void:
	_t += dt
	for i in _cards.size():
		var k := clampf((_t - 0.25 - i * 0.22) / 0.18, 0.0, 1.0)
		for c in _cards[i]:
			(c as Control).modulate.a = k
		if k > 0.0 and i >= _popped:
			_popped = i + 1
			Audio.play("ui_click")
