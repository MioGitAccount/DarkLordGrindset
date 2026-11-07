# Cuts the castle screen art out of the raw generated images.
# Run from the project root: python tools/castle/cut_buildings.py
#
# Reads art_src/castle/*.(png|webp|jpg) and writes images/castle/<id>.png:
# - castle_background: copied as it is (only converted to PNG)
# - everything else: the background is removed, then trimmed and scaled down.
#   A raw image that is already transparent keeps its alpha. Otherwise the flat colour in the
#   corners (magenta, green, white...) is flood-filled away from the edges, with soft edges and
#   the colour spill taken out.
# - a sheet listed in SHEETS holds several images side by side, split at the empty columns, left to right.
# - the images of a FRAMES group (same source size) are cropped to one shared frame, so a smaller
#   level stays smaller on screen (Tower I is narrower than Tower III).
# Plan and image ids: docs/CASTLE_ART_PLAN.md
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

SHEETS = {"plots_sheet": ["plot_empty", "plot_locked", "plot_scaffold"]}
BACKGROUNDS = {"castle_background"}
MAX_SIDE = 512
MAX_SIDE_TOWER = 768
# Colour distance to the key: below KEY_NEAR is background, above KEY_FAR is solid
KEY_NEAR = 70
KEY_FAR = 140
# An enclosed area counts as background when it has pixels this close to the key
KEY_PURE = 30
# On a black key the building's own shadows are close to the key too: tighter, no enclosed areas
DARK_KEY_NEAR = 22
DARK_KEY_FAR = 55
# Enclosed black areas larger than this (pixels in the raw image) stay see-through
DARK_HOLE_MAX = 600
FRAMES = [["tower_1", "tower_2", "tower_3"]]


def key_alpha(rgb):
    h, w, _ = rgb.shape
    corners = np.array([rgb[2, 2], rgb[2, w - 3], rgb[h - 3, 2], rgb[h - 3, w - 3]], dtype=float)
    key = np.median(corners, axis=0)
    dist = np.sqrt(((rgb.astype(float) - key) ** 2).sum(axis=2))
    dark = key.sum() < 90
    key_near, key_far = (DARK_KEY_NEAR, DARK_KEY_FAR) if dark else (KEY_NEAR, KEY_FAR)
    # Background = near-key areas that touch the border, or that hold pure key colour (gaps
    # between roofs); near-key shading inside the building stays
    near = dist < key_far
    labels, _ = ndimage.label(near)
    border = np.concatenate([labels[0], labels[-1], labels[:, 0], labels[:, -1]])
    keep = border if dark else np.union1d(border, np.unique(labels[dist < KEY_PURE]))
    outside = np.isin(labels, keep[keep > 0])
    alpha = np.where(outside, np.clip((dist - key_near) / (key_far - key_near), 0, 1), 1.0)
    if dark:
        # Shadows reach the black between spires: keep the closed silhouette solid, and leave the
        # colours alone (black spill only darkens, unmixing it would wash the shadows out)
        closed = ndimage.binary_closing(dist > key_near, iterations=6)
        # Fill small holes (shadow pockets), not big ones (the gap a chain or banner hangs across)
        holes, _ = ndimage.label(ndimage.binary_fill_holes(closed) & ~closed)
        sizes = np.bincount(holes.ravel())
        solid = closed | ((sizes < DARK_HOLE_MAX)[holes] & (holes > 0))
        alpha = np.where(ndimage.binary_erosion(solid, iterations=2), 1.0, np.where(solid, alpha, 0.0))
        return rgb, (alpha * 255).astype(np.uint8), key
    # Take the key colour out of the soft edge pixels: unmix them from the key
    rgb = rgb.astype(float)
    edge = outside & (alpha > 0) & (alpha < 1)
    a = np.maximum(alpha[..., None], 1e-3)
    unmixed = np.clip((rgb - key * (1 - a)) / a, 0, 255)
    rgb = np.where(edge[..., None], unmixed, rgb)
    return rgb.astype(np.uint8), (alpha * 255).astype(np.uint8), key


def cut(image):
    rgba = np.array(image.convert("RGBA"))
    rgb, alpha = rgba[..., :3], rgba[..., 3]
    if alpha[[0, 0, -1, -1], [0, -1, 0, -1]].max() < 16:
        # Already transparent: firm up lossy near-opaque alpha
        alpha = np.where(alpha > 235, 255, np.where(alpha < 12, 0, alpha)).astype(np.uint8)
        return np.dstack([rgb, alpha])
    rgb, alpha, _ = key_alpha(rgb)
    return np.dstack([rgb, alpha])


def split_columns(rgba, count):
    used = (rgba[..., 3] > 10).any(axis=0)
    runs, start = [], None
    for x, on in enumerate(used):
        if on and start is None:
            start = x
        elif not on and start is not None:
            runs.append((start, x))
            start = None
    if start is not None:
        runs.append((start, len(used)))
    # Merge tiny gaps until there are `count` parts: keep the widest gaps apart
    while len(runs) > count:
        gaps = [runs[i + 1][0] - runs[i][1] for i in range(len(runs) - 1)]
        i = int(np.argmin(gaps))
        runs[i:i + 2] = [(runs[i][0], runs[i + 1][1])]
    assert len(runs) == count, f"found {len(runs)} images, expected {count}"
    return [rgba[:, a:b] for a, b in runs]


def bbox(rgba):
    ys, xs = np.nonzero(rgba[..., 3] > 8)
    return xs.min(), xs.max() + 1, ys.min(), ys.max() + 1


def trim_and_scale(rgba, max_side, frame=None):
    x0, x1, y0, y1 = frame or bbox(rgba)
    rgba = rgba[y0:y1, x0:x1]
    image = Image.fromarray(rgba, "RGBA")
    scale = min(1.0, max_side / max(image.size))
    if scale < 1:
        image = image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)
    return image


def main():
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    src = os.path.join(root, "art_src", "castle")
    out = os.path.join(root, "images", "castle")
    os.makedirs(out, exist_ok=True)
    only = set(sys.argv[1:])
    # A frame group is cut whole when any of it is asked for
    for group in FRAMES:
        if only & set(group):
            only |= set(group)
    cuts = {}
    for name in sorted(os.listdir(src)):
        stem, ext = os.path.splitext(name)
        if ext.lower() not in (".png", ".webp", ".jpg", ".jpeg") or (only and stem not in only):
            continue
        image = Image.open(os.path.join(src, name))
        if stem in BACKGROUNDS:
            image.convert("RGB").save(os.path.join(out, stem + ".png"))
            print(f"{stem}: background {image.size}")
            continue
        rgba = cut(image)
        parts = split_columns(rgba, len(SHEETS[stem])) if stem in SHEETS else [rgba]
        cuts.update(zip(SHEETS.get(stem, [stem]), parts))
    frames = {}
    for group in FRAMES:
        members = [cuts[i] for i in group if i in cuts]
        if members and len({m.shape for m in members}) == 1:
            boxes = [bbox(m) for m in members]
            shared = (min(b[0] for b in boxes), max(b[1] for b in boxes), min(b[2] for b in boxes), max(b[3] for b in boxes))
            frames.update((i, shared) for i in group)
    for part_id, part in cuts.items():
        result = trim_and_scale(part, MAX_SIDE_TOWER if part_id.startswith("tower") else MAX_SIDE, frames.get(part_id))
        result.save(os.path.join(out, part_id + ".png"))
        print(f"{part_id}: {result.size}")


if __name__ == "__main__":
    main()
