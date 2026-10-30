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
var job := "warrior"
var args := {}
var _frame := 0
var _game_t := 0.0
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
	party = Party.new()
	add_child(party)
	party.setup(1)
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
	match name_:
		"forest":
			map = ForestMap.new()
		"nest":
			map = NestMap.new()
		_:
			map = CampMap.new()
	world.add_child(map)
	map.build(7)
	env.apply(map.env_preset, true)
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
	rig = CamRig.new()
	world.add_child(rig)
	rig.setup(pv)
	rig.bounds = Rect2(0, 0, map.WM, map.HM)
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
			if args.has("bot"):
				args["quit"] = str(_game_t + 4.0))
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
	ui.add_child(_vignette())
	hud = Hud.new()
	hud.party = party
	ui.add_child(hud)
	help = PixelFont.label("WASD 이동 Shift 달리기 Space 구르기/점프 클릭 공격 Q 스킬 Tab 직업 F2 시점 1·2·3 지역", "small", Color(1, 1, 1, 0.8))
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


## 화면 가장자리를 도트 디더로 살짝 어둡게 — 시선이 가운데(내 캐릭터)로 모이고 가장자리 정보가 덜 시끄럽게
func _vignette() -> TextureRect:
	var w := PixelView.GW
	var h := PixelView.GH
	var img := Image.create(w, h, false, Image.FORMAT_RGBA8)
	var bayer := [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5]
	for y in h:
		for x in w:
			var dx := (x + 0.5 - w * 0.5) / (w * 0.5)
			var dy := (y + 0.5 - h * 0.5) / (h * 0.5)
			var d := sqrt(dx * dx * 0.8 + dy * dy * 1.1)
			var k := clampf((d - 0.72) / 0.55, 0.0, 1.0)
			var th: float = (float(bayer[(y % 4) * 4 + (x % 4)]) + 0.5) / 16.0
			if k > th:
				img.set_pixel(x, y, Color(0.05, 0.06, 0.09, 0.22 + k * 0.18))
	var tr := TextureRect.new()
	tr.texture = ImageTexture.create_from_image(img)
	tr.size = Vector2(w, h)
	tr.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tr.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	return tr


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
		map.spawn_check([walker])
		map.tick([walker])
	# 도트 조준점
	var mp := pv.to_ui(get_tree().root.get_mouse_position())
	cursor.position = (mp - Vector2(5, 5)).round()
	cursor.visible = Input.mouse_mode == Input.MOUSE_MODE_HIDDEN and not args.has("bot")
	if Input.is_action_just_pressed("debug_side"):
		_manual_side = rig.side_goal < 0.5
	if Input.is_key_pressed(KEY_1) and (map_name != "camp" or party.over):
		if party.over:
			party.over = false
			party.setup(1)
		_go("camp", Vector3(20, 0, 19.5))
	if Input.is_key_pressed(KEY_2) and map_name != "forest":
		_go("forest", Vector3(32, 0, 44))
	if Input.is_key_pressed(KEY_3) and map_name != "nest":
		_go("nest", Vector3(18, 0, 30.5))
	if args.has("shot") and _frame == int(args.get("frames", "45")):
		var img := get_viewport().get_texture().get_image()
		img.save_png(String(args["shot"]))
		print("saved ", args["shot"])
		get_tree().quit()
	if args.has("tour"):
		_tour(dt)
	if args.has("bot"):
		_bot(dt)
	_game_t += dt
	if args.has("quit") and _game_t > float(args["quit"]):
		get_tree().quit()


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
