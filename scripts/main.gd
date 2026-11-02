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
##    --bot[=job]         자동 플레이 (가까운 적에게 가서 싸우고 스킬도 쓴다 — 시험·영상용)
##    --quit=초           몇 초 뒤 끝내기
##    --title             타이틀 화면을 강제로 띄운다 (기본: 위 인자가 없으면 타이틀부터)
##    --pause             (시험용) 바로 일시정지 메뉴 · --settings 설정 화면 · --result=win|lose 결과 화면
##    --selftest          화면 흐름 자가 시험 (tools/test_flow.gd)
##    --autostart=초      타이틀에서 몇 초 뒤 자동 시작 (영상용)

const MAPS := {"camp": "캠프", "forest": "속삭이는 숲", "nest": "거미 여왕의 둥지"}

var pv: PixelView
var world: Node3D
var rig: CamRig
var env: StageEnv
var map: Region
var map_name := ""
var walker: Player
var party: Party
var hud: Hud
var air: Node3D
var job := "warrior"
var args := {}
var _frame := 0
var _game_t := 0.0
var _switching := false
var _manual_side := false
# UI
var cursor: TextureRect
var title: Label
var fade: ColorRect
# 화면 상태: title(타이틀) · play(게임) · pause(일시정지)
var state := "play"
var menu_layer: Control             # 메뉴들이 붙는 곳 (어둡게 덮는 판 포함)
var menu_dim: ColorRect
var menu_stack: Array[PixelMenu] = []
var title_ui: Control
var _wind := 0.0
var _quality_dirty := false
var _seen_hints := {}
var _cross_tex: Texture2D
var _play_t := 0.0                  # 이번 판 시간 (결과 화면)


