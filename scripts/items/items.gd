class_name Items
extends RefCounted
## 아이템 표 (아이템 시스템.md 규칙):
##  · 희귀도 5단계: 일반 · 고급 · 희귀 · 영웅 · 전설 (색: 회 · 초 · 파 · 보 · 금)
##  · 종류: 무기 · 방어구 · 장신구 · 유물(플레이 방식을 크게 바꿈) · 소비(일회성)
##  · 모든 장비는 '위험 요소'를 하나 이상 가진다 — 좋기만 한 아이템은 없다
##  · 직업 전용 아이템은 없다 (누가 주워도 쓴다)
##  · 장비 칸 5개. 꽉 차면 가장 오래된 것을 바닥에 떨어뜨리고 바꾼다 (다른 사람이 주울 수 있다 — 트롤 거리)
##  · 소비 칸 1개 (같은 것 3개까지 겹침), F 로 쓴다
##
## mods: Fighter.set_mod 로 들어가는 능력치 (+0.2 = +20%)
##   attack 공격력 · speed 이동 · max_hp 최대 체력 · atk_rate 공격 속도 · cd 스킬 대기시간(-0.25 = 25% 짧게)
##   dmg_taken 받는 피해 · knock_taken 받는 넉백 · heal_taken 받는 회복 · gold 금화 · roll 구르기 거리 · iframe 구르기 무적
##   ult 궁극기 충전 · threat 적이 노리는 정도 · crit 치명타 확률(+0.1 = +10%p, 기본 8%) · crit_dmg 치명타 배율(기본 1.8배)
## fx: 특수 효과 (Inventory / Player 가 처리)

const SLOTS := 5
const STACK := 3

const RARITY := {
	"common": {"name": "일반", "col": Color("#c8c8d0"), "w": 50.0, "price": 6, "sfx": "item_c"},
	"uncommon": {"name": "고급", "col": Color("#6ad07a"), "w": 28.0, "price": 10, "sfx": "item_u"},
	"rare": {"name": "희귀", "col": Color("#5aa8ff"), "w": 14.0, "price": 18, "sfx": "item_r"},
	"epic": {"name": "영웅", "col": Color("#c070ff"), "w": 6.0, "price": 30, "sfx": "item_e"},
	"legendary": {"name": "전설", "col": Color("#ffc040"), "w": 2.0, "price": 50, "sfx": "item_l"},
}
const RARITY_ORDER := ["common", "uncommon", "rare", "epic", "legendary"]
const TYPE_NAME := {"weapon": "무기", "armor": "방어구", "accessory": "장신구", "relic": "유물", "consumable": "소비"}

