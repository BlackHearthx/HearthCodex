from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont


ROOT = Path(__file__).resolve().parent
SOURCE = ROOT / "hearthcodex_book_source.png"
MARK = ROOT / "blackhearth_mark_official.png"
PREVIEW = ROOT / "hearthcodex_icon_preview.png"
FINAL_SIZE = 256
SCALE = 4


def font(path: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(path, size * SCALE)


def fit_cover(image: Image.Image, width: int, height: int) -> Image.Image:
    ratio = max(width / image.width, height / image.height)
    resized = image.resize(
        (round(image.width * ratio), round(image.height * ratio)),
        Image.Resampling.LANCZOS,
    )
    left = (resized.width - width) // 2
    top = max(0, (resized.height - height) // 2 - 16 * SCALE)
    return resized.crop((left, top, left + width, top + height))


def centered(draw: ImageDraw.ImageDraw, text: str, y: int, face, fill, stroke=0):
    box = draw.textbbox((0, 0), text, font=face, stroke_width=stroke)
    x = (FINAL_SIZE * SCALE - (box[2] - box[0])) // 2
    draw.text(
        (x, y * SCALE),
        text,
        font=face,
        fill=fill,
        stroke_width=stroke,
        stroke_fill=(20, 11, 7, 255),
    )


def main() -> None:
    size = FINAL_SIZE * SCALE
    art_height = 194 * SCALE
    source = Image.open(SOURCE).convert("RGB")
    source = ImageEnhance.Contrast(source).enhance(1.08)
    source = ImageEnhance.Color(source).enhance(0.92)
    art = fit_cover(source, size, art_height)

    canvas = Image.new("RGBA", (size, size), (8, 8, 8, 255))
    canvas.alpha_composite(art.convert("RGBA"), (0, 0))

    # Pull the art into the established BlackHearth look: darker forest-like
    # edges, then a clean black footer with a thin ember line.
    shade = Image.new("RGBA", (size, art_height), (0, 0, 0, 0))
    shade_draw = ImageDraw.Draw(shade)
    for row in range(art_height):
        distance = abs(row - art_height * 0.43) / (art_height * 0.57)
        alpha = int(20 + 95 * min(1, distance) ** 1.8)
        shade_draw.line((0, row, size, row), fill=(0, 0, 0, alpha))
    shade = shade.filter(ImageFilter.GaussianBlur(10 * SCALE))
    canvas.alpha_composite(shade, (0, 0))

    draw = ImageDraw.Draw(canvas)
    draw.rectangle((0, 184 * SCALE, size, size), fill=(5, 5, 5, 246))
    draw.rectangle(
        (22 * SCALE, 184 * SCALE, 234 * SCALE, 186 * SCALE),
        fill=(203, 73, 19, 255),
    )

    mark = Image.open(MARK).convert("RGBA")
    mark.thumbnail((49 * SCALE, 49 * SCALE), Image.Resampling.LANCZOS)
    mark = ImageEnhance.Contrast(mark).enhance(1.04)
    canvas.alpha_composite(mark, (7 * SCALE, 139 * SCALE))

    title_font = font(r"C:\Windows\Fonts\impact.ttf", 31)
    subtitle_font = font(r"C:\Windows\Fonts\arialbd.ttf", 9)
    author_font = font(r"C:\Windows\Fonts\arialbd.ttf", 7)
    centered(draw, "HEARTHCODEX", 188, title_font, (235, 224, 206, 255), 1 * SCALE)
    centered(draw, "CREATURE BESTIARY", 224, subtitle_font, (231, 91, 28, 255))
    centered(draw, "BLACKHEARTHX", 241, author_font, (151, 137, 126, 255))

    canvas.resize((FINAL_SIZE, FINAL_SIZE), Image.Resampling.LANCZOS).convert("RGB").save(
        PREVIEW,
        optimize=True,
        quality=95,
    )


if __name__ == "__main__":
    main()
