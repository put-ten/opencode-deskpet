from PIL import Image

W, H = 48, 48
PINK = (255, 170, 190)
PINK_D = (210, 120, 150)
PINK_L = (255, 210, 225)
EAR_IN = (255, 200, 215)
EYE = (40, 40, 40)
EYE_G = (100, 200, 140)
NOSE = (255, 130, 150)
WHITE = (255, 255, 255)
WHISK = (200, 170, 185)
MOUTH = (180, 100, 130)
PAD = (180, 110, 140)
STRIPE = (230, 150, 170)
BG = (0, 0, 0, 0)


def frame():
    return Image.new("RGBA", (W, H), BG)


def p(img, x, y, c):
    if 0 <= x < W and 0 <= y < H:
        img.putpixel((x, y), c)


def cf(img, cx, cy, r, c):
    for y in range(H):
        for x in range(W):
            if (x-cx)*(x-cx) + (y-cy)*(y-cy) <= r*r:
                p(img, x, y, c)


def rf(img, x1, y1, x2, y2, c):
    for y in range(y1, y2+1):
        for x in range(x1, x2+1):
            p(img, x, y, c)


def lh(img, x1, x2, y, c):
    for x in range(x1, x2+1):
        p(img, x, y, c)


def draw_ears(img, top=4):
    # Left ear triangle
    for dy in range(12):
        w = 6 - dy//2
        rf(img, 14-w, top+dy, 14+w, top+dy, PINK)
    rf(img, 12, top+2, 16, top+6, EAR_IN)
    # Right ear triangle
    for dy in range(12):
        w = 6 - dy//2
        rf(img, 34-w, top+dy, 34+w, top+dy, PINK)
    rf(img, 32, top+2, 36, top+6, EAR_IN)


def draw_cat(img, eye_open=True, yofs=0):
    ty = yofs
    # Body: oval
    cf(img, 24, 33+ty, 10, PINK)
    # Head: round, flat bottom
    cf(img, 24, 20+ty, 11, PINK)
    for y in range(28+ty, 30+ty):
        for x in range(16, 33):
            if img.getpixel((x, y))[:3] == PINK:
                p(img, x, y, PINK)
    # Ears
    draw_ears(img, top=4+ty)
    # Eyes
    if eye_open:
        rf(img, 16, 18+ty, 20, 21+ty, EYE)   # left
        rf(img, 27, 18+ty, 31, 21+ty, EYE)   # right
        p(img, 17, 18+ty, WHITE); p(img, 28, 18+ty, WHITE)
        p(img, 18, 20+ty, EYE_G); p(img, 29, 20+ty, EYE_G)
    else:
        lh(img, 16, 20, 19+ty, EYE)
        lh(img, 27, 31, 19+ty, EYE)
    # Nose
    rf(img, 23, 22+ty, 25, 23+ty, NOSE)
    p(img, 24, 24+ty, MOUTH)
    # Whiskers
    for i in range(4):
        p(img, 8+i, 22+ty+i-2, WHISK); p(img, 7+i, 22+ty+i, WHISK)
        p(img, 36+i, 22+ty+i-2, WHISK); p(img, 35+i, 22+ty+i, WHISK)
    # Paws at bottom
    cf(img, 18, 42+ty, 4, PINK)
    cf(img, 30, 42+ty, 4, PINK)
    # Paw line
    lh(img, 16, 20, 43+ty, PAD); lh(img, 28, 32, 43+ty, PAD)
    # Tail: curved up
    rf(img, 32, 30+ty, 33, 28+ty, PINK)
    rf(img, 34, 29+ty, 34, 26+ty, PINK)
    rf(img, 35, 27+ty, 35, 25+ty, PINK)
    p(img, 34, 24+ty, PINK); p(img, 33, 23+ty, PINK); p(img, 32, 24+ty, PINK)
    # Shading under body
    lh(img, 17, 31, 39+ty, PINK_D)


def sheet(frames, name):
    s = Image.new("RGBA", (W * len(frames), H))
    for i, f in enumerate(frames):
        s.paste(f, (i * W, 0))
    s.save(f"Sprites/{name}.png")


# Idle - 4 frames, subtle bob + blink
fs = [frame() for _ in range(4)]
for i in range(4):
    draw_cat(fs[i], eye_open=(i != 1), yofs=(i % 2))
sheet(fs, "cat_idle")

