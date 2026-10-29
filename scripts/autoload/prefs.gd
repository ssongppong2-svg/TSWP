extends Node
## 설정 저장 (user://settings.cfg)

signal changed

const PATH := "user://settings.cfg"

var player_name := ""
var last_ip := "127.0.0.1"
var port := 24680
var job := "warrior"
var seen_help := false
# 소리
var vol_master := 1.0
var vol_music := 0.7
var vol_sfx := 0.8
var vol_amb := 0.7
var vol_voice := 1.0
# 음성채팅 (문서: 항상 켜진 마이크 · 거리/벽/울림 반영)
var voice_enabled := true
var voice_device := "Default"
var voice_threshold := 0.025      # 이 크기(RMS) 이상일 때만 보낸다 (입력 감도)
var voice_hear := true            # 남의 목소리 듣기
# 화면
var fullscreen := false
var quality := 2                  # 화질: 0 낮음 1 중간 2 높음 3 최고
var shake := true                 # 화면 흔들림 (끄면 30%로 약하게)
var reduce_flash := false         # 번쩍임 감소
# UI · 접근성
var ui_size := 0                  # -1 작게 / 0 보통 / 1 크게
var ui_opacity := 1.0
var damage_numbers := true
var captions := false             # 소리 자막
var colorblind := 0               # 0 없음 1 적록(제1·2색약) 2 청황(제3색약)
var show_net := false             # 핑·FPS 표시
var keybinds := {}                # 액션 → [physical_keycode …] (비면 기본값)


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	load_prefs()
	apply_window()


func load_prefs() -> void:
	var cf := ConfigFile.new()
	if cf.load(PATH) != OK:
		player_name = "용사%d" % randi_range(10, 99)
		return
	player_name = cf.get_value("p", "name", "용사%d" % randi_range(10, 99))
	last_ip = cf.get_value("p", "ip", last_ip)
	port = int(cf.get_value("p", "port", port))
	job = cf.get_value("p", "job", job)
	seen_help = bool(cf.get_value("p", "seen_help", seen_help))
	vol_master = float(cf.get_value("a", "master", vol_master))
	vol_music = float(cf.get_value("a", "music", vol_music))
	vol_sfx = float(cf.get_value("a", "sfx", vol_sfx))
	vol_amb = float(cf.get_value("a", "amb", vol_amb))
	vol_voice = float(cf.get_value("a", "voice", vol_voice))
	voice_enabled = bool(cf.get_value("voice", "enabled", voice_enabled))
	voice_device = String(cf.get_value("voice", "device", voice_device))
	voice_threshold = float(cf.get_value("voice", "threshold", voice_threshold))
	voice_hear = bool(cf.get_value("voice", "hear", voice_hear))
	fullscreen = bool(cf.get_value("v", "fullscreen", fullscreen))
	shake = bool(cf.get_value("v", "shake", shake))
	quality = clampi(int(cf.get_value("v", "quality", quality)), 0, 3)
	reduce_flash = bool(cf.get_value("v", "reduce_flash", reduce_flash))
	ui_size = clampi(int(cf.get_value("ui", "size", ui_size)), -1, 1)
	ui_opacity = clampf(float(cf.get_value("ui", "opacity", ui_opacity)), 0.3, 1.0)
	damage_numbers = bool(cf.get_value("ui", "damage_numbers", damage_numbers))
	captions = bool(cf.get_value("ui", "captions", captions))
	colorblind = clampi(int(cf.get_value("ui", "colorblind", colorblind)), 0, 2)
	show_net = bool(cf.get_value("ui", "show_net", show_net))
	keybinds = cf.get_value("input", "binds", {})


func save_prefs() -> void:
	var cf := ConfigFile.new()
	cf.set_value("p", "name", player_name)
	cf.set_value("p", "ip", last_ip)
	cf.set_value("p", "port", port)
	cf.set_value("p", "job", job)
	cf.set_value("p", "seen_help", seen_help)
	cf.set_value("a", "master", vol_master)
	cf.set_value("a", "music", vol_music)
	cf.set_value("a", "sfx", vol_sfx)
	cf.set_value("a", "amb", vol_amb)
	cf.set_value("a", "voice", vol_voice)
	cf.set_value("voice", "enabled", voice_enabled)
	cf.set_value("voice", "device", voice_device)
	cf.set_value("voice", "threshold", voice_threshold)
	cf.set_value("voice", "hear", voice_hear)
	cf.set_value("v", "fullscreen", fullscreen)
	cf.set_value("v", "shake", shake)
	cf.set_value("v", "quality", quality)
	cf.set_value("v", "reduce_flash", reduce_flash)
	cf.set_value("ui", "size", ui_size)
	cf.set_value("ui", "opacity", ui_opacity)
	cf.set_value("ui", "damage_numbers", damage_numbers)
	cf.set_value("ui", "captions", captions)
	cf.set_value("ui", "colorblind", colorblind)
	cf.set_value("ui", "show_net", show_net)
	cf.set_value("input", "binds", keybinds)
	cf.save(PATH)
	changed.emit()


func apply_window() -> void:
	if DisplayServer.get_name() == "headless":
		return
	if fullscreen:
		DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN)
	else:
		if DisplayServer.window_get_mode() == DisplayServer.WINDOW_MODE_FULLSCREEN:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
		fit_window()


## 화면에 들어가는 가장 큰 정수 배율(480x270 기준)로 창 크기를 맞춘다
func fit_window() -> void:
	var scr := DisplayServer.screen_get_usable_rect(DisplayServer.window_get_current_screen())
	if scr.size.x <= 0:
		return
	var k := clampi(mini(int((scr.size.x - 40) / 480.0), int((scr.size.y - 70) / 270.0)), 1, 8)
	var sz := Vector2i(480 * k, 270 * k)
	DisplayServer.window_set_size(sz)
	DisplayServer.window_set_position(scr.position + (scr.size - sz) / 2)


func toggle_fullscreen() -> void:
	fullscreen = not fullscreen
	apply_window()
	save_prefs()
