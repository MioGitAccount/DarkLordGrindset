// First half of the exact coast distance transform (see TerrainRenderer): for every mask pixel,
// the distance along its row to the nearest sea pixel (R) and nearest land pixel (G), / max_px.
shader_type canvas_item;
render_mode blend_disabled;

uniform sampler2D sea_mask : filter_nearest, repeat_disable;
uniform int max_px = 32;

void fragment() {
	vec2 texel = 1.0 / vec2(textureSize(sea_mask, 0));
	float to_sea = float(max_px);
	float to_land = float(max_px);
	for (int i = -max_px; i <= max_px; i++) {
		float d = abs(float(i));
		if (texture(sea_mask, UV + vec2(float(i), 0.0) * texel).r > 0.5)
			to_sea = min(to_sea, d);
		else
			to_land = min(to_land, d);
	}
	COLOR = vec4(to_sea / float(max_px), to_land / float(max_px), 0.0, 1.0);
}
