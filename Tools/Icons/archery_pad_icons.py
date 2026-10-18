"""Archery 조작 화면 버튼 아이콘 — 흰 실루엣, 투명 배경 PNG.

실행:  python3 Tools/Icons/archery_pad_icons.py [출력 폴더]
       출력 폴더를 안 주면 Assets/UI/ArcheryPad/Icons/ 에 쓴다(이 파일 기준 상대 경로).
출력:  bow_drawn.png  — 활 버튼: 시위를 당겨 화살을 건 활(오른쪽 위를 겨눈다)
       bow_slack.png  — 시위 풀기 버튼: 시위가 느슨하게 늘어진 활
       move.png       — 버튼 옮기기 손잡이: 네 방향 화살표(✥)
필요:  Pillow (pip install pillow)

모양은 활이 오른쪽(+x)을 겨누는 "로컬 좌표"(가로·세로 대략 −1~1, y는 아래가 +)로 적고,
다 그린 뒤 돌려서 캔버스에 맞춘다. 4배 크기로 그려 줄여서 가장자리를 부드럽게 한다.
같은 Pillow면 같은 바이트가 나온다 — 모양을 고치려면 이 파일을 고쳐 다시 뽑는다.
"""
import math
import os
import sys

from PIL import Image, ImageDraw

SIZE = 256
SS = 4                      # 4배로 그려 줄인다(가장자리 부드럽게)
MARGIN = 0.06               # 캔버스 가장자리 여백(한 변 대비)


# ── 모양 조각 ─────────────────────────────────────────────────────────
#   ('stroke', [(x, y, 반지름), ...])  점마다 굵기가 다른 둥근 선
#   ('poly',   [(x, y), ...])          채운 다각형

def stroke(points, radius):
    """같은 굵기의 둥근 선."""
    return ('stroke', [(x, y, radius) for x, y in points])


def tapered(fn, r_mid, r_end, n=160):
    """t∈[-1,1]을 따라 그리는 곡선. 가운데(t=0)가 r_mid, 양 끝이 r_end 굵기다."""
    pts = []
    for i in range(n + 1):
        t = -1 + 2 * i / n
        x, y = fn(t)
        pts.append((x, y, r_end + (r_mid - r_end) * (1 - abs(t)) ** 1.4))
    return ('stroke', pts)


def curve(fn, n=160):
    return [fn(-1 + 2 * i / n) for i in range(n + 1)]


def bow_limb(grip_x, tip_x, half_height, recurve):
    """활 몸. 손잡이가 앞(+x)으로 나오고 양 끝이 뒤로 휜다. 끝은 살짝 다시 앞으로 젖힌다(리커브)."""
    def fn(t):
        a = abs(t)
        x = grip_x + (tip_x - grip_x) * a ** 2
        x += recurve * max(0.0, a - 0.78) / 0.22
        return x, half_height * t
    return fn


def drawn_bow():
    """시위를 당겨 화살을 건 활. 화살은 +x로 나간다."""
    tip_x, half_h = -0.34, 0.86
    limb = bow_limb(0.30, tip_x, half_h, 0.08)
    top, bottom = limb(-1), limb(1)
    nock = (-0.66, 0.0)
    shapes = [
        tapered(limb, 0.10, 0.045),
        stroke([(0.27, -0.13), (0.27, 0.13)], 0.085),              # 손잡이 감은 자리 — 조금 더 굵게
        stroke([top, nock, bottom], 0.026),                         # 당겨진 시위(V자)
        stroke([(nock[0] + 0.02, 0.0), (0.70, 0.0)], 0.036),        # 화살대
        ('poly', [(0.96, 0.0), (0.66, -0.13), (0.72, 0.0), (0.66, 0.13)]),   # 화살촉
        ('poly', [(-0.30, -0.02), (-0.42, -0.13), (-0.58, -0.13), (-0.50, -0.02)]),  # 깃
        ('poly', [(-0.30, 0.02), (-0.42, 0.13), (-0.58, 0.13), (-0.50, 0.02)]),
    ]
    return shapes


