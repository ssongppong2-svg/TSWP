class_name BossBits
extends RefCounted
## 보스 01 소품 공장: 고치 · 거미줄 장판 · 돌기둥(+점프 발판) · 레버
const ATLAS := "res://assets/sprites/boss.png"


## 거미줄 장판: 6초 동안 밟으면 느려지고 0.5초마다 2 피해 (Behaviour_Web)
static func web_field(parent: Node, at: Vector3, r: float, owner_: Fighter) -> Node3D:
	var n := WebField.new()
	n.r = r
	n.owner_f = owner_
	parent.add_child(n)
	n.global_position = at
	return n


static func cocoon(parent: Node, at: Vector3, terrain: Terrain, rig: CamRig, hatch := 4.0) -> Cocoon:
	var c := Cocoon.new()
	c.terrain = terrain
	c.rig = rig
	c.hatch_t = hatch
	parent.add_child(c)
	c.global_position = at
	return c
