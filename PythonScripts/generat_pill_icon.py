"""
Generate pill (丹药) icons for ImmortalUnity.
Reads Assets/Resources/Data/Items/Pill.json, builds a prompt for each pill
based on its element (phase), rarity, realm, and pill type, then calls
Imagen 4.0 to generate a 1:1 icon and saves it to
Assets/Resources/Icons/Pills/<id>.png.

Already-generated files are skipped so the script is safe to re-run.
"""

import json
import pathlib
import re

from PIL import Image
from io import BytesIO

from image_gen import generate_image, get_client

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
PROJECT_ID = "aigame-437403"
LOCATION = "us-central1"

SCRIPT_DIR = pathlib.Path(__file__).parent
REPO_ROOT = SCRIPT_DIR.parent
PILL_JSON = REPO_ROOT / "Assets/Resources/Data/Items/Pill.json"
OUTPUT_DIR = REPO_ROOT / "Assets/Resources/Icons/Pills"
ORIGINAL_DIR = REPO_ROOT / "AIGenerated/Pills"  # 原图（1024x1024）存放路径，Assets 目录之外
OUTPUT_SIZE = (256, 256)  # 最终保存尺寸，可改为 (128, 128) 等
OUTLINE_IMAGE = SCRIPT_DIR / "outline.png"  # 丹药轮廓参考图
# 只生成指定稀有度的图标；设为 None 则生成全部。
# 例：RARITY_FILTER = {0}        → 只生成普通
#     RARITY_FILTER = {0, 1, 2}  → 生成普通/精良/稀有
#     RARITY_FILTER = None       → 生成全部
RARITY_FILTER: set | None = None

# ---------------------------------------------------------------------------
# Attribute look-up tables
# ---------------------------------------------------------------------------
PHASE_INFO = {
    0: {
        "name": "金",
        "color": "银色",
        "Specular": "非常淡的金属光泽",
        "fx": "半透明的光粒四溢",
    },
    1: {
        "name": "木",
        "color": "翠绿色",
        "Specular": "非常淡的绿色光泽",
        "fx": "半透明的旋转叶片灵光",
    },
    2: {
        "name": "水",
        "color": "海的浅蓝色",
        "Specular": "非常淡的蓝色光泽",
        "fx": "半透明的流动水波纹",
    },
    3: {
        "name": "火",
        "color": "火红色",
        "Specular": "非常淡的红色光泽",
        "fx": "半透明的跳动火焰",
    },
    4: {
        "name": "土",
        "color": "赭石色",
        "Specular": "非常淡的黄土色光泽",
        "fx": "半透明的旋转尘埃",
    },
}

RARITY_INFO = {
    0: {"name": "普通",   "quality": "普通",
        "complexity": "造型极简，光滑圆球，表面完全无纹路，无任何装饰"},
    1: {"name": "精良",   "quality": "精良",
        "complexity": "有极弱整的特效，不超过球体范围，仍以光滑圆球为主"},
    2: {"name": "稀有",   "quality": "精品",
        "complexity": "整体仍以光滑圆球为主,有极弱整的特效，透明度0.1，微微超过球体范围1/10"},
    3: {"name": "史诗",   "quality": "极品",
        "complexity": "整体仍以光滑圆球为主,有极的特效和淡淡的青铜器纹，透明度0.1，微微超过球体范围2/10"},
    4: {"name": "传说",   "quality": "传说级",
        "complexity": "整体仍以光滑圆球为主,有弱的特效和淡淡的青铜器纹，透明度0.2，稍微超过球体范围2/10"},
    5: {"name": "仙品",   "quality": "仙品",
        "complexity": "整体仍以光滑圆球为主,有弱特效和淡淡的青铜器纹，透明度0.3，超过球体范围3/10"},
}

REALM_INFO = {
    0: "练气期",
    1: "筑基期",
    2: "金丹期",
    3: "元婴期",
    4: "化神期",
    5: "返虚期",
    6: "渡劫期",
    7: "大乘期",
}

PILL_TYPE_INFO = {
    "recovery": {
        "label": "恢复丹",
        "detail": "恢复气血与真元，表面散发温暖疗愈的气息",
    },
    "cultivation": {
        "label": "修炼丹",
        "detail": "加速修为增长，表面萦绕道韵",
    },
    "breakthrough": {
        "label": "突破辅丹",
        "detail": "助力境界突破，表面蕴含凝实灵力",
    },
}

