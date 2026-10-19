// Final terrain look, on every terrain shape as it is drawn (see TerrainRenderer).
// The shapes are drawn in map coordinates, so VERTEX is the world position.
shader_type canvas_item;

uniform sampler2D distance_tex : filter_linear, repeat_disable;
uniform sampler2D blight_tex : filter_linear_mipmap, repeat_enable;
uniform sampler2D variation_tex : filter_linear_mipmap, repeat_enable; // TerrainTextures.Variation
uniform sampler2D ground_tex : filter_linear, repeat_disable;  // GroundInfluence: R forest, G rock, B settlement
uniform vec2 world_size = vec2(1024.0);
uniform float blight_tile = 256.0; // world px per blight texture tile
uniform sampler2D rock_tex : filter_linear_mipmap, repeat_enable;  // dirt texture, greyed: rocky foothills
uniform float rock_tile = 256.0;
uniform sampler2D worn_tex : filter_linear_mipmap, repeat_enable;  // road texture: worn ground around settlements
uniform float worn_tile = 256.0;
uniform vec3 forest_floor : source_color = vec3(0.27, 0.33, 0.17);
uniform vec3 rock_ground : source_color = vec3(0.5, 0.44, 0.36);
uniform vec3 worn_dirt : source_color = vec3(0.72, 0.62, 0.44);
uniform vec3 shallow_color : source_color = vec3(0.48, 0.58, 0.58);
uniform vec3 ring_color : source_color = vec3(0.64, 0.73, 0.72);
uniform vec3 coast_color : source_color = vec3(0.22, 0.23, 0.19);
uniform vec3 blight_edge_color : source_color = vec3(0.30, 0.22, 0.15);

const float MAX_DIST = 64.0; // world px, TerrainRenderer.MaxDistance

varying vec2 world;

void vertex() {
	world = VERTEX;
}

float ring(float d, float at, float width) {
	return 1.0 - smoothstep(width * 0.4, width, abs(d - at));
}

