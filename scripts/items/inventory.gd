class_name Inventory
extends RefCounted
## 한 플레이어의 가방: 장비 5칸 + 소비 1칸(3개까지 겹침).
##  · 지역을 옮기면 Player 노드는 새로 생기지만 가방은 Party 가 파티 번호별로 들고 있어 그대로 이어진다
##  · 능력치(mods)는 모두 더해서 Player 의 "items" 출처 하나로 넣는다 (패시브와 서로 덮어쓰지 않게)

signal changed

var slots: Array[String] = []      # 장비 (먼저 얻은 것이 앞)
var cons := ""                     # 소비 아이템
var cons_n := 0


func count(id: String) -> int:
	return slots.count(id)


## 특수 효과를 가진 장비 수 (같은 것 여러 개면 겹친다)
func fx(name_: String) -> int:
	var n := 0
	for id in slots:
		if name_ in (Items.get_item(id).get("fx", []) as Array):
			n += 1
	return n


func mods() -> Dictionary:
	var out := {}
	for id in slots:
		var m: Dictionary = Items.get_item(id).get("mods", {})
		for k in m:
			out[k] = float(out.get(k, 0.0)) + float(m[k])
	return out


## 넣기. 자리가 없어서 밀려난 것을 [[id, 개수], …] 로 돌려준다 (바닥에 떨어뜨리는 건 부른 쪽)
func add(id: String) -> Array:
	var out: Array = []
	if Items.is_consumable(id):
		if cons == id and cons_n < Items.STACK:
			cons_n += 1
		elif cons == "" or cons_n <= 0:
			cons = id
			cons_n = 1
		else:
			out.append([cons, cons_n])
			cons = id
			cons_n = 1
	else:
		if slots.size() >= Items.SLOTS:
			out.append([slots.pop_front(), 1])
		slots.append(id)
	changed.emit()
	return out


func remove_at(i: int) -> String:
	if i < 0 or i >= slots.size():
		return ""
	var id: String = slots[i]
	slots.remove_at(i)
	changed.emit()
	return id


func remove(id: String) -> bool:
	var i := slots.find(id)
	if i < 0:
		return false
	remove_at(i)
	return true


func take_consumable() -> String:
	if cons == "" or cons_n <= 0:
		return ""
	var id := cons
	cons_n -= 1
	if cons_n <= 0:
		cons = ""
	changed.emit()
	return id


func clear() -> void:
	slots.clear()
	cons = ""
	cons_n = 0
	changed.emit()
