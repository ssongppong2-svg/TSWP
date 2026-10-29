extends Node
## 소리 엔진
##  · 버스: Master(리미터) ← Music / SFX(UI·환경음·월드 4종) / Voice(4종)
##  · 입체 음향: 게임 세계 안의 AudioStreamPlayer3D — 귀(AudioListener3D)는 카메라가 아니라 초점(플레이어) 자리.
##    거리(r)는 화면 px 기준(480 = 화면 폭)으로 적고, 1m = 16px 로 바꿔 쓴다
##  · 공간 울림: 소리가 난 곳의 지형(숲·동굴·둥지)에 따라 다른 리버브 버스로 보낸다
##  · 차폐: 귀와 소리 사이에 흙·바위가 있으면 먹먹한(저역 통과) 버스로, 두꺼울수록 더 작게
##  · 같은 소리 동시 재생 제한·최소 간격·변주(파일 여러 개 + 피치 흔들림)
##  · 덕킹: 큰 소리 순간 음악을 잠깐 낮춘다
##  · 음악: 맵별 테마, 탐험/전투 두 겹(stem)을 박자 맞춰 같이 돌리고 전투 강도에 따라 섞는다
##  · 환경음: 맵별 배경 소리 + 모닥불·웅덩이 같은 위치 고정 반복음
##  · 자막: 중요한 소리는 captioned 신호로 (접근성)

signal captioned(text: String, pos: Variant)

const SFX_DIR := "res://assets/sfx/"
const MUSIC_DIR := "res://assets/music/"
const AMB_DIR := "res://assets/ambience/"
const POOL_3D := 40
const PPU := 16.0
const POOL_UI := 12
const MAX_VARIANTS := 6