func _ready() -> void:
	for a in OS.get_cmdline_user_args():
		var kv := String(a).trim_prefix("--").split("=", true, 1)
		args[kv[0]] = kv[1] if kv.size() > 1 else "1"
	Controls.ensure()
	process_mode = Node.PROCESS_MODE_ALWAYS          # 메뉴·커서는 일시정지 중에도 돈다
	pv = PixelView.new()
	pv.process_mode = Node.PROCESS_MODE_PAUSABLE     # 세계·HUD 는 멈춘다
	add_child(pv)
	party = Party.new()
	party.process_mode = Node.PROCESS_MODE_PAUSABLE
	add_child(party)
	party.setup(1)
	party.game_over.connect(func():
		get_tree().create_timer(1.6).timeout.connect(func(): _show_result(false)))
	_build_ui()
	var at: Variant = null
	if args.has("at"):
		var p := String(args["at"]).split(",")
		at = Vector3(float(p[0]), 0, float(p[1]))
	_load_map(String(args.get("map", "camp")), at)
	var skip_title := false
	for k in ["bot", "tour", "map", "at", "side", "shot_play"]:
		if args.has(k):
			skip_title = true
	if args.has("title") or not skip_title:
		_enter_title()
	else:
		_hint_basics()
	if args.has("selftest") and ResourceLoader.exists("res://tools/test_flow.gd"):
		add_child(load("res://tools/test_flow.gd").new())
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
	var _t0 := Time.get_ticks_usec()
	map_name = name_
	_manual_side = false
	world = Node3D.new()
	world.name = "World"
	pv.vp.add_child(world)
	env = StageEnv.new()
	world.add_child(env)
	env.setup(Prefs.quality)
	match name_:
		"forest":
			map = ForestMap.new()
		"nest":
			map = NestMap.new()
		_:
			map = CampMap.new()
	world.add_child(map)
	map.build(7)
	if args.has("prof"):
		print("build ", name_, " ", (Time.get_ticks_usec() - _t0) / 1000.0, " ms")
	env.apply(map.env_preset, true)
	var rm := pv.rays.material as ShaderMaterial
	rm.set_shader_parameter("strength", float(env.P["rays"]) if Prefs.quality >= 1 else 0.0)
	rm.set_shader_parameter("ray_col", env.P["ray_col"])
	pv.rays.visible = Prefs.quality >= 1
	RenderingServer.global_shader_parameter_set("leaf_k", float(env.P["leaf"]) if Prefs.quality >= 1 else 0.0)
	walker = Player.new()
	walker.job = job
	walker.terrain = map.terrain
	world.add_child(walker)
	walker.job_changed.connect(func(pl: Player): job = pl.job)
	var p: Vector3 = at if at != null else map.spawn
	p.y = map.terrain.height_at(p) + 0.1
	walker.position = p
	walker.spawn_point = p
	hud.player = walker
	# 출구 표지 (가까이 가면 "▲ 다음 지역" 이 떠오른다)
	for ex in map.exits:
		var r: Rect2 = ex[0]
		var c := r.get_center()
		var d3 := Vector3(1, 0, 0)
		if c.y < 2.0:
			d3 = Vector3(0, 0, -1)
		elif c.y > map.HM - 2.0:
			d3 = Vector3(0, 0, 1)
		elif c.x < 2.0:
			d3 = Vector3(-1, 0, 0)
		var sign_ := ExitSign.new()
		sign_.text = String(MAPS.get(ex[1], ex[1]))
		sign_.dir = d3
		sign_.target = walker
		map.add_child(sign_)
		var sp := Vector3(c.x, 0, c.y) - d3 * 2.2
		sp.y = map.terrain.height_at(sp)
		sign_.global_position = sp
	rig = CamRig.new()
	world.add_child(rig)
	rig.setup(pv)
	rig.bounds = Rect2(0, 0, map.WM, map.HM)
	# 공기 속 입자 (카메라를 따라다님)
	air = Node3D.new()
	world.add_child(air)
	if Prefs.quality >= 1:
		match map.env_preset:
			"forest":
				air.add_child(Particles.ambient("leaves"))
				air.add_child(Particles.ambient("pollen"))
			"nest":
				air.add_child(Particles.ambient("spores"))
			"camp":
				air.add_child(Particles.ambient("pollen"))
	var bd := Backdrop.new()
	bd.rig = rig
	world.add_child(bd)
	bd.build(map.WM, env.P)
	rig.target = walker
	rig.focus = walker.position + Vector3(0, 0.6, 0)
	walker.rig = rig
	map.rig = rig
	map.enemy_spawned.connect(func(e: Enemy): e.killed.connect(party.on_enemy_killed))
	hud.boss = null
	if map is NestMap:
		(map as NestMap).boss_started.connect(func(q: Queen):
			hud.boss = q
			_show_title("부화의 여왕"))
		(map as NestMap).victory.connect(func():
			hud.banner("부화의 여왕을 쓰러뜨렸다!", 6.0)
			get_tree().create_timer(5.0).timeout.connect(func(): _show_result(true))
			if args.has("bot"):
				args["quit"] = str(_game_t + 4.0))
	rig.side_changed.connect(func(on: bool):
		if on:
			map.terrain.make_cut(rig.lane_z + CamRig.CUT_NEAR)
		else:
			map.terrain.clear_cut())
	Audio.attach_world(world, pv.vp, func(_p): return {"room": "forest", "occl": 0})
	Audio.ambience(map.ambience)
	if state == "title":
		walker.local = false
		Audio.music("menu")
	else:
		Audio.music(map.music)
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
	ui.add_child(_vignette())
	hud = Hud.new()
	hud.party = party
	ui.add_child(hud)
	title = PixelFont.label("", "big", Color("#fff4d6"))
	title.position = Vector2(0, 56)
	title.size = Vector2(PixelView.GW, 16)
	title.add_theme_color_override("font_outline_color", Color("#1a1420"))
	title.add_theme_constant_override("outline_size", 4)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.modulate.a = 0.0
	ui.add_child(title)
	fade = ColorRect.new()
	fade.color = Color(0.05, 0.05, 0.08, 0.0)
	fade.size = Vector2(PixelView.GW, PixelView.GH)
	fade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var wm := ShaderMaterial.new()                 # 마름모 도트 전환 (color.a = 진행도)
	wm.shader = load("res://shaders/wipe.gdshader")
	fade.material = wm
	menu_layer = Control.new()
	menu_layer.size = Vector2(PixelView.GW, PixelView.GH)
	menu_layer.mouse_filter = Control.MOUSE_FILTER_IGNORE
	menu_layer.process_mode = Node.PROCESS_MODE_ALWAYS
	ui.add_child(menu_layer)
	menu_dim = ColorRect.new()
	menu_dim.color = Color(0.04, 0.03, 0.07, 0.55)
	menu_dim.size = menu_layer.size
	menu_dim.mouse_filter = Control.MOUSE_FILTER_IGNORE
	menu_dim.visible = false
	menu_layer.add_child(menu_dim)
	ui.add_child(fade)
	cursor = TextureRect.new()
	var at := AtlasTexture.new()
	at.atlas = BB.atlas(Fx.ATLAS)["tex"]
	var r: Array = BB.atlas(Fx.ATLAS)["rects"]["cursor"]
	at.region = Rect2(r[0], r[1], r[2], r[3])
	_cross_tex = at
	cursor.texture = at
	cursor.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.add_child(cursor)


