"""Transitional pixel-art sprites for chinese-grocery (32x32, fixed palette)."""
from pathlib import Path
from PIL import Image

# Sprites go straight into the Godot project; previews stay next to this script.
ART_DIR = Path(__file__).resolve().parent
OUT_DIR = ART_DIR.parents[1] / "game" / "assets" / "sprites"
OUT_DIR.mkdir(parents=True, exist_ok=True)

# ---------- palette ----------
C = dict(
    out=(52, 34, 32), wd=(104, 66, 46), wm=(150, 100, 62), wl=(196, 146, 92),
    fl=(226, 210, 176), fs=(204, 184, 148), fg=(186, 164, 128),
    wall=(242, 228, 198), walls=(218, 198, 162),
    red=(200, 54, 46), redd=(140, 34, 38), redl=(236, 102, 76),
    gold=(240, 190, 76), goldd=(188, 132, 50),
    grn=(90, 146, 76), grnd=(54, 98, 60), grnl=(150, 196, 110),
    blu=(72, 112, 164), blud=(44, 70, 114), blul=(120, 160, 206),
    pur=(130, 84, 150), purd=(90, 56, 110),
    wht=(250, 246, 236), gry=(170, 166, 160), gryd=(108, 104, 104),
    soy=(46, 30, 28), skin=(242, 198, 162), skins=(212, 158, 126),
    hair=(40, 32, 40), meat=(228, 122, 120), meatd=(178, 74, 82), fat=(248, 228, 216),
    glass=(170, 214, 222), glassd=(120, 172, 186), yel=(246, 214, 92),
)

def new(w=32, h=32):
    return Image.new("RGBA", (w, h), (0, 0, 0, 0))

def p(im, x, y, c):
    if 0 <= x < im.width and 0 <= y < im.height:
        im.putpixel((x, y), C[c] + (255,) if isinstance(c, str) else c)

def r(im, x, y, w, h, c):
    for yy in range(y, y + h):
        for xx in range(x, x + w):
            p(im, xx, yy, c)

def outline(im):
    """1px dark outline around every opaque shape."""
    src = im.copy()
    for y in range(im.height):
        for x in range(im.width):
            if src.getpixel((x, y))[3] == 0:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < im.width and 0 <= ny < im.height and src.getpixel((nx, ny))[3] > 0:
                        p(im, x, y, "out")
                        break
    return im

def shadow(im, cx=16, y=30, rx=7):
    for yy in (y, y + 1):
        for xx in range(cx - rx, cx + rx + 1):
            if 0 <= xx < 32 and yy < 32 and im.getpixel((xx, yy))[3] == 0:
                im.putpixel((xx, yy), (52, 34, 32, 70))
    return im

# ---------- tiles ----------
def floor():
    im = new()
    for ty in range(2):
        for tx in range(2):
            c = "fl" if (tx + ty) % 2 == 0 else "fs"
            r(im, tx * 16, ty * 16, 16, 16, c)
    r(im, 0, 15, 32, 1, "fg"); r(im, 15, 0, 1, 32, "fg")
    r(im, 0, 31, 32, 1, "fg"); r(im, 31, 0, 1, 32, "fg")
    p(im, 4, 4, "wht"); p(im, 20, 20, "wht")
    return im

def wall():
    im = new()
    r(im, 0, 0, 32, 32, "wall")
    r(im, 0, 0, 32, 3, "walls")
    for x in (0, 16):
        r(im, x, 3, 1, 22, "walls")
    r(im, 0, 24, 32, 2, "wl"); r(im, 0, 26, 32, 6, "wm"); r(im, 0, 31, 32, 1, "wd")
    return im

# ---------- fixtures ----------
PRODUCT_COLORS = [("red", "yel"), ("soy", "red"), ("grn", "gold"), ("yel", "red"),
                  ("blu", "wht"), ("wht", "red"), ("redd", "gold"), ("grnl", "grnd")]

