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
    sk, sks = cfg.get("skin", "skin"), cfg.get("skins", "skins")
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
    r(im, 8, la + 8, 2, 2, sk); r(im, 22, ra + 8, 2, 2, sks)
    apron = cfg.get("apron")
    if back:
        if apron:
            r(im, 10, 20, 12, 1, apron); r(im, 15, 21, 2, 2, apron)
        r(im, 11, 5, 10, 10, hair); r(im, 10, 3, 12, 4, hair)
        r(im, 13, 14, 6, 1, sks)
        if cfg.get("back"):
            cfg["back"](im)
    else:
        if apron:
            r(im, 12, 16, 8, 10, apron); r(im, 13, 15, 6, 1, apron)
        r(im, 11, 5, 10, 10, sk); r(im, 19, 6, 2, 9, sks)
        r(im, 13, 10, 1, 2, "soy"); r(im, 18, 10, 1, 2, "soy")
        p(im, 12, 12, "redl"); p(im, 19, 12, "redl"); r(im, 15, 13, 2, 1, sks)
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

# ---------- batch 3: seafood ----------
WATER, WATERD, MURK, MURKD = (96, 170, 210), (64, 130, 180), (120, 150, 100), (88, 116, 76)

def fish_shape(im, x, y, body, belly, d=1):
    """small side-view fish, 9x5, facing right if d=1"""
    for i in range(7):
        h = (1, 3, 5, 5, 5, 3, 1)[i]
        xx = x + (i if d == 1 else 8 - i)
        r(im, xx, y + (5 - h) // 2, 1, h, body)
    r(im, x + (2 if d == 1 else 3), y + 3, 4, 1, belly)
    tail = x - 1 if d == 1 else x + 9
    r(im, tail, y, 1, 5, body); r(im, tail + (-1 if d == 1 else 1), y + 1, 1, 3, body)
    p(im, x + (5 if d == 1 else 3), y + 1, "soy")

def fish_tank(state="clean"):
    im = new()
    water, waterd = (WATER, WATERD) if state != "murky" else (MURK, MURKD)
    r(im, 1, 4, 30, 22, water); r(im, 1, 4, 30, 2, waterd)
    for x in range(2, 30, 4):
        p(im, x, 6, "wht")
    if state != "empty":
        fish_shape(im, 5, 10, "gry", "wht", 1); fish_shape(im, 18, 14, (110, 140, 120), "wht", -1)
        fish_shape(im, 9, 19, "gry", "wht", 1)
        if state == "murky":
            fish_shape(im, 20, 22, (150, 150, 140), "fs", 1)   # sickly fish near bottom
    for x, y in ((26, 9), (27, 12), (25, 16)):
        p(im, x, y, "wht")
    if state == "murky":
        for x, y in ((6, 8), (14, 12), (22, 9), (11, 23), (27, 20)):
            p(im, x, y, MURKD)
    r(im, 1, 4, 1, 22, "glass"); r(im, 30, 4, 1, 22, "glass")
    r(im, 1, 26, 30, 5, "gryd"); r(im, 1, 26, 30, 1, "gry"); r(im, 24, 28, 4, 1, "grnl")
    r(im, 1, 2, 30, 2, "gry")
    return outline(im)

def ice_table(full=True):
    im = new()
    r(im, 1, 6, 30, 12, "wht")
    for x, y in ((4, 8), (9, 11), (15, 7), (21, 12), (26, 9), (6, 15), (18, 15), (12, 14)):
        r(im, x, y, 2, 1, "glass")
    if full:
        fish_shape(im, 4, 8, (170, 176, 176), "wht", 1)
        fish_shape(im, 17, 10, (170, 176, 176), "wht", -1)
        for x in (5, 9, 13):
            r(im, x, 14, 3, 2, (226, 210, 170)); p(im, x, 14, "wht")
        r(im, 21, 4, 6, 3, "wht"); p(im, 22, 5, "red")    # price card
    r(im, 1, 18, 30, 12, "gry"); r(im, 1, 18, 30, 1, "wht"); r(im, 1, 29, 30, 1, "gryd")
    r(im, 4, 21, 24, 1, "gryd")
    return outline(im)

def seafood_counter():
    im = new()
    r(im, 1, 10, 30, 6, "gry"); r(im, 1, 15, 30, 1, "gryd")
    r(im, 1, 16, 30, 14, "blud"); r(im, 1, 29, 30, 1, "out")
    for x in (6, 16, 26):
        r(im, x, 18, 1, 10, "blu")
    r(im, 1, 21, 30, 2, "blul")
    # scale
    r(im, 18, 6, 10, 2, "gry"); r(im, 21, 8, 4, 4, "gryd"); r(im, 21, 9, 4, 2, "grnl")
    # chopping board + knife
    r(im, 3, 7, 11, 5, "wl"); r(im, 5, 6, 6, 2, "gry"); r(im, 11, 6, 3, 2, "wd")
    return outline(im)

def live_fish():
    im = new()
    body, belly = (110, 140, 130), (220, 226, 214)
    for i in range(18):
        h = (2, 4, 6, 8, 10, 11, 12, 12, 12, 12, 11, 10, 9, 8, 6, 5, 4, 3)[i]
        r(im, 7 + i, 16 - h // 2, 1, h, body)
    r(im, 10, 17, 13, 3, belly)
    r(im, 4, 10, 3, 12, body); r(im, 3, 8, 2, 4, body); r(im, 3, 20, 2, 4, body)    # tail
    r(im, 12, 9, 6, 2, (80, 110, 100))                                               # fin
    r(im, 20, 13, 2, 2, "wht"); p(im, 21, 13, "soy")
    for x in (11, 14, 17):
        p(im, x, 14, (150, 180, 170))
    for x, y in ((27, 9), (28, 6), (26, 4)):
        p(im, x, y, "glass")
    return shadow(outline(im), rx=10)

def chilled_fish():
    im = new()
    r(im, 3, 18, 26, 7, "wht")
    for x in range(4, 28, 3):
        p(im, x, 19, "glass")
    body = (176, 182, 184)
    for i in range(18):
        h = (2, 3, 4, 6, 7, 8, 8, 8, 8, 8, 7, 7, 6, 5, 4, 4, 3, 2)[i]
        r(im, 7 + i, 15 - h // 2, 1, h, body)
    r(im, 4, 11, 3, 8, body); r(im, 10, 15, 13, 2, "wht")
    p(im, 21, 13, "gryd")
    return shadow(outline(im), rx=12)

def shrimp():
    im = new()
    for cx, cy, d in ((12, 12, 1), (19, 19, -1)):
        col, cold = (210, 170, 160), (170, 120, 112)
        for i in range(8):
            r(im, cx + d * i - (0 if d == 1 else 1), cy + (i // 3), 2, 4 - i // 3, col)
        r(im, cx + d * 8, cy + 2, 2, 3, cold)              # tail
        p(im, cx - d, cy, "soy")
        r(im, cx - d * 4, cy - 2, 3, 1, cold); r(im, cx - d * 5, cy - 3, 2, 1, cold)   # antennae
        for i in range(0, 7, 2):
            p(im, cx + d * i, cy + 3, cold)
    return shadow(outline(im), rx=10)

def crab():
    im = new()
    shell, shelld = (84, 112, 120), (56, 80, 90)
    r(im, 9, 12, 14, 9, shell); r(im, 11, 11, 10, 1, shell); r(im, 9, 19, 14, 2, shelld)
    r(im, 12, 9, 1, 3, "soy"); r(im, 19, 9, 1, 3, "soy")                          # eye stalks
    r(im, 4, 7, 5, 5, shell); r(im, 23, 7, 5, 5, shell); r(im, 4, 7, 2, 2, shelld); r(im, 26, 7, 2, 2, shelld)
    r(im, 7, 11, 2, 3, shell); r(im, 23, 11, 2, 3, shell)
    for i, y in enumerate((16, 19, 22)):
        r(im, 5 + i, y, 4, 1, shelld); r(im, 23 - i, y, 4, 1, shelld)
    r(im, 4, 7, 5, 1, "gold"); r(im, 23, 7, 5, 1, "gold")                         # straw ties
    p(im, 13, 14, "wht")
    return shadow(outline(im), rx=10)

def lobster():
    im = new()
    sh, shd = (120, 56, 52), (84, 36, 40)
    r(im, 13, 9, 6, 8, sh)                                   # head/thorax
    for i, y in enumerate(range(17, 26)):
        r(im, 14 - (1 if i % 3 == 0 else 0), y, 4 + (2 if i % 3 == 0 else 0), 1, sh if i % 2 else shd)
    r(im, 11, 26, 10, 2, shd); r(im, 13, 28, 6, 1, sh)       # tail fan
    r(im, 7, 4, 5, 6, sh); r(im, 20, 4, 5, 6, sh)            # claws
    r(im, 10, 9, 3, 2, sh); r(im, 19, 9, 3, 2, sh)
    r(im, 7, 6, 5, 1, "yel"); r(im, 20, 6, 5, 1, "yel")       # rubber bands
    r(im, 14, 3, 1, 6, shd); r(im, 17, 3, 1, 6, shd); r(im, 13, 1, 1, 2, shd); r(im, 18, 1, 1, 2, shd)
    p(im, 14, 10, "soy"); p(im, 17, 10, "soy"); p(im, 15, 12, "redl")
    for y in (12, 14, 16):
        p(im, 12, y, shd); p(im, 19, y, shd)
    return shadow(outline(im), rx=8)

def clams():
    im = new()
    for cx, cy in ((10, 18), (16, 16), (22, 18), (13, 22), (19, 22), (16, 11)):
        shell = (214, 196, 160) if (cx + cy) % 2 else (190, 170, 140)
        r(im, cx - 3, cy - 2, 6, 4, shell); r(im, cx - 2, cy - 3, 4, 1, shell)
        r(im, cx - 3, cy + 1, 6, 1, (140, 120, 100)); p(im, cx - 1, cy - 2, "wht")
        p(im, cx + 1, cy - 1, (150, 130, 110))
    return shadow(outline(im), rx=9)

def icon_water_quality(good=True):
    im = new()
    col = WATER if good else MURK
    for y in range(6, 27):
        if y < 14:
            half = (y - 6) // 2 + 1
        else:
            half = int((49 - (y - 20) ** 2) ** 0.5) if (y - 20) ** 2 < 49 else 0
        r(im, 16 - half, y, half * 2, 1, col)
    p(im, 13, 17, "wht"); p(im, 13, 18, "wht"); p(im, 14, 16, "wht")
    if not good:
        p(im, 17, 20, MURKD); p(im, 15, 23, MURKD); p(im, 19, 17, MURKD)
    return outline(im)

CHARACTERS["fishmonger"] = dict(
    shirt="wht", shirt_d="gry", pants="blud", apron=(60, 90, 130),
    hat=lambda im, back: (r(im, 10, 2, 12, 3, "blu"), r(im, 10, 4, 12, 1, "blud")),
    front=lambda im: (p(im, 14, 21, WATERD), p(im, 17, 19, WATERD)),
    any=lambda im, back: (r(im, 11, 28, 4, 2, "grnd"), r(im, 17, 28, 4, 2, "grnd")),   # rubber boots
)

SPRITES.update({
    "fish_tank": lambda: fish_tank("clean"), "fish_tank_murky": lambda: fish_tank("murky"),
    "fish_tank_empty": lambda: fish_tank("empty"),
    "ice_table": lambda: ice_table(True), "ice_table_empty": lambda: ice_table(False),
    "seafood_counter": seafood_counter,
    "product_live_fish": live_fish, "product_chilled_fish": chilled_fish,
    "product_live_shrimp": shrimp, "product_crab": crab, "product_lobster": lobster,
    "product_clams": clams,
    "ui_water_good": lambda: icon_water_quality(True), "ui_water_bad": lambda: icon_water_quality(False),
})
SPRITES.update(character_sprites())


# ---------- batch 4: deli ----------
DUCK, DUCKD, DUCKL = (176, 92, 44), (124, 58, 30), (220, 140, 70)
CHARSIU, CHARSIUD = (196, 60, 52), (150, 40, 40)
BRAISE, BRAISED = (150, 64, 40), (104, 40, 30)

def steam_table(full=True):
    im = new()
    r(im, 1, 3, 30, 2, "glass"); r(im, 1, 5, 30, 1, "glassd")          # sneeze guard
    r(im, 1, 8, 30, 10, "gry"); r(im, 1, 8, 30, 1, "wht")
    foods = [(BRAISE, BRAISED), ("grn", "grnl"), ((236, 120, 60), (196, 80, 40)), ("wht", "fs")]
    for i, x in enumerate((2, 9, 16, 23)):
        r(im, x, 10, 6, 6, "gryd")
        if full:
            a, b = foods[i]
            r(im, x + 1, 11, 4, 4, a)
            p(im, x + 1, 11, b); p(im, x + 3, 13, b); p(im, x + 2, 12, "wht")
    if full:
        for x in (5, 19):
            p(im, x, 7, "wht"); p(im, x + 1, 6, "wht")                     # steam
    r(im, 1, 18, 30, 12, "gry"); r(im, 1, 18, 30, 1, "wht"); r(im, 1, 29, 30, 1, "gryd")
    r(im, 1, 22, 30, 2, "red"); r(im, 13, 25, 6, 2, "gold")
    return outline(im)

def hanging_duck(im, x, y):
    r(im, x + 2, y, 1, 3, "gryd")                                         # hook
    r(im, x + 1, y + 3, 3, 2, DUCK)
    r(im, x, y + 5, 5, 8, DUCK); r(im, x + 1, y + 13, 3, 2, DUCK)
    r(im, x, y + 5, 1, 7, DUCKL); r(im, x + 4, y + 6, 1, 7, DUCKD)
    p(im, x + 2, y + 7, "gold")

def hanging_charsiu(im, x, y):
    r(im, x + 1, y, 1, 3, "gryd")
    r(im, x, y + 3, 3, 10, CHARSIU); r(im, x + 2, y + 3, 1, 10, CHARSIUD); p(im, x, y + 4, "redl")

def roast_station(full=True):
    im = new()
    r(im, 1, 1, 30, 2, "red"); r(im, 1, 1, 30, 1, "gold")
    r(im, 1, 3, 30, 16, "glass"); r(im, 1, 3, 1, 16, "red"); r(im, 30, 3, 1, 16, "red")
    r(im, 2, 4, 28, 1, "gryd")                                            # rail
    if full:
        hanging_duck(im, 3, 4); hanging_duck(im, 10, 4); hanging_charsiu(im, 18, 4)
        hanging_charsiu(im, 22, 4); hanging_charsiu(im, 26, 4)
    else:
        for x in (5, 12, 19, 23, 27):
            r(im, x, 5, 1, 3, "gryd")
    r(im, 1, 19, 30, 3, "wl"); r(im, 4, 19, 8, 2, (176, 120, 70))         # chopping board
    r(im, 1, 22, 30, 8, "red"); r(im, 1, 22, 30, 1, "redl"); r(im, 1, 29, 30, 1, "redd")
    r(im, 12, 24, 8, 4, "gold"); r(im, 13, 25, 6, 2, "redd")
    return outline(im)

def wok_stove():
    im = new()
    r(im, 1, 10, 30, 6, "gryd"); r(im, 1, 10, 30, 1, "gry")
    for cx in (9, 23):
        r(im, cx - 6, 8, 12, 4, "soy"); r(im, cx - 5, 7, 10, 1, "soy"); r(im, cx - 4, 8, 8, 2, (70, 60, 56))
        r(im, cx + 6, 9, 4, 1, "wd")                                      # handle
    r(im, 4, 13, 10, 2, (240, 146, 48)); r(im, 6, 12, 6, 1, "yel")        # flame
    for x, y in ((7, 4), (9, 2), (11, 4), (21, 5), (23, 3)):
        p(im, x, y, "wht")
    r(im, 1, 16, 30, 14, "gry"); r(im, 1, 16, 30, 1, "wht"); r(im, 1, 29, 30, 1, "gryd")
    for x in (5, 13, 21):
        r(im, x, 20, 6, 6, "gryd"); r(im, x + 2, 22, 2, 2, "gry")
    return outline(im)

def plate(food):
    im = new()
    r(im, 4, 13, 24, 12, "wht"); r(im, 6, 12, 20, 1, "wht"); r(im, 6, 25, 20, 1, "wht")
    r(im, 4, 23, 24, 2, "fs")
    food(im)
    return shadow(outline(im), rx=12)

def d_braised(im):
    for x, y in ((8, 15), (13, 14), (18, 15), (10, 19), (16, 19), (21, 19)):
        r(im, x, y, 4, 4, BRAISE); r(im, x, y, 4, 1, "fat"); p(im, x + 3, y + 3, BRAISED); p(im, x + 1, y + 1, "redl")

def d_ribs(im):
    for x, y in ((8, 15), (14, 14), (20, 16), (11, 19), (17, 19)):
        r(im, x, y, 5, 4, (232, 110, 56)); r(im, x + 2, y - 1, 1, 6, "wht"); p(im, x, y, "yel")
    for x, y in ((10, 18), (19, 21)):
        p(im, x, y, "wht")

def d_dumplings(im):
    body, edge = (238, 222, 188), (168, 140, 104)
    for x, y in ((6, 15), (15, 14), (9, 19), (18, 19)):
        r(im, x, y, 9, 5, edge)
        r(im, x + 1, y + 1, 7, 3, body); r(im, x + 2, y, 5, 1, body)
        for i in range(x + 2, x + 8, 2):
            p(im, i, y, edge)
        p(im, x + 2, y + 1, "wht")

def d_bokchoy(im):
    for x in (7, 12, 17, 22):
        r(im, x, 15, 4, 5, "grn"); r(im, x + 1, 20, 2, 3, (220, 236, 196)); p(im, x, 15, "grnl")
    r(im, 6, 22, 20, 1, (200, 170, 100))

def d_fish(im):
    body = (220, 214, 196)
    for i in range(16):
        h = (2, 3, 4, 5, 6, 6, 6, 6, 6, 6, 5, 5, 4, 3, 3, 2)[i]
        r(im, 8 + i, 18 - h // 2, 1, h, body)
    r(im, 5, 15, 3, 6, body)
    r(im, 6, 21, 20, 2, (150, 90, 50))                                    # soy sauce
    for x in range(10, 22, 3):
        p(im, x, 16, "grn"); p(im, x + 1, 17, "yel")
    p(im, 21, 17, "soy")

def d_shrimp(im):
    for x, y in ((7, 15), (14, 14), (20, 16), (10, 19), (17, 19)):
        r(im, x, y, 5, 3, (238, 126, 70)); r(im, x + 4, y + 1, 2, 2, (200, 90, 50))
        p(im, x + 1, y, "soy"); p(im, x + 3, y + 2, "soy")
    for x, y in ((12, 18), (22, 20), (9, 22)):
        p(im, x, y, "grn")

def d_duck(im):
    for x in range(7, 25, 4):
        r(im, x, 15, 3, 7, DUCK); r(im, x, 15, 3, 1, DUCKL); r(im, x, 21, 3, 1, (240, 220, 200))
    r(im, 6, 22, 20, 1, DUCKD)

def d_charsiu(im):
    for x in range(7, 25, 4):
        r(im, x, 15, 3, 7, (236, 200, 180)); r(im, x, 15, 3, 1, CHARSIU); r(im, x + 2, 15, 1, 7, CHARSIU)
    r(im, 6, 22, 20, 1, (180, 60, 40))

def whole_duck_raw():
    im = new()
    skin, skind = (240, 214, 186), (210, 176, 146)
    r(im, 8, 12, 16, 12, skin); r(im, 10, 10, 12, 2, skin); r(im, 10, 24, 12, 2, skin)
    r(im, 23, 9, 3, 6, skin); r(im, 25, 7, 3, 3, skin); r(im, 27, 8, 2, 1, (240, 160, 60))   # neck/head/bill
    r(im, 8, 21, 16, 3, skind); r(im, 6, 16, 2, 5, skin)
    p(im, 26, 8, "soy")
    return shadow(outline(im), rx=11)

def pork_shoulder():
    im = new()
    r(im, 6, 10, 20, 14, "meat"); r(im, 8, 8, 16, 2, "meat")
    for x, y in ((9, 13), (14, 16), (19, 12), (11, 19), (20, 18), (16, 11)):
        r(im, x, y, 3, 1, "fat")
    r(im, 6, 21, 20, 3, "meatd")
    return shadow(outline(im), rx=11)

def takeaway_box():
    im = new()
    r(im, 8, 12, 16, 15, "wht"); r(im, 6, 10, 20, 3, "wht"); r(im, 6, 10, 20, 1, "fs")
    r(im, 12, 6, 8, 1, "red"); r(im, 11, 7, 1, 4, "red"); r(im, 20, 7, 1, 4, "red")     # handle
    r(im, 12, 17, 8, 5, "red"); r(im, 14, 18, 4, 3, "gold")
    return shadow(outline(im), rx=8)

CHARACTERS["chef"] = dict(
    shirt="wht", shirt_d="gry", pants=(70, 70, 76),
    hat=lambda im, back: (r(im, 11, 0, 10, 5, "wht"), r(im, 10, 1, 12, 2, "wht"), r(im, 10, 4, 12, 1, "gry")),
    front=lambda im: (p(im, 13, 18, "gryd"), p(im, 13, 21, "gryd"), p(im, 18, 18, "gryd"), p(im, 18, 21, "gryd"),
                      r(im, 13, 15, 6, 1, "red")),                           # buttons + red neckerchief
)

SPRITES.update({
    "steam_table": lambda: steam_table(True), "steam_table_empty": lambda: steam_table(False),
    "roast_station": lambda: roast_station(True), "roast_station_empty": lambda: roast_station(False),
    "wok_stove": wok_stove,
    "dish_braised_pork": lambda: plate(d_braised), "dish_sweet_sour_ribs": lambda: plate(d_ribs),
    "dish_dumplings": lambda: plate(d_dumplings), "dish_stir_fried_bok_choy": lambda: plate(d_bokchoy),
    "dish_steamed_fish": lambda: plate(d_fish), "dish_salt_pepper_shrimp": lambda: plate(d_shrimp),
    "dish_roast_duck": lambda: plate(d_duck), "dish_char_siu": lambda: plate(d_charsiu),
    "product_whole_duck": whole_duck_raw, "product_pork_shoulder": pork_shoulder,
    "product_takeaway_box": takeaway_box,
})
SPRITES.update(character_sprites())


# ---------- batch 5: generic item helpers ----------
def jar(lid, content, label, label2="gold"):
    im = new()
    r(im, 10, 6, 12, 4, lid); r(im, 10, 6, 12, 1, "wht")
    r(im, 9, 10, 14, 17, "glass"); r(im, 10, 12, 12, 14, content)
    r(im, 9, 16, 14, 6, label); r(im, 12, 17, 8, 4, label2)
    r(im, 10, 11, 1, 14, "wht")
    return shadow(outline(im), rx=7)

def can(color, band, mark="wht"):
    im = new()
    r(im, 11, 4, 10, 2, "gry"); r(im, 10, 6, 12, 22, color)
    r(im, 10, 6, 12, 1, "gry"); r(im, 10, 27, 12, 1, "gry")
    r(im, 10, 13, 12, 7, band); r(im, 14, 14, 4, 5, mark)
    r(im, 11, 7, 1, 19, "wht")
    return shadow(outline(im), rx=6)

def pack(color, accent, window=None, crinkle=True):
    im = new()
    r(im, 7, 7, 18, 20, color)
    if crinkle:
        r(im, 7, 5, 18, 2, accent); r(im, 7, 27, 18, 2, accent)
        for x in range(7, 25, 2):
            p(im, x, 4, accent); p(im, x, 29, accent)
    r(im, 10, 11, 12, 10, accent)
    if window:
        window(im)
    r(im, 8, 8, 1, 17, "wht")
    return shadow(outline(im), rx=8)

def boxy(color, accent, mark="gold", tall=False):
    im = new()
    top = 5 if tall else 10
    r(im, 7, top, 18, 28 - top, color); r(im, 7, top, 18, 2, accent)
    r(im, 11, top + 6, 10, 8, accent); r(im, 13, top + 8, 6, 4, mark)
    r(im, 8, top + 2, 1, 24 - top, "wht")
    return shadow(outline(im), rx=9)

def ball_cluster(points, col, cold, rad=3, stem=None):
    im = new()
    for cx, cy in points:
        for y in range(cy - rad, cy + rad + 1):
            for x in range(cx - rad, cx + rad + 1):
                if (x - cx) ** 2 + (y - cy) ** 2 <= rad * rad + 1:
                    p(im, x, y, col)
        p(im, cx + 1, cy + 1, cold); p(im, cx - 1, cy - 1, "wht")
    if stem:
        stem(im)
    return shadow(outline(im), rx=9)

# ---------- batch 5: more dry goods ----------
def seaweed():
    def w(im):
        r(im, 10, 11, 12, 10, (40, 60, 44)); r(im, 12, 13, 8, 6, (60, 88, 60))
    return pack((230, 210, 150), "red", w, crinkle=False)

def rice_crackers():
    def w(im):
        for x, y in ((12, 13), (17, 14), (14, 17)):
            r(im, x, y, 4, 3, (236, 200, 130)); p(im, x + 1, y + 1, "wd")
    return pack("redl", "wht", w)

def shiitake():
    def w(im):
        for x, y in ((11, 13), (17, 14), (13, 17)):
            r(im, x, y, 5, 3, (120, 80, 52)); r(im, x + 2, y + 3, 1, 2, (220, 196, 160)); p(im, x + 1, y, (170, 120, 80))
    return pack("glass", (200, 170, 120), w, crinkle=False)

def noodle_5pack():
    im = new()
    r(im, 4, 9, 24, 17, "red"); r(im, 4, 9, 24, 2, "redd")
    for x in (7, 15, 23):
        r(im, x - 2, 13, 5, 4, "yel")
    r(im, 6, 19, 20, 4, "wht"); r(im, 9, 20, 14, 2, "gold")
    r(im, 5, 11, 1, 13, "redl")
    return shadow(outline(im), rx=12)

def soy_milk():
    im = new()
    r(im, 11, 9, 10, 19, "wht"); r(im, 12, 5, 8, 4, "wht"); r(im, 14, 3, 4, 2, "fs")
    r(im, 11, 15, 10, 8, "grn"); r(im, 13, 16, 6, 5, (240, 220, 160))
    r(im, 12, 10, 1, 16, "glass")
    return shadow(outline(im), rx=6)

# ---------- batch 5: asian produce ----------
def leafy(col, cold, stemcol, fluffy=False, long=False):
    im = new()
    if long:
        for x in (10, 14, 18):
            r(im, x, 3, 4, 20, col); r(im, x + 1, 3, 1, 20, cold); p(im, x, 3, "grnl")
        r(im, 11, 23, 10, 5, stemcol)
    elif fluffy:
        for x, y in ((9, 6), (14, 4), (19, 6), (11, 10), (17, 10), (14, 13)):
            r(im, x, y, 5, 5, col); p(im, x, y, "grnl"); p(im, x + 2, y + 3, cold)
        for x in (12, 15, 18):
            r(im, x, 17, 1, 10, stemcol)
        r(im, 11, 22, 9, 2, "red")
    return shadow(outline(im), rx=6)

def bitter_melon():
    im = new()
    for y in range(4, 28):
        w = 4 if y in (4, 27) else (6 if y in (5, 26) else 8)
        r(im, 16 - w // 2, y, w, 1, "grn")
        if y % 2 == 0:
            p(im, 13, y, "grnl"); p(im, 17, y, "grnl")
        else:
            p(im, 15, y, "grnd")
    r(im, 15, 2, 2, 2, "grnd")
    return shadow(outline(im), rx=5)

def winter_melon():
    im = new()
    for y in range(8, 26):
        half = int((90 - (y - 17) ** 2 * 1.1) ** 0.5) if (y - 17) ** 2 * 1.1 < 90 else 0
        r(im, 16 - half, y, half * 2, 1, (240, 244, 226))
    r(im, 7, 8, 18, 3, "grnd"); r(im, 7, 11, 18, 1, "grn")
    for x, y in ((12, 16), (16, 18), (20, 15), (14, 21), (18, 22)):
        p(im, x, y, (210, 200, 150))
    return shadow(outline(im), rx=9)

def chives():
    im = new()
    for i, x in enumerate(range(9, 23, 2)):
        r(im, x, 3 + (i % 2), 1, 22, "grn"); p(im, x, 3 + (i % 2), "grnl")
    r(im, 9, 20, 14, 2, "red"); r(im, 9, 25, 14, 2, "wht")
    return shadow(outline(im), rx=6)

def gai_lan():
    im = new()
    for x, y in ((8, 5), (17, 4), (12, 9)):
        r(im, x, y, 7, 6, (54, 110, 80)); p(im, x + 1, y + 1, "grnl")
    p(im, 13, 4, "yel"); p(im, 14, 3, "yel")
    for x in (11, 15, 19):
        r(im, x, 14, 2, 13, (130, 176, 110))
    r(im, 10, 22, 12, 2, "red")
    return shadow(outline(im), rx=6)

def mango():
    im = new()
    for y in range(8, 26):
        half = int((70 - (y - 17) ** 2) ** 0.5) if (y - 17) ** 2 < 70 else 0
        off = (y - 17) // 4
        r(im, 16 - half + off, y, half * 2, 1, (246, 190, 60))
    r(im, 18, 9, 5, 5, (230, 120, 50)); r(im, 19, 10, 3, 3, "redl")
    r(im, 14, 6, 2, 3, "grnd"); p(im, 12, 13, "wht")
    return shadow(outline(im), rx=8)

def dragon_fruit():
    im = new()
    pink, pinkd = (226, 70, 130), (170, 40, 96)
    for y in range(9, 27):
        half = int((64 - (y - 18) ** 2) ** 0.5) if (y - 18) ** 2 < 64 else 0
        r(im, 16 - half, y, half * 2, 1, pink)
    for x, y in ((9, 13), (22, 12), (12, 22), (21, 22), (15, 9), (16, 26)):
        r(im, x, y, 2, 2, "grnl")
    p(im, 13, 15, pinkd); p(im, 18, 19, pinkd); p(im, 12, 12, "wht")
    r(im, 15, 6, 2, 3, "grnl")
    return shadow(outline(im), rx=8)

def persimmon():
    im = new()
    oc, od = (238, 120, 40), (196, 84, 30)
    for y in range(11, 26):
        half = int((90 - (y - 18) ** 2 * 1.6) ** 0.5) if (y - 18) ** 2 * 1.6 < 90 else 0
        r(im, 16 - half, y, half * 2, 1, oc)
    r(im, 12, 9, 8, 3, "grnd"); r(im, 15, 7, 2, 3, "wd"); r(im, 9, 20, 14, 1, od); p(im, 11, 13, "yel")
    return shadow(outline(im), rx=9)

def taro():
    im = new()
    for y in range(9, 27):
        half = int((49 - (y - 18) ** 2 * .8) ** 0.5) if (y - 18) ** 2 * .8 < 49 else 0
        r(im, 16 - half, y, half * 2, 1, (150, 110, 80))
    for y in (12, 16, 20, 24):
        r(im, 11, y, 10, 1, (110, 76, 56))
    r(im, 15, 5, 2, 4, (190, 160, 200)); p(im, 13, 13, (190, 160, 200))
    return shadow(outline(im), rx=7)

def yangmei():
    return ball_cluster(((11, 17), (20, 17), (16, 11), (15, 22)), (150, 30, 60), (100, 20, 44), rad=4,
                        stem=lambda im: (r(im, 15, 4, 2, 4, "grnd"), r(im, 17, 4, 4, 2, "grn")))

def longan():
    return ball_cluster(((10, 16), (16, 14), (22, 16), (13, 21), (19, 21), (16, 26)), (200, 166, 110), (160, 126, 80), rad=3,
                        stem=lambda im: (r(im, 15, 3, 2, 8, "wd"), r(im, 12, 9, 8, 1, "wd")))

# ---------- batch 5: festival ----------
def mooncake():
    im = new()
    gold, goldd = (214, 150, 70), (170, 106, 44)
    for y in range(8, 26):
        half = int((81 - (y - 17) ** 2) ** 0.5) if (y - 17) ** 2 < 81 else 0
        r(im, 16 - half, y, half * 2, 1, gold)
    for y in range(11, 23):
        half = int((30 - (y - 17) ** 2) ** 0.5) if (y - 17) ** 2 < 30 else 0
        r(im, 16 - half, y, half * 2, 1, goldd)
    r(im, 14, 15, 4, 4, gold); p(im, 11, 11, "yel")
    for x, y in ((8, 17), (24, 17), (16, 9), (16, 25)):
        p(im, x, y, goldd)
    return shadow(outline(im), rx=9)

def mooncake_box():
    return boxy("red", "gold", "redd")

def zongzi():
    im = new()
    for i, y in enumerate(range(6, 27)):
        w = 2 + i
        r(im, 16 - w // 2, y, w, 1, "grn" if i % 4 else "grnd")
    r(im, 6, 18, 20, 1, (220, 200, 140)); r(im, 11, 10, 1, 17, (220, 200, 140))
    r(im, 20, 10, 1, 17, (220, 200, 140)); p(im, 15, 8, "grnl")
    return shadow(outline(im), rx=10)

def glutinous_rice():
    im = new()
    r(im, 8, 6, 16, 22, "wht"); r(im, 12, 3, 8, 3, "fs")
    r(im, 8, 13, 16, 8, "blu"); r(im, 13, 15, 6, 4, "wht"); r(im, 15, 16, 2, 2, "blu")
    return shadow(outline(im), rx=8)

def red_envelope():
    im = new()
    r(im, 9, 5, 14, 22, "red"); r(im, 9, 5, 14, 6, "redd")
    for i in range(7):
        r(im, 9 + i, 11 + i // 2, 14 - 2 * i, 1, "redd") if 14 - 2 * i > 0 else None
    for y in range(15, 22):
        half = 3 - abs(y - 18) // 1 if abs(y - 18) < 3 else 0
        r(im, 16 - half, y, half * 2, 1, "gold")
    r(im, 14, 17, 4, 2, "gold")
    return shadow(outline(im), rx=7)

def candy_box():
    im = new()
    for y in range(8, 26):
        half = int((110 - (y - 17) ** 2 * 1.3) ** 0.5) if (y - 17) ** 2 * 1.3 < 110 else 0
        r(im, 16 - half, y, half * 2, 1, "red")
    r(im, 8, 14, 16, 6, "gold"); r(im, 15, 9, 2, 16, "gold")
    for x, y, c in ((10, 11, "yel"), (20, 11, "grnl"), (10, 22, "blul"), (20, 22, "wht")):
        r(im, x, y, 3, 2, c)
    r(im, 14, 15, 4, 4, "redd")
    return shadow(outline(im), rx=10)

def mandarins():
    oc, od = (246, 150, 40), (210, 110, 30)
    return ball_cluster(((11, 19), (21, 19), (16, 14)), oc, od, rad=5,
                        stem=lambda im: (r(im, 15, 7, 2, 3, "grnd"), r(im, 17, 6, 5, 2, "grn"), r(im, 10, 7, 5, 2, "grn")))

def nian_gao():
    im = new()
    r(im, 6, 13, 20, 12, (240, 210, 160)); r(im, 6, 13, 20, 2, (250, 230, 190)); r(im, 6, 23, 20, 2, (200, 160, 110))
    r(im, 12, 17, 8, 4, "red"); r(im, 14, 18, 4, 2, "gold")
    return shadow(outline(im), rx=11)

def couplet(side):
    im = new()
    x = 3 if side == "l" else 23
    r(im, x, 2, 6, 28, "red"); r(im, x, 2, 6, 1, "redl")
    for y in range(5, 28, 5):
        r(im, x + 2, y, 2, 3, "gold")
    return outline(im)

def festival_banner():
    im = new()
    r(im, 0, 6, 32, 2, "gold")
    for i, x in enumerate(range(1, 31, 6)):
        r(im, x, 8, 5, 8, "red" if i % 2 == 0 else "gold")
        r(im, x + 1, 16, 3, 1, "red" if i % 2 == 0 else "gold"); p(im, x + 2, 17, "red" if i % 2 == 0 else "gold")
    return outline(im)

def rabbit_lantern():
    im = new()
    r(im, 8, 10, 16, 12, "wht"); r(im, 9, 9, 14, 1, "wht"); r(im, 9, 22, 14, 1, "wht")
    r(im, 10, 3, 3, 7, "wht"); r(im, 18, 3, 3, 7, "wht"); p(im, 11, 5, "redl"); p(im, 19, 5, "redl")
    p(im, 12, 14, "red"); p(im, 18, 14, "red"); r(im, 9, 18, 14, 2, "redl")
    r(im, 6, 15, 2, 4, "wht"); r(im, 15, 23, 2, 3, "gryd"); r(im, 14, 26, 4, 4, "wd")   # handle stick
    return outline(im)

# ---------- batch 5: logistics, seating ----------
def pallet():
    im = new()
    r(im, 2, 24, 28, 3, "wm"); r(im, 2, 27, 28, 2, "wd")
    for x in (3, 15, 26):
        r(im, x, 27, 3, 3, "wm")
    for (x, y, w, h) in ((3, 12, 13, 12), (16, 12, 13, 12), (8, 2, 15, 10)):
        r(im, x, y, w, h, (196, 150, 96)); r(im, x, y, w, 1, (220, 180, 126))
        r(im, x + w // 2 - 1, y, 2, h, (176, 130, 80)); r(im, x + 2, y + 4, 4, 3, "wht")
    return outline(im)

def cardboard_box():
    im = new()
    r(im, 6, 11, 20, 16, (196, 150, 96)); r(im, 6, 11, 20, 3, (220, 180, 126))
    r(im, 15, 11, 2, 16, (176, 130, 80)); r(im, 9, 18, 5, 4, "wht"); r(im, 19, 17, 4, 2, "red")
    return shadow(outline(im), rx=10)

def dining_table():
    im = new()
    r(im, 6, 8, 20, 11, "wl"); r(im, 6, 18, 20, 2, "wm")
    r(im, 8, 20, 2, 7, "wd"); r(im, 22, 20, 2, 7, "wd")
    for x, y in ((1, 12), (27, 12)):
        r(im, x, y, 4, 4, "red"); r(im, x + 1, y + 4, 2, 4, "wd")
    r(im, 10, 10, 5, 4, "wht"); r(im, 17, 11, 5, 4, "wht"); p(im, 12, 11, BRAISE); p(im, 19, 12, "grn")
    return shadow(outline(im), rx=12)

# ---------- batch 5: UI icons ----------
def icon(draw):
    im = new()
    draw(im)
    return outline(im)

def ic_calendar(im):
    r(im, 6, 8, 20, 18, "wht"); r(im, 6, 8, 20, 5, "red")
    r(im, 10, 5, 2, 5, "gryd"); r(im, 20, 5, 2, 5, "gryd")
    for x in range(9, 24, 4):
        for y in (16, 20):
            r(im, x, y, 2, 2, "gry")
    r(im, 13, 16, 2, 2, "red")

def ic_thumb(up):
    def d(im):
        if up:
            r(im, 8, 15, 5, 11, "blu"); r(im, 13, 13, 11, 13, "skin"); r(im, 15, 6, 5, 8, "skin")
            for y in (16, 19, 22):
                r(im, 13, y, 10, 1, "skins")
        else:
            r(im, 8, 6, 5, 11, "redd"); r(im, 13, 6, 11, 13, "skin"); r(im, 15, 18, 5, 8, "skin")
            for y in (9, 12, 15):
                r(im, 13, y, 10, 1, "skins")
    return d

def ic_warning(im):
    for i, y in enumerate(range(5, 27)):
        w = 2 + i
        r(im, 16 - w // 2, y, w, 1, "yel")
    r(im, 15, 11, 2, 9, "soy"); r(im, 15, 22, 2, 2, "soy")

def ic_clipboard(im):
    r(im, 8, 6, 16, 21, "wm"); r(im, 10, 8, 12, 17, "wht"); r(im, 12, 4, 8, 4, "gryd")
    for y in (12, 16, 20):
        r(im, 12, y, 2, 2, "grn"); r(im, 15, y, 6, 1, "gry")

def ic_power_out(im):
    r(im, 16, 4, 4, 8, "yel"); r(im, 12, 11, 10, 3, "yel"); r(im, 12, 13, 4, 8, "yel"); r(im, 10, 20, 4, 6, "yel")
    for i in range(20):
        p(im, 6 + i, 6 + i, "red"); p(im, 7 + i, 6 + i, "red")

def ic_truck(im):
    r(im, 3, 9, 17, 12, "wht"); r(im, 5, 12, 11, 5, "red")
    r(im, 20, 13, 8, 8, "blu"); r(im, 22, 14, 4, 3, "glass")
    for x in (8, 23):
        r(im, x, 21, 5, 5, "soy"); r(im, x + 2, 23, 1, 1, "gry")

def ic_ship(im):
    r(im, 4, 18, 24, 6, "blud"); r(im, 6, 24, 20, 2, "blud")
    for i, (x, c) in enumerate(((7, "red"), (12, "grn"), (17, "yel"))):
        r(im, x, 12, 5, 6, c); r(im, x, 12, 5, 1, "wht")
    r(im, 22, 9, 4, 9, "wht"); r(im, 23, 10, 2, 2, "glass")
    r(im, 2, 27, 28, 2, WATER)

def ic_phone(im):
    r(im, 10, 5, 12, 22, "soy"); r(im, 11, 7, 10, 16, "blul"); r(im, 15, 24, 2, 1, "gry")
    for x, y in ((5, 9), (4, 13), (5, 17), (26, 9), (27, 13), (26, 17)):
        p(im, x, y, "red"); p(im, x, y + 1, "red")

def ic_face(mood):
    def d(im):
        col = {"happy": "yel", "ok": (246, 200, 120), "sad": (180, 200, 220)}[mood]
        for y in range(6, 27):
            half = int((100 - (y - 16) ** 2) ** 0.5) if (y - 16) ** 2 < 100 else 0
            r(im, 16 - half, y, half * 2, 1, col)
        r(im, 12, 12, 2, 3, "soy"); r(im, 18, 12, 2, 3, "soy")
        if mood == "happy":
            r(im, 11, 19, 10, 1, "soy"); p(im, 10, 18, "soy"); p(im, 21, 18, "soy")
        elif mood == "ok":
            r(im, 12, 20, 8, 1, "soy")
        else:
            r(im, 12, 20, 8, 1, "soy"); p(im, 11, 21, "soy"); p(im, 20, 21, "soy"); r(im, 21, 14, 1, 3, WATER)
    return d

def ic_level_up(im):
    for i, y in enumerate(range(5, 15)):
        r(im, 16 - i, y, 2 * i + 1, 1, "grn")
    r(im, 12, 15, 9, 11, "grn"); r(im, 13, 16, 2, 9, "grnl")

def ic_star_rating(im):
    import math
    from PIL import ImageDraw
    for k, cx in enumerate((7, 16, 25)):
        pts = []
        for i in range(10):
            a = -math.pi / 2 + i * math.pi / 5
            rad = 5 if i % 2 == 0 else 2.2
            pts.append((cx + rad * math.cos(a), 16 + rad * math.sin(a)))
        ImageDraw.Draw(im).polygon(pts, fill=(C["gold"] if k < 2 else C["gry"]) + (255,))

# ---------- batch 5: characters ----------
CHARACTERS.update({
    "cashier": dict(shirt="wht", shirt_d="gry", pants="soy", apron="red",
                    hat=lambda im, back: (r(im, 21, 6, 3, 8, "hair"), p(im, 21, 6, "red"))),
    "stocker": dict(shirt="gry", shirt_d="gryd", pants="blud", apron="gold",
                    hat=lambda im, back: (r(im, 10, 2, 12, 3, "red"), r(im, 9 if not back else 10, 5, 14 if not back else 12, 1, "redd"))),
    "manager": dict(shirt=(64, 64, 84), shirt_d=(44, 44, 60), pants=(44, 44, 60),
                    front=lambda im: (r(im, 14, 15, 4, 3, "wht"), r(im, 15, 16, 2, 7, "red"), r(im, 12, 9, 3, 2, "gryd"), r(im, 17, 9, 3, 2, "gryd"))),
    "abc_teen": dict(shirt=(232, 140, 170), shirt_d=(196, 100, 136), pants="blu",
                     hat=lambda im, back: (r(im, 10, 2, 12, 3, "soy"), r(im, 9, 14, 2, 3, "gryd"), r(im, 21, 14, 2, 3, "gryd"), r(im, 10, 15, 12, 1, "gryd")),
                     front=lambda im: (r(im, 13, 19, 6, 3, (196, 100, 136)),)),
    "local_a": dict(shirt="blul", shirt_d="blu", pants=(120, 100, 80), hair=(226, 196, 120)),
    "local_b": dict(shirt=(240, 190, 76), shirt_d=(188, 132, 50), pants="soy", hair=(40, 30, 30),
                    skin=(150, 100, 70), skins=(120, 78, 56)),
    "restaurant_buyer": dict(shirt=(70, 100, 70), shirt_d=(50, 74, 54), pants="gryd",
                             hat=lambda im, back: (r(im, 10, 2, 12, 3, "wd"), r(im, 9, 5, 14, 1, "wd")),
                             any=lambda im, back: (r(im, 6, 18, 20, 9, (196, 150, 96)), r(im, 6, 18, 20, 2, (220, 180, 126)), r(im, 15, 18, 2, 9, (176, 130, 80)))),
    "driver": dict(shirt=(150, 110, 70), shirt_d=(110, 80, 50), pants=(110, 80, 50),
                   hat=lambda im, back: (r(im, 10, 2, 12, 3, (110, 80, 50)), r(im, 9, 5, 14, 1, (90, 64, 40))),
                   front=lambda im: (r(im, 17, 17, 3, 2, "gold"),)),
    "inspector": dict(shirt="blud", shirt_d=(30, 50, 84), pants=(30, 50, 84),
                      front=lambda im: (r(im, 14, 15, 4, 3, "wht"),),
                      any=lambda im, back: (r(im, 23, 17, 5, 7, "wm"), r(im, 24, 18, 3, 5, "wht"))),
})

# ---------- batch 5: dialogue portraits (64x64) ----------
def portrait(cfg, mood="normal"):
    im = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    bg = C["walls"] + (255,)
    for y in range(64):
        for x in range(64):
            im.putpixel((x, y), bg)
    hair = cfg.get("hair", "hair")
    sk, sks = cfg.get("skin", "skin"), cfg.get("skins", "skins")
    shirt, shirt_d = cfg["shirt"], cfg["shirt_d"]
    r(im, 10, 48, 44, 16, shirt); r(im, 46, 48, 8, 16, shirt_d)       # shoulders
    if cfg.get("apron"):
        r(im, 22, 50, 20, 14, cfg["apron"])
    r(im, 27, 42, 10, 7, sks)                                           # neck
    r(im, 18, 14, 28, 30, sk); r(im, 40, 16, 6, 28, sks)               # face
    r(im, 16, 24, 2, 8, sk); r(im, 46, 24, 2, 8, sks)                   # ears
    r(im, 16, 6, 32, 10, hair); r(im, 16, 16, 3, 10, hair); r(im, 45, 16, 3, 10, hair)
    if cfg.get("grey_temples"):
        r(im, 16, 18, 3, 8, cfg["grey_temples"]); r(im, 45, 18, 3, 8, cfg["grey_temples"])
    if cfg.get("bun"):
        r(im, 26, 1, 12, 7, hair)
    # eyes
    if mood == "happy":
        for x0 in (23, 36):
            r(im, x0, 26, 5, 1, "soy"); p(im, x0, 27, "soy"); p(im, x0 + 4, 27, "soy")
    else:
        r(im, 24, 25, 3, 4, "soy"); r(im, 37, 25, 3, 4, "soy"); p(im, 24, 25, "wht"); p(im, 37, 25, "wht")
    if mood == "worried":
        r(im, 22, 21, 6, 1, "soy"); p(im, 27, 20, "soy"); r(im, 36, 21, 6, 1, "soy"); p(im, 36, 20, "soy")
    else:
        r(im, 22, 21, 6, 1, hair); r(im, 36, 21, 6, 1, hair)
    r(im, 20, 31, 3, 2, "redl"); r(im, 41, 31, 3, 2, "redl")            # blush
    r(im, 31, 30, 2, 2, sks)                                            # nose
    if mood == "happy":
        r(im, 27, 35, 10, 2, "soy"); r(im, 28, 37, 8, 1, "redd")
    elif mood == "worried":
        r(im, 28, 37, 8, 1, "soy"); p(im, 27, 38, "soy"); p(im, 36, 38, "soy")
    else:
        r(im, 28, 36, 8, 1, "soy")
    if cfg.get("wrinkles"):
        r(im, 21, 34, 2, 1, sks); r(im, 41, 34, 2, 1, sks); r(im, 28, 18, 8, 1, sks)
    if cfg.get("glasses"):
        for x0 in (21, 34):
            r(im, x0, 23, 9, 1, "gryd"); r(im, x0, 30, 9, 1, "gryd"); r(im, x0, 23, 1, 8, "gryd"); r(im, x0 + 8, 23, 1, 8, "gryd")
        r(im, 30, 25, 4, 1, "gryd")
    if cfg.get("extra"):
        cfg["extra"](im)
    # frame
    for i in range(64):
        for (x, y) in ((i, 0), (i, 63), (0, i), (63, i)):
            im.putpixel((x, y), C["out"] + (255,))
    return im

PORTRAITS = {
    "protagonist": dict(shirt="blu", shirt_d="blud", apron="red"),
    "dad": dict(shirt=(140, 104, 72), shirt_d=(104, 76, 54), hair=(80, 76, 76), grey_temples=(176, 172, 168), wrinkles=True, glasses=True),
    "mom": dict(shirt="redl", shirt_d="red", apron=(246, 214, 160), bun=True, wrinkles=True),
}

SPRITES.update({
    # dry goods
    "product_doubanjiang": lambda: jar("red", (170, 60, 40), "yel", "red"),
    "product_fermented_tofu": lambda: jar("gold", (210, 120, 80), "wht", "red"),
    "product_seaweed": seaweed, "product_rice_crackers": rice_crackers,
    "product_dried_shiitake": shiitake, "product_instant_noodles_5pack": noodle_5pack,
    "product_soy_milk": soy_milk, "product_herbal_tea": lambda: can("red", "gold", "red"),
    "product_vinegar": lambda: bottle2((70, 30, 30), "gold", "red"),
    "product_frozen_buns": lambda: pack("wht", "blu", lambda im: (r(im, 12, 13, 8, 6, "wht"), r(im, 14, 12, 4, 1, "wht")), crinkle=True),
    # asian produce
    "product_a_choy": lambda: leafy("grn", "grnl", (220, 236, 196), long=True),
    "product_tong_ho": lambda: leafy((70, 140, 70), "grnd", "grnl", fluffy=True),
    "product_bitter_melon": bitter_melon, "product_winter_melon": winter_melon,
    "product_chinese_chives": chives, "product_gai_lan": gai_lan,
    "product_mango": mango, "product_dragon_fruit": dragon_fruit, "product_persimmon": persimmon,
    "product_taro": taro, "product_yangmei": yangmei, "product_longan": longan,
    # festival
    "product_mooncake": mooncake, "product_mooncake_box": mooncake_box, "product_zongzi": zongzi,
    "product_glutinous_rice": glutinous_rice, "product_red_envelope": red_envelope,
    "product_candy_box": candy_box, "product_mandarins": mandarins, "product_nian_gao": nian_gao,
    "deco_couplet_left": lambda: couplet("l"), "deco_couplet_right": lambda: couplet("r"),
    "deco_festival_banner": festival_banner, "deco_rabbit_lantern": rabbit_lantern,
    # logistics + seating
    "pallet": pallet, "product_cardboard_box": cardboard_box, "dining_table": dining_table,
    # UI
    "ui_calendar": lambda: icon(ic_calendar), "ui_thumb_up": lambda: icon(ic_thumb(True)),
    "ui_thumb_down": lambda: icon(ic_thumb(False)), "ui_warning": lambda: icon(ic_warning),
    "ui_inspection": lambda: icon(ic_clipboard), "ui_power_out": lambda: icon(ic_power_out),
    "ui_truck": lambda: icon(ic_truck), "ui_ship": lambda: icon(ic_ship), "ui_phone": lambda: icon(ic_phone),
    "ui_morale_happy": lambda: icon(ic_face("happy")), "ui_morale_ok": lambda: icon(ic_face("ok")),
    "ui_morale_sad": lambda: icon(ic_face("sad")), "ui_level_up": lambda: icon(ic_level_up),
    "ui_rating": lambda: icon(ic_star_rating),
})
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

    base = OUT_DIR.parent
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

    # seafood corner preview, 9 x 5 tiles
    sea = Image.new("RGBA", (9 * 32, 5 * 32), C["fl"] + (255,))
    def sput(n, x, y, dx=0, dy=0):
        im = imgs[n]
        sea.paste(im, (x * 32 + dx, y * 32 + dy), im)
    for x in range(9):
        sput("tile_wall", x, 0)
        for y in range(1, 5):
            sput("tile_floor", x, y)
    sput("deco_lantern", 4, 0)
    sput("fish_tank", 1, 1); sput("fish_tank", 2, 1); sput("fish_tank_murky", 3, 1)
    sput("ice_table", 5, 1); sput("ice_table", 6, 1); sput("ice_table_empty", 7, 1)
    sput("seafood_counter", 2, 3); sput("seafood_counter", 3, 3)
    sput("char_fishmonger_front_0", 3, 2); sput("char_auntie_back_0", 2, 4, dx=8)
    sput("char_family_front_1", 6, 2, dx=6); sput("ui_bubble_frown", 3, 1, dx=12, dy=-6)
    sput("ui_water_bad", 3, 1, dx=-8, dy=-14)
    sea.resize((sea.width * 4, sea.height * 4), Image.NEAREST).save(base / "preview_seafood_4x.png")

    # deli corner preview, 9 x 5 tiles
    deli = Image.new("RGBA", (9 * 32, 5 * 32), C["fl"] + (255,))
    def dput(n, x, y, dx=0, dy=0):
        im = imgs[n]
        deli.paste(im, (x * 32 + dx, y * 32 + dy), im)
    for x in range(9):
        dput("tile_wall", x, 0)
        for y in range(1, 5):
            dput("tile_floor", x, y)
    dput("deco_lantern", 1, 0); dput("deco_lantern", 7, 0); dput("deco_fu_poster", 4, 0, dy=-2)
    dput("wok_stove", 1, 1); dput("wok_stove", 2, 1)
    dput("steam_table", 3, 3); dput("steam_table", 4, 3); dput("steam_table_empty", 5, 3)
    dput("roast_station", 6, 1); dput("roast_station", 7, 1)
    dput("char_chef_front_0", 2, 2, dx=6); dput("char_student_back_1", 4, 4, dx=-6)
    dput("char_family_back_0", 6, 2, dx=10); dput("ui_bubble_heart", 4, 3, dx=-6, dy=-22)
    deli.resize((deli.width * 4, deli.height * 4), Image.NEAREST).save(base / "preview_deli_4x.png")

    # dialogue portraits (64x64)
    ports = []
    for pname, pcfg in PORTRAITS.items():
        for mood in ("normal", "happy", "worried"):
            pim = portrait(pcfg, mood)
            pim.save(OUT_DIR / f"portrait_{pname}_{mood}.png")
            ports.append(pim)
    psheet = Image.new("RGBA", (64 * 3, 64 * len(PORTRAITS)), (0, 0, 0, 0))
    for k, pim in enumerate(ports):
        psheet.paste(pim, ((k % 3) * 64, (k // 3) * 64))
    psheet.resize((psheet.width * 3, psheet.height * 3), Image.NEAREST).save(base / "preview_portraits_3x.png")

    # festival corner preview, 9 x 5 tiles
    fest = Image.new("RGBA", (9 * 32, 5 * 32), C["fl"] + (255,))
    def fput(n, x, y, dx=0, dy=0):
        im = imgs[n]
        fest.paste(im, (x * 32 + dx, y * 32 + dy), im)
    for x in range(9):
        fput("tile_wall", x, 0)
        for y in range(1, 5):
            fput("tile_floor", x, y)
    fput("deco_couplet_left", 0, 0); fput("deco_couplet_right", 8, 0)
    for x in range(1, 8):
        fput("deco_festival_banner", x, 0, dy=-4)
    fput("deco_fu_poster", 4, 0, dy=6)
    fput("pallet", 1, 1); fput("pallet", 2, 1)
    fput("shelf_full", 4, 1); fput("shelf_full_b", 5, 1)
    fput("produce_stand_full", 7, 1)
    for k, n in enumerate(("product_mooncake_box", "product_candy_box", "product_mandarins", "product_zongzi", "product_red_envelope", "product_nian_gao")):
        fput(n, 1 + k, 3, dy=-4)
    fput("char_cashier_front_0", 7, 3); fput("char_abc_teen_front_1", 3, 2, dx=10)
    fput("char_local_a_back_0", 5, 3, dx=-4); fput("char_driver_front_0", 0, 2, dx=8)
    fest.resize((fest.width * 4, fest.height * 4), Image.NEAREST).save(base / "preview_festival_4x.png")
    print(f"{len(imgs)} sprites written ({len(static)} static, {len(chars)} character frames)")

if __name__ == "__main__":
    main()
