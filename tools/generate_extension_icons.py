from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "frontend" / "extension" / "public" / "icons"
SIZES = (16, 32, 48, 128)


def load_font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for name in ("segoeuib.ttf", "arialbd.ttf", "DejaVuSans-Bold.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def create_icon(size: int) -> Image.Image:
    scale = 4
    canvas_size = size * scale
    image = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    gradient = Image.new("RGBA", image.size)
    pixels = gradient.load()
    for y in range(canvas_size):
        mix = y / max(canvas_size - 1, 1)
        for x in range(canvas_size):
            horizontal = x / max(canvas_size - 1, 1)
            pixels[x, y] = (
                int(34 + 49 * mix),
                int(139 - 45 * mix),
                int(242 - 4 * horizontal),
                255,
            )

    mask = Image.new("L", image.size, 0)
    mask_draw = ImageDraw.Draw(mask)
    inset = int(canvas_size * 0.06)
    radius = int(canvas_size * 0.25)
    mask_draw.rounded_rectangle(
        (inset, inset, canvas_size - inset, canvas_size - inset),
        radius=radius,
        fill=255,
    )
    image.alpha_composite(Image.composite(gradient, Image.new("RGBA", image.size), mask))

    draw = ImageDraw.Draw(image)
    font = load_font(int(canvas_size * 0.48))
    box = draw.textbbox((0, 0), "S", font=font)
    width = box[2] - box[0]
    height = box[3] - box[1]
    draw.text(
        ((canvas_size - width) / 2, (canvas_size - height) / 2 - box[1]),
        "S",
        fill=(255, 255, 255, 255),
        font=font,
    )
    return image.resize((size, size), Image.Resampling.LANCZOS)


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for size in SIZES:
        create_icon(size).save(OUTPUT / f"icon-{size}.png")


if __name__ == "__main__":
    main()