const ITEMS := {
	# ---------------------------------------------------------------- 일반
	"rusty_sword": {"name": "녹슨 대검", "type": "weapon", "rarity": "common",
		"mods": {"attack": 0.15, "speed": -0.05}, "desc": "공격력 +15%", "risk": "이동 속도 -5%"},
	"swift_glove": {"name": "날쌘 장갑", "type": "armor", "rarity": "common",
		"mods": {"atk_rate": 0.22, "attack": -0.08}, "desc": "공격 속도 +22%", "risk": "공격력 -8%"},
	"feather_boots": {"name": "깃털 신발", "type": "armor", "rarity": "common",
		"mods": {"speed": 0.15, "knock_taken": 0.4}, "desc": "이동 속도 +15%", "risk": "맞으면 40% 더 멀리 날아간다"},
	"whetstone": {"name": "날카로운 숫돌", "type": "weapon", "rarity": "common",
		"mods": {"crit": 0.1, "atk_rate": -0.06}, "desc": "치명타 확률 +10%", "risk": "공격 속도 -6%"},
	"lucky_coin": {"name": "행운의 동전", "type": "accessory", "rarity": "common",
		"mods": {"gold": 0.5}, "fx": ["coin_drop"], "desc": "금화 +50%", "risk": "맞을 때마다 파티 금화 1개를 흘린다 (아무나 주울 수 있다)"},
	# ---------------------------------------------------------------- 고급
	"turtle_shell": {"name": "거북 등껍질", "type": "armor", "rarity": "uncommon",
		"mods": {"dmg_taken": -0.2, "speed": -0.15}, "desc": "받는 피해 -20%", "risk": "이동 속도 -15%"},
	"vampire_fang": {"name": "흡혈 송곳니", "type": "accessory", "rarity": "uncommon",
		"mods": {"max_hp": -0.1}, "fx": ["kill_heal"], "desc": "적을 쓰러뜨리면 체력 5 회복", "risk": "최대 체력 -10%"},
	"thorn_armor": {"name": "가시 갑옷", "type": "armor", "rarity": "uncommon",
		"mods": {"dmg_taken": 0.1}, "fx": ["thorns"], "desc": "나를 때린 상대에게 피해 8 되돌림 (아군이어도!)", "risk": "받는 피해 +10%"},
	"flame_charm": {"name": "불꽃 부적", "type": "accessory", "rarity": "uncommon",
		"fx": ["burn"], "desc": "공격이 3초 동안 불태운다 (초당 3)", "risk": "맞을 때 20% 확률로 나도 불붙는다"},
	"venom_fang": {"name": "독사의 이빨", "type": "weapon", "rarity": "uncommon",
		"mods": {"heal_taken": -0.3}, "fx": ["venom"], "desc": "공격에 독 (초당 2.5, 3초)", "risk": "받는 회복 -30%"},
	"combo_band": {"name": "연타 머리띠", "type": "accessory", "rarity": "uncommon",
		"fx": ["combo_keep"], "desc": "콤보가 1.5초 더 이어진다 · 10콤보마다 공격력 +3% (콤보가 끊기면 사라짐)", "risk": "콤보가 끊기면 1.5초 느려진다"},
	"giant_belt": {"name": "거인의 허리띠", "type": "armor", "rarity": "uncommon",
		"mods": {"max_hp": 0.3, "roll": -0.3}, "desc": "최대 체력 +30%", "risk": "구르기 거리 -30%"},
	"heart_locket": {"name": "심장 목걸이", "type": "accessory", "rarity": "uncommon",
		"mods": {"max_hp": 0.18, "heal_taken": 0.25, "attack": -0.06}, "desc": "최대 체력 +18% · 받는 회복 +25%", "risk": "공격력 -6%"},
	# ---------------------------------------------------------------- 희귀
	"berserk_helm": {"name": "광전사의 투구", "type": "armor", "rarity": "rare",
		"mods": {"attack": 0.4, "dmg_taken": 0.25}, "desc": "공격력 +40%", "risk": "받는 피해 +25%"},
	"hourglass": {"name": "모래시계", "type": "accessory", "rarity": "rare",
		"mods": {"cd": -0.3, "max_hp": -0.15}, "desc": "스킬 대기시간 -30%", "risk": "최대 체력 -15%"},
	"storm_ring": {"name": "폭풍 반지", "type": "accessory", "rarity": "rare",
		"fx": ["chain"], "desc": "공격 시 20% 확률로 번개가 주변 3명에게 튄다 (8 피해)", "risk": "번개는 아군에게도 튄다"},
	"shadow_cloak": {"name": "그림자 망토", "type": "armor", "rarity": "rare",
		"mods": {"iframe": 1.4, "attack": -0.1}, "fx": ["roll_stealth"], "desc": "구르기 무적 시간 2.4배 · 구른 뒤 1초 숨기", "risk": "공격력 -10%"},
	"ult_battery": {"name": "번개 전지", "type": "accessory", "rarity": "rare",
		"mods": {"ult": 0.6, "cd": 0.15}, "desc": "궁극기 충전 +60%", "risk": "스킬 대기시간 +15%"},
	"gambler_dice": {"name": "도박꾼의 주사위", "type": "accessory", "rarity": "rare",
		"mods": {"crit": 0.25, "crit_dmg": 0.3, "attack": -0.15}, "desc": "치명타 확률 +25% · 치명타 피해 +30%", "risk": "공격력 -15% (운이 나쁘면 약하다)"},
	"prank_glove": {"name": "장난꾸러기 장갑", "type": "weapon", "rarity": "rare",
		"mods": {"attack": -0.1}, "fx": ["prank"], "desc": "아군을 때리면 피해 대신 크게 날려 보내고 1초 빨라지게 한다", "risk": "적에게 공격력 -10%"},
	# ---------------------------------------------------------------- 영웅
	"glass_cannon": {"name": "유리 대포", "type": "relic", "rarity": "epic",
		"mods": {"attack": 1.0, "max_hp": -0.5}, "desc": "공격력 +100%", "risk": "최대 체력 -50%"},
	"phoenix_feather": {"name": "불사조 깃털", "type": "accessory", "rarity": "epic",
		"mods": {"max_hp": -0.1}, "fx": ["revive"], "desc": "쓰러지면 그 자리에서 체력 50%로 한 번 되살아난다 (깃털은 타서 없어짐)", "risk": "최대 체력 -10%"},
	"assassin_dagger": {"name": "암살자의 단검", "type": "weapon", "rarity": "epic",
		"mods": {"crit": 0.12, "crit_dmg": 1.0, "max_hp": -0.2}, "fx": ["crit_hide"], "desc": "치명타 확률 +12% · 치명타 피해 2배 · 치명타로 쓰러뜨리면 1초 숨는다", "risk": "최대 체력 -20%"},
	"echo_bell": {"name": "메아리 종", "type": "relic", "rarity": "epic",
		"mods": {"cd": 0.3}, "fx": ["echo"], "desc": "스킬이 0.7초 뒤 절반 위력으로 한 번 더 나간다", "risk": "스킬 대기시간 +30%"},
	# ---------------------------------------------------------------- 전설
	"cursed_crown": {"name": "저주받은 왕관", "type": "relic", "rarity": "legendary",
		"mods": {"attack": 0.5, "gold": 1.0, "threat": 2.0}, "fx": ["crown"], "desc": "공격력 +50% · 금화 +100%", "risk": "모든 적이 나만 노리고, 아군에게 맞으면 피해 2배"},
	"blood_pact": {"name": "피의 계약서", "type": "relic", "rarity": "legendary",
		"fx": ["pact"], "desc": "적을 쓰러뜨릴 때마다 공격력 +4% (이 지역에서 최대 +80%)", "risk": "체력이 초당 0.8씩 줄어든다 (1 아래로는 안 떨어짐)"},
	# ---------------------------------------------------------------- 소비 (F)
	"potion": {"name": "회복 물약", "type": "consumable", "rarity": "common", "desc": "체력 45 회복", "risk": ""},
	"banana": {"name": "바나나", "type": "consumable", "rarity": "common", "desc": "먹고 발밑에 껍질을 버린다 — 밟은 사람(적·아군)은 미끄러져 넘어진다", "risk": "나도 밟을 수 있다"},
	"smoke": {"name": "연막탄", "type": "consumable", "rarity": "uncommon", "desc": "4초 동안 숨는다 (적이 놓친다)", "risk": ""},
	"firecracker": {"name": "폭죽 다발", "type": "consumable", "rarity": "uncommon", "desc": "주변에 폭죽 6발 (피아 구분 없음)", "risk": "나도 맞을 수 있다"},
	"recall": {"name": "귀환 두루마리", "type": "consumable", "rarity": "uncommon", "desc": "캠프로 돌아간다", "risk": ""},
	"big_potion": {"name": "큰 회복 물약", "type": "consumable", "rarity": "rare", "desc": "체력을 모두 회복하고 독·불을 씻는다", "risk": ""},
	"curse_doll": {"name": "저주 인형", "type": "consumable", "rarity": "rare", "desc": "가장 가까운 아군 체력 25%를 빼앗아 내 것으로", "risk": "친구를 잃을 수 있다"},
}