## 화면 가장자리를 계단 띠로 살짝 어둡게 — 시선이 가운데(내 캐릭터)로 모이고 가장자리 정보가 덜 시끄럽게
func _vignette() -> TextureRect:
	var w := PixelView.GW
	var h := PixelView.GH
	var img := Image.create(w, h, false, Image.FORMAT_RGBA8)
	for y in h:
		for x in w:
			var dx := (x + 0.5 - w * 0.5) / (w * 0.5)
			var dy := (y + 0.5 - h * 0.5) / (h * 0.5)
			var d := sqrt(dx * dx * 0.8 + dy * dy * 1.1)
			var k := clampf((d - 0.72) / 0.55, 0.0, 1.0)
			# 디더 없이 5단 띠 (밝은 배경에서 바둑판 무늬가 드러나지 않게)
			img.set_pixel(x, y, Color(0.05, 0.06, 0.09, floorf(k * 5.0) / 5.0 * 0.38))
	var tr := TextureRect.new()
	tr.texture = ImageTexture.create_from_image(img)
	tr.size = Vector2(w, h)
	tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tr.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	return tr


var _title_tw: Tween


func _show_title(text: String) -> void:
	title.text = "~ %s ~" % text
	if _title_tw:
		_title_tw.kill()
	var tw := create_tween()
	_title_tw = tw
	title.modulate.a = 0.0
	tw.tween_property(title, "modulate:a", 1.0, 0.6)
	tw.tween_interval(1.6)
	tw.tween_property(title, "modulate:a", 0.0, 0.9)


# ------------------------------------------------------------------ 매 프레임
func _process(dt: float) -> void:
	_frame += 1
	# 도트 조준점 (메뉴에선 손가락)
	var mp := pv.to_ui(get_tree().root.get_mouse_position())
	var in_menu := state != "play"
	cursor.texture = UiKit.tex("hand") if in_menu else _cross_tex
	cursor.position = (mp - (Vector2(0, 0) if in_menu else Vector2(5, 5))).round()
	cursor.visible = Input.mouse_mode == Input.MOUSE_MODE_HIDDEN and not args.has("bot")
	if args.has("shot") and _frame == int(args.get("frames", "45")):
		var img := get_viewport().get_texture().get_image()
		img.save_png(String(args["shot"]))
		print("saved ", args["shot"])
		get_tree().quit()
	if args.has("result") and _frame == 40:
		_show_result(args["result"] == "win")
	if (args.has("pause") or args.has("settings")) and _frame == 40:
		_pause()
		if args.has("settings"):
			_open_settings()
	if get_tree().paused:
		return
	_wind += dt
	_game_t += dt
	if args.has("quit") and _game_t > float(args["quit"]):
		get_tree().quit()
	RenderingServer.global_shader_parameter_set("wind_time", _wind)
	if state == "title":
		_title_tick(dt)
		if args.has("autostart") and _game_t > float(args["autostart"]):
			args.erase("autostart")
			_start_game()
	# 사람이 지나가면 풀이 밀리게 (최대 4명)
	if walker and is_instance_valid(walker):
		var wp := walker.global_position
		RenderingServer.global_shader_parameter_set("push_a", Vector4(wp.x, wp.y, wp.z, clampf(Vector2(walker.velocity.x, walker.velocity.z).length(), 0.3, 1.0)))
		_check_zones(wp)
		if air and is_instance_valid(air) and rig:
			air.global_position = Vector3(rig.focus.x, rig.focus.y, rig.focus.z)
		map.spawn_check([walker])
		map.tick([walker])
	if state != "play":
		return
	_play_t += dt
	_hints_tick()
	if Input.is_action_just_pressed("debug_side"):
		_manual_side = rig.side_goal < 0.5
	if Input.is_key_pressed(KEY_1) and (map_name != "camp" or party.over):
		if party.over:
			party.setup(1)
		_go("camp", Vector3(20, 0, 19.5))
	if Input.is_key_pressed(KEY_2) and map_name != "forest":
		_go("forest", Vector3(32, 0, 44))
	# F10: 화질 단계 바꾸기 (0 낮음 · 1 중간 · 2 높음 · 3 최고) — 지역을 다시 불러 적용
	if Input.is_action_just_pressed("quality") and not _switching:
		Prefs.quality = (Prefs.quality + 1) % 4
		Prefs.save_prefs()
		_go(map_name, walker.global_position)
		hud.banner("화질: %s" % ["낮음", "중간", "높음", "최고"][Prefs.quality], 1.5)
	if Input.is_key_pressed(KEY_3) and map_name != "nest":
		_go("nest", Vector3(18, 0, 30.5))
	if args.has("tour"):
		_tour(dt)
	if args.has("bot"):
		_bot(dt)


