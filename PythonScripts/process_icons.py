"""
批量将 AIGenerated/Pills/ 下的原图缩放并抠图后输出到 Assets/Resources/Icons/Pills/。
抠图以 outline.png 为模板：圆周内部 alpha=1，外部 alpha=1-亮度。
已存在的目标文件默认跳过，使用 --force 强制覆盖。

用法:
    python resize_icons.py                  # 缩放至默认尺寸 256x256
    python resize_icons.py --size 128       # 缩放至 128x128
    python resize_icons.py --force          # 强制覆盖已有文件
    python resize_icons.py --size 128 --force
"""

import argparse
import pathlib

import numpy as np
from PIL import Image

SCRIPT_DIR = pathlib.Path(__file__).parent
REPO_ROOT = SCRIPT_DIR.parent

INPUT_DIR = REPO_ROOT / "AIGenerated/Pills"
OUTPUT_DIR = REPO_ROOT / "Assets/Resources/Icons/Pills"
OUTLINE_IMAGE = SCRIPT_DIR / "mask.png"
DEFAULT_SIZE = 128
DEFAULT_SCALE = 0.165   # 缩小系数


def load_mask(mask_path: pathlib.Path, size: tuple[int, int]) -> Image.Image:
    """直接读取 mask.png 的 alpha 通道作为 mask。"""
    return Image.open(mask_path).convert("RGBA").resize(size, Image.LANCZOS).getchannel("A")


def process(src: pathlib.Path, dst: pathlib.Path, size: int, mask: Image.Image,
            scale: float) -> None:
    img = Image.open(src).convert("RGBA")
    orig_w, orig_h = img.size

    # 1. 应用 alpha mask（将 mask 缩放到原图尺寸）
    full_mask = np.array(mask.resize((orig_w, orig_h), Image.LANCZOS))
    gray = np.array(img.convert("L")).astype(float) / 255.0
    luminance_alpha = ((1.0 - gray) * 255).astype(np.uint8)
    final_alpha = np.where(full_mask > 0, full_mask, luminance_alpha)
    img.putalpha(Image.fromarray(final_alpha.astype(np.uint8), mode="L"))

    # 2. 缩小到 scale 倍
    scaled_w = round(orig_w * scale)
    scaled_h = round(orig_h * scale)
    img = img.resize((scaled_w, scaled_h), Image.LANCZOS)

    # 3. 中心裁剪到最终输出尺寸
    left = (scaled_w - size) // 2
    top  = (scaled_h - size) // 2
    img = img.crop((left, top, left + size, top + size))

    img.save(str(dst))


def resize_all(size: int, scale: float) -> None:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    print(f"Loading mask from {OUTLINE_IMAGE.name} at {size}x{size}...")
    mask = load_mask(OUTLINE_IMAGE, (size, size))

    sources = sorted(INPUT_DIR.glob("*.png"))
    if not sources:
        print(f"No PNG files found in {INPUT_DIR}")
        return

    print(f"Processing {len(sources)} images → scale={scale} → crop to {size}x{size} → {OUTPUT_DIR}")
    for src in sources:
        dst = OUTPUT_DIR / src.name
        process(src, dst, size, mask, scale)
        print(f"  OK    {src.name}")

    print("Done.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Batch resize and mask pill icons.")
    parser.add_argument("--size", type=int, default=DEFAULT_SIZE,
                        help=f"Output pixel size (square). Default: {DEFAULT_SIZE}")
    parser.add_argument("--scale", type=float, default=DEFAULT_SCALE,
                        help=f"Scale factor applied before crop (e.g. 0.8). Default: {DEFAULT_SCALE}")
    args = parser.parse_args()
    resize_all(args.size, args.scale)
