shader_type spatial;
// 섬광·불꽃·불티(더하기 혼합). 가운데가 하얗게 타는 둥근 빛.
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

#include "res://game/presentation/fx_particle_common.gdshaderinc"

void fragment() {
	vec2 q = UV * 2.0 - 1.0;
	float n = fx_noise(q * 3.0 + p_custom.y * 23.0);
	float r = length(q) + (n - 0.5) * 0.35;
	float glow = pow(clamp(1.0 - r, 0.0, 1.0), 1.6);
	float core = pow(clamp(1.0 - r * 1.8, 0.0, 1.0), 2.0) * (1.0 - p_custom.z);
	ALBEDO = p_color.rgb + vec3(core * 0.8);
	ALPHA = clamp(p_color.a * glow, 0.0, 1.0);
}