## 자동 플레이: 가까운 적에게 다가가 공격, 가끔 스킬, 적이 없으면 다음 무리 쪽으로
var _bot_t := 0.0
var _bot_jobs := 0
func _bot(dt: float) -> void:
	if walker == null or walker.dead:
		return
	walker.local = false
	var c := walker.ctrl
	c.clear()
	_bot_t += dt
	if args["bot"] == "cycle":
		var want_job: String = DB.JOB_ORDER[int(_bot_t / 6.0) % DB.JOB_ORDER.size()]
		if walker.job != want_job:
			walker.set_job(want_job)
	elif args["bot"] != "1" and walker.job != args["bot"]:
		walker.set_job(String(args["bot"]))
	var best: Node3D = null
	var bd := 1e9
	for e in get_tree().get_nodes_in_group("enemies"):
		if (e as Fighter).dead:
			continue
		var d := (e as Node3D).global_position.distance_to(walker.global_position)
		if d < bd:
			bd = d
			best = e
	var nest := map as NestMap
	if nest and nest.queen and not nest.queen.dead and best == null:
		best = nest.queen
	var goal: Vector3
	var no_stop := false
	if nest and nest.queen and nest.queen.shell and walker.global_position.y < 1.0:
		# 껍질: 가까운 기둥 점프 발판으로
		var pd := 1e9
		for pl in nest.pillars:
			var pp: Vector3 = (pl as Pillar).pad_pos
			if (pl as Pillar).up and pp.distance_to(walker.global_position) < pd:
				pd = pp.distance_to(walker.global_position)
				goal = pp
		no_stop = true
	elif nest and nest.act2 and not nest.won and best == null:
		for l in nest.levers:
			if not (l as Lever).on:
				goal = (l as Node3D).global_position
				c.interact = goal.distance_to(walker.global_position) < 1.2
				break
		no_stop = true
	elif best:
		goal = best.global_position
	else:
		goal = walker.global_position
		for g in map.groups:
			if not g["done"]:
				goal = g["at"]
				break
		if nest and not nest.started:
			goal = Vector3(NestMap.CENTER.x, 0, NestMap.CENTER.y)
	var to := goal - walker.global_position
	to.y = 0.0
	var want_d := 1.0 if walker.job == "warrior" else 4.5
	if best is Queen:
		want_d += 2.2
	if no_stop:
		if to.length() > 0.3:
			c.move = Vector2(to.x, to.z).normalized()
		if best:
			c.has_aim = true
			c.aim_world = best.global_position
			c.attack_held = true
		return
	if walker.global_position.y > 1.5 and best:
		c.has_aim = true
		c.aim_world = best.global_position
		c.attack_held = true
		c.skill = fmod(_bot_t, 3.0) < dt
		return
	if best and to.length() > want_d:
		c.move = Vector2(to.x, to.z).normalized()
	elif best and to.length() < want_d - 1.5:
		c.move = -Vector2(to.x, to.z).normalized()
	elif not best and to.length() > 1.0:
		c.move = Vector2(to.x, to.z).normalized()
	c.run = not best
	if best:
		c.has_aim = true
		c.aim_world = best.global_position
		c.attack_held = to.length() < want_d + 3.5
		c.skill = to.length() < 3.5 and fmod(_bot_t, 3.0) < dt
		c.roll = fmod(_bot_t, 2.3) < dt and walker.hp < walker.max_hp() * 0.5


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