## 소리 묶음 설정
##  v: 기본 볼륨(dB)  p: 피치 흔들림(±)  n: 동시 최대  cd: 같은 소리 최소 간격(초)
##  r: 들리는 최대 거리(px, 화면 폭 480 기준)  ui: 위치 없이 UI 버스로  cap: 자막 문구
##  duck: 음악을 이만큼(dB) 잠깐 낮춘다
const KITS := {
	# ---- UI
	"ui_click": {"v": -6.0, "p": 0.0, "n": 3, "ui": true},
	"ui_hover": {"v": -14.0, "p": 0.02, "n": 2, "cd": 0.03, "ui": true},
	"ui_confirm": {"v": -4.0, "p": 0.0, "n": 2, "ui": true},
	"ui_back": {"v": -6.0, "p": 0.0, "n": 2, "ui": true},
	"ui_toggle": {"v": -8.0, "p": 0.0, "n": 2, "ui": true},
	"ui_tick": {"v": -6.0, "p": 0.0, "n": 2, "ui": true},
	"ui_start": {"v": -2.0, "p": 0.0, "n": 1, "ui": true},
	"ui_denied": {"v": -6.0, "p": 0.0, "n": 1, "ui": true},
	"ui_toast": {"v": -8.0, "p": 0.0, "n": 2, "ui": true},
	# ---- 플레이어 움직임
	"step_grass": {"v": -15.0, "p": 0.12, "n": 6, "cd": 0.05, "r": 300.0},
	"step_stone": {"v": -14.0, "p": 0.1, "n": 6, "cd": 0.05, "r": 300.0},
	"step_wood": {"v": -13.0, "p": 0.1, "n": 6, "cd": 0.05, "r": 300.0},
	"jump": {"v": -12.0, "p": 0.08, "n": 4, "cd": 0.04, "r": 360.0},
	"double_jump": {"v": -9.0, "p": 0.05, "n": 3, "r": 380.0},
	"land": {"v": -11.0, "p": 0.1, "n": 4, "cd": 0.05, "r": 340.0},
	"land_heavy": {"v": -5.0, "p": 0.06, "n": 3, "r": 460.0},
	"dash": {"v": -7.0, "p": 0.06, "n": 3, "r": 420.0},
	"climb": {"v": -16.0, "p": 0.1, "n": 4, "cd": 0.12, "r": 260.0},
	"splash": {"v": -6.0, "p": 0.1, "n": 3, "cd": 0.08, "r": 420.0},
	# ---- 전투 (플레이어)
	"swing": {"v": -8.0, "p": 0.08, "n": 6, "cd": 0.02, "r": 420.0},
	"sword_hit": {"v": -4.0, "p": 0.08, "n": 6, "cd": 0.025, "r": 480.0},
	"smash": {"v": -1.0, "p": 0.04, "n": 3, "r": 520.0, "duck": 4.0, "cap": "강타"},
	"throw": {"v": -10.0, "p": 0.08, "n": 5, "cd": 0.03, "r": 380.0},
	"bomb_bounce": {"v": -10.0, "p": 0.15, "n": 6, "cd": 0.04, "r": 420.0},
	"fuse": {"v": -9.0, "p": 0.05, "n": 4, "r": 380.0, "cap": "도화선 소리"},
	"explode_s": {"v": -3.0, "p": 0.1, "n": 6, "cd": 0.02, "r": 560.0, "duck": 3.0, "cap": "펑"},
	"explode_l": {"v": 1.0, "p": 0.06, "n": 3, "r": 640.0, "duck": 8.0, "cap": "대폭발"},
	"syringe": {"v": -10.0, "p": 0.08, "n": 5, "cd": 0.03, "r": 360.0},
	"inject": {"v": -7.0, "p": 0.1, "n": 5, "cd": 0.03, "r": 400.0},
	"heal_pulse": {"v": -4.0, "p": 0.02, "n": 3, "r": 480.0, "cap": "치유 파동"},
	"heal_get": {"v": -10.0, "p": 0.06, "n": 4, "cd": 0.06, "r": 380.0},
	"hurt": {"v": -5.0, "p": 0.1, "n": 5, "cd": 0.04, "r": 460.0},
	"die": {"v": -2.0, "p": 0.04, "n": 3, "r": 560.0, "cap": "누군가 쓰러짐"},
	"revive": {"v": -4.0, "p": 0.0, "n": 3, "r": 560.0, "cap": "부활"},
	"item_c": {"v": -7.0, "p": 0.0, "n": 3, "r": 420.0},
	"item_u": {"v": -6.0, "p": 0.0, "n": 3, "r": 420.0},
	"item_r": {"v": -5.0, "p": 0.0, "n": 3, "r": 440.0},
	"item_e": {"v": -4.0, "p": 0.0, "n": 3, "r": 460.0, "cap": "영웅 아이템"},
	"item_l": {"v": -2.0, "p": 0.0, "n": 2, "r": 520.0, "duck": 3.0, "cap": "전설 아이템!"},
	"coin": {"v": -12.0, "p": 0.08, "n": 6, "cd": 0.03, "r": 380.0},
	"emote": {"v": -8.0, "p": 0.06, "n": 4, "r": 480.0},
	"ping": {"v": -4.0, "p": 0.0, "n": 3, "cd": 0.1, "ui": true, "cap": "핑"},
	# ---- 장치
	"chest": {"v": -3.0, "p": 0.02, "n": 2, "r": 480.0, "cap": "상자 열림"},
	"lever": {"v": -5.0, "p": 0.05, "n": 3, "r": 480.0, "cap": "레버"},
	"plate": {"v": -8.0, "p": 0.05, "n": 4, "r": 420.0},
	"gate_open": {"v": -4.0, "p": 0.03, "n": 2, "r": 520.0, "cap": "철창이 열림"},
	"gate_close": {"v": -4.0, "p": 0.03, "n": 2, "r": 520.0, "cap": "철창이 닫힘"},
	"crate_push": {"v": -7.0, "p": 0.08, "n": 3, "cd": 0.05, "r": 400.0},
	"crate_break": {"v": -4.0, "p": 0.08, "n": 3, "r": 460.0},
	"wall_crumble": {"v": 0.0, "p": 0.04, "n": 2, "r": 600.0, "duck": 5.0, "cap": "바위가 무너짐"},
	"shroom": {"v": -6.0, "p": 0.08, "n": 4, "cd": 0.05, "r": 420.0},
	"bridge": {"v": -12.0, "p": 0.12, "n": 3, "cd": 0.25, "r": 360.0},
	"thorns": {"v": -6.0, "p": 0.08, "n": 3, "cd": 0.1, "r": 420.0},
	"troll": {"v": -1.0, "p": 0.0, "n": 2, "r": 640.0, "duck": 6.0, "cap": "누르지 말라던 버튼!"},
	"alarm": {"v": -5.0, "p": 0.0, "n": 1, "ui": true, "cap": "적이 나타났다"},
	"checkpoint": {"v": -6.0, "p": 0.0, "n": 1, "r": 480.0},
	"crystal_ping": {"v": -5.0, "p": 0.04, "n": 3, "r": 480.0},
	"totem": {"v": -4.0, "p": 0.0, "n": 2, "r": 480.0},
	# ---- 적
	"telegraph": {"v": -9.0, "p": 0.05, "n": 4, "cd": 0.08, "r": 420.0, "cap": "공격 예고"},
	"spawn": {"v": -8.0, "p": 0.1, "n": 4, "cd": 0.05, "r": 520.0},
	"acorn_swing": {"v": -8.0, "p": 0.1, "n": 4, "cd": 0.03, "r": 400.0},
	"acorn_hurt": {"v": -7.0, "p": 0.12, "n": 4, "cd": 0.03, "r": 420.0},
	"acorn_die": {"v": -5.0, "p": 0.08, "n": 3, "r": 460.0},
	"shroom_shoot": {"v": -9.0, "p": 0.1, "n": 4, "cd": 0.03, "r": 440.0},
	"shroom_hurt": {"v": -7.0, "p": 0.12, "n": 4, "cd": 0.03, "r": 420.0},
	"shroom_die": {"v": -5.0, "p": 0.08, "n": 3, "r": 460.0},
	"spider_skitter": {"v": -15.0, "p": 0.15, "n": 4, "cd": 0.12, "r": 300.0},
	"spider_bite": {"v": -8.0, "p": 0.1, "n": 4, "cd": 0.03, "r": 400.0},
	"spider_hurt": {"v": -8.0, "p": 0.12, "n": 4, "cd": 0.03, "r": 400.0},
	"spider_die": {"v": -6.0, "p": 0.1, "n": 3, "r": 440.0},
	"spore_hit": {"v": -8.0, "p": 0.1, "n": 4, "cd": 0.03, "r": 400.0},
	"web_shot": {"v": -7.0, "p": 0.08, "n": 4, "cd": 0.04, "r": 480.0},
	"web_hit": {"v": -7.0, "p": 0.1, "n": 4, "cd": 0.04, "r": 440.0},
	# ---- 보스
	"q_roar": {"v": 1.0, "p": 0.02, "n": 1, "r": 900.0, "duck": 9.0, "cap": "여왕의 포효"},
	"q_screech": {"v": -2.0, "p": 0.04, "n": 2, "r": 800.0, "duck": 5.0, "cap": "여왕의 비명"},
	"q_step": {"v": -6.0, "p": 0.1, "n": 4, "cd": 0.08, "r": 600.0},
	"q_slam": {"v": 2.0, "p": 0.04, "n": 2, "r": 900.0, "duck": 8.0, "cap": "내려찍기"},
	"q_charge": {"v": -1.0, "p": 0.04, "n": 1, "r": 900.0, "duck": 4.0, "cap": "돌진 준비"},
	"q_bite": {"v": -2.0, "p": 0.06, "n": 2, "r": 700.0},
	"q_hiss": {"v": -3.0, "p": 0.03, "n": 1, "r": 900.0, "cap": "위험 게이지 폭발 직전!"},
	"q_burst": {"v": 2.0, "p": 0.03, "n": 1, "r": 900.0, "duck": 10.0, "cap": "독 폭발"},
	"q_cocoon": {"v": -4.0, "p": 0.08, "n": 3, "r": 700.0, "cap": "고치"},
	"q_hatch": {"v": -3.0, "p": 0.08, "n": 3, "r": 700.0, "cap": "부화!"},
	"q_shield": {"v": -2.0, "p": 0.0, "n": 1, "r": 900.0, "duck": 5.0, "cap": "껍질이 단단해짐"},
	"q_shatter": {"v": 2.0, "p": 0.0, "n": 1, "r": 900.0, "duck": 9.0, "cap": "껍질이 깨짐!"},
	"q_weak": {"v": -2.0, "p": 0.05, "n": 3, "cd": 0.04, "r": 700.0},
	"q_clink": {"v": -6.0, "p": 0.1, "n": 3, "cd": 0.05, "r": 600.0},
	"q_open": {"v": -3.0, "p": 0.0, "n": 1, "r": 900.0, "cap": "발판이 열림"},
	"q_wallhit": {"v": 0.0, "p": 0.05, "n": 2, "r": 900.0, "duck": 6.0},
	"q_die": {"v": 3.0, "p": 0.0, "n": 1, "r": 1200.0, "duck": 12.0, "cap": "여왕이 쓰러짐"},
	# ---- 메타
	"crown_get": {"v": -3.0, "p": 0.0, "n": 2, "r": 640.0, "cap": "왕관을 주웠다"},
	"crown_drop": {"v": -4.0, "p": 0.06, "n": 2, "r": 640.0, "cap": "왕관을 떨어뜨림"},
	# ---- 반복음 (위치 고정)
	"fire_loop": {"v": -8.0, "loop": true, "r": 260.0},
	"bubble_loop": {"v": -14.0, "loop": true, "r": 220.0},
	"water_loop": {"v": -14.0, "loop": true, "r": 220.0},
	"crystal_loop": {"v": -20.0, "loop": true, "r": 200.0},
	"fuse_loop": {"v": -12.0, "loop": true, "r": 260.0},
}

