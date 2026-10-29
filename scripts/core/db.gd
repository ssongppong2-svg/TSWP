class_name DB
extends RefCounted
## 숫자 표 (직업 · 적). 근거 문서: 직업 시스템.md · 전투 시스템.md · 적 시스템.md (밸런스 값은 문서 미정 → 플레이하며 조정)

## 직업: 기본 공격 · Q 스킬 · 패시브 — 모두 장점과 위험을 함께 가진다
const JOBS := {
	"warrior": {
		"name": "용사", "sprite": "warrior", "hp": 120.0, "speed": 1.0, "diff": 1,
		"basic": {"kind": "melee", "dmg": 12.0, "rate": 2.6, "range": 1.35, "arc": 70.0, "knock": 4.5},
		"skill": {"name": "강타", "cd": 6.0, "dmg": 60.0, "radius": 3.2, "half": 75.0, "knock": 12.0, "self_cost": 0.1, "recoil": 5.0},
		"passive": {"name": "역경의 투지", "desc": "체력이 낮을수록 강해진다 (높으면 약해진다)"},
		"desc": "검 · 강타는 아군도 날리고 내 체력도 깎는다",
	},
	"bomber": {
		"name": "폭탄마", "sprite": "bomber", "hp": 90.0, "speed": 1.0, "diff": 3,
		"basic": {"kind": "bomb", "dmg": 14.0, "rate": 1.6, "range": 6.0, "radius": 1.4, "fuse": 0.7, "knock": 6.0},
		"skill": {"name": "거대 폭탄", "cd": 9.0, "dmg": 70.0, "radius": 3.0, "fuse": 1.2, "knock": 16.0, "ff_cur": 0.2, "self": 0.5},
		"passive": {"name": "폭풍 질주", "desc": "폭발에 맞으면 3초 동안 아주 빨라진다 (자기 폭탄도)"},
		"desc": "폭탄 · 폭발은 적·아군·나를 가리지 않는다",
	},
	"doctor": {
		"name": "의사", "sprite": "healer", "hp": 95.0, "speed": 1.05, "diff": 2,
		"basic": {"kind": "syringe", "dmg": 7.0, "heal": 9.0, "rate": 2.2, "speed": 13.0, "poison": 3.0},
		"skill": {"name": "수혈 파동", "cd": 10.0, "heal": 35.0, "radius": 4.5, "cost": 8.0},
		"passive": {"name": "응급 분류", "desc": "위급한 아군이 가까우면 빨라지지만 최대 체력이 준다"},
		"desc": "주사 · 아군은 회복, 적은 독 · 살릴수록 내가 위험",
	},
	"archer": {
		"name": "궁수", "sprite": "rogue", "hp": 85.0, "speed": 1.05, "diff": 2,
		"basic": {"kind": "arrow", "dmg": 9.0, "rate": 2.4, "speed": 18.0, "range": 12.0, "knock": 2.0},
		"skill": {"name": "화살비", "cd": 8.0, "dmg": 6.0, "radius": 2.6, "ticks": 8, "time": 1.6},
		"passive": {"name": "집중", "desc": "멈춰 서 있을수록 화살이 세진다 (움직이면 풀린다)"},
		"desc": "활 · 화살비는 아군 머리 위에도 떨어진다",
	},
}
const JOB_ORDER := ["warrior", "bomber", "doctor", "archer"]

## 적
const ENEMIES := {
	"acorn": {"name": "도토리 병정", "hp": 34.0, "speed": 2.3, "dmg": 10.0, "range": 1.25, "aggro": 9.0, "role": "melee",
		"windup": 0.42, "recover": 0.55, "knock": 5.0, "height": 1.3, "radius": 0.35, "gold": 2},
	"shroom": {"name": "독버섯", "hp": 26.0, "speed": 1.3, "dmg": 8.0, "range": 6.5, "aggro": 10.0, "role": "ranged",
		"windup": 0.5, "recover": 1.4, "knock": 2.0, "height": 1.3, "radius": 0.35, "gold": 2, "shot": 5.5},
	"spider": {"name": "숲 거미", "hp": 22.0, "speed": 3.6, "dmg": 9.0, "range": 3.2, "aggro": 11.0, "role": "lunger",
		"windup": 0.32, "recover": 0.7, "knock": 4.0, "height": 0.9, "radius": 0.4, "gold": 3, "lunge": 8.5},
}
