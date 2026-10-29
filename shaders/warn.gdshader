shader_type spatial;
// 공격 예고 장판 (땅에 눕힘): 원 또는 직사각형. 도트 단위 테두리 + 안쪽은 시간이 갈수록 차오른다
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec2 size_px = vec2(64.0, 64.0);
uniform int shape = 0;                      // 0 원 · 1 사각
uniform float fill = 0.0;                   // 0 → 1 (터지는 순간 1)
uniform vec4 col : source_color = vec4(1.0, 0.25, 0.2, 1.0);

void fragment() {
	vec2 p = (floor(UV * size_px) + 0.5) / size_px * 2.0 - 1.0;
	float d = shape == 0 ? length(p) : max(abs(p.x), abs(p.y));
	float px = 2.0 / min(size_px.x, size_px.y);
	if (d > 1.0) {
		discard;
	}
	float edge = step(1.0 - px * 1.5, d);
	float inner = step(d, fill);
	ivec2 bi = ivec2(mod(floor(UV * size_px), 2.0));
	float checker = float((bi.x + bi.y) % 2);
	float a = edge * 0.9 + inner * 0.35 + (1.0 - inner) * (1.0 - edge) * 0.12 * checker;
	if (a < 0.05) {
		discard;
	}
	ALBEDO = col.rgb;
	ALPHA = a;
}
