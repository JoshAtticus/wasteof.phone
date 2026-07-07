import os
from PIL import Image, ImageDraw

# Normalize coordinates
viewbox_w = 188.943
viewbox_h = 94.471

rect_coords = [
    (0.0, 0.0),
    (188.943, 0.0),
    (188.943, 94.471),
    (0.0, 94.471)
]

w_coords = [
    (347.808 - 336, 335.618 - 312),
    (360.715 - 336, 382.889 - 312),
    (374.612 - 336, 382.889 - 312),
    (382.764 - 336, 353.152 - 312),
    (383.344 - 336, 353.152 - 312),
    (391.496 - 336, 382.889 - 312),
    (405.421 - 336, 382.889 - 312),
    (418.299 - 336, 335.618 - 312),
    (405.025 - 336, 335.618 - 312),
    (397.793 - 336, 367.188 - 312),
    (397.397 - 336, 367.188 - 312),
    (389.613 - 336, 335.618 - 312),
    (376.551 - 336, 335.618 - 312),
    (368.895 - 336, 367.372 - 312),
    (368.47 - 336, 367.372 - 312),
    (361.111 - 336, 335.618 - 312)
]

assets_path = r"c:\Users\Josh\Repos\wasteof.phone\wasteof.phone\Assets"

def render_logo(width, height, is_splash=False):
    # Determine the drawing area for the logo itself
    if is_splash:
        # Splash screen logo should be centered and relatively small
        logo_w = min(width, height) * 0.4
        logo_h = logo_w * (viewbox_h / viewbox_w)
    else:
        # Icons
        logo_w = width * 0.55
        logo_h = logo_w * (viewbox_h / viewbox_w)
        if logo_h > height * 0.8:
            logo_h = height * 0.8
            logo_w = logo_h * (viewbox_w / viewbox_h)

    logo_w = int(logo_w)
    logo_h = int(logo_h)

    # Scaling function
    scale_x = logo_w / viewbox_w
    scale_y = logo_h / viewbox_h

    scaled_rect = [(x * scale_x, y * scale_y) for (x, y) in rect_coords]
    scaled_w = [(x * scale_x, y * scale_y) for (x, y) in w_coords]

    # Create 1-bit mask for the logo
    mask = Image.new('L', (logo_w, logo_h), 0)
    mask_draw = ImageDraw.Draw(mask)
    mask_draw.polygon(scaled_rect, fill=255)
    mask_draw.polygon(scaled_w, fill=0)

    # Create logo image (white)
    logo_img = Image.new('RGBA', (logo_w, logo_h), (255, 255, 255, 255))
    logo_img.putalpha(mask)

    # Create target canvas
    canvas = Image.new('RGBA', (width, height), (0, 0, 0, 0)) # transparent background
    
    # Calculate centering offset
    offset_x = (width - logo_w) // 2
    offset_y = (height - logo_h) // 2

    canvas.paste(logo_img, (offset_x, offset_y), logo_img)
    return canvas

# List files and replace
for filename in os.listdir(assets_path):
    if not filename.endswith(".png"):
        continue
    filepath = os.path.join(assets_path, filename)
    try:
        with Image.open(filepath) as img:
            w, h = img.size
        print(f"Replacing {filename} with dimensions {w}x{h}")
        is_splash = "splash" in filename.lower()
        new_img = render_logo(w, h, is_splash)
        new_img.save(filepath, "PNG")
    except Exception as e:
        print(f"Error processing {filename}: {e}")
