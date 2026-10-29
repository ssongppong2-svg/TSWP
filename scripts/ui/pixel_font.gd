class_name PixelFont
extends RefCounted
## 갈무리 도트 글꼴 (OFL). 흐림 없이 또렷하게: 안티에일리어싱·힌팅·서브픽셀 끔.
##  big: 갈무리11 (12px) · small: 갈무리9 (10px)

static var _cache := {}


static func get_font(kind := "big") -> FontFile:
	if _cache.has(kind):
		return _cache[kind]
	var path := "res://assets/fonts/Galmuri11.ttf" if kind == "big" else "res://assets/fonts/Galmuri9.ttf"
	var f: FontFile = load(path).duplicate()
	f.antialiasing = TextServer.FONT_ANTIALIASING_NONE
	f.hinting = TextServer.HINTING_NONE
	f.subpixel_positioning = TextServer.SUBPIXEL_POSITIONING_DISABLED
	f.generate_mipmaps = false
	_cache[kind] = f
	return f


static func size_of(kind := "big") -> int:
	return 12 if kind == "big" else 10


## 글자 라벨 (그림자 1px 포함)
static func label(text: String, kind := "big", col := Color.WHITE, shadow := Color(0.1, 0.1, 0.14, 0.9)) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_override("font", get_font(kind))
	l.add_theme_font_size_override("font_size", size_of(kind))
	l.add_theme_color_override("font_color", col)
	l.add_theme_color_override("font_shadow_color", shadow)
	l.add_theme_constant_override("shadow_offset_x", 1)
	l.add_theme_constant_override("shadow_offset_y", 1)
	l.add_theme_constant_override("line_spacing", 0)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l
