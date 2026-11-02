class_name UiKit
extends RefCounted
## 도트 UI 부품 (assets/sprites/ui.png 아틀라스 — tools/art/ui.py 가 만든다)
##  · panel(): 9칸 판 (panel / panel_hi 선택됨 / panel_dark)
##  · icon(): 아이콘 (heart, heart_empty, coin, gem, job_*, skill_*, sel, hand)
##  · key(): 키캡 + 글자 ("Q", "Space" …) — 상황 힌트에 쓴다

const ATLAS := "res://assets/sprites/ui.png"
static var _tex_cache := {}


static func tex(name: String) -> AtlasTexture:
	if _tex_cache.has(name):
		return _tex_cache[name]
	var a := BB.atlas(ATLAS)
	var r: Array = a["rects"].get(name, [0, 0, 1, 1])
	var t := AtlasTexture.new()
	t.atlas = a["tex"]
	t.region = Rect2(r[0], r[1], r[2], r[3])
	_tex_cache[name] = t
	return t


static func icon(name: String, at := Vector2.ZERO) -> TextureRect:
	var r := TextureRect.new()
	r.texture = tex(name)
	r.position = at
	r.size = r.texture.get_size()
	r.stretch_mode = TextureRect.STRETCH_KEEP
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	return r


static func panel(kind := "panel", at := Vector2.ZERO, sz := Vector2(32, 16)) -> NinePatchRect:
	var p := NinePatchRect.new()
	p.texture = tex(kind)
	p.patch_margin_left = 4
	p.patch_margin_top = 4
	p.patch_margin_right = 4
	p.patch_margin_bottom = 4
	p.position = at
	p.size = sz
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	p.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	return p


## 키캡: 글자 길이에 맞춰 늘어난다. 돌려주는 Control 의 size.x = 전체 폭
static func key(text: String) -> Control:
	var root := Control.new()
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var l := PixelFont.label(text, "small", Color("#2a2030"), Color(0, 0, 0, 0))
	var f := PixelFont.get_font("small")
	var w := int(f.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, PixelFont.size_of("small")).x)
	var p := NinePatchRect.new()
	p.texture = tex("key")
	p.patch_margin_left = 3
	p.patch_margin_right = 3
	p.patch_margin_top = 3
	p.patch_margin_bottom = 3
	p.size = Vector2(w + 6, 15)
	p.mouse_filter = Control.MOUSE_FILTER_IGNORE
	root.add_child(p)
	l.position = Vector2(3, 2)
	root.add_child(l)
	root.size = p.size
	return root


## 키캡 + 설명을 한 줄로: [["WASD", "이동"], ["Shift", "달리기"]] → 가운데 정렬은 부른 쪽에서
static func hint_row(pairs: Array) -> Control:
	var row := Control.new()
	row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var x := 0.0
	var f := PixelFont.get_font("small")
	for pr in pairs:
		var k := key(String(pr[0]))
		k.position = Vector2(x, 0)
		row.add_child(k)
		x += k.size.x + 3
		var l := PixelFont.label(String(pr[1]), "small", Color("#fff4d6"))
		l.position = Vector2(x, 2)
		row.add_child(l)
		x += f.get_string_size(String(pr[1]), HORIZONTAL_ALIGNMENT_LEFT, -1, PixelFont.size_of("small")).x + 9
	row.size = Vector2(x - 9, 15)
	return row
