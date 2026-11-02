shader_type canvas_item;
// 화면 전환: 마름모 도트가 왼쪽 위에서 오른쪽 아래로 번지며 덮는다 (ColorRect 의 color.a = 진행도 0~1)
//  · 칸 12px 마름모 · 진행도에 따라 칸이 커지며 메워짐 → 도트 게임다운 전환
uniform float cell = 12.0;
uniform vec2 screen_px = vec2(480.0, 270.0);

void fragment() {
	float p = COLOR.a;
	vec2 fc = floor(FRAGCOORD.xy);
	vec2 l = abs(fract((fc + 0.5) / cell) - 0.5);
	float d = l.x + l.y;                                   // 0 가운데 … 1 모서리
	float sweep = (fc.x + fc.y) / (screen_px.x + screen_px.y);
	float thr = p * 1.7 - sweep * 0.7;
	COLOR = vec4(COLOR.rgb, d < thr ? 1.0 : 0.0);
}
