// Paper overlay over the whole map (terrain and objects), multiplied in: warm parchment tone,
// faint stains and canvas grain, and a darker, burnt edge around the map border.
// Ties the painted objects and the terrain together into one picture. See MapView.
shader_type canvas_item;
render_mode blend_mul;

uniform sampler2D variation_tex : filter_linear_mipmap, repeat_enable; // TerrainTextures.Variation
uniform vec2 world_size = vec2(1024.0);
uniform vec3 tint : source_color = vec3(1.0, 0.965, 0.9);
uniform vec3 edge_color : source_color = vec3(0.55, 0.45, 0.33);
uniform float edge_width = 260.0; // world px

varying vec2 world;

void vertex() {
	world = VERTEX;
}

void fragment() {
	float stains = texture(variation_tex, world / 1700.0 + 0.13).r;
	float blotch = texture(variation_tex, world / 520.0 + 0.61).r;
	float grain = texture(variation_tex, world / 23.0).r;
	float fibers = texture(variation_tex, world * vec2(1.0 / 210.0, 1.0 / 14.0)).r; // long thin streaks
	vec3 paper = tint;
	paper *= 0.95 + 0.05 * stains + 0.03 * (blotch - 0.5);
	paper *= 0.985 + 0.03 * (grain - 0.5) + 0.02 * (fibers - 0.5);
	// Burnt, darker edge of the map, wobbly
	float edge = min(min(world.x, world_size.x - world.x), min(world.y, world_size.y - world.y));
	edge += (blotch - 0.5) * edge_width * 0.5;
	float e = smoothstep(0.0, edge_width, edge);
	paper *= mix(edge_color, vec3(1.0), e * e * (3.0 - 2.0 * e));
	COLOR = vec4(paper, 1.0);
}