## 예전 이름 → 새 이름 (아직 바꾸지 못한 호출 대비)
const ALIAS := {
	"click": "ui_click", "hover": "ui_hover", "tick": "ui_tick", "ready": "ui_confirm", "denied": "ui_denied",
	"sword": "swing", "hit": "sword_hit", "explode": "explode_s", "heal": "heal_pulse", "item": "item_c",
	"pickup": "totem", "door": "gate_open", "shove": "crate_push", "web": "web_hit", "buzzer": "troll",
	"boss_roar": "q_roar", "boss_slam": "q_slam", "charge": "q_charge", "win": "q_die", "crown": "crown_get",
	"grab": "crown_drop", "inject": "inject", "fuse": "fuse", "emote": "emote",
}

## 음악: 이름 → 파일 묶음 (stem 이 있으면 전투 겹)
const MUSIC := {
	"menu": {"base": "menu"},
	"camp": {"base": "camp"},
	"forest": {"base": "forest", "stem": "forest_combat"},
	"cave": {"base": "cave", "stem": "cave_combat"},
	"nest": {"base": "nest", "stem": "nest_combat"},
	"boss": {"base": "boss", "stem": "boss_p2"},
	"crown": {"base": "crown"},
	"results": {"base": "results"},
	"gameover": {"base": "gameover"},
}

