extends Node
## 화면 흐름 자가 시험: 타이틀 → 시작 → 일시정지 → 설정(값 바꾸기) → 뒤로 → 계속 → 전멸 결과 → 캠프 → 타이틀
## 실행: godot --headless --path . -- --selftest   (main 이 이 노드를 붙인다 · 내보낸 게임엔 없음)
## 메뉴 입력은 실제 키 이벤트로 넣는다 (PixelMenu.handle 경로까지 시험)

var main: Node
var step := 0
var t := 0.0
var fails := 0
var _v0 := 0.0


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	main = get_parent()


func key(k: Key) -> void:
	var e := InputEventKey.new()
	e.physical_keycode = k
	e.keycode = k
	e.pressed = true
	Input.parse_input_event(e)
	var u := e.duplicate()
	u.pressed = false
	Input.parse_input_event(u)


func check(cond: bool, what: String) -> void:
	print(("ok   " if cond else "FAIL ") + what)
	if not cond:
		fails += 1


func _process(dt: float) -> void:
	t += dt
	if t < 0.6:
		return
	t = 0.0
	match step:
		0:
			check(main.state == "title", "타이틀에서 시작")
			check(not main.hud._root.visible, "타이틀에서 HUD 숨김")
			key(KEY_ENTER)                                  # 모험 시작
		1:
			pass
		2:
			check(main.state == "play", "시작 → 게임")
			check(main.walker.local, "조작 켜짐")
			key(KEY_ESCAPE)
		3:
			check(main.state == "pause" and get_tree().paused, "Esc → 일시정지")
			key(KEY_DOWN)
			key(KEY_DOWN)
			key(KEY_ENTER)                                  # 설정 (계속 · 장비 · 설정)
		4:
			check(main.menu_stack.size() == 2, "설정 열림")
			key(KEY_ENTER)                                  # 소리
			step = 39
			return
		39:
			check(main.menu_stack.size() == 3, "소리 열림")
			_v0 = Prefs.vol_master
			key(KEY_LEFT)
			step = 40
			return
		40:
			check(absf(Prefs.vol_master - (_v0 - 0.1)) < 0.01 or _v0 < 0.05, "왼쪽 → 음량 줄어듦")
			key(KEY_RIGHT)
			key(KEY_ESCAPE)                                 # 소리 닫기
			key(KEY_ESCAPE)                                 # 설정 닫기
			step = 5
			return
		5:
			check(main.menu_stack.size() == 1, "Esc → 설정 닫힘")
			key(KEY_ESCAPE)                                 # 일시정지 닫기
		6:
			check(main.state == "play" and not get_tree().paused, "Esc → 계속")
			main.party.lives = 0
			var h := Hit.new()
			h.amount = 9999.0
			main.walker.take(h)
		7, 8, 9, 10, 11, 12:
			pass
		13:
			check(main.state == "result", "전멸 → 결과 화면")
			key(KEY_ENTER)                                  # 캠프에서 다시
			step = 20
			return
		20, 21:
			pass
		22:
			check(main.state == "play" and main.map_name == "camp", "결과 → 캠프")
			check(main.party.lives == 3 and main.party.gold == 0, "새 판으로 초기화")
			key(KEY_ESCAPE)
		23:
			key(KEY_UP)
			key(KEY_UP)                                     # 게임 종료 바로 위 = 타이틀로
			key(KEY_ENTER)
		24, 25:
			pass
		26:
			check(main.state == "title" and not get_tree().paused, "일시정지 → 타이틀")
			print("RESULT ", "PASS" if fails == 0 else "FAIL %d" % fails)
			get_tree().quit(1 if fails > 0 else 0)
	step += 1