# ------------------------------------------------------------------ 타이틀 · 메뉴
func _enter_title() -> void:
	state = "title"
	hud.set_shown(false)
	hud.clear_hints()
	if walker:
		walker.local = false
		walker.ctrl.clear()
	Audio.music("menu")
	title_ui = Control.new()
	title_ui.size = Vector2(PixelView.GW, PixelView.GH)
	title_ui.mouse_filter = Control.MOUSE_FILTER_IGNORE
	title_ui.process_mode = Node.PROCESS_MODE_ALWAYS
	menu_layer.add_child(title_ui)
	menu_layer.move_child(title_ui, 1)
	# 위쪽을 어둡게 (6단 띠) — 로고가 배경과 섞이지 않게
	var w := PixelView.GW
	var h := 96
	var img := Image.create(w, h, false, Image.FORMAT_RGBA8)
	for y in h:
		var k := 1.0 - float(y) / h
		for x in w:
			img.set_pixel(x, y, Color(0.06, 0.04, 0.1, floorf(k * 6.0) / 6.0 * 0.7))
	var band := TextureRect.new()
	band.texture = ImageTexture.create_from_image(img)
	band.mouse_filter = Control.MOUSE_FILTER_IGNORE
	title_ui.add_child(band)
	var logo := TextureRect.new()
	logo.texture = load("res://assets/sprites/logo.png")
	logo.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	logo.mouse_filter = Control.MOUSE_FILTER_IGNORE
	logo.position = Vector2(roundf((w - logo.texture.get_width()) / 2.0), 18)
	logo.name = "Logo"
	title_ui.add_child(logo)
	var ver := PixelFont.label("v%s · 초기 개발판" % ProjectSettings.get_setting("application/config/version", "0"), "small", Color(1, 1, 1, 0.55))
	ver.position = Vector2(PixelView.GW - 166, PixelView.GH - 14)
	ver.size = Vector2(160, 10)
	ver.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	title_ui.add_child(ver)
	var m := PixelMenu.new("", [
		{"label": "모험 시작", "do": _start_game},
		{"label": "설정", "do": _open_settings},
		{"label": "조작법", "do": _open_controls},
		{"label": "게임 종료", "do": func(): get_tree().quit()},
	], 112)
	m.top = 168.0
	m.boxless = true
	m.back.connect(func(): pass)
	_push_menu(m, false)


func _title_tick(_dt: float) -> void:
	if walker:
		walker.ctrl.clear()
	# 로고가 천천히 숨 쉬듯 1px 오르내림
	if title_ui and title_ui.has_node("Logo"):
		var lg := title_ui.get_node("Logo") as Control
		lg.position.y = 18.0 + (1.0 if int(_wind * 1.2) % 2 == 0 else 0.0)


func _start_game() -> void:
	if state != "title" or _switching:
		return
	Audio.play("ui_start")
	_switching = true
	var tw := create_tween()
	tw.tween_property(fade, "color:a", 1.0, 0.4)
	tw.tween_callback(func():
		_close_all_menus()
		if title_ui:
			title_ui.queue_free()
			title_ui = null
		state = "play"
		hud.set_shown(true)
		walker.local = true
		Audio.music(map.music)
		_show_title(MAPS.get(map_name, map_name))
		_hint_basics())
	tw.tween_property(fade, "color:a", 0.0, 0.5)
	tw.tween_callback(func(): _switching = false)


func _back_to_title() -> void:
	_switching = true
	var tw := create_tween()
	tw.tween_property(fade, "color:a", 1.0, 0.4)
	tw.tween_callback(func():
		_close_all_menus()
		get_tree().paused = false
		menu_dim.visible = false
		state = "title"
		party.setup(1)
		_play_t = 0.0
		_load_map("camp", null)
		_enter_title())
	tw.tween_property(fade, "color:a", 0.0, 0.5)
	tw.tween_callback(func(): _switching = false)


