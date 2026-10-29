class_name Controls
extends RefCounted
## 조작 키 (WASD + 마우스 조준 · 패드 왼스틱 이동/오른스틱 조준)
##  Prefs.keybinds 에 바꾼 키가 있으면 그것을 쓴다.

const DEFAULT := {
	"move_up": [KEY_W, KEY_UP],
	"move_down": [KEY_S, KEY_DOWN],
	"move_left": [KEY_A, KEY_LEFT],
	"move_right": [KEY_D, KEY_RIGHT],
	"roll": [KEY_SPACE],          # 탑다운: 구르기 · 측면: 점프
	"run": [KEY_SHIFT],
	"interact": [KEY_E],
	"emote": [KEY_Q],
	"ping": [KEY_F],
	"menu": [KEY_ESCAPE],
	"debug_side": [KEY_F2],
}
const MOUSE := {"attack": MOUSE_BUTTON_LEFT, "throw": MOUSE_BUTTON_RIGHT}
const PAD_BTN := {"roll": JOY_BUTTON_A, "attack": JOY_BUTTON_X, "throw": JOY_BUTTON_Y, "interact": JOY_BUTTON_B,
	"run": JOY_BUTTON_LEFT_SHOULDER, "menu": JOY_BUTTON_START, "emote": JOY_BUTTON_DPAD_UP, "ping": JOY_BUTTON_RIGHT_SHOULDER}
const PAD_AXIS := {"move_left": [JOY_AXIS_LEFT_X, -1.0], "move_right": [JOY_AXIS_LEFT_X, 1.0],
	"move_up": [JOY_AXIS_LEFT_Y, -1.0], "move_down": [JOY_AXIS_LEFT_Y, 1.0]}


static func ensure() -> void:
	var names := {}
	for k in DEFAULT:
		names[k] = true
	for k in MOUSE:
		names[k] = true
	for k in PAD_BTN:
		names[k] = true
	for a in names:
		if not InputMap.has_action(a):
			InputMap.add_action(a, 0.25)
		InputMap.action_erase_events(a)
		var keys: Array = Prefs.keybinds.get(a, DEFAULT.get(a, []))
		for kc in keys:
			var e := InputEventKey.new()
			e.physical_keycode = int(kc)
			InputMap.action_add_event(a, e)
		if MOUSE.has(a):
			var m := InputEventMouseButton.new()
			m.button_index = MOUSE[a]
			InputMap.action_add_event(a, m)
		if PAD_BTN.has(a):
			var j := InputEventJoypadButton.new()
			j.button_index = PAD_BTN[a]
			InputMap.action_add_event(a, j)
		if PAD_AXIS.has(a):
			var ax := InputEventJoypadMotion.new()
			ax.axis = PAD_AXIS[a][0]
			ax.axis_value = PAD_AXIS[a][1]
			InputMap.action_add_event(a, ax)


## 이동 입력 (화면 기준 2D, 길이 ≤ 1)
static func move_vec() -> Vector2:
	return Input.get_vector("move_left", "move_right", "move_up", "move_down")


## 패드 오른스틱 조준 (없으면 0)
static func aim_stick() -> Vector2:
	var v := Vector2(Input.get_joy_axis(0, JOY_AXIS_RIGHT_X), Input.get_joy_axis(0, JOY_AXIS_RIGHT_Y))
	return v if v.length() > 0.3 else Vector2.ZERO