def shelf(full=True, seed=0):
    im = new()
    r(im, 1, 1, 30, 5, "wl"); r(im, 1, 5, 30, 1, "wm")           # top surface
    r(im, 1, 6, 30, 25, "wd")                                      # back panel
    r(im, 1, 6, 2, 25, "wm"); r(im, 29, 6, 2, 25, "wm")            # side frames
    for by in (13, 21, 29):
        r(im, 3, by, 26, 2, "wl")                                  # boards
    if full:
        i = seed
        for by in (13, 21, 29):
            x = 4
            while x < 27:
                a, b = PRODUCT_COLORS[i % len(PRODUCT_COLORS)]
                h = 5 + (i % 2)
                r(im, x, by - h, 3, h, a); r(im, x, by - h + 2, 3, 1, b)
                p(im, x, by - h, "wht")
                x += 4; i += 3
    return outline(im)

def counter(register=True):
    im = new()
    r(im, 1, 10, 30, 6, "wl"); r(im, 1, 15, 30, 1, "wm")
    r(im, 1, 16, 30, 14, "wm"); r(im, 1, 29, 30, 1, "wd")
    for x in (6, 16, 26):
        r(im, x, 18, 1, 10, "wd")
    r(im, 1, 21, 30, 2, "red")                                     # red trim
    if not register:
        return outline(im)
    # register
    r(im, 4, 4, 12, 8, "gryd"); r(im, 5, 5, 10, 3, "grnl"); r(im, 5, 9, 10, 2, "gry")
    p(im, 6, 6, "grnd"); p(im, 8, 6, "grnd")
    return outline(im)

def meat_fridge():
    im = new()
    r(im, 1, 2, 30, 12, "glass"); r(im, 1, 2, 30, 1, "wht")
    for i, x in enumerate(range(3, 27, 6)):
        r(im, x, 6, 5, 6, "meat"); r(im, x, 9, 5, 1, "fat"); r(im, x, 11, 5, 1, "meatd")
    r(im, 1, 13, 30, 1, "glassd")
    r(im, 1, 14, 30, 16, "wht"); r(im, 1, 14, 30, 2, "gry")
    r(im, 1, 29, 30, 1, "gryd"); r(im, 3, 20, 26, 2, "blul")
    return outline(im)

def cutting_table():
    im = new()
    r(im, 2, 6, 28, 10, "wl"); r(im, 2, 15, 28, 1, "wm")
    r(im, 2, 16, 28, 14, "wm"); r(im, 2, 29, 28, 1, "wd")
    for y in (20, 25):
        r(im, 4, y, 24, 1, "wd")
    r(im, 5, 8, 11, 6, "meat"); r(im, 5, 11, 11, 1, "fat"); r(im, 5, 13, 11, 1, "meatd")
    r(im, 19, 7, 7, 5, "gry"); r(im, 19, 7, 7, 1, "wht"); r(im, 21, 12, 3, 3, "wd")  # cleaver
    return outline(im)

def door():
    im = wall()
    r(im, 4, 3, 24, 29, "wd")
    r(im, 6, 5, 9, 26, "glass"); r(im, 17, 5, 9, 26, "glass")
    r(im, 6, 5, 9, 2, "wht"); r(im, 17, 5, 9, 2, "wht")
    r(im, 13, 17, 1, 4, "gold"); r(im, 18, 17, 1, 4, "gold")
    r(im, 9, 9, 14, 5, "red"); r(im, 11, 11, 10, 1, "gold")         # hanging sign
    return im

def lantern():
    im = new()
    r(im, 15, 0, 2, 4, "goldd")
    r(im, 11, 4, 10, 2, "gold")
    r(im, 9, 6, 14, 12, "red"); r(im, 10, 5, 12, 1, "red"); r(im, 10, 18, 12, 1, "red")
    r(im, 11, 7, 2, 10, "redl")
    for x in (13, 16, 19):
        r(im, x, 6, 1, 12, "redd")
    r(im, 11, 19, 10, 2, "gold")
    r(im, 15, 21, 2, 6, "redl")
    return outline(im)

