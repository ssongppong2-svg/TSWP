shader_type spatial;
// 발밑 그늘: 땅에 눕힌 타원. 도트 단위로 끊고 가장자리는 디더. 땅 색에 곱한다 (지형 그늘과 같은 색).
render_mode unshaded, blend_mul, depth_draw_never, cull_disabled, shadows_disabled;

global uniform vec4 cut_plane;

uniform vec2 size_px = vec2(16.0, 8.0);
uniform float strength = 1.0;
uniform vec3 shade_col : source_color = vec3(0.50, 0.66, 0.70);
uniform vec3 shade_deep : source_color = vec3(0.36, 0.50, 0.56);

void fragment() {
	if (dot(cut_plane.xyz, (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz) > cut_plane.w) {
		discard;
	}
	vec2 p = (floor(UV * size_px) + 0.5) / size_px * 2.0 - 1.0;
	float d = length(p);
	ivec2 bi = ivec2(mod(floor(UV * size_px), 4.0));
	float b4[16] = float[](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	float th = (b4[bi.y * 4 + bi.x] + 0.5) / 16.0;
	float v = clamp((1.0 - d) / 0.35, 0.0, 1.0) * strength;
	vec3 c = vec3(1.0);
	if (v > 0.62 + (th - 0.5) * 0.25) {
		c = shade_deep;
	} else if (v > 0.22 + (th - 0.5) * 0.3) {
		c = shade_col;
	}
	ALBEDO = c;
	ALPHA = 1.0;
}
