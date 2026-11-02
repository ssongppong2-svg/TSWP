class_name Diff
extends RefCounted
## 난이도 (게임 시작과 선택, 직업, 플레이.md) — 캠프 난이도 표지판에서 고른다 (Prefs.difficulty)
##  0 슈퍼 겁쟁이 · 1 인간 · 2 신 · 3 밈 (특수 규칙이 무작위로 — 지역마다 2개)

const NAMES := ["슈퍼 겁쟁이", "인간", "신", "밈"]
const DESC := ["적·보스가 약하다. 처음 하는 사람용", "기본 난이도", "적이 강하고 엘리트가 자주 나온다. 보상도 많다", "무슨 일이 일어날지 모른다"]
const COLS := [Color("#9aff9a"), Color("#fff4d6"), Color("#ff8a5a"), Color("#d08aff")]
const HP := [0.6, 1.0, 1.45, 1.0]
const DMG := [0.6, 1.0, 1.5, 1.0]
const ELITE := [0.05, 0.18, 0.4, 0.25]
const LOOT := [1.0, 1.0, 1.6, 1.2]

## 밈 모드 규칙
const MEME := {
	"same_job": "모두 같은 직업!",
	"boss_size": "보스 크기가 제멋대로",
	"cursed_start": "시작 아이템이 전부 저주받았다",
	"banana_rain": "바나나 껍질이 여기저기",
	"moon": "달 중력 (높이 뛰고 멀리 날아간다)",
	"double_boom": "모든 폭발이 두 배",
	"reverse": "좌우가 거꾸로!",
	"tiny_foes": "작고 빠른 적들",
	"gold_rain": "하늘에서 금화가",
}

static var meme_rules: Array = []


static func level() -> int:
	return clampi(Prefs.difficulty, 0, 3)


static func roll_meme(rng: RandomNumberGenerator) -> void:
	meme_rules.clear()
	if level() != 3:
		return
	var keys: Array = MEME.keys()
	while meme_rules.size() < 2:
		var k: String = keys[rng.randi_range(0, keys.size() - 1)]
		if not meme_rules.has(k):
			meme_rules.append(k)


static func meme(rule: String) -> bool:
	return level() == 3 and meme_rules.has(rule)