## 짧은 음악 조각 (방 클리어 · 아이템 · 보스 등장 …)
const STINGERS := ["st_clear", "st_item", "st_boss", "st_win", "st_lose", "st_puzzle", "st_secret", "st_phase"]

var streams := {}               # 소리 이름 → [AudioStream …]
var _last_variant := {}
var _last_time := {}
var _active := {}               # 소리 이름 → [player …]
var pool3d: Array[AudioStreamPlayer3D] = []
var pool_ui: Array[AudioStreamPlayer] = []
var world_root: Node3D = null   # 게임 세계 안 소리 노드 부모 (없으면 UI 버스로 대체)
var space_fn: Callable          # (pos) -> {"room": "forest|cave|nest", "occl": 0~3}
var listener := Vector3.ZERO    # 세계가 없을 때 거리 계산용 귀 위치
var use_listener := false
# 음악
var _decks: Array = []          # [{base: player, stem: player, name}]
var _deck := 0
var music_name := ""
var _intensity := 0.0
var _intensity_target := 0.0
var _stinger: AudioStreamPlayer
var _duck_db := 0.0
var _duck_hold := 0.0
# 환경음
var _amb: Array[AudioStreamPlayer] = []
var _amb_i := 0
var amb_name := ""


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	_build_buses()
	_load_all()
	for i in POOL_UI:
		var a := AudioStreamPlayer.new()
		a.bus = "UI"
		add_child(a)
		pool_ui.append(a)
	for k in 2:
		var deck := {"base": AudioStreamPlayer.new(), "stem": AudioStreamPlayer.new(), "name": ""}
		for key in ["base", "stem"]:
			var p: AudioStreamPlayer = deck[key]
			p.bus = "Music"
			p.volume_db = -80.0
			add_child(p)
		_decks.append(deck)
	_stinger = AudioStreamPlayer.new()
	_stinger.bus = "Music"
	add_child(_stinger)
	for k in 2:
		var a2 := AudioStreamPlayer.new()
		a2.bus = "Ambience"
		a2.volume_db = -80.0
		add_child(a2)
		_amb.append(a2)
	apply_volumes()


