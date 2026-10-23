shader_type spatial;
// 지면 셰이더: 지형 종류 마스크(10 m 셀, 선형 보간) + 절차적 노이즈로 들판·밭 구획·길가·물·숲 바닥·시가지를 그린다.
// 시뮬 좌표: x = 동, y = 북. 3D: X = 동, -Z = 북. 마스크 텍스처 (u, v) = (x / size, y / size).
// 웹(Compatibility)에서도 돌도록 텍스처 하나 + 산술 노이즈만 쓴다.

render_mode cull_disabled;

uniform sampler2D type_mask : filter_linear, repeat_disable;  // R 도로, G 물, B 숲, A 시가지
uniform sampler2D type_mask_blur : filter_linear_mipmap, repeat_disable;  // 같은 텍스처(밉맵): 근처 판정용
uniform vec2 map_size = vec2(4000.0, 4000.0);
uniform float height_min = 20.0;
uniform float height_max = 110.0;
uniform float field_angle = 0.35;
uniform vec2 field_size = vec2(230.0, 150.0);

varying vec3 world_pos;
varying vec3 world_normal;

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++) {
		v += a * vnoise(p);
		p = p * 2.03 + vec2(17.1, 9.7);
		a *= 0.5;
	}
	return v;
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

// 밭 구획: 회전한 격자 칸마다 작물. x = 작물 색 번호(-1 = 풀밭), y = 가장자리까지 거리(m).
vec2 field_cell(vec2 p, out vec2 local, out float row_dir) {
	float c = cos(field_angle);
	float s = sin(field_angle);
	vec2 r = vec2(c * p.x + s * p.y, -s * p.x + c * p.y);
	// 칸 크기를 칸마다 조금씩 다르게(열마다 오프셋).
	float col = floor(r.x / field_size.x);
	r.y += fract(col * 0.6180339) * field_size.y;  // CPU 쪽 생울타리 배치와 같은 식(정밀도 차이에 강한 식)
	vec2 cell = vec2(col, floor(r.y / field_size.y));
	local = vec2(fract(r.x / field_size.x) * field_size.x, fract(r.y / field_size.y) * field_size.y);
	float edge = min(min(local.x, field_size.x - local.x), min(local.y, field_size.y - local.y));
	float h = hash12(cell + vec2(11.0, 5.0));
	row_dir = step(0.5, hash12(cell + 2.0));
	float crop = h < 0.32 ? -1.0 : floor(hash12(cell * 1.7 + 4.0) * 5.0);
	return vec2(crop, edge);
}

void fragment() {
	vec2 sim = vec2(world_pos.x, -world_pos.z);
	vec2 uv = sim / map_size;
	vec4 m = texture(type_mask, uv);
	// 마스크 경계를 노이즈로 흐트러뜨린다(10 m 계단 숨기기).
	float wob = (vnoise(sim * 0.09) - 0.5) * 0.35;
	float water = smoothstep(0.3, 0.55, m.g + wob);
	float forest = smoothstep(0.25, 0.6, m.b + wob);
	float urban = smoothstep(0.3, 0.6, m.a + wob);
	// 도로 근처(흐린 마스크): 밭을 빼고 길가 풀을 둔다.
	float near_road = smoothstep(0.02, 0.18, textureLod(type_mask_blur, uv, 3.0).r);
	float near_forest = smoothstep(0.05, 0.4, textureLod(type_mask_blur, uv, 2.0).b);

	float n_big = fbm(sim * 0.0035);
	float n_mid = fbm(sim * 0.02);
	float n_fine = vnoise(sim * 0.45);
	float hk = clamp((world_pos.y - height_min) / max(height_max - height_min, 1.0), 0.0, 1.0);

	// 풀밭.
	vec3 grass_a = vec3(0.27, 0.37, 0.17);
	vec3 grass_b = vec3(0.37, 0.43, 0.22);
	vec3 col = mix(grass_a, grass_b, n_big);
	col = mix(col, col * vec3(1.08, 1.04, 0.86), n_mid * 0.6);
	col = mix(col, vec3(0.55, 0.55, 0.36), hk * 0.25);

	// 밭 구획.
	vec2 local;
	float row_dir;
	vec2 fc = field_cell(sim, local, row_dir);
	if (fc.x >= 0.0) {
		vec3 crop;
		if (fc.x < 1.0) crop = vec3(0.62, 0.54, 0.28);       // 밀
		else if (fc.x < 2.0) crop = vec3(0.36, 0.28, 0.19);  // 갈아엎은 밭
		else if (fc.x < 3.0) crop = vec3(0.30, 0.42, 0.17);  // 푸른 작물
		else if (fc.x < 4.0) crop = vec3(0.48, 0.46, 0.28);  // 휴경지
		else crop = vec3(0.42, 0.40, 0.24);                  // 건초
		float rows = 0.5 + 0.5 * sin((row_dir > 0.5 ? local.x : local.y) * 2.2);
		crop *= 0.94 + 0.07 * rows;
		crop *= 0.92 + 0.16 * n_mid;
		float in_field = smoothstep(2.0, 5.0, fc.y) * (1.0 - near_road) * (1.0 - near_forest);
		col = mix(col, crop, in_field * 0.95);
	}
	// 밭 둑(얇은 어두운 선).
	float margin = (1.0 - smoothstep(0.6, 2.0, fc.y)) * (1.0 - near_road) * (1.0 - near_forest);
	col = mix(col, vec3(0.30, 0.34, 0.20), margin * 0.7);

	// 경사: 흙·바위.
	float slope = 1.0 - clamp(world_normal.y, 0.0, 1.0);
	col = mix(col, vec3(0.45, 0.40, 0.33), smoothstep(0.12, 0.35, slope));

	// 숲 바닥.
	vec3 forest_floor = mix(vec3(0.16, 0.22, 0.12), vec3(0.24, 0.27, 0.15), n_mid);
	col = mix(col, forest_floor, forest);

	// 시가지(포장·마당).
	// 시가지: 마당·포장 얼룩(풀밭과 섞임).
	vec3 town = mix(vec3(0.46, 0.45, 0.40), vec3(0.38, 0.40, 0.30), n_mid);
	col = mix(col, town, urban * mix(0.45, 0.8, n_fine));

	// 도로 자체는 띠 메시(road.gdshader)가 그린다. 여기서는 길가 풀만 조금 밝게.
	col = mix(col, col * vec3(1.06, 1.05, 0.92), near_road * 0.5);

	// 물: 깊은 청록 + 잔물결, 물가 진흙.
	float bank = smoothstep(0.12, 0.3, m.g + wob) - water;
	col = mix(col, vec3(0.36, 0.32, 0.24), clamp(bank, 0.0, 1.0) * 0.8);
	// 강바닥(수면은 water.gdshader 띠 메시).
	col = mix(col, vec3(0.20, 0.22, 0.18), water);

	// 아주 가까이서의 잔무늬.
	col *= 0.94 + 0.12 * n_fine;

	ALBEDO = col;
	ROUGHNESS = 0.95;
	SPECULAR = 0.25;
}
