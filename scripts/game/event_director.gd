class_name EventDirector
extends Node
## 돌발 이벤트 (게임 성경: "30초 안에는 반드시 새로운 상황") — 탐험 지역에서 35~50초마다
##  · 시청자 투표(설정에서 켬): 세 가지를 띄우고 10초 동안 채팅 !1 !2 !3 으로 고른다 (치지직 · 없으면 가짜 시청자)
##  · 투표를 끄면 무작위로 바로
## main 이 players()/map/world 를 알려 준다

const EVENTS := {
	"coin_rain": ["금화 비", "하늘에서 금화가 쏟아진다 — 먼저 줍는 사람이 임자"],
	"meteor": ["메테오", "운석이 떨어진다! 빨간 원을 피하라"],
	"banana": ["바나나 대잔치", "바닥에 바나나 껍질이 가득"],
	"hot_potato": ["뜨거운 감자", "폭탄이 누군가에게 붙었다 — 남에게 부딪혀 넘겨라!"],
	"ambush": ["기습", "시청자 이름표를 단 적들이 몰려온다"],
	"blessing": ["축복", "모두 체력 회복 + 잠깐 빨라진다"],
	"curse_fog": ["혼란의 안개", "8초 동안 좌우가 뒤바뀐다"],
	"giant": ["거인화", "누군가 거대해진다 (15초)"],
	"goldbug": ["황금 거미", "잡으면 금화와 좋은 아이템 — 도망간다!"],
}

var main: Node
var chat: ChzzkChat
var t := 25.0
var voting := false
var vote_t := 0.0
var choices: Array = []
var votes := [0, 0, 0]
var _voters := {}
var _rng := RandomNumberGenerator.new()


func _ready() -> void:
	_rng.randomize()
	chat.chat.connect(_on_chat)


func active() -> bool:
	return main.state == "play" and main.map_name != "camp" and not main._switching and not main.afterparty


func _process(dt: float) -> void:
	if not active():
		if voting:
			_end_vote(false)
		return
	if Diff.meme("gold_rain"):
		_meme_gold(dt)
	if voting:
		vote_t -= dt
		main.hud.vote_update(choices, votes, vote_t, chat.connected)
		if vote_t <= 0.0:
			_end_vote(true)
		return
	t -= dt
	if t <= 0.0:
		t = _rng.randf_range(35.0, 50.0)
		var keys: Array = EVENTS.keys()
		keys.shuffle()
		choices = keys.slice(0, 3)
		if Prefs.chat_vote and not main.args.has("bot"):
			voting = true
			vote_t = 10.0
			votes = [0, 0, 0]
			_voters.clear()
			chat.set_vote(true, 3)
			Audio.play("alarm")
			main.hud.vote_update(choices, votes, vote_t, chat.connected)
		else:
			run(String(choices[0]))


func _on_chat(nick: String, msg: String) -> void:
	if not voting:
		return
	var m := msg.strip_edges()
	for i in 3:
		if m.begins_with("!%d" % (i + 1)) and not _voters.has(nick):
			_voters[nick] = true
			votes[i] += 1


func _end_vote(do_run: bool) -> void:
	voting = false
	chat.set_vote(false)
	main.hud.vote_update([], [], 0.0, false)
	if not do_run:
		return
	var best := 0
	for i in 3:
		if votes[i] > votes[best] or (votes[i] == votes[best] and _rng.randf() < 0.5):
			best = i
	run(String(choices[best]))


## 이벤트 실행
func run(id: String) -> void:
	var info: Array = EVENTS[id]
	main.hud.banner("⚡ %s ⚡" % info[0], 2.4)
	main.hud.notify("%s — %s" % [info[0], info[1]], Color("#ffd860"))
	Audio.play("troll")
	Achievements.add(get_tree(), "events")
	call("_ev_" + id)


func _players() -> Array:
	var out: Array = []
	for f in get_tree().get_nodes_in_group("fighters"):
		if f is Player and not (f as Player).dead:
			out.append(f)
	return out


func _near(p: Node3D, r: float) -> Vector3:
	var a := _rng.randf() * TAU
	var d := _rng.randf_range(0.5, r)
	var at := p.global_position + Vector3(cos(a) * d, 0, sin(a) * d)
	at.y = main.map.terrain.height_at(at)
	return at


# ------------------------------------------------------------------ 이벤트들
func _ev_coin_rain() -> void:
	var ps := _players()
	for k in 36:
		if ps.is_empty() or not active():
			return
		var at := _near(ps[k % ps.size()], 6.0) + Vector3(0, 5.0, 0)
		Coin.burst(main.map, at, 1, main.map.terrain)
		await get_tree().create_timer(0.11, false).timeout