# ================================================================== 버스
func _bus(bus_name: String, send: String) -> int:
	var i := AudioServer.get_bus_index(bus_name)
	if i == -1:
		AudioServer.add_bus()
		i = AudioServer.bus_count - 1
		AudioServer.set_bus_name(i, bus_name)
	AudioServer.set_bus_send(i, send)
	return i


func _build_buses() -> void:
	# 마스터: 소리가 겹쳐도 찢어지지 않게 리미터
	var lim := AudioEffectHardLimiter.new()
	lim.ceiling_db = -0.8
	lim.pre_gain_db = 0.0
	AudioServer.add_bus_effect(0, lim)
	_bus("Music", "Master")
	var sfx := _bus("SFX", "Master")
	var comp := AudioEffectCompressor.new()
	comp.threshold = -10.0
	comp.ratio = 3.0
	comp.attack_us = 2000.0
	comp.release_ms = 180.0
	AudioServer.add_bus_effect(sfx, comp)
	_bus("UI", "SFX")
	_bus("Ambience", "SFX")
	# 공간별 월드 버스
	_bus("World", "SFX")                          # 숲·캠프: 아주 짧은 반사
	var wr := AudioEffectReverb.new()
	wr.room_size = 0.25
	wr.damping = 0.6
	wr.wet = 0.07
	wr.dry = 1.0
	wr.spread = 0.8
	AudioServer.add_bus_effect(AudioServer.get_bus_index("World"), wr)
	var cave := _bus("World_Cave", "SFX")        # 동굴: 길고 밝은 울림
	var cr := AudioEffectReverb.new()
	cr.room_size = 0.72
	cr.damping = 0.35
	cr.wet = 0.32
	cr.dry = 0.9
	cr.predelay_msec = 40.0
	cr.spread = 1.0
	AudioServer.add_bus_effect(cave, cr)
	var nest := _bus("World_Nest", "SFX")        # 둥지: 크고 어두운 울림
	var nr := AudioEffectReverb.new()
	nr.room_size = 0.88
	nr.damping = 0.75
	nr.wet = 0.36
	nr.dry = 0.85
	nr.predelay_msec = 60.0
	AudioServer.add_bus_effect(nest, nr)
	var nlow := AudioEffectLowShelfFilter.new()
	nlow.cutoff_hz = 220.0
	nlow.gain = 1.4
	AudioServer.add_bus_effect(nest, nlow)
	var muf := _bus("World_Muffled", "SFX")      # 벽 너머: 먹먹하게
	var lp := AudioEffectLowPassFilter.new()
	lp.cutoff_hz = 650.0
	lp.resonance = 0.4
	AudioServer.add_bus_effect(muf, lp)
	var mr := AudioEffectReverb.new()
	mr.room_size = 0.5
	mr.wet = 0.18
	mr.damping = 0.8
	AudioServer.add_bus_effect(muf, mr)
	# 음성 (같은 공간 처리)
	_bus("Voice", "Master")
	var vcomp := AudioEffectCompressor.new()
	vcomp.threshold = -16.0
	vcomp.ratio = 4.0
	AudioServer.add_bus_effect(AudioServer.get_bus_index("Voice"), vcomp)
	_bus("Voice_World", "Voice")
	var vc := _bus("Voice_Cave", "Voice")
	AudioServer.add_bus_effect(vc, cr.duplicate())
	var vn := _bus("Voice_Nest", "Voice")
	AudioServer.add_bus_effect(vn, nr.duplicate())
	var vm := _bus("Voice_Muffled", "Voice")
	AudioServer.add_bus_effect(vm, lp.duplicate())
	_bus("VoiceTest", "Master")
	var mic := _bus("Mic", "Master")
	AudioServer.set_bus_mute(mic, true)