def lucky_cat():
    im = new()
    r(im, 10, 14, 12, 14, "wht"); r(im, 9, 20, 14, 8, "wht")        # body
    r(im, 10, 6, 12, 9, "wht")                                      # head
    r(im, 10, 4, 3, 3, "wht"); r(im, 19, 4, 3, 3, "wht")            # ears
    p(im, 11, 5, "redl"); p(im, 20, 5, "redl")
    p(im, 13, 10, "soy"); p(im, 18, 10, "soy"); p(im, 15, 12, "redl"); p(im, 16, 12, "redl")
    r(im, 22, 7, 4, 8, "wht")                                       # raised paw
    r(im, 10, 15, 12, 2, "red"); r(im, 15, 17, 2, 2, "gold")        # collar + bell
    r(im, 12, 21, 8, 5, "gold"); r(im, 13, 22, 6, 1, "goldd")      # coin
    return outline(im)

# ---------- product icons ----------
def bottle(label, label2):
    im = new()
    r(im, 14, 2, 4, 3, "red")                      # cap
    r(im, 14, 5, 4, 5, "soy")                      # neck
    r(im, 11, 10, 10, 19, "soy"); r(im, 12, 9, 8, 1, "soy")
    r(im, 11, 15, 10, 9, label); r(im, 11, 17, 10, 1, label2); r(im, 11, 21, 10, 1, label2)
    r(im, 12, 11, 1, 16, (110, 80, 70))            # glint
    return shadow(outline(im), rx=6)

def noodle_cup():
    im = new()
    r(im, 8, 8, 16, 3, "wht"); r(im, 8, 8, 16, 1, "gry")           # lid
    for y in range(11, 28):
        inset = (y - 11) // 6
        r(im, 9 + inset, y, 14 - inset * 2, 1, "red")
    r(im, 10, 15, 12, 4, "wht"); r(im, 13, 16, 6, 2, "yel")
    r(im, 11, 23, 10, 1, "gold")
    return shadow(outline(im), rx=6)

def rice_bag():
    im = new()
    r(im, 8, 6, 16, 22, "wht"); r(im, 9, 5, 14, 1, "wht")
    r(im, 12, 3, 8, 3, "fs")                                        # tied top
    r(im, 8, 13, 16, 8, "red"); r(im, 13, 15, 6, 4, "gold"); r(im, 15, 16, 2, 2, "red")
    r(im, 8, 24, 16, 1, "fs")
    return shadow(outline(im), rx=8)

def chili_jar():
    im = new()
    r(im, 10, 6, 12, 4, "red"); r(im, 10, 6, 12, 1, "redl")         # lid
    r(im, 9, 10, 14, 17, "glass")
    r(im, 10, 13, 12, 13, "redd"); r(im, 10, 13, 12, 2, (196, 90, 40))
    for x, y in ((12, 17), (17, 20), (14, 23), (19, 16)):
        p(im, x, y, "gold")
    r(im, 9, 18, 14, 5, "yel"); r(im, 13, 19, 6, 3, "red")
    return shadow(outline(im), rx=7)

def tea_can():
    im = new()
    r(im, 11, 4, 10, 2, "gry"); r(im, 10, 6, 12, 22, "grn")
    r(im, 10, 6, 12, 1, "gry"); r(im, 10, 27, 12, 1, "gry")
    r(im, 10, 12, 12, 9, "gold"); r(im, 13, 14, 6, 5, "grnd")
    r(im, 11, 7, 1, 19, "grnl")
    return shadow(outline(im), rx=6)

def snack_bag():
    im = new()
    r(im, 7, 7, 18, 20, "yel"); r(im, 7, 5, 18, 2, "red"); r(im, 7, 27, 18, 2, "red")
    for x in range(7, 25, 2):
        p(im, x, 4, "red"); p(im, x, 29, "red")
    r(im, 10, 12, 12, 9, "redl"); r(im, 12, 14, 8, 5, "wht"); r(im, 14, 15, 4, 3, "goldd")
    r(im, 8, 8, 1, 17, "wht")
    return shadow(outline(im), rx=8)

