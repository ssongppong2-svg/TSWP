shader_type spatial;
// 플레이어 발밑 고리 (팀 색) + 조준 방향 표시. 땅에 눕혀 그린다 — 화면이 복잡해도 내 캐릭터 위치·방향이 바로 보이게
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec2 size_px = vec2(24.0, 24.0);
uniform float aim = 0.0;                     // 세계 각도 (x 축 기준, z 쪽이 +)
uniform vec4 col : source_color = vec4(1.0, 0.88, 0.35, 1.0);
uniform float alpha = 0.9;

void fragment() {
	vec2 cell = floor(UV * size_px);
	vec2 p = (cell + 0.5) / size_px * 2.0 - 1.0;
	float d = length(p);
	float px = 2.0 / size_px.x;
	float ring = step(0.80 - px, d) * step(d, 0.80 + px * 0.6);
	// 조준 쪽 작은 화살촉 (고리 바깥)
	vec2 dir = vec2(cos(aim), sin(aim));
	float along = dot(p, dir);
	float side = abs(dot(p, vec2(-dir.y, dir.x)));
	float pip = step(0.84, along) * step(along, 1.0) * step(side, (1.0 - along) * 1.6);
	float a = max(ring, pip);
	if (a < 0.5) {
		discard;
	}
	ALBEDO = col.rgb;
	ALPHA = alpha;
}