func apply_volumes() -> void:
	_set_vol("Master", Prefs.vol_master)
	_set_vol("Music", Prefs.vol_music)
	_set_vol("SFX", Prefs.vol_sfx)
	_set_vol("Ambience", Prefs.vol_amb)
	_set_vol("Voice", Prefs.vol_voice)


func _set_vol(bus_name: String, v: float) -> void:
	var i := AudioServer.get_bus_index(bus_name)
	if i < 0:
		return
	AudioServer.set_bus_volume_db(i, linear_to_db(maxf(v, 0.0001)) + (_duck_db if bus_name == "Music" else 0.0))
	AudioServer.set_bus_mute(i, v <= 0.001)


# ================================================================== 불러오기
func _load_all() -> void:
	for n in KITS:
		var arr: Array = []
		var single: String = SFX_DIR + n + ".wav"
		if ResourceLoader.exists(single):
			arr.append(load(single))
		for k in range(1, MAX_VARIANTS + 1):
			var p := SFX_DIR + "%s_%d.wav" % [n, k]
			if ResourceLoader.exists(p):
				arr.append(load(p))
		if KITS[n].get("loop", false):
			for s in arr:
				if s is AudioStreamWAV:
					var w: AudioStreamWAV = s
					w.loop_mode = AudioStreamWAV.LOOP_FORWARD
					w.loop_begin = 0
					w.loop_end = int(w.get_length() * w.mix_rate)
		if not arr.is_empty():
			streams[n] = arr


func _pick(n: String) -> AudioStream:
	var arr: Array = streams.get(n, [])
	if arr.is_empty():
		return null
	if arr.size() == 1:
		return arr[0]
	var last: int = _last_variant.get(n, -1)
	var i := randi() % arr.size()
	if i == last:
		i = (i + 1) % arr.size()
	_last_variant[n] = i
	return arr[i]


# ================================================================== 게임 세계 연결
## GameWorld 가 생성될 때: 세계 뷰포트 안에 소리 노드를 만든다 (카메라가 귀가 된다)
func attach_world(root: Node3D, vp: SubViewport, fn: Callable) -> void:
	detach_world()
	vp.audio_listener_enable_3d = true
	world_root = Node3D.new()
	world_root.name = "AudioRoot"
	root.add_child(world_root)
	space_fn = fn
	pool3d.clear()
	for i in POOL_3D:
		var a := _new3d()
		world_root.add_child(a)
		pool3d.append(a)


func _new3d() -> AudioStreamPlayer3D:
	var a := AudioStreamPlayer3D.new()
	a.bus = "World"
	a.attenuation_model = AudioStreamPlayer3D.ATTENUATION_INVERSE_DISTANCE
	a.unit_size = 7.0
	a.max_distance = 560.0 / PPU
	a.panning_strength = 1.2
	a.doppler_tracking = AudioStreamPlayer3D.DOPPLER_TRACKING_DISABLED
	return a


func detach_world() -> void:
	pool3d.clear()
	_active.clear()
	if world_root and is_instance_valid(world_root):
		world_root.queue_free()
	world_root = null
	space_fn = Callable()


func _space(pos: Vector3) -> Dictionary:
	if space_fn.is_valid():
		return space_fn.call(pos)
	return {"room": "forest", "occl": 0}


func _bus_for(sp: Dictionary, voice := false) -> String:
	var occl: int = int(sp.get("occl", 0))
	if occl >= 2:
		return "Voice_Muffled" if voice else "World_Muffled"
	match String(sp.get("room", "forest")):
		"cave":
			return "Voice_Cave" if voice else "World_Cave"
		"nest":
			return "Voice_Nest" if voice else "World_Nest"
	return "Voice_World" if voice else "World"