def slack_bow():
    """시위가 풀린 활. 몸은 화살 건 활과 같고, 시위가 양 끝 사이에서 물결치며 늘어져 있다. 화살은 없다."""
    tip_x, half_h = -0.34, 0.86
    limb = bow_limb(0.30, tip_x, half_h, 0.08)
    top, bottom = limb(-1), limb(1)

    def string(t):
        #  양 끝을 잇는 직선에서 뒤(−x)로 처지고 물결친다 — 팽팽하지 않다는 것이 한눈에 읽힌다.
        s = (t + 1) / 2
        x = top[0] + (bottom[0] - top[0]) * s
        x -= 0.06 * math.sin(math.pi * s)                       # 처짐
        x += 0.06 * math.sin(math.pi * 7 * s)                   # 물결
        y = top[1] + (bottom[1] - top[1]) * s
        return x, y

    return [
        tapered(limb, 0.10, 0.045),
        stroke([(0.27, -0.13), (0.27, 0.13)], 0.085),
        stroke(curve(string, 300), 0.032),
    ]


def move_glyph():
    """네 방향 화살표(✥). 손잡이 원 안에 들어갈 만큼 굵게."""
    shapes = []
    shaft, head_base, head_half = 0.10, 0.50, 0.30
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        px, py = -dy, dx            # 화살표 방향에 수직
        shapes.append(stroke([(0.0, 0.0), (dx * head_base, dy * head_base)], shaft))
        shapes.append(('poly', [
            (dx * 1.0, dy * 1.0),
            (dx * head_base + px * head_half, dy * head_base + py * head_half),
            (dx * head_base - px * head_half, dy * head_base - py * head_half),
        ]))
    return shapes


# ── 그리기 ───────────────────────────────────────────────────────────

def rotate(shapes, degrees):
    """패널 좌표(y 아래가 +)에서 돌린다. −45°면 +x가 오른쪽 위를 가리킨다."""
    a = math.radians(degrees)
    c, s = math.cos(a), math.sin(a)

    def rot(x, y):
        return x * c - y * s, x * s + y * c

    out = []
    for kind, pts in shapes:
        if kind == 'stroke':
            out.append((kind, [(*rot(x, y), r) for x, y, r in pts]))
        else:
            out.append((kind, [rot(x, y) for x, y in pts]))
    return out


def bounds(shapes):
    xs, ys = [], []
    for kind, pts in shapes:
        for p in pts:
            r = p[2] if kind == 'stroke' else 0.0
            xs += [p[0] - r, p[0] + r]
            ys += [p[1] - r, p[1] + r]
    return min(xs), min(ys), max(xs), max(ys)


def render(shapes, path):
    big = SIZE * SS
    x0, y0, x1, y1 = bounds(shapes)
    scale = big * (1 - 2 * MARGIN) / max(x1 - x0, y1 - y0)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2

    def px(x, y):
        return (big / 2 + (x - cx) * scale, big / 2 + (y - cy) * scale)

    mask = Image.new('L', (big, big), 0)
    d = ImageDraw.Draw(mask)
    for kind, pts in shapes:
        if kind == 'poly':
            d.polygon([px(x, y) for x, y in pts], fill=255)
            continue
        #  둥근 선: 이웃한 점 사이를 사다리꼴로 메우고 점마다 원을 찍는다(굵기가 변해도 매끈하다).
        for i, (x, y, r) in enumerate(pts):
            X, Y = px(x, y)
            R = r * scale
            d.ellipse([X - R, Y - R, X + R, Y + R], fill=255)
            if i + 1 < len(pts):
                x2, y2, r2 = pts[i + 1]
                X2, Y2 = px(x2, y2)
                R2 = r2 * scale
                L = math.hypot(X2 - X, Y2 - Y)
                if L > 1e-6:
                    nx, ny = -(Y2 - Y) / L, (X2 - X) / L
                    d.polygon([(X + nx * R, Y + ny * R), (X2 + nx * R2, Y2 + ny * R2),
                               (X2 - nx * R2, Y2 - ny * R2), (X - nx * R, Y - ny * R)], fill=255)

    alpha = mask.resize((SIZE, SIZE), Image.LANCZOS)
    img = Image.new('RGBA', (SIZE, SIZE), (255, 255, 255, 0))
    img.putalpha(alpha)
    img.save(path, optimize=False)
    print('wrote', path)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, '..', '..', 'Assets', 'UI', 'ArcheryPad', 'Icons')
    os.makedirs(out, exist_ok=True)
    render(rotate(drawn_bow(), -45), os.path.join(out, 'bow_drawn.png'))
    render(rotate(slack_bow(), -45), os.path.join(out, 'bow_slack.png'))
    render(move_glyph(), os.path.join(out, 'move.png'))


if __name__ == '__main__':
    main()
