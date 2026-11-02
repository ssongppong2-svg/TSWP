class_name PushCrate
extends CharacterBody3D
## 밀 수 있는 나무 상자: 사람이 밀면 같은 쪽으로 미끄러진다 (발판 퍼즐 혼자 풀기 · 길 막기 장난)

var terrain: Terrain


func _ready() -> void:
	add_to_group("crates")
	collision_layer = 1
	collision_mask = 1
	var cs := CollisionShape3D.new()
	var b := BoxShape3D.new()
	b.size = Vector3(0.8, 0.8, 0.8)
	cs.shape = b
	cs.position.y = 0.4
	add_child(cs)
	add_child(BB.make(Region.CAMP, "crate", {"unique": true, "up_normal": 0.7}))
	add_child(BB.blob(0.5, 0.25, 0.9))


func _physics_process(dt: float) -> void:
	var push := Vector3.ZERO
	for f in get_tree().get_nodes_in_group("fighters"):
		if not (f is Player):
			continue
		var p := f as Player
		var d := global_position - p.global_position
		d.y = 0.0
		var hv := Vector3(p.velocity.x, 0, p.velocity.z)
		if d.length() < 0.85 and hv.length() > 0.5 and hv.normalized().dot(d.normalized()) > 0.6:
			push = hv * 0.75
	if push != Vector3.ZERO and Engine.get_physics_frames() % 18 == 0:
		Audio.play("crate_push", global_position, -6.0)
	velocity.x = move_toward(velocity.x, push.x, 30.0 * dt)
	velocity.z = move_toward(velocity.z, push.z, 30.0 * dt)
	if not is_on_floor():
		velocity.y -= 26.0 * dt
	move_and_slide()