# Walk - 4 frames, horizontal lean + leg alternation
fs = [frame() for _ in range(4)]
for i in range(4):
    ox = [0, 1, 0, -1][i]
    oy = [0, 1, 2, 1][i]
    draw_cat(fs[i], yofs=oy)
    # Alternating walking legs
    if i % 2 == 0:
        rf(fs[i], 14+ox, 44, 19+ox, 46, PINK_D)
        rf(fs[i], 28+ox, 44, 33+ox, 46, PINK_D)
    else:
        rf(fs[i], 16+ox, 44, 21+ox, 46, PINK_D)
        rf(fs[i], 26+ox, 44, 31+ox, 46, PINK_D)
sheet(fs, "cat_walk")

# Stretch - 4 frames, front paws extend forward
fs = [frame() for _ in range(4)]
for i in range(4):
    s = i
    draw_cat(fs[i], yofs=0)
    # Extended forward paws
    px = 6 + s * 3
    rf(fs[i], px, 40, px+5, 44, PINK)
    rf(fs[i], px+16, 40, px+21, 44, PINK)
    # Arch back
    for x in range(15, 34):
        for y in range(30, 35):
            if fs[i].getpixel((x, y))[:3] == PINK[:3]:
                p(fs[i], x, y+s//2, PINK_L)
sheet(fs, "cat_stretch")

# Sleep - 2 frames, curled cat
fs = [frame() for _ in range(2)]
for i in range(2):
    bob = i * 2
    # Oval curled body
    for y in range(20+bob, 42+bob):
        for x in range(8, 42):
            if (x-24)*(x-24)*0.45 + (y-30-bob)*(y-30-bob)*0.7 <= 150:
                p(fs[i], x, y, PINK)
    # Shading at bottom
    for x in range(12, 36):
        for y in range(36+bob, 42+bob):
            if fs[i].getpixel((x, y))[:3] == PINK[:3]:
                p(fs[i], x, y, PINK_D)
    # Head on top, resting on paws (bigger)
    for y in range(5+bob, 24+bob):
        for x in range(11, 39):
            if (x-25)*(x-25) + (y-15-bob)*(y-15-bob) <= 72:
                p(fs[i], x, y, PINK)
    # Ears — custom position, closer to head center
    # Left ear
    rf(fs[i], 15, 3+bob, 19, 7+bob, PINK)
    rf(fs[i], 16, 4+bob, 18, 6+bob, EAR_IN)
    # Right ear
    rf(fs[i], 31, 3+bob, 35, 7+bob, PINK)
    rf(fs[i], 32, 4+bob, 34, 6+bob, EAR_IN)
    # Closed eyes
    lh(fs[i], 17, 23, 15+bob, EYE)
    lh(fs[i], 27, 33, 15+bob, EYE)
    # Nose
    p(fs[i], 24, 18+bob, NOSE); p(fs[i], 25, 18+bob, NOSE); p(fs[i], 26, 18+bob, NOSE)
    # Body stripes
    lh(fs[i], 16, 22, 27+bob, STRIPE)
    lh(fs[i], 14, 20, 31+bob, STRIPE)
    lh(fs[i], 16, 22, 35+bob, STRIPE)
    # Tail wraps right side
    for y in range(26+bob, 36+bob):
        for x in range(34, 42):
            if (x-36)*(x-36) + (y-31-bob)*(y-31-bob) <= 25:
                p(fs[i], x, y, PINK)
    p(fs[i], 35, 24+bob, PINK_L); p(fs[i], 36, 23+bob, PINK_L)
    # Front paws visible under chin
    cf(fs[i], 20, 23+bob, 3, PINK_L)
    cf(fs[i], 30, 23+bob, 3, PINK_L)
    # Zzz
    if i == 1:
        p(fs[i], 36, 8+bob, WHITE); p(fs[i], 37, 7+bob, WHITE); p(fs[i], 38, 6+bob, WHITE)
sheet(fs, "cat_sleep")

# Bounce - 2 frames, normal → startled puffed up
fs = [frame() for _ in range(2)]
draw_cat(fs[0])
# Puffed up
cf(fs[1], 24, 28, 14, PINK)
draw_ears(fs[1], top=3)
rf(fs[1], 15, 18, 20, 23, EYE)
rf(fs[1], 27, 18, 32, 23, EYE)
p(fs[1], 16, 19, WHITE); p(fs[1], 28, 19, WHITE)
p(fs[1], 17, 20, EYE_G); p(fs[1], 29, 20, EYE_G)
p(fs[1], 21, 23, NOSE); p(fs[1], 24, 23, NOSE); p(fs[1], 25, 23, NOSE)
rf(fs[1], 35, 26, 40, 36, PINK)
# Spiky fur
for i in range(5):
    p(fs[1], 20+i*2, 14, PINK_D); p(fs[1], 21+i*2, 15, PINK_D)
sheet(fs, "cat_bounce")

print("Cat sprites restored!")