NEGATIVE_PROMPT = (
    "文字，水印，标签，模糊，低质量，写实人脸，照片，"
    "背景风景，杂乱背景，多粒丹药"
)

# ---------------------------------------------------------------------------
# 全局风格标准
# ---------------------------------------------------------------------------
STYLE_BASE = (
    "风格：扁平2D游戏图标，细节极简，造型简洁干净"
    "粗深色轮廓线"
    "纯色填充，表面呈现类似光滑巧克力的质感：左上角有一处小的、极柔和、低饱和度的反光区，与主色融合自然，无强烈白色高光，无镜面反射，无内部发光，无正中心高亮，无内核光点，"
    "无裂纹，无复杂图案，"
    "东方中世纪木刻剪影风格"
    "纯白色背景，无阴影，无渐变，方便后续抠图使用，"
    "物体外无阴影，背景无渐变。"
)


def get_pill_type(pill_id: str) -> str:
    """Derive pill type (recovery / cultivation / breakthrough) from its id."""
    for t in ("recovery", "cultivation", "breakthrough"):
        if f"_{t}_" in pill_id:
            return t
    return "recovery"


def build_prompt(pill: dict) -> str:
    phase = PHASE_INFO.get(pill["phase"], PHASE_INFO[0])
    rarity = RARITY_INFO.get(pill["rarity"], RARITY_INFO[0])
    realm = REALM_INFO.get(pill["requiredRealm"], REALM_INFO[0])
    ptype = PILL_TYPE_INFO[get_pill_type(pill["id"])]

    buff_clause = ""
    if pill.get("buff"):
        buff_text = re.sub(r'[+\-]?\d+%?', '', pill['buff']).strip()
        buff_clause = f"，丹面浮现淡淡的特效，象征{buff_text}得到强化"


    prompt = (
        f"严格保持参考图片中的丹药轮廓形状，绘制一枚{rarity['quality']}的仙侠修真{ptype['label']}，"
        f"供{realm}修士使用。"
        f"严格保持参考图片中的轮廓与构图，仅替换颜色和纹饰。"
        f"实体颜色为{phase['color']}，{phase['Specular']}，{phase['fx']}。"
        f"{rarity['complexity']}。"
        f"{ptype['detail']}{buff_clause}。"
        f"{STYLE_BASE}"
    )
    return prompt


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
def main():
    client = get_client()

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    ORIGINAL_DIR.mkdir(parents=True, exist_ok=True)

    # 加载轮廓参考图
    outline_bytes = OUTLINE_IMAGE.read_bytes()
    print(f"Loaded outline image: {OUTLINE_IMAGE.name} ({len(outline_bytes)} bytes)")
    with open(PILL_JSON, encoding="utf-8-sig") as f:
        data = json.load(f)

    pills = data["items"]
    total = len(pills)
    print(f"Found {total} pills in {PILL_JSON.name}")

    for idx, pill in enumerate(pills, 1):
        pill_id = pill["id"]
        out_path = OUTPUT_DIR / f"{pill_id}.png"

        if out_path.exists():
            print(f"[{idx}/{total}] SKIP  {pill_id}  (already exists)")
            continue

        if RARITY_FILTER is not None and pill.get("rarity") not in RARITY_FILTER:
            print(f"[{idx}/{total}] SKIP  {pill_id}  (rarity {pill.get('rarity')} not in filter)")
            continue
        prompt = build_prompt(pill)
        print(f"[{idx}/{total}] GEN   {pill_id}")
        print(f"        prompt: {prompt}")

        try:
            img_bytes, img_ext = generate_image(
                client=client,
                prompt=prompt,
                reference_image_bytes=outline_bytes,
            )
            img = Image.open(BytesIO(img_bytes))
            # 保存原图到 Resources 之外
            img.save(str(ORIGINAL_DIR / f"{pill_id}.png"))
            print(f"        saved → {(ORIGINAL_DIR / f'{pill_id}.png').relative_to(REPO_ROOT)}")
        except RuntimeError as exc:
            print(f"        ERROR: {exc}")
        except Exception as exc:
            print(f"        ERROR: {exc}")

    print("Done.")


if __name__ == "__main__":
    main()