def tray(fill):
    im = new()
    r(im, 4, 12, 24, 14, "wht"); r(im, 4, 24, 24, 2, "gry")
    fill(im)
    r(im, 4, 12, 24, 1, "glass")
    return shadow(outline(im), rx=12)

def pork_belly():
    def f(im):
        for i, y in enumerate(range(14, 24)):
            r(im, 6, y, 20, 1, "fat" if i in (2, 5, 8) else ("meatd" if i == 0 else "meat"))
    return tray(f)

def ribs():
    def f(im):
        for x in (6, 12, 18):
            r(im, x, 15, 5, 8, "meat"); r(im, x + 2, 14, 1, 10, "wht"); r(im, x, 22, 5, 1, "meatd")
    return tray(f)

def minced():
    def f(im):
        r(im, 7, 16, 18, 7, "meat"); r(im, 9, 14, 14, 2, "meat")
        for x, y in ((9, 17), (13, 15), (16, 19), (20, 17), (11, 21), (22, 21), (18, 15)):
            p(im, x, y, "meatd"); p(im, x + 1, y + 1, "fat")
    return tray(f)

def pork_primal():
    im = new()
    r(im, 4, 9, 24, 16, "meat"); r(im, 6, 7, 20, 2, "meat")
    r(im, 4, 9, 24, 3, "fat"); r(im, 4, 22, 24, 3, "meatd")
    r(im, 22, 13, 4, 8, "wht"); r(im, 23, 14, 2, 6, "fat")
    return shadow(outline(im), rx=12)

# ---------- characters (front view) ----------
def person(shirt, shirt_d, pants, hair="hair", hat=None, apron=None, extra=None):
    im = new()
    # legs + shoes
    r(im, 12, 25, 3, 4, pants); r(im, 17, 25, 3, 4, pants)
    r(im, 11, 29, 4, 1, "soy"); r(im, 17, 29, 4, 1, "soy")
    # body + arms
    r(im, 10, 15, 12, 11, shirt); r(im, 20, 15, 2, 11, shirt_d)
    r(im, 8, 16, 2, 8, shirt); r(im, 22, 16, 2, 8, shirt_d)
    r(im, 8, 24, 2, 2, "skin"); r(im, 22, 24, 2, 2, "skins")
    if apron:
        r(im, 12, 16, 8, 10, apron); r(im, 13, 15, 6, 1, apron)
    # head
    r(im, 11, 5, 10, 10, "skin"); r(im, 19, 6, 2, 9, "skins")
    r(im, 13, 10, 1, 2, "soy"); r(im, 18, 10, 1, 2, "soy")
    p(im, 12, 12, "redl"); p(im, 19, 12, "redl"); r(im, 15, 13, 2, 1, "skins")
    # hair
    r(im, 10, 3, 12, 4, hair); r(im, 10, 7, 1, 4, hair); r(im, 21, 7, 1, 4, hair)
    if hat:
        hat(im)
    if extra:
        extra(im)
    return shadow(outline(im))

def protagonist():
    def hoodie(im):
        r(im, 14, 15, 1, 4, "wht"); r(im, 17, 15, 1, 4, "wht")      # strings
    return person("blu", "blud", "soy", apron="red", extra=lambda im: (
        r(im, 13, 18, 6, 2, "gold"), hoodie(im)))

def butcher():
    def cap(im):
        r(im, 10, 1, 12, 5, "wht"); r(im, 10, 5, 12, 1, "gry")
    def stain(im):
        p(im, 14, 20, "meatd"); p(im, 17, 22, "meatd"); p(im, 15, 23, "meat")
    return person("gry", "gryd", "blud", hat=cap, apron="wht", extra=stain)

