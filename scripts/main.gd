extends Node
## 시작점: 저해상도 화면 + 지역(캠프·숲) + 카메라 + 조작 캐릭터 + 도트 UI.
##  · 지역 끝 출구로 걸어 나가면 화면이 어두워졌다가 다음 지역으로 (캠프 북쪽 길 ↔ 숲 남쪽)
##  · 측면 구간(Region.side_zones)에 들어가면 카메라가 돌아 횡스크롤, 나오면 탑다운
##  실행 인자 (-- 뒤):
##    --shot=<경로.png>   몇 프레임 뒤 화면을 저장하고 끝낸다 (--frames=N)
##    --side              측면 시점으로 시작
##    --tour              정해진 길을 걸으며 중간에 측면 전환 (godot --write-movie 와 함께 영상 만들기용)
##    --at=x,z            시작 위치
##    --map=camp|forest   지역 (기본: 캠프)

const MAPS := {"camp": "캠프", "forest": "속삭이는 숲"}

var pv: PixelView
var world: Node3D
var rig: CamRig
var env: StageEnv
var map: Region
var map_name := ""
var walker: Walker
var args := {}
var _frame := 0
var _switching := false
var _manual_side := false
# UI
var cursor: TextureRect
var help: Label
var title: Label
var fade: ColorRect


func _ready() -> void:
	for a in OS.get_cmdline_user_args():
		var kv := String(a).trim_prefix("--").split("=", true, 1)
		args[kv[0]] = kv[1] if kv.size() > 1 else "1"
	Controls.ensure()
	pv = PixelView.new()
	add_child(pv)
	_build_ui()
	var at: Variant = null
	if args.has("at"):
		var p := String(args["at"]).split(",")
		at = Vector3(float(p[0]), 0, float(p[1]))
	_load_map(String(args.get("map", "camp")), at)
	if args.has("side"):
		_manual_side = true
		rig.set_side(true, 0.0, walker.position.z)
		rig.side_t = 1.0
	if DisplayServer.get_name() != "headless" and not args.has("shot") and not args.has("tour"):
		Input.mouse_mode = Input.MOUSE_MODE_HIDDEN


# ------------------------------------------------------------------ 지역
func _load_map(name_: String, at: Variant = null) -> void:
	if world:
		Audio.detach_world()
		world.queue_free()
	map_name = name_
	_manual_side = false
	world = Node3D.new()
	world.name = "World"
	pv.vp.add_child(world)
	env = StageEnv.new()
	world.add_child(env)
	env.setup(Prefs.quality)
	map = ForestMap.new() if name_ == "forest" else CampMap.new()
	world.add_child(map)
	map.build(7)
	env.apply(map.env_preset, true)
	walker = Walker.new()
	walker.terrain = map.terrain
	world.add_child(walker)
	var p: Vector3 = at if at != null else map.spawn
	p.y = map.terrain.height_at(p) + 0.1
	walker.position = p
	rig = CamRig.new()
	world.add_child(rig)
	rig.setup(pv)
	rig.bounds = Rect2(0, 0, map.WM, map.HM)
	rig.target = walker
	rig.focus = walker.position + Vector3(0, 0.6, 0)
	walker.rig = rig
	rig.side_changed.connect(func(on: bool):
		if on:
			map.terrain.make_cut(rig.lane_z + CamRig.CUT_NEAR)
		else:
			map.terrain.clear_cut())
	Audio.attach_world(world, pv.vp, func(_p): return {"room": "forest", "occl": 0})
	Audio.music(map.music)
	Audio.ambience(map.ambience)
	_show_title(MAPS.get(name_, name_))


## 출구로 나가기: 어두워짐 → 지역 바꾸기 → 밝아짐
func _go(name_: String, at: Vector3) -> void:
	if _switching:
		return
	_switching = true
	var tw := create_tween()
	tw.tween_property(fade, "color:a", 1.0, 0.35)
	tw.tween_callback(func(): _load_map(name_, at))
	tw.tween_property(fade, "color:a", 0.0, 0.45)
	tw.tween_callback(func(): _switching = false)


