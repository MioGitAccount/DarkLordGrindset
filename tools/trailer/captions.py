"""Renders the trailer's captions and title card as transparent 1920x1080 PNGs.

    python tools/trailer/captions.py <out_dir>

Captions are gold capitals with a dark glow; the title is DEATHLAND in Castellar.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H = 1920, 1080
FONTS = "C:/Windows/Fonts/"
CAPTION_FONT = FONTS + "palab.ttf"
TITLE_FONT = FONTS + "CASTELAR.TTF"

# name -> (text, size, vertical centre as a share of the height)
CAPTIONS = {
    "cap_land": ("A LAND OF DEATH AWAITS ITS LORD", 64, 0.80),
    "cap_army": ("RAISE YOUR ARMY", 72, 0.80),
    "cap_conquer": ("CONQUER EVERY LAND", 72, 0.80),
    "cap_crush": ("CRUSH EVERY FOE", 76, 0.895),  # over the battle log bar
    "cap_magic": ("COMMAND DARK MAGIC", 76, 0.895),
}


def spaced_text(draw, xy, text, font, spacing, fill):
    x, y = xy
    for ch in text:
        draw.text((x, y), ch, font=font, fill=fill)
        x += draw.textlength(ch, font=font) + spacing


def spaced_width(draw, text, font, spacing):
    return sum(draw.textlength(ch, font=font) for ch in text) + spacing * (len(text) - 1)


def gold_text(text, font_path, size, centre_y, spacing_share=0.18, glow=18, shadow_alpha=230):
    font = ImageFont.truetype(font_path, size)
    probe = ImageDraw.Draw(Image.new("L", (1, 1)))
    spacing = size * spacing_share
    width = spaced_width(probe, text, font, spacing)
    ascent, descent = font.getmetrics()
    x = (W - width) / 2
    y = H * centre_y - (ascent + descent) / 2

    mask = Image.new("L", (W, H), 0)
    spaced_text(ImageDraw.Draw(mask), (x, y), text, font, spacing, 255)

    # Vertical gold gradient through the letters
    gradient = Image.new("RGB", (W, H))
    gd = ImageDraw.Draw(gradient)
    top, bottom = int(y), int(y + ascent + descent)
    for row in range(H):
        k = min(1, max(0, (row - top) / max(1, bottom - top)))
        # pale gold -> deep amber
        r = int(255 - 40 * k)
        g = int(236 - 110 * k)
        b = int(170 - 130 * k)
        gd.line([(0, row), (W, row)], fill=(r, g, b))

    out = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # Dark glow behind for legibility over the map and battles
    shadow = mask.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(glow))
    shadow = shadow.point(lambda v: min(255, int(v * 1.6)) * shadow_alpha // 255)
    out.paste(Image.new("RGBA", (W, H), (8, 4, 6, 255)), (0, 0), shadow)
    # Thin dark outline
    outline = mask.filter(ImageFilter.MaxFilter(5))
    out.paste(Image.new("RGBA", (W, H), (20, 10, 5, 255)), (0, 0), outline)
    # Warm outer glow
    warm = mask.filter(ImageFilter.GaussianBlur(6)).point(lambda v: v * 120 // 255)
    out.paste(Image.new("RGBA", (W, H), (255, 140, 40, 255)), (0, 0), warm)
    rgba = gradient.convert("RGBA")
    out.paste(rgba, (0, 0), mask)
    return out


def title_card():
    card = gold_text("DEATHLAND", TITLE_FONT, 190, 0.46, spacing_share=0.12, glow=30)
    rule = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(rule)
    # Thin gold rules either side of the subtitle
    y = int(H * 0.60)
    d.line([(W / 2 - 540, y), (W / 2 - 350, y)], fill=(210, 160, 80, 220), width=2)
    d.line([(W / 2 + 350, y), (W / 2 + 540, y)], fill=(210, 160, 80, 220), width=2)
    sub = gold_text("RISE, DARK LORD", CAPTION_FONT, 44, 0.60, spacing_share=0.35, glow=10)
    card.alpha_composite(rule)
    card.alpha_composite(sub)
    return card


def main():
    out_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(out_dir, exist_ok=True)
    for name, (text, size, centre_y) in CAPTIONS.items():
        gold_text(text, CAPTION_FONT, size, centre_y).save(os.path.join(out_dir, name + ".png"))
    title_card().save(os.path.join(out_dir, "title.png"))
    print("captions in", out_dir)


if __name__ == "__main__":
    main()