void fragment() {
	// Vertex alpha < 1: the soft band around land shapes, turned into a brushy, irregular edge below
	float band = COLOR.a;
	// Anti-tiling: a second look at the texture (other scale and offset), blended in by slow
	// noise, so the same patch of the texture never repeats in a visible grid
	float pick = smoothstep(0.38, 0.62, texture(variation_tex, world / 900.0 + 0.21).r);
	vec4 c = mix(texture(TEXTURE, UV), texture(TEXTURE, UV * 0.81 + vec2(0.37, 0.59)), pick);
	c.a = 1.0;
	// Large, slow color variation so the repeating texture tiles don't show
	float big = texture(variation_tex, world / 2300.0).r - 0.5;
	float mid = texture(variation_tex, world / 760.0 + 0.37).r - 0.5;
	c.rgb *= 1.0 + big * 0.16 + mid * 0.08;
	c.rgb = mix(c.rgb, c.rgb * vec3(1.05, 1.0, 0.88), clamp(0.5 + mid * 1.5, 0.0, 1.0) * 0.35);
	vec2 uv = world / world_size;
	vec3 info = texture(distance_tex, uv).rgb;
	// Average a small ring of samples: hides the stair-steps of the low-resolution distance field
	vec2 texel = 1.0 / vec2(textureSize(distance_tex, 0));
	float dist = info.r * 2.0;
	for (int k = 0; k < 6; k++) {
		float a = 6.2831853 * float(k) / 6.0;
		dist += texture(distance_tex, uv + vec2(cos(a), sin(a)) * 1.2 * texel).r;
	}
	float d = (dist / 8.0 - 0.5) * 2.0 * MAX_DIST; // > 0 in water (distance to land), < 0 on land

	// Ground changed by the objects on it (land only): forest floor, rocky foothills, worn dirt
	vec3 g = texture(ground_tex, uv).rgb;
	float land = 1.0 - smoothstep(-3.0, 0.0, d);
	float gn = texture(variation_tex, world / 300.0).r * 0.65 + texture(variation_tex, world / 110.0 + 0.2).r * 0.35;
	gn = clamp(0.5 + (gn - 0.5) * 1.6, 0.2, 0.8);
	float fine = texture(variation_tex, world / 60.0 + 0.7).r;
	float forest = smoothstep(gn - 0.22, gn + 0.22, g.r * 1.3) * land;
	float rock = smoothstep(gn - 0.18, gn + 0.18, g.g * 1.25) * land;
	float worn = smoothstep(gn - 0.18, gn + 0.18, g.b * 1.25) * land;
	c.rgb = mix(c.rgb, mix(c.rgb, forest_floor * (0.8 + 0.4 * fine), 0.7), forest * 0.75);
	// Grit: small dark and light stones in the rocky ground
	float grit = texture(variation_tex, world / 17.0 + 0.4).r;
	vec3 rock_paint = mix(texture(rock_tex, world / rock_tile).rgb, texture(rock_tex, world / rock_tile * 0.81 + vec2(0.37, 0.59)).rgb, pick);
	// The dirt texture, greyed and darkened towards the rock color, keeps its pebbles and cracks
	vec3 rocky = mix(rock_paint, rock_ground, 0.45) * (0.82 + 0.3 * rock_paint.r) * (1.0 - 0.15 * smoothstep(0.6, 0.75, grit));
	c.rgb = mix(c.rgb, rocky, rock * 0.85);
	vec3 worn_paint = mix(texture(worn_tex, world / worn_tile).rgb, texture(worn_tex, world / worn_tile * 0.81 + vec2(0.37, 0.59)).rgb, pick);
	c.rgb = mix(c.rgb, mix(worn_paint, worn_dirt, 0.3), worn * 0.8);

	if (d > 0.0) {
		// Lighter, shallow water along every coast
		c.rgb = mix(c.rgb, shallow_color, 0.55 * exp(-d / 12.0));
		// Rings following the coastline, fading out to sea
		float rings = 0.5 * ring(d, 8.0, 2.0) + 0.32 * ring(d, 19.0, 2.2) + 0.18 * ring(d, 33.0, 2.5);
		c.rgb = mix(c.rgb, ring_color, rings);
	} else {
		// Land gets a little darker right at the shore
		c.rgb *= 1.0 - 0.18 * exp(d / 7.0);
	}
	// Thin dark line on the coastline itself
	c.rgb = mix(c.rgb, coast_color, 0.55 * (1.0 - smoothstep(0.7, 2.0, abs(d))));

	// Blight: dark stain with a brown rim, soft edges from the blurred mask
	float b = info.b;
	vec3 stain = mix(texture(blight_tex, world / blight_tile).rgb, texture(blight_tex, world / blight_tile * 0.81 + vec2(0.37, 0.59)).rgb, pick);
	c.rgb = mix(c.rgb, blight_edge_color, 0.5 * smoothstep(0.05, 0.35, b) * (1.0 - smoothstep(0.35, 0.7, b)));
	c.rgb = mix(c.rgb, stain, 0.9 * smoothstep(0.3, 0.75, b));

	if (band < 0.999) {
		float n = texture(variation_tex, world / 140.0).r * 0.6 + texture(variation_tex, world / 45.0 + 0.5).r * 0.4;
		n = clamp(0.5 + (n - 0.5) * 1.8, 0.12, 0.88);
		c.a = smoothstep(n - 0.18, n + 0.18, band);
		// Never fade land out over water: the coast stays crisp
		c.a *= 1.0 - smoothstep(-1.5, 1.0, d);
	}
	// Nothing outside the map (feathered edges of land touching the border)
	if (world.x < 0.0 || world.y < 0.0 || world.x > world_size.x || world.y > world_size.y)
		c.a = 0.0;
	COLOR = c;
}
