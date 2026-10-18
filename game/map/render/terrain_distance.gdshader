// Second half of the exact coast distance transform (see TerrainRenderer), using the row
// distances from terrain_rows.gdshader:
// R: signed distance to the coastline, 0.5 + 0.5 * d / MAX_DIST (d > 0 in water, < 0 on land)
// B: blight mask, blurred so the stain has soft edges
shader_type canvas_item;
render_mode blend_disabled;

uniform sampler2D sea_mask : filter_nearest, repeat_disable;
uniform sampler2D row_distance : filter_nearest, repeat_disable;
uniform sampler2D blight_mask : filter_linear, repeat_disable;
uniform float world_per_pixel = 2.0;
uniform int max_px = 32;

const float MAX_DIST = 64.0; // world px, TerrainRenderer.MaxDistance

void fragment() {
	vec2 texel = 1.0 / vec2(textureSize(sea_mask, 0));
	float m = texture(sea_mask, UV).r; // anti-aliased: how much of this pixel is water
	bool sea = m > 0.5;
	float r = float(max_px);
	float best = r * r;
	for (int j = -max_px; j <= max_px; j++) {
		vec2 rows = texture(row_distance, UV + vec2(0.0, float(j)) * texel).rg * r;
		float along_row = sea ? rows.g : rows.r; // distance to the other side in that row
		best = min(best, float(j * j) + along_row * along_row);
	}
	float d = sqrt(best);
	// The coast lies between pixel centres; right next to it, the pixel's coverage tells where exactly
	float signed_px = d <= 1.5 ? m - 0.5 : (sea ? d - 0.5 : 0.5 - d);
	float signed_dist = clamp(signed_px * world_per_pixel, -MAX_DIST, MAX_DIST);

	float blight = texture(blight_mask, UV).r;
	float n = 1.0;
	for (int ring = 1; ring <= 3; ring++) {
		for (int k = 0; k < 8; k++) {
			float a = 6.2831853 * (float(k) + 0.5 * float(ring % 2)) / 8.0;
			blight += texture(blight_mask, UV + vec2(cos(a), sin(a)) * float(ring) * 6.8 / world_per_pixel * texel).r;
			n += 1.0;
		}
	}
	COLOR = vec4(0.5 + 0.5 * signed_dist / MAX_DIST, 0.0, blight / n, 1.0);
}
