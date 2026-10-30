class_name SpringBone
extends RefCounted
## 가상 뼈 하나 = 감쇠 스프링 (물리 기반 애니메이션).
##  그림을 직접 프레임으로 그리지 않아도 몸이 출렁이고(스쿼시·스트레치), 기울고(이동 방향), 맞으면 흔들린다.
##  value 가 target 을 따라가되 관성이 있어 지나쳤다 돌아온다.

var value := 0.0
var vel := 0.0
var target := 0.0
var k := 220.0          # 단단함
var damp := 14.0        # 감쇠


func _init(v := 0.0, k_ := 220.0, damp_ := 14.0) -> void:
	value = v
	target = v
	k = k_
	damp = damp_


func kick(impulse: float) -> void:
	vel += impulse


func step(dt: float) -> float:
	# 반암시적 오일러 — 프레임이 튀어도 터지지 않게 잘게 나눔
	var n := 2
	var h := dt / n
	for i in n:
		vel += (target - value) * k * h - vel * damp * h
		value += vel * h
	return value