func _pause() -> void:
	if state != "play" or _switching:
		return
	state = "pause"
	get_tree().paused = true
	menu_dim.visible = true
	hud.set_shown(false)
	Audio.play("ui_click")
	var items: Array = [
		{"label": "계속하기", "do": _resume},
		{"label": "설정", "do": _open_settings},
		{"label": "조작법", "do": _open_controls},
	]
	if map_name != "camp":
		items.append({"label": "캠프로 돌아가기", "do": func():
			_resume()
			_go("camp", Vector3(20, 0, 19.5))})
	items.append({"label": "타이틀로", "do": _back_to_title})
	items.append({"label": "게임 종료", "do": func(): get_tree().quit()})
	var m := PixelMenu.new("일시정지", items, 156)
	m.back.connect(_resume)
	_push_menu(m)


func _resume() -> void:
	if state != "pause":
		return
	_close_all_menus()
	menu_dim.visible = false
	hud.set_shown(true)
	get_tree().paused = false
	state = "play"
	if _quality_dirty:
		_quality_dirty = false
		_go(map_name, walker.global_position)


func _open_settings() -> void:
	var q0 := Prefs.quality
	var m := PixelMenu.new("설정", [
		{"label": "전체 음량", "type": "slider", "get": func(): return Prefs.vol_master, "set": func(v): Prefs.vol_master = v; Audio.apply_volumes()},
		{"label": "음악", "type": "slider", "get": func(): return Prefs.vol_music, "set": func(v): Prefs.vol_music = v; Audio.apply_volumes()},
		{"label": "효과음", "type": "slider", "get": func(): return Prefs.vol_sfx, "set": func(v): Prefs.vol_sfx = v; Audio.apply_volumes()},
		{"label": "환경음", "type": "slider", "get": func(): return Prefs.vol_amb, "set": func(v): Prefs.vol_amb = v; Audio.apply_volumes()},
		{"label": "화질", "type": "choice", "options": ["낮음", "중간", "높음", "최고"], "get": func(): return Prefs.quality, "set": func(v): Prefs.quality = v},
		{"label": "전체 화면", "type": "toggle", "get": func(): return Prefs.fullscreen, "set": func(v): Prefs.fullscreen = v; Prefs.apply_window()},
		{"label": "화면 흔들림", "type": "toggle", "get": func(): return Prefs.shake, "set": func(v): Prefs.shake = v},
		{"label": "번쩍임 줄이기", "type": "toggle", "get": func(): return Prefs.reduce_flash, "set": func(v): Prefs.reduce_flash = v},
		{"label": "피해 숫자", "type": "toggle", "get": func(): return Prefs.damage_numbers, "set": func(v): Prefs.damage_numbers = v},
		{"label": "뒤로", "do": func(): _pop_menu()},
	], 236)
	m.back.connect(func(): _pop_menu())
	m.tree_exiting.connect(func():
		Prefs.save_prefs()
		if Prefs.quality != q0:
			_apply_quality())
	_push_menu(m)


## 화질 바뀜: 게임 중이면 메뉴를 닫을 때 지역을 다시 불러 적용, 타이틀이면 바로
func _apply_quality() -> void:
	if state == "title":
		_load_map(map_name, null)
		walker.local = false
	elif state == "pause":
		_quality_dirty = true
	else:
		_go(map_name, walker.global_position)


func _open_controls() -> void:
	var rows := [["이동", "W A S D"], ["달리기", "Shift"], ["구르기 · 측면 점프", "Space"], ["공격", "왼쪽 클릭"],
		["스킬", "Q · 오른쪽 클릭"], ["상호작용", "E"], ["직업 바꾸기", "Tab"], ["시점 전환 (시험)", "F2"], ["지역 이동 (시험)", "1 · 2 · 3"]]
	var items: Array = []
	for r in rows:
		items.append({"label": r[0], "value": r[1]})
	items.append({"label": "뒤로", "do": func(): _pop_menu()})
	var m := PixelMenu.new("조작법", items, 260)
	m.back.connect(func(): _pop_menu())
	_push_menu(m)


