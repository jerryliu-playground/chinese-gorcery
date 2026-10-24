"""Transitional pixel-art sprites for chinese-grocery (32x32, fixed palette)."""
from pathlib import Path
from PIL import Image

# Sprites go straight into the Godot project; sheets and previews stay next to this script.
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

# ---------- batch 2: more products ----------
def bottle2(liquid, cap, label, wide=False):
    im = new()
    w = 12 if wide else 8
    x0 = 16 - w // 2
    r(im, 14, 3, 4, 3, cap)
    r(im, 14, 6, 4, 4, liquid)
    r(im, x0, 10, w, 18, liquid); r(im, x0 + 1, 9, w - 2, 1, liquid)
    r(im, x0, 15, w, 7, label); r(im, x0, 18, w, 1, "gold")
    r(im, x0 + 1, 11, 1, 15, "wht")
    return shadow(outline(im), rx=6)

def vermicelli():
    im = new()
    r(im, 6, 9, 20, 16, "wht")
    for y in range(11, 23, 2):
        for x in range(8, 24, 3):
            p(im, x + (y // 2) % 2, y, "fs")
    r(im, 6, 14, 20, 5, "blu"); r(im, 12, 15, 8, 3, "yel")
    r(im, 6, 9, 20, 1, "glass")
    return shadow(outline(im), rx=10)

def dumplings_frozen():
    im = new()
    r(im, 7, 6, 18, 21, "blul"); r(im, 7, 4, 18, 2, "blud"); r(im, 7, 27, 18, 2, "blud")
    # dumpling
    r(im, 10, 15, 12, 5, "wht"); r(im, 12, 13, 8, 2, "wht"); r(im, 10, 20, 12, 1, "fs")
    for x in range(12, 21, 2):
        p(im, x, 13, "fs")
    r(im, 9, 8, 14, 3, "red"); r(im, 9, 23, 14, 2, "wht")
    p(im, 8, 6, "wht"); p(im, 8, 7, "wht")
    return shadow(outline(im), rx=8)

def tofu():
    im = new()
    r(im, 6, 12, 20, 13, "wht"); r(im, 6, 12, 20, 2, "glass")
    r(im, 9, 15, 14, 8, (246, 238, 214)); r(im, 9, 22, 14, 1, "fs")
    r(im, 6, 23, 20, 2, "grn")
    return shadow(outline(im), rx=10)

def red_dates():
    im = new()
    r(im, 8, 6, 16, 21, "glass"); r(im, 8, 4, 16, 2, "red")
    for (x, y) in ((10, 10), (14, 9), (18, 11), (11, 14), (16, 15), (20, 16), (10, 19), (14, 20), (19, 21), (12, 24), (17, 24)):
        r(im, x, y, 3, 2, "redd"); p(im, x, y, "redl")
    r(im, 8, 16, 16, 3, "gold")
    return shadow(outline(im), rx=8)

# ---------- produce ----------
def napa():
    im = new()
    r(im, 10, 6, 12, 22, (232, 236, 196)); r(im, 9, 9, 14, 17, (232, 236, 196))
    r(im, 10, 6, 12, 8, "grnl"); r(im, 9, 9, 2, 6, "grnl"); r(im, 21, 9, 2, 6, "grnl")
    for y in range(14, 27, 3):
        r(im, 15, y, 2, 2, "wht")
    r(im, 15, 6, 1, 22, (210, 216, 170))
    return shadow(outline(im), rx=7)

def bok_choy():
    im = new()
    r(im, 9, 4, 6, 12, "grn"); r(im, 17, 4, 6, 12, "grn"); r(im, 13, 3, 6, 11, "grnl")
    r(im, 12, 15, 8, 12, "wht"); r(im, 11, 18, 10, 8, "wht")
    r(im, 13, 15, 1, 11, "grnl"); r(im, 18, 15, 1, 11, "grnl")
    r(im, 12, 26, 8, 1, "fs")
    return shadow(outline(im), rx=6)

def scallion():
    im = new()
    for x in (11, 14, 17, 20):
        r(im, x, 3, 2, 15, "grn"); p(im, x, 3, "grnl")
        r(im, x, 18, 2, 8, "wht")
        p(im, x, 26, (220, 210, 180)); p(im, x + 1, 27, (220, 210, 180))
    r(im, 10, 18, 13, 2, "red")
    return shadow(outline(im), rx=6)

def ginger():
    im = new()
    col, cold = (216, 172, 104), (176, 132, 72)
    r(im, 7, 14, 18, 9, col); r(im, 10, 10, 5, 5, col); r(im, 18, 11, 5, 4, col); r(im, 22, 17, 4, 4, col)
    r(im, 7, 21, 18, 2, cold)
    for x, y in ((11, 16), (16, 18), (20, 15), (13, 11)):
        p(im, x, y, cold)
    p(im, 12, 10, "yel"); p(im, 19, 11, "yel")
    return shadow(outline(im), rx=10)

def orange():
    im = new()
    oc, od = (240, 146, 48), (204, 104, 32)
    for y in range(9, 27):
        half = int((81 - (y - 18) ** 2) ** 0.5)
        r(im, 16 - half, y, half * 2, 1, oc)
        r(im, 16 + half - 3, y, 3, 1, od) if half > 2 else None
    r(im, 15, 7, 2, 3, "grnd"); r(im, 17, 6, 4, 2, "grn")
    p(im, 11, 12, "yel"); p(im, 12, 12, "yel"); p(im, 11, 13, "yel")
    return shadow(outline(im), rx=8)

def lychee():
    im = new()
    for cx, cy in ((12, 18), (20, 18), (16, 12)):
        for y in range(cy - 4, cy + 4):
            for x in range(cx - 4, cx + 4):
                if (x - cx + .5) ** 2 + (y - cy + .5) ** 2 < 17:
                    p(im, x, y, "redl" if (x + y) % 3 else "redd")
    r(im, 15, 5, 2, 4, "grnd"); r(im, 17, 4, 4, 2, "grn")
    return shadow(outline(im), rx=9)

def daikon():
    im = new()
    for y in range(9, 28):
        w = max(2, 8 - (y - 9) // 3)
        r(im, 16 - w // 2, y, w, 1, "wht")
    r(im, 12, 3, 3, 7, "grn"); r(im, 17, 3, 3, 7, "grn"); r(im, 15, 2, 2, 8, "grnl")
    for y in (14, 19, 23):
        p(im, 14, y, "fs")
    return shadow(outline(im), rx=5)

# ---------- fixtures batch 2 ----------
PRODUCE_FILL = [("grnl", "grn"), ((240, 146, 48), (204, 104, 32)), ("redl", "redd"), ((232, 236, 196), "grnl")]

def produce_stand(full=True, seed=0):
    im = new()
    r(im, 1, 18, 30, 12, "wm"); r(im, 1, 29, 30, 1, "wd")
    for x in (2, 16):
        r(im, x, 20, 13, 1, "wd"); r(im, x, 25, 13, 1, "wd")
    # two slanted crates
    for i, x in enumerate((2, 17)):
        r(im, x, 4, 13, 14, "wl"); r(im, x, 4, 13, 1, "wm"); r(im, x, 17, 13, 1, "wd")
        r(im, x, 4, 1, 14, "wm"); r(im, x + 12, 4, 1, 14, "wm")
        if full:
            a, b = PRODUCE_FILL[(seed + i) % len(PRODUCE_FILL)]
            for yy in range(6, 16, 3):
                for xx in range(x + 2, x + 11, 3):
                    r(im, xx, yy, 3, 3, a); p(im, xx + 2, yy + 2, b); p(im, xx, yy, "wht")
    r(im, 6, 21, 6, 3, "wht"); r(im, 20, 21, 6, 3, "wht")          # price cards
    p(im, 7, 22, "red"); p(im, 21, 22, "red")
    return outline(im)

def freezer():
    im = new()
    r(im, 1, 6, 30, 10, "glass"); r(im, 1, 6, 30, 1, "wht"); r(im, 16, 6, 1, 10, "gry")
    for x, c in ((4, "blul"), (9, "red"), (19, "blul"), (24, "yel")):
        r(im, x, 9, 4, 5, c); p(im, x, 9, "wht")
    r(im, 1, 16, 30, 14, "wht"); r(im, 1, 16, 30, 2, "gry"); r(im, 1, 29, 30, 1, "gryd")
    r(im, 4, 22, 24, 1, "blul"); r(im, 26, 19, 3, 2, "grnl")
    return outline(im)

def fu_poster():
    im = new()
    for y in range(4, 28):
        half = 12 - abs(y - 16)
        r(im, 16 - half, y, half * 2, 1, "red")
    for y in range(9, 23):
        half = max(0, 7 - abs(y - 16))
        r(im, 16 - half, y, half * 2, 1, "redd")
    r(im, 14, 13, 4, 6, "gold"); r(im, 12, 15, 8, 2, "gold")
    r(im, 15, 1, 2, 3, "goldd")
    return outline(im)

def plant():
    im = new()
    r(im, 11, 21, 10, 8, "redd"); r(im, 10, 20, 12, 2, "red"); r(im, 12, 28, 8, 1, "out")
    for x, y in ((10, 8), (15, 4), (20, 9), (13, 13), (18, 14), (8, 15), (22, 16)):
        r(im, x, y, 4, 4, "grn"); p(im, x, y, "grnl"); p(im, x + 3, y + 3, "grnd")
    r(im, 15, 12, 2, 9, "wd")
    return shadow(outline(im), rx=6)

def basket():
    im = new()
    r(im, 7, 16, 18, 11, "red"); r(im, 6, 15, 20, 2, "redl")
    for x in range(9, 24, 3):
        r(im, x, 18, 1, 8, "redd")
    r(im, 8, 9, 2, 7, "gryd"); r(im, 22, 9, 2, 7, "gryd"); r(im, 8, 8, 16, 2, "gryd")
    return shadow(outline(im), rx=9)

def mat():
    im = new()
    r(im, 3, 10, 26, 14, "redd"); r(im, 5, 12, 22, 10, "red")
    for x in range(7, 26, 4):
        r(im, x, 14, 2, 6, "gold")
    return im

# ---------- UI icons ----------
def icon_coin():
    im = new()
    for y in range(8, 25):
        half = int((72 - (y - 16) ** 2) ** 0.5) if (y - 16) ** 2 < 72 else 0
        r(im, 16 - half, y, half * 2, 1, "gold")
    r(im, 13, 12, 6, 8, "goldd"); r(im, 14, 13, 4, 6, "gold"); r(im, 11, 10, 2, 2, "wht")
    return outline(im)

def icon_clock():
    im = new()
    for y in range(7, 26):
        half = int((90 - (y - 16) ** 2) ** 0.5) if (y - 16) ** 2 < 90 else 0
        r(im, 16 - half, y, half * 2, 1, "wht")
    r(im, 15, 10, 2, 7, "soy"); r(im, 16, 16, 5, 2, "red")
    return outline(im)

def bubble(draw):
    im = new()
    r(im, 6, 3, 20, 18, "wht"); r(im, 5, 5, 22, 14, "wht")
    r(im, 12, 21, 4, 2, "wht"); r(im, 12, 23, 2, 2, "wht")
    draw(im)
    return outline(im)

def b_question(im):
    r(im, 13, 6, 6, 2, "blu"); r(im, 18, 7, 2, 4, "blu"); r(im, 15, 11, 3, 2, "blu"); r(im, 15, 13, 2, 2, "blu"); r(im, 15, 16, 2, 2, "blu")

def b_frown(im):
    r(im, 11, 8, 2, 3, "soy"); r(im, 19, 8, 2, 3, "soy")
    r(im, 12, 15, 8, 1, "soy"); p(im, 11, 16, "soy"); p(im, 20, 16, "soy")
    r(im, 10, 6, 4, 1, "soy"); r(im, 18, 6, 4, 1, "soy")

def b_angry(im):
    r(im, 8, 5, 16, 15, "redl")
    r(im, 11, 9, 3, 2, "soy"); r(im, 18, 9, 3, 2, "soy"); p(im, 10, 8, "soy"); p(im, 21, 8, "soy")
    r(im, 12, 15, 8, 1, "soy")

def b_heart(im):
    r(im, 10, 7, 5, 4, "red"); r(im, 17, 7, 5, 4, "red"); r(im, 9, 9, 14, 3, "red")
    for i, y in enumerate(range(12, 18)):
        r(im, 10 + i, y, 12 - i * 2, 1, "red")
    p(im, 11, 8, "wht")

def icon_star():
    import math
    from PIL import ImageDraw
    im = new()
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rad = 12 if i % 2 == 0 else 5
        pts.append((16 + rad * math.cos(a), 16.5 + rad * math.sin(a)))
    ImageDraw.Draw(im).polygon(pts, fill=C["gold"] + (255,))
    p(im, 15, 9, "wht"); p(im, 15, 10, "wht")
    return outline(im)

# ---------- characters: front / back, with walk frames ----------
CHARACTERS = {
    "protagonist": dict(shirt="blu", shirt_d="blud", pants="soy", apron="red",
                        front=lambda im: (r(im, 13, 18, 6, 2, "gold"), r(im, 14, 15, 1, 4, "wht"), r(im, 17, 15, 1, 4, "wht"))),
    "butcher": dict(shirt="gry", shirt_d="gryd", pants="blud", apron="wht",
                    hat=lambda im, back: (r(im, 10, 1, 12, 5, "wht"), r(im, 10, 5, 12, 1, "gry")),
                    front=lambda im: (p(im, 14, 20, "meatd"), p(im, 17, 22, "meatd"), p(im, 15, 23, "meat"))),
    "auntie": dict(shirt="pur", shirt_d="purd", pants="soy", hair=(70, 60, 70),
                   hat=lambda im, back: ([p(im, x, 2, (70, 60, 70)) for x in range(10, 22, 2)], r(im, 9, 4, 1, 6, (70, 60, 70)), r(im, 22, 4, 1, 6, (70, 60, 70))),
                   any=lambda im, back: (r(im, 23, 20, 6, 7, "red"), r(im, 24, 18, 4, 2, "red"), r(im, 24, 22, 4, 1, "yel"))),
    "student": dict(shirt="grn", shirt_d="grnd", pants="blu",
                    front=lambda im: (r(im, 12, 10, 3, 2, "gryd"), r(im, 17, 10, 3, 2, "gryd"), r(im, 15, 10, 2, 1, "gryd"),
                                      p(im, 13, 11, "skin"), p(im, 18, 11, "skin"), r(im, 11, 15, 1, 10, "redd"), r(im, 20, 15, 1, 10, "redd")),
                    back=lambda im: (r(im, 11, 16, 10, 9, "redd"), r(im, 12, 17, 8, 2, "red"), r(im, 13, 21, 6, 2, "redl"))),
    "family": dict(shirt=(84, 146, 150), shirt_d=(56, 104, 112), pants=(120, 100, 80), hair="hair",
                   front=lambda im: (r(im, 14, 15, 4, 2, "wht"),),
                   any=lambda im, back: (r(im, 2, 20, 7, 6, "red"), r(im, 3, 17, 5, 1, "gryd"), r(im, 3, 17, 1, 3, "gryd"), r(im, 7, 17, 1, 3, "gryd"),
                                         r(im, 3, 19, 2, 2, "grnl"), r(im, 5, 18, 2, 2, (240, 146, 48)))),
    "dad": dict(shirt=(140, 104, 72), shirt_d=(104, 76, 54), pants="gryd", hair=(176, 172, 168),
                front=lambda im: (r(im, 15, 15, 2, 10, (104, 76, 54)), p(im, 16, 18, "gold"), p(im, 16, 21, "gold"),
                                  r(im, 12, 9, 8, 1, "skins"))),
    "mom": dict(shirt="redl", shirt_d="red", pants="soy", apron=(246, 214, 160),
                hat=lambda im, back: (r(im, 13, 0, 6, 4, "hair"), p(im, 15, 1, "gold")),
                front=lambda im: (r(im, 13, 18, 6, 1, "redl"),)),
}

def person(cfg, back=False, step=0):
    im = new()
    hair = cfg.get("hair", "hair")
    shirt, shirt_d, pants = cfg["shirt"], cfg["shirt_d"], cfg["pants"]
    # legs: step 1 lifts left leg, step 2 lifts right leg
    for i, lx in enumerate((12, 17)):
        lifted = (step == 1 and i == 0) or (step == 2 and i == 1)
        h = 3 if lifted else 4
        r(im, lx, 25, 3, h, pants)
        r(im, lx - (1 if i == 0 else 0), 25 + h, 4, 1, "soy")
    # body
    r(im, 10, 15, 12, 11, shirt); r(im, 20, 15, 2, 11, shirt_d)
    # arms swing opposite to legs
    la = {0: 16, 1: 17, 2: 15}[step]; ra = {0: 16, 1: 15, 2: 17}[step]
    r(im, 8, la, 2, 8, shirt); r(im, 22, ra, 2, 8, shirt_d)
    r(im, 8, la + 8, 2, 2, "skin"); r(im, 22, ra + 8, 2, 2, "skins")
    apron = cfg.get("apron")
    if back:
        if apron:
            r(im, 10, 20, 12, 1, apron); r(im, 15, 21, 2, 2, apron)
        r(im, 11, 5, 10, 10, hair); r(im, 10, 3, 12, 4, hair)
        r(im, 13, 14, 6, 1, "skins")
        if cfg.get("back"):
            cfg["back"](im)
    else:
        if apron:
            r(im, 12, 16, 8, 10, apron); r(im, 13, 15, 6, 1, apron)
        r(im, 11, 5, 10, 10, "skin"); r(im, 19, 6, 2, 9, "skins")
        r(im, 13, 10, 1, 2, "soy"); r(im, 18, 10, 1, 2, "soy")
        p(im, 12, 12, "redl"); p(im, 19, 12, "redl"); r(im, 15, 13, 2, 1, "skins")
        r(im, 10, 3, 12, 4, hair); r(im, 10, 7, 1, 4, hair); r(im, 21, 7, 1, 4, hair)
        if cfg.get("front"):
            cfg["front"](im)
    if cfg.get("hat"):
        cfg["hat"](im, back)
    if cfg.get("any"):
        cfg["any"](im, back)
    return shadow(outline(im))

def character_sprites():
    out = {}
    for name, cfg in CHARACTERS.items():
        out[f"char_{name}"] = (lambda c: lambda: person(c))(cfg)    # keeps M7 file names
        for view, back in (("front", False), ("back", True)):
            for step in (0, 1, 2):
                out[f"char_{name}_{view}_{step}"] = (lambda c, b, s: lambda: person(c, b, s))(cfg, back, step)
    return out

SPRITES = {
    # batch 1
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
    # batch 2: fixtures + deco
    "produce_stand_empty": lambda: produce_stand(False), "produce_stand_full": lambda: produce_stand(True, 0),
    "produce_stand_full_b": lambda: produce_stand(True, 2), "freezer": freezer,
    "deco_fu_poster": fu_poster, "deco_plant": plant, "deco_basket": basket, "deco_mat": mat,
    # batch 2: products
    "product_sesame_oil": lambda: bottle2((214, 150, 52), "red", "redd"),
    "product_oyster_sauce": lambda: bottle2("soy", "gold", "blu", wide=True),
    "product_vermicelli": vermicelli, "product_dumplings_frozen": dumplings_frozen,
    "product_tofu": tofu, "product_red_dates": red_dates,
    # batch 2: produce
    "product_napa_cabbage": napa, "product_bok_choy": bok_choy, "product_scallion": scallion,
    "product_ginger": ginger, "product_orange": orange, "product_lychee": lychee, "product_daikon": daikon,
    # batch 2: UI
    "ui_coin": icon_coin, "ui_clock": icon_clock, "ui_star": icon_star,
    "ui_bubble_question": lambda: bubble(b_question), "ui_bubble_frown": lambda: bubble(b_frown),
    "ui_bubble_angry": lambda: bubble(b_angry), "ui_bubble_heart": lambda: bubble(b_heart),
}
SPRITES.update(character_sprites())

def sheet(imgs, names, cols, path, scale):
    rows = (len(names) + cols - 1) // cols
    sh = Image.new("RGBA", (cols * 32, rows * 32), (0, 0, 0, 0))
    for i, n in enumerate(names):
        sh.paste(imgs[n], ((i % cols) * 32, (i // cols) * 32), imgs[n])
    sh.save(path)
    if scale:
        sh.resize((sh.width * scale, sh.height * scale), Image.NEAREST).save(
            path.with_name(path.stem + f"_{scale}x.png"))

def main():
    imgs = {name: fn() for name, fn in SPRITES.items()}
    for f in OUT_DIR.glob("*.png"):
        f.unlink()
    for name, im in imgs.items():
        im.save(OUT_DIR / f"{name}.png")

    base = ART_DIR
    static = [n for n in imgs if not n.startswith("char_")]
    sheet(imgs, static, 10, base / "sheet_static.png", 4)
    # character sheet: one row per character, front 0-2 then back 0-2
    chars = []
    for name in CHARACTERS:
        chars += [f"char_{name}_{v}_{s}" for v in ("front", "back") for s in (0, 1, 2)]
    sheet(imgs, chars, 6, base / "sheet_characters.png", 4)

    # mock store scene, 14 x 9 tiles
    W, H = 14, 9
    scene = Image.new("RGBA", (W * 32, H * 32), C["fl"] + (255,))
    def put(n, x, y, dx=0, dy=0):
        im = imgs[n]
        scene.paste(im, (x * 32 + dx, y * 32 + dy), im)
    for x in range(W):
        put("tile_wall", x, 0)
        for y in range(1, H):
            put("tile_floor", x, y)
    put("door", 12, 0); put("deco_mat", 12, 1)
    put("deco_lantern", 2, 0); put("deco_lantern", 9, 0); put("deco_fu_poster", 5, 0, dy=-2)
    for x in range(1, 5):
        put("shelf_full" if x % 2 else "shelf_full_b", x, 2)
        put("shelf_full_b" if x % 2 else "shelf_full", x, 5)
    put("produce_stand_full", 1, 7); put("produce_stand_full_b", 2, 7); put("produce_stand_full", 3, 7, )
    put("freezer", 6, 2); put("freezer", 6, 5)
    put("meat_fridge", 8, 2); put("meat_fridge", 9, 2); put("cutting_table", 11, 2)
    put("counter", 9, 7); put("counter_plain", 10, 7)
    scene.paste(imgs["deco_lucky_cat"], (10 * 32 + 4, 7 * 32 - 14), imgs["deco_lucky_cat"])
    put("deco_plant", 13, 1); put("deco_plant", 0, 1); put("deco_basket", 11, 1, dx=-6, dy=6)
    put("char_protagonist_front_1", 3, 3); put("char_butcher_front_0", 11, 3)
    put("char_auntie_back_2", 5, 4); put("char_student_front_0", 9, 8)
    put("char_family_front_1", 4, 6, dx=10); put("char_mom_front_0", 7, 4, dx=6)
    put("ui_bubble_heart", 4, 5, dx=14, dy=6)
    put("ui_bubble_question", 5, 3, dy=8)
    scene.resize((scene.width * 3, scene.height * 3), Image.NEAREST).save(base / "preview_scene_3x.png")
    print(f"{len(imgs)} sprites written ({len(static)} static, {len(chars)} character frames)")

if __name__ == "__main__":
    main()
