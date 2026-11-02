class_name Achievements
extends RefCounted
## 업적 · 칭호 (업적 시스템.md · 이름 시스템.md) — 이 컴퓨터에 저장 (user://achievements.cfg)
##  · 누적 기록(stats)이 기준을 넘으면 열린다 → 우측 상단 알림 + 칭호
##  · 칭호는 꾸미기에서 골라 이름 앞에 [칭호] 로 붙인다 (전설 칭호 금색)

const PATH := "user://achievements.cfg"
## id: [이름, 설명, 등급, 기록 이름, 기준, 칭호]
const LIST := {
	"first_boss": ["첫 보스", "부화의 여왕을 쓰러뜨렸다", "일반", "boss_kills", 1, "여왕 사냥꾼"],
	"no_death": ["불사신", "한 번도 쓰러지지 않고 보스를 잡았다", "영웅", "boss_nodeath", 1, "불사신"],
	"kills_100": ["숲의 공포", "적 100마리 처치", "희귀", "kills", 100, "숲의 공포"],
	"ff_100": ["팀킬 장인", "아군을 100번 때렸다", "일반", "ff_hits", 100, "팀킬 장인"],
	"throw_10": ["투척 전문가", "아군을 10번 던졌다", "일반", "ally_throws", 10, "투척 전문가"],
	"banana": ["미끄덩", "바나나 껍질을 밟았다", "일반", "slips", 1, "미끄덩"],
	"mimic": ["상자 불신자", "미믹에게 물렸다", "일반", "mimic_bites", 1, "상자 불신자"],
	"items_20": ["수집가", "아이템 20개를 주웠다", "희귀", "items", 20, "수집가"],
	"spend_50": ["큰손", "상점에서 금화 50개를 썼다", "희귀", "spent", 50, "큰손"],
	"ping_50": ["핑 장인", "핑을 50번 찍었다", "일반", "pings", 50, "핑 장인"],
	"emote_30": ["표정 부자", "이모트를 30번 썼다", "일반", "emotes", 30, "표정 부자"],
	"secret": ["탐험가", "비밀방을 찾았다", "희귀", "secrets", 1, "탐험가"],
	"puzzle": ["머리 좋은 파티", "협동 퍼즐을 풀었다", "일반", "puzzles", 1, "두뇌파"],
	"events_10": ["사건 사고", "돌발 이벤트 10번 겪기", "일반", "events", 10, "사고뭉치"],
	"meme_win": ["밈의 신", "밈 모드로 보스를 잡았다", "전설", "meme_wins", 1, "밈의 신"],
	"god_win": ["신", "신 모드로 보스를 잡았다", "전설", "god_wins", 1, "진짜 신"],
	"deaths_20": ["포기를 모르는", "20번 쓰러졌다", "일반", "deaths", 20, "오뚝이"],
}
const GRADE_COL := {"일반": Color("#c8c8d0"), "희귀": Color("#5aa8ff"), "영웅": Color("#c070ff"), "전설": Color("#ffc040"), "개발자": Color("#ff70d0")}

static var stats := {}
static var unlocked := {}
static var _loaded := false


static func _load() -> void:
	if _loaded:
		return
	_loaded = true
	var cf := ConfigFile.new()
	if cf.load(PATH) == OK:
		stats = cf.get_value("a", "stats", {})
		unlocked = cf.get_value("a", "unlocked", {})


static func _save() -> void:
	var cf := ConfigFile.new()
	cf.set_value("a", "stats", stats)
	cf.set_value("a", "unlocked", unlocked)
	cf.save(PATH)


## 기록 더하기 (tree: 알림 띄울 곳)
static func add(tree: SceneTree, stat: String, n := 1) -> void:
	_load()
	stats[stat] = int(stats.get(stat, 0)) + n
	for id in LIST:
		var a: Array = LIST[id]
		if a[3] == stat and not unlocked.has(id) and int(stats[stat]) >= int(a[4]):
			unlocked[id] = true
			if tree:
				tree.call_group("hud", "notify", "업적: %s — 칭호 [%s]" % [a[0], a[5]], GRADE_COL.get(a[2], Color.WHITE))
				Audio.play("item_e")
	_save()


static func is_unlocked(id: String) -> bool:
	_load()
	return unlocked.has(id)


static func title_name(id: String) -> String:
	return String(LIST[id][5]) if LIST.has(id) else ""


static func title_col(id: String) -> Color:
	return GRADE_COL.get(LIST[id][2], Color.WHITE) if LIST.has(id) else Color.WHITE


## 쓸 수 있는 칭호 (열린 것만) — [[id, 칭호], …]
static func titles() -> Array:
	_load()
	var out: Array = [["", "없음"]]
	for id in LIST:
		if unlocked.has(id):
			out.append([id, LIST[id][5]])
	return out