func _push_menu(m: PixelMenu, sound := true) -> void:
	if not menu_stack.is_empty():
		menu_stack[-1].visible = false
	if _title_tw:
		_title_tw.kill()
	title.modulate.a = 0.0                      # 지역 이름이 메뉴 뒤로 비치지 않게
	menu_stack.append(m)
	menu_layer.add_child(m)
	if sound:
		Audio.play("ui_confirm")


func _pop_menu() -> void:
	if menu_stack.size() <= 1:
		if state == "pause":
			_resume()
		return
	var m: PixelMenu = menu_stack.pop_back()
	m.queue_free()
	menu_stack[-1].visible = true
	menu_stack[-1].refresh()


func _close_all_menus() -> void:
	for m in menu_stack:
		m.queue_free()
	menu_stack.clear()


func _input(e: InputEvent) -> void:
	if _switching:
		return
	if not menu_stack.is_empty():
		var p := pv.to_ui(e.position) if e is InputEventMouse else pv.to_ui(get_tree().root.get_mouse_position())
		if menu_stack[-1].handle(e, p):
			get_viewport().set_input_as_handled()
		return
	if state == "play" and e.is_action_pressed("menu"):
		_pause()
		get_viewport().set_input_as_handled()


# ------------------------------------------------------------------ 상황 힌트 (한 번씩)
func _hint_once(id: String, pairs: Array, dur := 5.0) -> void:
	if _seen_hints.has(id) or ((args.has("bot") or args.has("tour")) and not args.has("demo")):
		return
	_seen_hints[id] = true
	hud.hint(pairs, dur)


func _hint_basics() -> void:
	_hint_once("move", [["WASD", "이동"], ["Shift", "달리기"], ["Space", "구르기"]], 6.0)
	_hint_once("fight", [["클릭", "공격"], ["Q", "스킬"], ["Esc", "메뉴"]], 6.0)


func _hints_tick() -> void:
	if walker == null or walker.dead:
		return
	if rig and rig.side_goal > 0.5:
		_hint_once("side", [["Space", "점프"], ["A  D", "이동"]], 4.0)
	if map_name == "camp":
		for n in get_tree().get_nodes_in_group("interact"):
			if n is JobStand and (n as Node3D).global_position.distance_to(walker.global_position) < 3.0:
				_hint_once("job", [["E", "이 직업으로"], ["Tab", "직업 돌려 보기"]], 5.0)
	if walker.hp < walker.max_hp() * 0.35:
		_hint_once("low", [["Space", "구르면 잠깐 무적"]], 4.0)
	if hud.boss != null:
		_hint_once("boss", [["Space", "구르기로 피하기"], ["Q", "스킬은 아껴서"]], 5.0)


# ------------------------------------------------------------------ 결과 화면 (전멸 · 승리)
func _show_result(win: bool) -> void:
	if state != "play" or args.has("bot"):
		return
	state = "result"
	if walker:
		walker.local = false
		walker.ctrl.clear()
	hud.set_shown(false)
	menu_dim.visible = true
	Audio.stinger("st_win" if win else "st_lose")
	var st: Dictionary = party.stats.get(walker.slot, {"kills": 0, "deaths": 0, "dmg": 0.0, "heal": 0.0})
	var mins := int(_play_t / 60.0)
	var items: Array = [
		{"label": "처치", "type": "info", "value": "%d" % int(st["kills"])},
		{"label": "쓰러짐", "type": "info", "value": "%d" % int(st["deaths"])},
		{"label": "모은 금화", "type": "info", "value": "%d" % party.gold},
		{"label": "걸린 시간", "type": "info", "value": "%d:%02d" % [mins, int(_play_t) % 60]},
		{"label": "캠프에서 다시" if not win else "캠프로 돌아가기", "do": _result_to_camp},
		{"label": "타이틀로", "do": _back_to_title},
	]
	var m := PixelMenu.new("승리!  여왕을 쓰러뜨렸다" if win else "전멸했다…", items, 220)
	m.back.connect(func(): pass)
	_push_menu(m)


func _result_to_camp() -> void:
	_close_all_menus()
	menu_dim.visible = false
	party.setup(1)
	state = "play"
	_play_t = 0.0
	hud.set_shown(true)
	_go("camp", Vector3(20, 0, 19.5))