static func get_item(id: String) -> Dictionary:
	return ITEMS.get(id, {})


static func is_consumable(id: String) -> bool:
	return String(get_item(id).get("type", "")) == "consumable"


static func color(id: String) -> Color:
	return RARITY[String(get_item(id).get("rarity", "common"))]["col"]


static func price(id: String) -> int:
	var p := int(RARITY[String(get_item(id).get("rarity", "common"))]["price"])
	return maxi(3, p / 2) if is_consumable(id) else p


## 무작위 하나 고르기 (희귀도 가중치 · 행운 luck 만큼 높은 등급 쪽으로)
static func roll(rng: RandomNumberGenerator, consumable := false, luck := 0.0, min_rarity := "common") -> String:
	var pool := {}
	for id in ITEMS:
		var it: Dictionary = ITEMS[id]
		if (String(it["type"]) == "consumable") != consumable:
			continue
		var r := String(it["rarity"])
		if RARITY_ORDER.find(r) < RARITY_ORDER.find(min_rarity):
			continue
		pool[id] = float(RARITY[r]["w"]) * (1.0 + luck * RARITY_ORDER.find(r))
	var total := 0.0
	for id in pool:
		total += float(pool[id])
	var x := rng.randf() * total
	for id in pool:
		x -= float(pool[id])
		if x <= 0.0:
			return id
	return pool.keys()[0] if not pool.is_empty() else "potion"


## 설명 한 줄 (HUD 알림 · 메뉴)
static func line(id: String) -> String:
	var it := get_item(id)
	var r: Dictionary = RARITY[String(it.get("rarity", "common"))]
	return "%s · %s" % [r["name"], TYPE_NAME.get(String(it.get("type", "")), "")]
