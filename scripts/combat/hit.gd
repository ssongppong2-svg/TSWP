class_name Hit
extends RefCounted
## 한 번의 타격 정보 (전투 시스템.md: 모든 피해는 한 길로)
##  · 아군 공격: 기본 50% (ff 로 바꿀 수 있음: ff_cur = 대상 현재 체력 비율)
##  · 폭발(explosive)만 구조물을 부순다 · 폭발 맞으면 폭탄마 패시브 발동

var amount := 0.0
var source: Node = null           # 때린 쪽 (Fighter 또는 null = 환경)
var team := -1                    # 때린 쪽 팀 (-1 = 환경: 모두에게 그대로)
var knock := Vector3.ZERO         # 밀어내는 힘 (m/s)
var explosive := false
var heal := false                 # 회복 (amount 만큼)
var poison := 0.0                 # 초당 독 피해 (3초)
var slow := 0.0                   # 느려짐 (0~1, 2초)
var ff_cur := -1.0                # 아군에게 '현재 체력의 n%' (예: 거대 폭탄 0.2)
var self_ratio := 1.0             # 시전자 본인이 맞을 때 비율
var kind := "hit"                 # 소리·효과 고르기용 (slash · bomb · arrow · bite · spore …)


static func make(amount_: float, source_: Node, knock_ := Vector3.ZERO, kind_ := "hit") -> Hit:
	var h := Hit.new()
	h.amount = amount_
	h.source = source_
	h.knock = knock_
	h.kind = kind_
	if source_ is Fighter:
		h.team = (source_ as Fighter).team
	return h
