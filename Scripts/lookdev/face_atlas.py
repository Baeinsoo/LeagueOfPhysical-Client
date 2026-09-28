"""LookDev 얼굴 아틀라스: 3열×2행, 칸 512px. 순서 Normal, Surprise, Cheer, Despair, Focus.

기본은 바탕이 투명(이목구비만) — PolyOne 치비는 눈이 따로 된 메시라 그 메시를 끄면 된다.
몸 텍스처에 입이 그려져 있어 덮어야 하면 --skin-base 로 피부색 바탕을 깐다.

    python Scripts/lookdev/face_atlas.py [--skin-base] [--skin #F7D2AE]
"""
import argparse
from PIL import Image, ImageDraw, ImageFilter

CELL = 512
INK = (27, 20, 32, 255)
WHITE = (255, 255, 255, 255)
MOUTH = (122, 33, 48, 255)
TONGUE = (255, 138, 149, 255)
BLUSH = (255, 120, 130, 150)
SWEAT = (124, 200, 255, 255)
GLOOM = (80, 90, 160, 200)


def base(skin, with_skin):
    cell = Image.new('RGBA', (CELL, CELL), skin + (0,))
    if with_skin:
        #  피부색 둥근 바탕 — 가장자리를 흐려 머리 곡면과 이어지게 한다.
        mask = Image.new('L', (CELL, CELL), 0)
        ImageDraw.Draw(mask).ellipse((40, 60, CELL - 40, CELL - 30), fill=255)
        cell.putalpha(mask.filter(ImageFilter.GaussianBlur(24)))
    return cell


def eye(d, cx, cy, rx, ry):
    d.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=INK)
    d.ellipse((cx - rx * 0.65, cy - ry * 0.7, cx - rx * 0.05, cy - ry * 0.15), fill=WHITE)


def blush(d):
    for cx in (140, CELL - 140):
        d.ellipse((cx - 44, 294, cx + 44, 342), fill=BLUSH)


def normal(d):
    eye(d, 190, 250, 30, 44)
    eye(d, CELL - 190, 250, 30, 44)
    blush(d)
    d.arc((226, 300, 286, 350), 20, 160, fill=INK, width=10)


def surprise(d):
    for cx in (180, CELL - 180):
        d.ellipse((cx - 62, 180, cx + 62, 320), fill=WHITE, outline=INK, width=10)
        d.ellipse((cx - 18, 238, cx + 18, 274), fill=INK)
    d.ellipse((232, 330, 280, 392), fill=MOUTH)
    d.polygon([(CELL - 110, 150), (CELL - 90, 196), (CELL - 130, 196)], fill=SWEAT)


def cheer(d):
    for cx in (190, CELL - 190):
        d.arc((cx - 40, 222, cx + 40, 290), 200, 340, fill=INK, width=12)
    blush(d)
    d.chord((196, 296, 316, 400), 0, 180, fill=MOUTH)
    d.ellipse((230, 350, 282, 384), fill=TONGUE)


def despair(d):
    d.line((150, 236, 222, 254), fill=INK, width=12)
    d.line((CELL - 150, 236, CELL - 222, 254), fill=INK, width=12)
    #  물결 입
    for k, x in enumerate(range(200, 320, 30)):
        start, end = (180, 360) if k % 2 else (0, 180)
        d.arc((x, 322, x + 30, 352), start, end, fill=INK, width=8)
    #  이마의 우울 줄
    for x in (190, 230, 270, 310):
        d.line((x, 90, x, 170), fill=GLOOM, width=8)


def focus(d):
    #  조준 — 한쪽 눈 감고 다른 눈은 가늘게, 입은 꾹.
    d.line((150, 250, 226, 250), fill=INK, width=12)
    d.ellipse((CELL - 224, 226, CELL - 164, 262), fill=INK)
    d.line((CELL - 232, 206, CELL - 150, 222), fill=INK, width=10)
    d.line((232, 330, 280, 330), fill=INK, width=10)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--skin', default='#F7D2AE')
    ap.add_argument('--skin-base', action='store_true')
    ap.add_argument('--out', default='Assets/LookDev/Face/FaceAtlas.png')
    a = ap.parse_args()
    skin = tuple(int(a.skin[i:i + 2], 16) for i in (1, 3, 5))
    atlas = Image.new('RGBA', (CELL * 3, CELL * 2), (0, 0, 0, 0))
    for i, draw in enumerate((normal, surprise, cheer, despair, focus)):
        cell = base(skin, a.skin_base)
        draw(ImageDraw.Draw(cell))
        atlas.paste(cell, ((i % 3) * CELL, (i // 3) * CELL))
    atlas.save(a.out)


if __name__ == '__main__':
    main()