# ------------------------------------------------------------------ UI
func _build_ui() -> void:
	var ui := pv.ui
	help = PixelFont.label("WASD 이동  Shift 달리기  Space 구르기/점프  클릭 베기  F2 시점  1·2 지역", "small", Color(1, 1, 1, 0.85))
	help.position = Vector2(6, PixelView.GH - 15)
	ui.add_child(help)
	title = PixelFont.label("", "big", Color("#fff4d6"))
	title.position = Vector2(0, 30)
	title.size = Vector2(PixelView.GW, 16)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.modulate.a = 0.0
	ui.add_child(title)
	fade = ColorRect.new()
	fade.color = Color(0.05, 0.05, 0.08, 0.0)
	fade.size = Vector2(PixelView.GW, PixelView.GH)
	fade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.add_child(fade)
	cursor = TextureRect.new()
	var at := AtlasTexture.new()
	at.atlas = BB.atlas(Fx.ATLAS)["tex"]
	var r: Array = BB.atlas(Fx.ATLAS)["rects"]["cursor"]
	at.region = Rect2(r[0], r[1], r[2], r[3])
	cursor.texture = at
	cursor.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.add_child(cursor)


func _show_title(text: String) -> void:
	title.text = "~ %s ~" % text
	var tw := create_tween()
	title.modulate.a = 0.0
	tw.tween_property(title, "modulate:a", 1.0, 0.6)
	tw.tween_interval(1.6)
	tw.tween_property(title, "modulate:a", 0.0, 0.9)


# ------------------------------------------------------------------ 매 프레임
func _process(dt: float) -> void:
	_frame += 1
	RenderingServer.global_shader_parameter_set("wind_time", Time.get_ticks_msec() / 1000.0)
	# 사람이 지나가면 풀이 밀리게 (최대 4명)
	if walker and is_instance_valid(walker):
		var wp := walker.global_position
		RenderingServer.global_shader_parameter_set("push_a", Vector4(wp.x, wp.y, wp.z, clampf(Vector2(walker.velocity.x, walker.velocity.z).length(), 0.3, 1.0)))
		_check_zones(wp)
	# 도트 조준점
	var mp := pv.to_ui(get_tree().root.get_mouse_position())
	cursor.position = (mp - Vector2(5, 5)).round()
	cursor.visible = Input.mouse_mode == Input.MOUSE_MODE_HIDDEN
	if Input.is_action_just_pressed("debug_side"):
		_manual_side = rig.side_goal < 0.5
	if Input.is_key_pressed(KEY_1) and map_name != "camp":
		_go("camp", Vector3(20, 0, 19.5))
	if Input.is_key_pressed(KEY_2) and map_name != "forest":
		_go("forest", Vector3(32, 0, 44))
	if args.has("shot") and _frame == int(args.get("frames", "45")):
		var img := get_viewport().get_texture().get_image()
		img.save_png(String(args["shot"]))
		print("saved ", args["shot"])
		get_tree().quit()
	if args.has("tour"):
		_tour(dt)


## 측면 구간 · 출구
func _check_zones(wp: Vector3) -> void:
	if _switching:
		return
	var p2 := Vector2(wp.x, wp.z)
	for ex in map.exits:
		if (ex[0] as Rect2).has_point(p2):
			_go(ex[1], ex[2])
			return
	if _manual_side:
		return
	var zone: Variant = null
	for z in map.side_zones:
		if (z[0] as Rect2).has_point(p2):
			zone = z
			break
	if zone != null and rig.side_goal < 0.5:
		rig.set_side(true, 0.0, float(zone[1]))
	elif zone == null and rig.side_goal > 0.5:
		rig.set_side(false)


## 영상용: 숲길을 걷다가 측면으로 돌아가 점프, 다시 탑다운으로
var _tour_t := 0.0
var _tour_jumps := {}
func _tour(dt: float) -> void:
	_tour_t += dt
	var t := _tour_t
	if t < 4.6:
		walker.auto_dir = Vector3(sin(t * 0.9) * 0.5, 0, -1).normalized()
	elif t < 10.0:
		walker.auto_dir = Vector3(1, 0, 0)
	else:
		walker.auto_dir = Vector3(0.6, 0, 1).normalized()
	if t > 4.6 and not _tour_jumps.has("side"):
		_tour_jumps["side"] = true
		_manual_side = true
		rig.set_side(true, 0.0, walker.position.z)
	for jt in [6.6, 7.6, 8.8]:
		if t > jt and not _tour_jumps.has(jt):
			_tour_jumps[jt] = true
			walker.jump()
	if t > 10.0 and not _tour_jumps.has("top"):
		_tour_jumps["top"] = true
		rig.set_side(false)
	if t > 13.0:
		get_tree().quit()
