shader_type spatial;
// 도로 띠: UV.x = 가로(0~1, 양쪽 갓길 포함), UV.y = 길이 방향 거리(m).
render_mode cull_disabled;

uniform float shoulder = 0.16;   // 양쪽 갓길 비율
uniform float center_dash = 1.0; // 1 이면 가운데 점선

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash12(i), hash12(i + vec2(1.0, 0.0)), u.x), mix(hash12(i + vec2(0.0, 1.0)), hash12(i + vec2(1.0, 1.0)), u.x), u.y);
}

void fragment() {
	float x = UV.x;
	float along = UV.y;
	float n = vnoise(vec2(x * 8.0, along * 0.5));
	vec3 asphalt = mix(vec3(0.19, 0.19, 0.20), vec3(0.24, 0.24, 0.24), n);
	vec3 dirt = mix(vec3(0.50, 0.45, 0.35), vec3(0.42, 0.40, 0.30), n);
	float edge = min(x, 1.0 - x);
	float paved = smoothstep(shoulder - 0.02, shoulder + 0.02, edge + (n - 0.5) * 0.04);
	vec3 col = mix(dirt, asphalt, paved);
	// 가장자리 흰 선, 가운데 점선.
	float edge_line = smoothstep(0.025, 0.0, abs(edge - shoulder - 0.04));
	col = mix(col, vec3(0.78, 0.78, 0.74), edge_line * 0.7 * paved);
	float dash = step(0.5, fract(along / 12.0)) * smoothstep(0.02, 0.0, abs(x - 0.5)) * center_dash;
	col = mix(col, vec3(0.85, 0.80, 0.55), dash * 0.8);
	ALBEDO = col;
	ROUGHNESS = 0.85;
	// 갓길 바깥 끝은 지면과 섞이도록 투명하게.
	ALPHA = smoothstep(0.0, 0.05, edge);
	ALPHA_SCISSOR_THRESHOLD = 0.5;
}
