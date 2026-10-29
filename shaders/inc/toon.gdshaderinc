// 도트용 빛: 부드러운 그러데이션 대신 3단 계단 + 그림자. 그림자 쪽은 차가운 청록으로 (레퍼런스 색감)
uniform vec3 shade_cool = vec3(0.30, 0.42, 0.52);
uniform float band_soft = 0.08;

void light() {
	float ndl = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float lit = ndl * ATTENUATION;
	// 3단: 그늘 · 반그늘 · 빛
	float b = smoothstep(0.18 - band_soft, 0.18 + band_soft, lit) * 0.5 + smoothstep(0.55 - band_soft, 0.55 + band_soft, lit) * 0.5;
	DIFFUSE_LIGHT += LIGHT_COLOR * b / 3.14159;
	// 그늘에 청록 기운 (어두운 곳이 탁해지지 않게)
	DIFFUSE_LIGHT += (1.0 - b) * shade_cool * LIGHT_COLOR * 0.05 * (1.0 - ATTENUATION * 0.0);
}
