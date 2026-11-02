class_name CharLook
extends RefCounted
## 캐릭터 꾸미기: 머리색 · 피부 · 옷색 · 모자. 그림의 색을 셰이더에서 바꾼다 (billboard.gdshader swap_*)
##  · SRC 는 tools/art/chars.py 의 CHARS 색과 같아야 한다 (그림을 다시 만들면 여기도 맞출 것)
##  · 옷 0 = 직업 대표색 그대로 (팔레트 시스템.md)

const SKIN_SRC := ["#c98a72", "#e8b394", "#f6d5b8"]
const SRC := {
	"warrior": {"hair": ["#4a2f22", "#6b4a32", "#8c6644"], "cloth": ["#2e4470", "#4a68a8", "#7a9ad8"]},
	"bomber": {"hair": ["#3a2a22", "#5a4232", "#7a5a44"], "cloth": ["#8a4a1e", "#c8742e", "#eaa45a"]},
	"healer": {"hair": ["#c07a9a", "#eaa8c0", "#fbd4e2"], "cloth": ["#2e5a3e", "#4a8a5a", "#7ab884"]},
	"rogue": {"hair": ["#2a3048", "#3e4a6e", "#5a6a98"], "cloth": ["#4a3422", "#74563a", "#a07e58"]},
}
const HAIR := [
	["기본", []],
	["검정", ["#1e1a22", "#2e2a34", "#4a4452"]],
	["갈색", ["#4a2f22", "#6b4a32", "#8c6644"]],
	["금발", ["#a8782a", "#e0b04a", "#fbe08a"]],
	["빨강", ["#7a2a22", "#b84a32", "#e07a50"]],
	["분홍", ["#c07a9a", "#eaa8c0", "#fbd4e2"]],
	["하늘", ["#3a6a9a", "#5aa0d0", "#a8d8f4"]],
	["초록", ["#2e5a3e", "#4a8a5a", "#8ac88a"]],
	["흰색", ["#a8a4b0", "#d8d4e0", "#fbf8ff"]],
	["보라", ["#4a2e6a", "#7a4aa8", "#b88ae0"]],
]
const SKIN := [
	["밝음", ["#d8a088", "#f4c8a8", "#fde4cc"]],
	["보통", []],
	["구릿빛", ["#a8684a", "#c88a64", "#e0a882"]],
	["진함", ["#6a4030", "#8a5a42", "#a8765a"]],
	["초록 (외계인)", ["#5a8a4a", "#7ab06a", "#a8d890"]],
]
const CLOTH := [
	["직업색", []],
	["빨강", ["#7a2e2e", "#b84a3e", "#e0705a"]],
	["파랑", ["#2e4470", "#4a68a8", "#7a9ad8"]],
	["초록", ["#2e5a3e", "#4a8a5a", "#7ab884"]],
	["노랑", ["#8a6a1e", "#c8a02e", "#f0d060"]],
	["보라", ["#4a2e6a", "#7a4aa8", "#b08ad8"]],
	["분홍", ["#8a3a5a", "#d06a8a", "#f4a8c0"]],
	["검정", ["#1e1a24", "#34303e", "#545064"]],
	["하양", ["#9aa0b0", "#e8ecf0", "#ffffff"]],
]
const HATS := [["없음", ""], ["파티 모자", "party"], ["털모자", "beanie"], ["꽃", "flower"], ["뿔", "horns"], ["고양이 귀", "catears"],
	["왕관", "crown"], ["새싹", "sprout"], ["요리사 모자", "chef"], ["해적 모자", "pirate"], ["실크햇", "tophat"]]


static func current() -> Dictionary:
	return {"hair": Prefs.look_hair, "skin": Prefs.look_skin, "cloth": Prefs.look_cloth, "hat": Prefs.look_hat}


static func random_look(rng: RandomNumberGenerator) -> Dictionary:
	return {"hair": rng.randi_range(0, HAIR.size() - 1), "skin": rng.randi_range(0, 3), "cloth": rng.randi_range(0, CLOTH.size() - 1), "hat": rng.randi_range(1, HATS.size() - 1)}


static func _lin(h: String) -> Vector3:
	var c := Color(h).srgb_to_linear()
	return Vector3(c.r, c.g, c.b)


## 재질에 색 바꾸기 적용
static func apply(mat: ShaderMaterial, who: String, look: Dictionary) -> void:
	var src: Array[Vector3] = []
	var dst: Array[Vector3] = []
	var base: Dictionary = SRC.get(who, SRC["warrior"])
	var groups := [[base["hair"], HAIR[clampi(int(look.get("hair", 0)), 0, HAIR.size() - 1)][1]],
		[SKIN_SRC, SKIN[clampi(int(look.get("skin", 1)), 0, SKIN.size() - 1)][1]],
		[base["cloth"], CLOTH[clampi(int(look.get("cloth", 0)), 0, CLOTH.size() - 1)][1]]]
	for g in groups:
		var to: Array = g[1]
		if to.is_empty():
			continue
		for i in 3:
			src.append(_lin(String(g[0][i])))
			dst.append(_lin(String(to[i])))
	var n := mini(12, src.size())
	while src.size() < 12:
		src.append(Vector3(-1, -1, -1))
		dst.append(Vector3.ZERO)
	mat.set_shader_parameter("swap_src", src)
	mat.set_shader_parameter("swap_dst", dst)
	mat.set_shader_parameter("swap_n", n)


static func hat_name(look: Dictionary) -> String:
	return String(HATS[clampi(int(look.get("hat", 0)), 0, HATS.size() - 1)][1])