func _ev_meteor() -> void:
	var ps := _players()
	for k in 8:
		if ps.is_empty() or not active():
			return
		var at := _near(ps[k % ps.size()], 4.5)
		Telegraph.circle(main.map, at, 1.6, 1.3, Color(1, 0.4, 0.15))
		_meteor_hit(at)
		await get_tree().create_timer(0.35, false).timeout


func _meteor_hit(at: Vector3) -> void:
	await get_tree().create_timer(1.3, false).timeout
	if not is_instance_valid(main.map):
		return
	Audio.play("explode_l", at, -4.0)
	get_tree().call_group("breakable", "blast", at, 1.6)
	Fx.pop(main.map, at + Vector3(0, 0.5, 0), ["boom0", "boom1", "boom2", "boom3", "boom4", "boom5"], 16.0)
	Particles.burst(main.map, at + Vector3(0, 0.4, 0), Color("#fff0a0"), Color("#e05a2a"), 24, 6.0)
	for f in Combat.in_radius(get_tree(), at, 1.6):
		var h := Hit.make(22.0, null, Combat.push(at, (f as Node3D).global_position, 9.0, 0.6), "bomb")
		h.explosive = true
		h.status = {"burn": 2.0}
		(f as Fighter).take(h)
	if main.rig:
		main.rig.shake(4.0)


func _ev_banana() -> void:
	for p in _players():
		for k in 6:
			Peel.drop(main.map, _near(p, 6.0), null, main.map.terrain)


func _ev_hot_potato() -> void:
	var ps := _players()
	if ps.is_empty():
		return
	HotPotato.attach(ps[_rng.randi_range(0, ps.size() - 1)])


func _ev_ambush() -> void:
	var ps := _players()
	if ps.is_empty():
		return
	var kinds := ["acorn", "acorn", "spider", "shroom", "boomer"]
	for k in 5:
		var p := ps[k % ps.size()] as Node3D
		var a := _rng.randf() * TAU
		var at := p.global_position + Vector3(cos(a), 0, sin(a)) * 7.0
		at.y = main.map.terrain.height_at(at) + 0.2
		var e := Enemy.spawn(main.map, kinds[k], at, main.map.terrain, main.rig, k == 0, chat.random_viewer())
		main.map.enemy_spawned.emit(e)
	Audio.play("spawn")


func _ev_blessing() -> void:
	for p in _players():
		var pl := p as Player
		var h := Hit.make(pl.max_hp() * 0.5, null, Vector3.ZERO, "item")
		h.heal = true
		pl.take(h)
		pl.set_timed_mod("bless", {"speed": 0.35, "attack": 0.2}, 10.0)
		Fx.pop(pl.get_parent(), pl.center(), ["plus0", "plus1", "plus2"], 10.0, Vector3(0, 1.2, 0))
	Audio.play("heal_pulse")


func _ev_curse_fog() -> void:
	for p in _players():
		(p as Fighter).apply_status("confuse", 8.0)


func _ev_giant() -> void:
	var ps := _players()
	if ps.is_empty():
		return
	var pl := ps[_rng.randi_range(0, ps.size() - 1)] as Player
	pl.set_timed_mod("giant", {"max_hp": 0.6, "attack": 0.4, "speed": -0.2, "knock_taken": -0.6}, 15.0)
	pl.hp = pl.max_hp()
	var tw := pl.create_tween()
	tw.tween_property(pl.spr.mesh, "scale", Vector3(1.6, 1.6, 1), 0.4).set_trans(Tween.TRANS_BACK)
	tw.tween_interval(14.6)
	tw.tween_property(pl.spr.mesh, "scale", Vector3.ONE, 0.3)
	main.hud.notify("%s 거대해졌다!" % pl.nick, Color("#ffb060"))


func _ev_goldbug() -> void:
	var ps := _players()
	if ps.is_empty():
		return
	var p := ps[0] as Node3D
	var a := _rng.randf() * TAU
	var at := p.global_position + Vector3(cos(a), 0, sin(a)) * 6.0
	at.y = main.map.terrain.height_at(at) + 0.2
	var e := Enemy.spawn(main.map, "goldbug", at, main.map.terrain, main.rig)
	main.map.enemy_spawned.emit(e)


## 밈 모드 '금화 비': 6초마다 조금씩
var _gold_t := 6.0
func _meme_gold(dt: float) -> void:
	_gold_t -= dt
	if _gold_t <= 0.0:
		_gold_t = 6.0
		for p in _players():
			Coin.burst(main.map, _near(p, 5.0) + Vector3(0, 5, 0), 2, main.map.terrain)