def auntie():
    def curls(im):
        for x in range(10, 22, 2):
            p(im, x, 2, "hair")
        r(im, 9, 4, 1, 6, "hair"); r(im, 22, 4, 1, 6, "hair")
    def bag(im):
        r(im, 23, 20, 6, 7, "red"); r(im, 24, 18, 4, 2, "red"); r(im, 25, 19, 2, 1, (0, 0, 0, 0))
        r(im, 24, 22, 4, 1, "yel")
    return person("pur", "purd", "soy", hair=(70, 60, 70), hat=curls, extra=bag)

def student():
    def glasses(im):
        r(im, 12, 10, 3, 2, "gryd"); r(im, 17, 10, 3, 2, "gryd"); r(im, 15, 10, 2, 1, "gryd")
        p(im, 13, 11, "skin"); p(im, 18, 11, "skin")
    def straps(im):
        r(im, 11, 15, 1, 10, "redd"); r(im, 20, 15, 1, 10, "redd")
    return person("grn", "grnd", "blu", extra=lambda im: (glasses(im), straps(im)))

SPRITES = {
    "tile_floor": floor, "tile_wall": wall, "door": door,
    "shelf_empty": lambda: shelf(False), "shelf_full": lambda: shelf(True, 0),
    "shelf_full_b": lambda: shelf(True, 5), "counter": counter, "counter_plain": lambda: counter(False),
    "meat_fridge": meat_fridge, "cutting_table": cutting_table,
    "deco_lantern": lantern, "deco_lucky_cat": lucky_cat,
    "product_soy_sauce_light": lambda: bottle("red", "yel"),
    "product_soy_sauce_dark": lambda: bottle("soy", "gold"),
    "product_instant_noodles": noodle_cup, "product_rice": rice_bag,
    "product_chili_crisp": chili_jar, "product_tea_drink": tea_can,
    "product_snack": snack_bag, "product_pork_primal": pork_primal,
    "product_pork_belly": pork_belly, "product_pork_ribs": ribs,
    "product_pork_mince": minced,
    "char_protagonist": protagonist, "char_butcher": butcher,
    "char_auntie": auntie, "char_student": student,
}

def main():
    imgs = {name: fn() for name, fn in SPRITES.items()}
    for name, im in imgs.items():
        im.save(OUT_DIR / f"{name}.png")

    # sprite sheet (1x), 8 per row
    names = list(imgs)
    cols = 8
    rows = (len(names) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * 32, rows * 32), (0, 0, 0, 0))
    for i, n in enumerate(names):
        sheet.paste(imgs[n], ((i % cols) * 32, (i // cols) * 32), imgs[n])
    sheet.save(ART_DIR / "spritesheet.png")
    sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(ART_DIR / "preview_sheet_4x.png")

    # mock store scene, 12 x 8 tiles
    W, H = 12, 8
    scene = Image.new("RGBA", (W * 32, H * 32), C["fl"] + (255,))
    def put(n, cx, cy):
        im = imgs[n]
        scene.paste(im, (cx * 32, cy * 32), im)
    for x in range(W):
        put("tile_wall", x, 0)
        for y in range(1, H):
            put("tile_floor", x, y)
    put("door", 10, 0)
    put("deco_lantern", 2, 0); put("deco_lantern", 7, 0)
    for x in range(1, 5):
        put("shelf_full" if x % 2 else "shelf_full_b", x, 2)
        put("shelf_full_b" if x % 2 else "shelf_full", x, 5)
    put("shelf_empty", 5, 5)
    put("meat_fridge", 7, 2); put("meat_fridge", 8, 2); put("cutting_table", 10, 2)
    put("counter", 8, 6); put("counter_plain", 9, 6)
    scene.paste(imgs["deco_lucky_cat"], (9 * 32 + 4, 6 * 32 - 14), imgs["deco_lucky_cat"])
    put("char_protagonist", 3, 3); put("char_butcher", 10, 3)
    put("char_auntie", 6, 4); put("char_student", 8, 7)
    scene.resize((scene.width * 3, scene.height * 3), Image.NEAREST).save(ART_DIR / "preview_scene_3x.png")
    print(f"{len(imgs)} sprites written")

if __name__ == "__main__":
    main()