# ================================================================== 재생
## 소리 재생. pos 가 있으면 세계 속 그 자리에서 (입체), 없으면 화면 소리.
func play(sfx_name: String, pos: Variant = null, vol_db: float = 0.0, pitch_var: float = -1.0) -> void:
	var n: String = ALIAS.get(sfx_name, sfx_name)
	var kit: Dictionary = KITS.get(n, {})
	var s := _pick(n)
	if s == null:
		return
	var now := Time.get_ticks_msec() / 1000.0
	if now - float(_last_time.get(n, -9.0)) < float(kit.get("cd", 0.0)):
		return
	_last_time[n] = now
	var pv: float = float(kit.get("p", 0.06)) if pitch_var < 0.0 else pitch_var
	var pitch := 1.0 + randf_range(-pv, pv)
	var v: float = float(kit.get("v", -6.0)) + vol_db
	var positional: bool = pos != null and not bool(kit.get("ui", false)) and not pool3d.is_empty()
	var p: Node = null
	if positional:
		var sp := _space(pos)
		var occl: int = int(sp.get("occl", 0))
		v -= [0.0, 2.5, 6.0, 9.0][clampi(occl, 0, 3)]
		var a := _take3d(n, kit)
		a.stream = s
		a.global_position = pos
		a.volume_db = v
		a.pitch_scale = pitch
		a.max_distance = float(kit.get("r", 560.0)) / PPU
		a.bus = _bus_for(sp)
		a.play()
		p = a
	else:
		# 세계가 없거나 UI 소리: 거리만 반영해 화면 소리로
		if pos != null and use_listener and not bool(kit.get("ui", false)):
			var d: float = (Vector3(pos) - listener).length()
			var r: float = float(kit.get("r", 560.0)) / PPU
			if d > r:
				return
			v -= clampf(d / r, 0.0, 1.0) * 14.0
		var b := _take_ui(n, kit)
		b.stream = s
		b.volume_db = v
		b.pitch_scale = pitch
		b.play()
		p = b
	_track(n, p)
	if kit.has("duck"):
		duck(float(kit["duck"]))
	if kit.has("cap") and Prefs.captions:
		captioned.emit(String(kit["cap"]), pos)


func _track(n: String, p: Node) -> void:
	var arr: Array = _active.get(n, [])
	arr = arr.filter(func(x): return is_instance_valid(x) and x.playing and x != p)
	arr.append(p)
	_active[n] = arr


func _take3d(n: String, kit: Dictionary) -> AudioStreamPlayer3D:
	var limit: int = int(kit.get("n", 4))
	var arr: Array = (_active.get(n, []) as Array).filter(func(x): return is_instance_valid(x) and x.playing)
	if arr.size() >= limit and arr[0] is AudioStreamPlayer3D:
		return arr[0]                       # 같은 소리가 너무 많으면 가장 오래된 것을 끊고 다시 쓴다
	for a in pool3d:
		if not a.playing:
			return a
	# 다 쓰는 중: 가장 작은 소리를 끊는다
	var best: AudioStreamPlayer3D = pool3d[0]
	for a in pool3d:
		if a.volume_db < best.volume_db:
			best = a
	return best


func _take_ui(n: String, kit: Dictionary) -> AudioStreamPlayer:
	var bus := "UI"
	for a in pool_ui:
		if not a.playing:
			a.bus = bus
			return a
	return pool_ui[randi() % pool_ui.size()]


## 위치 고정 반복음 (모닥불·웅덩이·수정): 노드에 붙어 따라다닌다
func loop_at(node: Node3D, sfx_name: String, vol_db := 0.0) -> AudioStreamPlayer3D:
	var s := _pick(sfx_name)
	if s == null or node == null:
		return null
	var kit: Dictionary = KITS.get(sfx_name, {})
	var a := _new3d()
	a.stream = s
	a.volume_db = float(kit.get("v", -10.0)) + vol_db
	a.max_distance = float(kit.get("r", 260.0)) / PPU
	a.unit_size = 4.0
	a.autoplay = true
	node.add_child(a)
	if node.is_inside_tree():
		var sp := _space(node.global_position)
		a.bus = _bus_for(sp)
		a.play(randf() * maxf(0.1, s.get_length() - 0.1))
	return a


# ================================================================== 덕킹
func duck(db: float, hold := 0.25) -> void:
	_duck_db = minf(_duck_db, -absf(db))
	_duck_hold = maxf(_duck_hold, hold)
	_set_vol("Music", Prefs.vol_music)


