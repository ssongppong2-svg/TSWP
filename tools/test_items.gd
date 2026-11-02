extends Node
## 아이템·궁극기 자가 시험: 모든 장비를 차례로 줍고(5칸 넘치면 떨어지는지) · 소비 아이템을 다 써 보고 · 직업마다 궁극기
## 실행: godot --headless --path . -- --map=forest --itemtest   (main 이 붙인다)

var main: Node
var t := 0.0
var step := 0
var fails := 0
var ids: Array = []
var cons: Array = []
var _last_map := ""


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	main = get_parent()
	for id in Items.ITEMS:
		if Items.is_consumable(id):
			cons.append(id)
		else:
			ids.append(id)


func check(cond: bool, what: String) -> void:
	print(("ok   " if cond else "FAIL ") + what)
	if not cond:
		fails += 1


func _process(dt: float) -> void:
	t += dt
	if t < 0.25:
		return
	t = 0.0
	var w: Player = main.walker
	if w == null or not is_instance_valid(w):
		return
	w.hp = w.max_hp()
	if main.map_name != _last_map:
		print("map -> ", main.map_name, " at step ", step)
		_last_map = main.map_name
	if pre < 3:
		_pre(w)
		return
	if step < ids.size():
		var before := get_tree().get_nodes_in_group("loot").size()
		w.pick_up(ids[step])
		check(w.inv.slots.has(ids[step]), "장비 줍기 %s" % ids[step])
		if step >= Items.SLOTS:
			check(get_tree().get_nodes_in_group("loot").size() == before + 1, "넘치면 오래된 것 떨어짐")
		check(w.inv.slots.size() <= Items.SLOTS, "5칸 넘지 않음")
		w.ult = DB.ULT_MAX
		w.ctrl.ult = true
	elif step < ids.size() + cons.size():
		var id: String = cons[step - ids.size()]
		if id == "recall":
			step += 1
			return
		w.pick_up(id)
		w.stun_t = 0.0                      # 앞에서 버린 바나나 껍질을 밟았을 수 있다
		check(w.inv.cons == id, "소비 줍기 %s" % id)
		w.use_consumable()
		check(w.inv.cons == "", "소비 쓰기 %s" % id)
	elif step < ids.size() + cons.size() + 4:
		var k := step - ids.size() - cons.size()
		w.set_job(DB.JOB_ORDER[k])
		check(w.inv.slots.size() > 0 and w.mods.has("items"), "직업 바꿔도 장비 능력치 유지 (%s)" % w.job)
		w.ult = DB.ULT_MAX
		w._ult()
		check(w.ult == 0.0, "궁극기 %s" % w.J["ult"]["name"])
	elif step == ids.size() + cons.size() + 8:
		w.inv.clear()
		check(not w.mods.has("items") or (w.mods["items"] as Dictionary).is_empty(), "가방 비우면 능력치 0")
		var party = get_tree().get_first_node_in_group("party")
		var st: Dictionary = party.stats.get(w.slot, {})
		check(float(st.get("dmg", 0)) > 0.0 and int(st.get("items", 0)) > 0, "결과 기록: 피해 %d · 아이템 %d" % [int(st.get("dmg", 0)), int(st.get("items", 0))])
		party.add_gold(100)
		var g: int = party.gold
		w.pick_up("lucky_coin")
		w.collect_gold(10)
		check(party.gold == g + 15, "행운의 동전 금화 +50%% (%d)" % (party.gold - g))
		print("RESULT ", "PASS" if fails == 0 else "FAIL %d" % fails)
		get_tree().quit(1 if fails > 0 else 0)
	step += 1


## 지역 장치 시험 (아이템 시험 전에 — 궁극기로 날아가 다른 지역에 갈 수 있어서)
var pre := 0
func _pre(w: Player) -> void:
	if pre == 0:
		# 협동 퍼즐: 한 발판엔 사람, 다른 발판엔 상자
		var pz := get_tree().get_first_node_in_group("puzzles") as PlatePuzzle
		check(pz != null, "숲에 협동 퍼즐이 있다 (%s)" % main.map_name)
		if pz:
			w.global_position = pz.plate_pos[0] + Vector3(0, 0.1, 0)
			w.velocity = Vector3.ZERO
			var cr := get_tree().get_first_node_in_group("crates") as Node3D
			check(cr != null, "밀 수 있는 상자가 있다")
			if cr:
				cr.global_position = pz.plate_pos[1] + Vector3(0, 0.1, 0)
	elif pre == 2:
		var pz := get_tree().get_first_node_in_group("puzzles") as PlatePuzzle
		check(pz != null and pz.solved, "발판 둘 → 철창 열림")
		# 금 간 바위: 폭발 두 번에 부서진다
		var rocks := get_tree().get_nodes_in_group("breakable")
		check(rocks.size() >= 1, "금 간 바위가 있다 (%d)" % rocks.size())
		if rocks.size() > 0:
			var rk := rocks[0] as Node3D
			var chests := get_tree().get_nodes_in_group("chests").size()
			get_tree().call_group("breakable", "blast", rk.global_position, 1.5)
			get_tree().call_group("breakable", "blast", rk.global_position, 1.5)
			check(rk.is_queued_for_deletion() and get_tree().get_nodes_in_group("chests").size() == chests + 1, "바위가 부서지고 보물 상자")
	pre += 1