# ================================================================== 음악
func music(track: String, fade := 1.2) -> void:
	if track == music_name:
		return
	music_name = track
	var spec: Dictionary = MUSIC.get(track, {})
	var old: Dictionary = _decks[_deck]
	_fade(old["base"], -80.0, fade, true)
	_fade(old["stem"], -80.0, fade, true)
	if spec.is_empty():
		return
	_deck = 1 - _deck
	var d: Dictionary = _decks[_deck]
	var base_s := _music_stream(String(spec["base"]))
	if base_s == null:
		return
	var bp: AudioStreamPlayer = d["base"]
	var spl: AudioStreamPlayer = d["stem"]
	bp.stream = base_s
	bp.volume_db = -40.0
	var stem_s: AudioStream = _music_stream(String(spec.get("stem", ""))) if spec.has("stem") else null
	spl.stream = stem_s
	spl.volume_db = -80.0
	bp.play()
	if stem_s:
		spl.play()
	d["name"] = track
	_fade(bp, 0.0, fade, false)
	_intensity = 0.0


func _music_stream(n: String) -> AudioStream:
	for ext in [".ogg", ".wav"]:
		var p: String = MUSIC_DIR + n + ext
		if ResourceLoader.exists(p):
			var s = load(p)
			if s is AudioStreamOggVorbis:
				(s as AudioStreamOggVorbis).loop = true
			elif s is AudioStreamWAV:
				var w: AudioStreamWAV = s
				w.loop_mode = AudioStreamWAV.LOOP_FORWARD
				w.loop_end = int(w.get_length() * w.mix_rate)
			return s
	return null


func _fade(p: AudioStreamPlayer, to_db: float, t: float, stop_after: bool) -> void:
	var tw := create_tween()
	tw.tween_property(p, "volume_db", to_db, t).set_trans(Tween.TRANS_SINE)
	if stop_after:
		tw.tween_callback(p.stop)


## 전투 강도 0~1: 전투 겹(stem)을 섞는다
func set_intensity(x: float) -> void:
	_intensity_target = clampf(x, 0.0, 1.0)


func stinger(n: String, vol_db := 0.0) -> void:
	var s := _music_stream(n)
	if s == null:
		return
	if s is AudioStreamOggVorbis:
		(s as AudioStreamOggVorbis).loop = false
	elif s is AudioStreamWAV:
		(s as AudioStreamWAV).loop_mode = AudioStreamWAV.LOOP_DISABLED
	_stinger.stream = s
	_stinger.volume_db = vol_db
	_stinger.play()
	duck(7.0, s.get_length() * 0.7)


# ================================================================== 환경음
func ambience(n: String, fade := 2.0) -> void:
	if n == amb_name:
		return
	amb_name = n
	var old := _amb[_amb_i]
	_fade(old, -80.0, fade, true)
	if n == "":
		return
	var s := _amb_stream(n)
	if s == null:
		return
	_amb_i = 1 - _amb_i
	var a := _amb[_amb_i]
	a.stream = s
	a.volume_db = -50.0
	a.play(randf() * maxf(0.1, s.get_length() - 1.0))
	_fade(a, 0.0, fade, false)


func _amb_stream(n: String) -> AudioStream:
	for ext in [".ogg", ".wav"]:
		var p: String = AMB_DIR + n + ext
		if ResourceLoader.exists(p):
			var s = load(p)
			if s is AudioStreamOggVorbis:
				(s as AudioStreamOggVorbis).loop = true
			return s
	return null


# ================================================================== 프레임
func _process(dt: float) -> void:
	# 덕킹 복귀
	if _duck_hold > 0.0:
		_duck_hold -= dt
	elif _duck_db < 0.0:
		_duck_db = minf(0.0, _duck_db + dt * 14.0)
		_set_vol("Music", Prefs.vol_music)
	# 전투 겹 섞기
	_intensity = move_toward(_intensity, _intensity_target, dt * (0.6 if _intensity_target > _intensity else 0.25))
	var d: Dictionary = _decks[_deck]
	var spl: AudioStreamPlayer = d["stem"]
	if spl.playing and spl.stream:
		spl.volume_db = linear_to_db(maxf(_intensity, 0.0001))
		var bp: AudioStreamPlayer = d["base"]
		# 박자 어긋남 보정 (같은 길이의 겹이 따로 돌다 조금 밀리면 맞춘다)
		if bp.playing and absf(bp.get_playback_position() - spl.get_playback_position()) > 0.05:
			spl.seek(bp.get_playback_position())
