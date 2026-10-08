"""Flappy 광산 부품 — 원점 기준 단위 부품을 FBX로, 나무결·바위 무늬를 512² 타일 PNG로 뽑는다.

실행:  blender -b -P Tools/Blender/mine_parts.py -- <LeagueOfPhysical-Client/Assets/Art 절대경로> [부품 이름 ...]
출력:  <Art>/Models/Mine/<부품>.fbx,  <Art>/Textures/Mine/{wood_grain,rock}.png
       부품 이름을 주면 그 부품 FBX만 다시 뽑는다(텍스처는 안 건드린다) — FBX는 뽑을 때마다 바이트가 달라져
       안 바뀐 부품까지 커밋에 섞이지 않게.

좌표는 전부 **게임(유니티) 좌표**로 적는다: x = 오른쪽(코스 진행), y = 위, z = 깊이(+가 카메라 반대쪽, 카메라는 −z).
단위 m. 블렌더로 옮길 때 (x, y, z) → 블렌더 (x, z, y)로 바꾸고, FBX 축 설정이 다시 유니티 축으로 돌린다.
각 부품의 원점 규칙은 함수 docstring에 — 스펙 §5 표와 Task 6 배치가 이것을 따른다.
재질은 슬롯 이름만 넣는다(색·텍스처는 유니티 재질에서). 시안: .superpowers/sdd/2026-10-07-flappy-mine-look-slice1/mine_lib.py·mine_variants5.py.
"""
import bpy, bmesh, math, os, random, sys
import numpy as np
from mathutils import Vector

ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ART = ARGS[0] if ARGS else None
ONLY = set(ARGS[1:])     # 비면 전부
if not ART or not os.path.isdir(ART):
    raise SystemExit('usage: blender -b -P mine_parts.py -- <Assets/Art 절대경로>')
MODEL_DIR = os.path.join(ART, 'Models', 'Mine')
TEX_DIR = os.path.join(ART, 'Textures', 'Mine')

# 블렌더 → 유니티 축. 유니티 FBX 임포터가 x를 뒤집으므로 forward는 'Z'여야 블렌더 −Y(= 게임 −z, 카메라 쪽)가 유니티 −Z로 간다.
AXIS_FORWARD = 'Z'
AXIS_UP = 'Y'

# 무늬 텍스처 한 장이 덮는 길이(m). UV = 상자 투영 / 이 값.
UV_TILE = {'Plank': 1.0, 'Wood': 1.0, 'Tie': 1.0, 'Rock': 2.0, 'RockDark': 2.0}

HALF = 7.28   # 통로 반 높이(MineCourse.BaseHalf) — CaveMouth 안쪽 선


def B(p):
    """게임 좌표 → 블렌더 좌표."""
    return Vector((p[0], p[2], p[1]))


class Part:
    """한 부품 = 메시 하나(재질 슬롯 여럿). 게임 좌표로 조각을 붙인다."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.slots = []
        self.uvo = self.bm.faces.layers.float.new('uvo')

    def _slot(self, mat):
        if mat not in self.slots:
            self.slots.append(mat)
        return self.slots.index(mat)

    def _faces(self, faces, mat, uvo=0.0, closed=True, smooth=False):
        idx = self._slot(mat)
        for f in faces:
            f.material_index = idx
            f.smooth = smooth
            f[self.uvo] = uvo
        if closed:
            bmesh.ops.recalc_face_normals(self.bm, faces=list(faces))
        return faces

    def _ring_prism(self, ring0, ring1, mat, uvo=0.0, smooth_sides=False, caps=True):
        """같은 개수의 꼭짓점 고리 둘을 옆면으로 잇고 양끝을 막는다(볼록 단면)."""
        bm = self.bm
        v0 = [bm.verts.new(B(p)) for p in ring0]
        v1 = [bm.verts.new(B(p)) for p in ring1]
        n = len(v0)
        sides = [bm.faces.new((v0[i], v0[(i + 1) % n], v1[(i + 1) % n], v1[i])) for i in range(n)]
        allf = list(sides)
        if caps:
            allf += [bm.faces.new(v0), bm.faces.new(list(reversed(v1)))]
        self._faces(allf, mat, uvo)
        if smooth_sides:
            for f in sides:
                f.smooth = True
        return allf

    def box(self, c, s, mat, uvo=0.0):
        x, y, z = c; hx, hy, hz = s[0] / 2, s[1] / 2, s[2] / 2
        r0 = [(x - hx, y - hy, z - hz), (x + hx, y - hy, z - hz), (x + hx, y + hy, z - hz), (x - hx, y + hy, z - hz)]
        r1 = [(p[0], p[1], z + hz) for p in r0]
        return self._ring_prism(r0, r1, mat, uvo)

    def prism_x(self, profile, x0, x1, mat, uvo=0.0):
        """x 방향으로 뽑은 기둥. profile = [(y, z)...] 볼록."""
        return self._ring_prism([(x0, p[0], p[1]) for p in profile], [(x1, p[0], p[1]) for p in profile], mat, uvo)

    def prism_y(self, profile, y0, y1, mat, uvo=0.0):
        """y(위) 방향으로 뽑은 기둥. profile = [(x, z)...] 볼록."""
        return self._ring_prism([(p[0], y0, p[1]) for p in profile], [(p[0], y1, p[1]) for p in profile], mat, uvo)

    def strut(self, p0, p1, t, d, zc, mat, uvo=0.0):
        """xy 평면에서 p0→p1로 비스듬히 놓인 각재(두께 t, 깊이 d)."""
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]; L = math.hypot(dx, dy)
        nx, ny = -dy / L * t / 2, dx / L * t / 2
        quad = [(p0[0] - nx, p0[1] - ny), (p1[0] - nx, p1[1] - ny), (p1[0] + nx, p1[1] + ny), (p0[0] + nx, p0[1] + ny)]
        return self._ring_prism([(q[0], q[1], zc - d / 2) for q in quad], [(q[0], q[1], zc + d / 2) for q in quad], mat, uvo)

    def cyl(self, c, r, length, axis, segs, mat, smooth=True):
        ring = [(r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs)) for i in range(segs)]
        x, y, z = c; h = length / 2
        if axis == 'y':
            r0 = [(x + a, y - h, z + b) for a, b in ring]; r1 = [(x + a, y + h, z + b) for a, b in ring]
        elif axis == 'z':
            r0 = [(x + a, y + b, z - h) for a, b in ring]; r1 = [(x + a, y + b, z + h) for a, b in ring]
        else:
            r0 = [(x - h, y + a, z + b) for a, b in ring]; r1 = [(x + h, y + a, z + b) for a, b in ring]
        return self._ring_prism(r0, r1, mat, smooth_sides=smooth)

    def lump(self, c, r, scale, mat, seed):
        """각진 바위 덩이(아이코 구 1단 + 흔들기)."""
        rnd = random.Random(seed)
        before = set(self.bm.faces)
        bmesh.ops.create_icosphere(self.bm, subdivisions=1, radius=r)
        faces = [f for f in self.bm.faces if f not in before]
        verts = {v for f in faces for v in f.verts}
        for v in verts:
            # create_icosphere는 블렌더 좌표로 만든다: 블렌더 (x, y, z) = 게임 (x, z, y)
            k = 1.0 + (rnd.random() - 0.5) * 0.35
            v.co = Vector((c[0] + v.co.x * scale[0] * k, c[2] + v.co.y * scale[2] * k, c[1] + v.co.z * scale[1] * k))
        return self._faces(faces, mat)

    def flat(self, pts, mat):
        """카메라(−z)를 보는 평면 다각형 하나(오목 가능 — 삼각형으로 쪼갠다)."""
        bm = self.bm
        vs = [bm.verts.new(B(p)) for p in pts]
        f = bm.faces.new(vs)
        f.normal_update()
        if f.normal.y > 0:          # 블렌더 −Y = 게임 −z = 카메라 쪽
            f.normal_flip()
        res = bmesh.ops.triangulate(bm, faces=[f])
        return self._faces(res['faces'], mat, closed=False)

    def slab(self, outline, z0, z1, mat):
        """xy 외곽선(오목 가능)을 z0~z1로 두께를 준 판. 앞뒤 면은 삼각형으로 쪼갠다."""
        bm = self.bm
        v0 = [bm.verts.new(B((p[0], p[1], z0))) for p in outline]
        v1 = [bm.verts.new(B((p[0], p[1], z1))) for p in outline]
        n = len(outline)
        sides = [bm.faces.new((v0[i], v0[(i + 1) % n], v1[(i + 1) % n], v1[i])) for i in range(n)]
        front = bm.faces.new(v0); back = bm.faces.new(list(reversed(v1)))
        tri = bmesh.ops.triangulate(bm, faces=[front, back])['faces']
        return self._faces(sides + tri, mat)

    # ── 마무리 ──
    def tris(self):
        return sum(len(f.verts) - 2 for f in self.bm.faces)

    def bounds(self):
        """게임 좌표 경계 (min, max)."""
        cs = [(v.co.x, v.co.z, v.co.y) for v in self.bm.verts]
        return tuple(min(c[i] for c in cs) for i in range(3)), tuple(max(c[i] for c in cs) for i in range(3))

    def uv_box(self):
        """상자 투영 UV(게임 좌표): 앞뒤 면 (x, y) · 옆면 (z, y) · 윗아랫면 (x, z). 나무결은 v를 따라 바뀐다."""
        bm = self.bm
        uv = bm.loops.layers.uv.new('UVMap')
        bm.normal_update()
        for f in bm.faces:
            n = f.normal; g = (abs(n.x), abs(n.z), abs(n.y))   # 게임 축 크기
            ax = g.index(max(g))
            tile = UV_TILE.get(self.slots[f.material_index], 1.0)
            o = f[self.uvo]
            for l in f.loops:
                gx, gy, gz = l.vert.co.x, l.vert.co.z, l.vert.co.y
                u, v = ((gz, gy) if ax == 0 else (gx, gz) if ax == 1 else (gx, gy))
                l[uv].uv = (u / tile + o, v / tile + o)


def chamfer_rect(cx, cy, w, h, c):
    """모서리를 c만큼 깎은 직사각형 단면(8각) — 이어 붙이는 방향으로는 깎지 않으므로 이음매가 안 보인다."""
    x0, x1, y0, y1 = cx - w / 2, cx + w / 2, cy - h / 2, cy + h / 2
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


def psin(x, y, terms):
    """x 방향 주기가 2π/정수인 사인 합 — 조각 끝이 이웃과 맞물린다."""
    return sum(a * math.sin(k * x + b * y + p) for a, k, b, p in terms)


# ════════════════════════ 부품 ════════════════════════

def gate_plank():
    """GatePlank — 판자 세 장 1 m 칸. x ∈ [−0.975, 0.975](폭 1.95 = 파이프 폭), y ∈ [0, 1](아래 끝이 원점), z ∈ [−0.65, 0.65].
    기둥 높이만큼 세로로 쌓고 마지막 칸은 y 축척으로 줄인다. 위아래로는 깎지 않아 쌓아도 이음매가 없다."""
    p = Part('GatePlank')
    for i, dx in enumerate((-0.66, 0.0, 0.66)):
        p.prism_y(chamfer_rect(dx, 0.0, 0.63, 1.3, 0.04), 0.0, 1.0, 'Plank', uvo=0.37 * i)
    return p


def gate_strap():
    """GateStrap — 쇠띠. 가운데가 원점: y ∈ [−0.08, 0.08], x는 파이프 폭 그대로(±0.975), 앞뒤로 0.04씩 튀어나온다."""
    p = Part('GateStrap')
    p.prism_x(chamfer_rect(0.0, 0.0, 0.16, 1.38, 0.03), -0.975, 0.975, 'Strap')
    return p


def gate_cap():
    """GateCap — 틈 쪽 끝 쇠테 + 볼트. 위 끝이 원점: y ∈ [−0.4, 0]. x는 파이프 폭 그대로(±0.975 — 밝은 테두리 = 닿는 끝).
    볼트는 앞(−z)에만. 위 관문(틈이 아래)에는 z축으로 180° 돌려 쓴다(앞뒤는 그대로, 위아래·좌우만 뒤집힌다)."""
    p = Part('GateCap')
    p.prism_x(chamfer_rect(-0.2, 0.0, 0.4, 1.46, 0.05), -0.975, 0.975, 'Band')
    for bx in (-0.7, 0.7):
        p.cyl((bx, -0.2, -0.75), 0.07, 0.1, 'z', 8, 'Iron')
    return p


def trestle_bay():
    """TrestleBay — 비계 한 칸 2.4 m. 가운데가 원점: x ∈ [−1.2, 1.2], 윗판 윗면 y = 0, 기둥은 y = −12까지.
    기둥 둘은 칸 양끝 안쪽(±1.04)이라 이어 깔면 칸 사이에 기둥 두 개가 짝으로 선다."""
    p = Part('TrestleBay')
    p.prism_x(chamfer_rect(-0.175, 0.0, 0.35, 1.3, 0.04), -1.2, 1.2, 'Wood')
    for px in (-1.04, 1.04):
        p.prism_y(chamfer_rect(px, 0.0, 0.32, 0.32, 0.03), -12.0, -0.35, 'Wood', uvo=0.21 * (px > 0))
    p.box((0.0, -6.0, 0.0), (1.76, 0.22, 0.2), 'Wood')
    for top, bot in ((-0.5, -5.9), (-6.1, -11.8)):
        p.strut((-1.04, top), (1.04, bot), 0.18, 0.14, -0.08, 'Wood')
        p.strut((1.04, top), (-1.04, bot), 0.18, 0.14, 0.08, 'Wood')
    return p


def rail_span(p=None, length=2.4, y0=0.0, tie_step=0.6):
    """RailSpan — 레일 두 줄 + 침목 2.4 m. 가운데가 원점: x ∈ [−1.2, 1.2], 침목 아랫면 y = 0, 레일 윗면 y = 0.28."""
    p = p or Part('RailSpan')
    n = int(round(length / tie_step))
    for i in range(n):
        p.box((-length / 2 + tie_step * (i + 0.5), y0 + 0.08, 0.0), (0.28, 0.16, 1.1), 'Tie', uvo=0.13 * i)
    for zz in (-0.35, 0.35):
        p.prism_x(chamfer_rect(y0 + 0.22, zz, 0.12, 0.12, 0.02), -length / 2, length / 2, 'Rail')
    return p


def ceiling_rock():
    """CeilingRock — 천장 바위 조각 2 m. 가운데가 원점: x ∈ [−1, 1], 아랫면 y = 0(= 천장선), 위로 4 m.
    아랫면에 밝은 띠(RockEdge, y 0~0.18). 앞면 울퉁불퉁은 x 주기 2라 이어 붙여도 맞물린다."""
    p = Part('CeilingRock')
    p.prism_x(chamfer_rect(0.09, 0.0, 0.18, 2.7, 0.05), -1.0, 1.0, 'RockEdge')
    p.box((0.0, 2.09, 0.05), (2.0, 3.82, 2.5), 'Rock')     # 몸통(앞면은 아래 격자가 덮는다)
    nx, ny, y0, y1, zf = 8, 8, 0.18, 4.0, -1.2
    terms = [(0.45, math.pi, 1.7, 0.3), (0.3, 2 * math.pi, -2.3, 1.1), (0.2, 3 * math.pi, 3.1, 2.0)]
    bm = p.bm
    grid = []
    for j in range(ny + 1):
        y = y0 + (y1 - y0) * j / ny
        fade = min(1.0, (y - y0) / 0.6)
        row = []
        for i in range(nx + 1):
            x = -1.0 + 2.0 * i / nx
            d = fade * (0.22 + 0.22 * psin(x, y, terms))
            dy = 0.12 * fade * psin(x, y, [(1.0, 2 * math.pi, 1.3, 0.7)]) if 0 < j < ny else 0.0
            row.append(bm.verts.new(B((x, y + dy, zf - max(0.0, d)))))
        grid.append(row)
    faces = []
    for j in range(ny):
        for i in range(nx):
            faces.append(bm.faces.new((grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i])))
    for f in faces:
        f.normal_update()
        if f.normal.y > 0:
            f.normal_flip()
    p._faces(faces, 'Rock', closed=False)
    for k, (x, y) in enumerate(((-0.6, 1.2), (0.15, 2.5), (0.68, 1.5))):
        p.lump((x, y, -1.45), 0.3, (1.0, 1.0, 0.45), 'RockDark', seed=40 + k)
    return p


# 굴 입구 아치의 앞뒤. 뒷면 = 판정면(z 0) — 판정면 뒤로 띠가 뻗으면 원근 때문에 통로가 좁아 보이고(10-07 리뷰),
# z 2.2 안개 막 뒤로 가면 흐린 상자로 읽힌다(10-08 캡처). 깊이 2 m 틀로 판정면 앞에만 둔다.
MOUTH_FRONT = -2.0
# 아래 턱은 비계 데크(TrestleBay z −1.3~0, 윗면 = 바닥선) 앞에만 — 데크 윗면과 같은 높이 같은 깊이에 겹치면 깜빡인다.
MOUTH_LOW_BACK = -1.32


def cave_mouth():
    """CaveMouth — 굴 입구 바위 아치(틀). 원점 = 통로 가운데: x ∈ [−5, 5](−x가 바깥 노을 쪽), 안쪽 천장선 y = +7.28, 바닥선 y = −7.28.
    뒷면이 z = 0(판정면)이고 앞으로 2 m(z −2~0): 유니티에서 z 0에 놓으면 전부 판정면·안개 막 앞이다.
    위 덩이는 z −2~0, 아래 턱은 z −2~−1.32(비계 데크 앞). 안쪽 선에 밝은 띠(RockEdge), 앞면에 각진 덩이(RockDark).
    바깥 쪽 외곽은 통로 선에서 가장 튀어나오고 멀어질수록 물러나는 둥근 아치 모양."""
    p = Part('CaveMouth')
    rnd = random.Random(24)
    H = 12.0
    for sgn, z0, z1 in ((1, MOUTH_FRONT, 0.0), (-1, MOUTH_FRONT, MOUTH_LOW_BACK)):
        edge = []
        steps = 9
        for k in range(steps + 1):
            t = k / steps
            x = max(-5.0, -5.0 + 3.2 * t * t + (rnd.random() - 0.5) * 0.5 * (0 < k < steps))
            edge.append((x, sgn * (HALF + 0.17 + (H - 0.17) * t)))   # 바위는 띠(0~0.18) 바로 안쪽부터 — 아랫면이 띠와 겹쳐 깜빡이지 않게
        outline = [(5.0, sgn * (HALF + 0.17)), (5.0, sgn * (HALF + H))] + list(reversed(edge))
        if sgn < 0:
            outline.reverse()
        p.slab(outline, z0, z1, 'Rock')
        # 띠는 바위 앞면보다 0.05 앞으로 — 뒤로는 z1을 넘지 않는다(판정면 뒤로 안 간다).
        p.prism_x(chamfer_rect(sgn * (HALF + 0.09), (z0 - 0.05 + z1) / 2, 0.18, z1 - z0 + 0.05, 0.05), -5.0, 5.0, 'RockEdge')
        for k in range(3 if sgn > 0 else 2):
            x = -2.5 + 2.6 * k + rnd.random() * 0.8
            y = sgn * (HALF + 1.4 + rnd.random() * 3.0)
            p.lump((x, y, z0 - 0.15), 0.55, (1.0, 1.0, 0.45), 'RockDark', seed=60 + k + (sgn > 0) * 10)
    return p


def beam():
    """Beam — 노을 바깥 천장선의 나무 들보 1 m. 가운데가 원점: x ∈ [−0.5, 0.5], 아랫면 y = 0(= 천장선), 높이 0.8, 깊이 1.6."""
    p = Part('Beam')
    p.prism_x(chamfer_rect(0.4, 0.0, 0.8, 1.6, 0.05), -0.5, 0.5, 'Wood')
    return p


def bg_frame():
    """BgFrame — 배경 갱목 틀. 원점 = 윗 들보 윗면 가운데: x ∈ [−3.3, 3.3], y ∈ [−20, 0](기둥이 아래로 20 m)."""
    p = Part('BgFrame')
    p.box((0.0, -0.35, 0.0), (6.6, 0.7, 0.7), 'Wood')
    for sx in (-1, 1):
        p.prism_y(chamfer_rect(sx * 2.8, 0.0, 0.6, 0.6, 0.04), -20.0, -0.7, 'Wood', uvo=0.3 * (sx > 0))
        p.strut((sx * 2.5, -2.5), (sx * 1.2, -0.7), 0.35, 0.4, 0.0, 'Wood')
    return p


def ladder():
    """Ladder — 배경 사다리. 원점 = 위 끝 가운데: x ∈ [−0.41, 0.41], y ∈ [−16, 0], 가로대 0.5 m 간격."""
    p = Part('Ladder')
    for dx in (-0.35, 0.35):
        p.box((dx, -8.0, 0.0), (0.12, 16.0, 0.12), 'Wood')
    y = -0.4
    while y > -16.0:
        p.box((0.0, y, 0.0), (0.7, 0.1, 0.1), 'Wood')
        y -= 0.5
    return p


def walkway():
    """Walkway — 배경 발판. 원점 = 윗면 가운데: x ∈ [−3, 3], 판 y ∈ [−0.25, 0](아래 받침목까지 −0.55), z ∈ [−0.55, 0.55]."""
    p = Part('Walkway')
    p.prism_x(chamfer_rect(-0.125, 0.0, 0.25, 1.0, 0.03), -3.0, 3.0, 'Wood')
    for sx in (-2.4, 0.0, 2.4):
        p.box((sx, -0.4, 0.0), (0.2, 0.3, 1.1), 'Wood')
    return p


def bg_trestle_bay():
    """BgTrestleBay — 먼 층 비계 한 칸 3 m + 레일. 가운데가 원점: x ∈ [−1.5, 1.5], 데크 윗면 y = 0(레일 윗면 0.28), 기둥 y = −20까지.
    높이는 배치에서 사인 곡선으로 칸마다 다르게(기울이지 않고 계단처럼)."""
    p = Part('BgTrestleBay')
    p.prism_x(chamfer_rect(-0.175, 0.0, 0.35, 1.3, 0.04), -1.5, 1.5, 'Wood')
    for px in (-1.3, 1.3):
        p.prism_y(chamfer_rect(px, 0.0, 0.4, 0.4, 0.03), -20.0, -0.35, 'Wood')
    for top, bot in ((-0.5, -6.8), (-7.0, -13.3), (-13.5, -19.8)):
        p.strut((-1.3, top), (1.3, bot), 0.22, 0.16, -0.09, 'Wood')
        p.strut((1.3, top), (-1.3, bot), 0.22, 0.16, 0.09, 'Wood')
    rail_span(p, length=3.0, y0=0.0, tie_step=0.75)
    return p


def mine_cart():
    """MineCart — 광차. 원점 = 바퀴 아래 끝 가운데(레일 윗면에 놓는다): x ∈ [−0.975, 0.975], y ∈ [0, ~1.6]."""
    p = Part('MineCart')
    p.box((0.0, 0.75, 0.0), (1.8, 0.9, 1.2), 'Cart')
    p.box((0.0, 1.2, 0.0), (1.95, 0.14, 1.3), 'CartRim')
    for dx in (-0.55, 0.55):
        p.cyl((dx, 0.24, 0.0), 0.24, 1.3, 'z', 12, 'Iron')
    for i in range(6):
        p.lump(((i % 3 - 1) * 0.45, 1.3, (i // 3 - 0.5) * 0.4), 0.28, (1.0, 1.0, 1.0), 'Ore', seed=80 + i)
    return p


def lantern():
    """Lantern — 매단 랜턴. 원점 = 사슬 위 끝(거는 점): y ∈ [−2.2, 0], 유리 가운데 y = −1.85. 빛은 재질로만(점광원 없음)."""
    p = Part('Lantern')
    c = -1.85
    p.cyl((0.0, -0.75, 0.0), 0.03, 1.5, 'y', 6, 'Iron')
    p.cyl((0.0, c + 0.32, 0.0), 0.26, 0.12, 'y', 8, 'Iron')
    p.cyl((0.0, c, 0.0), 0.3, 0.7, 'y', 8, 'Glass')
    p.cyl((0.0, c - 0.3, 0.0), 0.24, 0.1, 'y', 8, 'Iron')
    return p


def silhouette_strip():
    """SilhouetteStrip — 먼 굴벽·협곡 실루엣 띠 20 m. 카메라를 보는 평면(z = 0). 원점 = 띠 기준선 가운데:
    x ∈ [−10, 10], 들쭉날쭉한 윗선 y ∈ [1.4, 5.4](양끝은 같은 높이라 이어 붙는다), 몸통은 y = −30까지.
    천장 쪽은 z축 180° 돌려 쓰고, 높이는 y 축척으로 조절한다."""
    p = Part('SilhouetteStrip')
    rnd = random.Random(11)
    pts = []; x = -10.0
    while x < 10.0 - 1e-6:
        pts.append((x, 4.0 * (0.35 + rnd.random()) if x > -10.0 else 3.0, 0.0))
        x = min(10.0, x + 1.5 * (0.6 + rnd.random() * 0.8))
        if x > 10.0 - 0.6:
            x = 10.0
    pts.append((10.0, 3.0, 0.0))
    p.flat(pts + [(10.0, -30.0, 0.0), (-10.0, -30.0, 0.0)], 'Silhouette')
    return p


PARTS = [gate_plank, gate_strap, gate_cap, trestle_bay, rail_span, ceiling_rock, cave_mouth, beam,
         bg_frame, ladder, walkway, bg_trestle_bay, mine_cart, lantern, silhouette_strip]


def export(part):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    part.uv_box()
    me = bpy.data.meshes.new(part.name)
    part.bm.to_mesh(me)
    part.bm.free()
    for s in part.slots:
        me.materials.append(bpy.data.materials.get(s) or bpy.data.materials.new(s))
    ob = bpy.data.objects.new(part.name, me)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    path = os.path.join(MODEL_DIR, part.name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
                             axis_forward=AXIS_FORWARD, axis_up=AXIS_UP, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, use_mesh_modifiers=False, mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
    return path


# ════════════════════════ 무늬 텍스처 ════════════════════════
N = 512


def periodic_noise(u, v, rng, n_terms, fmax):
    """정수 주파수 사인 합 — 0~1 정사각형에서 양방향으로 이어진다."""
    out = np.zeros_like(u)
    for _ in range(n_terms):
        i, j = rng.integers(-fmax, fmax + 1, 2)
        if i == 0 and j == 0:
            continue
        a = 1.0 / math.hypot(i, j)
        out += a * np.sin(2 * np.pi * (i * u + j * v) + rng.uniform(0, 2 * np.pi))
    return out / (np.abs(out).max() + 1e-9)


def to_srgb(lin):
    lin = np.clip(lin, 0.0, 1.0)
    return np.where(lin <= 0.0031308, lin * 12.92, 1.055 * np.power(lin, 1 / 2.4) - 0.055)


def save_gray(name, lin):
    """선형 배율(기본색에 곱할 값)을 sRGB로 적어 PNG로. 유니티 선형 공간에서 다시 같은 배율이 된다."""
    s = to_srgb(lin).astype(np.float32)
    rgba = np.stack([s, s, s, np.ones_like(s)], axis=-1)
    img = bpy.data.images.new(name, N, N, alpha=False)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = os.path.join(TEX_DIR, name + '.png')
    img.file_format = 'PNG'
    img.save()
    return img.filepath_raw


def bake_textures():
    """시안의 무늬(파동 = 나무결, 보로노이 = 바위)를 주기 함수로 다시 만든다. 값은 기본색에 곱하는 0.72~1.0 배율."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    u, v = np.meshgrid(np.arange(N) / N, np.arange(N) / N)   # v = 행(아래→위)
    rng = np.random.default_rng(7)
    # 나무결: v를 따라 8줄, 낮은 주파수 일그러뜨림(시안 Distortion 6) + 잔결
    warp = periodic_noise(u, v, rng, 10, 3)
    fine = periodic_noise(u, v, rng, 24, 24)
    bands = 0.5 + 0.5 * np.sin(2 * np.pi * (8 * v + 0.9 * warp + 0.08 * fine))
    wood = 0.76 + 0.24 * np.power(bands, 1.6) + 0.03 * fine
    paths = [save_gray('wood_grain', np.clip(wood, 0.72, 1.0))]
    # 바위: 이어지는 보로노이(점 14개, 9칸 감싸기) — F1 거리로 밝기, F2−F1이 작은 곳(경계)은 금
    pts = rng.random((14, 2))
    f1 = np.full(u.shape, 9.0); f2 = np.full(u.shape, 9.0)
    for px, py in pts:
        for ox in (-1, 0, 1):
            for oy in (-1, 0, 1):
                d = np.hypot(u - (px + ox), v - (py + oy))
                f2 = np.where(d < f1, f1, np.minimum(f2, d)); f1 = np.minimum(f1, d)
    f1n = f1 / f1.max()
    crack = np.clip((f2 - f1) / 0.025, 0.0, 1.0)
    grain = periodic_noise(u, v, rng, 30, 32)
    rock = (0.74 + 0.26 * f1n) * (0.84 + 0.16 * crack) + 0.025 * grain
    paths.append(save_gray('rock', np.clip(rock, 0.6, 1.0)))
    for pth, arr in zip(paths, (wood, rock)):
        print(f'TEX {os.path.basename(pth)} {N}x{N} mean={float(np.clip(arr, 0, 1).mean()):.3f} '
              f'min={float(arr.min()):.3f} max={float(min(arr.max(), 1.0)):.3f}')


def main():
    os.makedirs(MODEL_DIR, exist_ok=True)
    os.makedirs(TEX_DIR, exist_ok=True)
    print(f'mine_parts: Art={ART} axis_forward={AXIS_FORWARD} axis_up={AXIS_UP}')
    for make in PARTS:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        part = make()
        if ONLY and part.name not in ONLY:
            part.bm.free()
            continue
        tris = part.tris(); (x0, y0, z0), (x1, y1, z1) = part.bounds(); slots = ','.join(part.slots)
        path = export(part)
        print(f'PART {part.name:16s} tris={tris:4d} x=[{x0:.3f},{x1:.3f}] y=[{y0:.3f},{y1:.3f}] z=[{z0:.3f},{z1:.3f}] slots={slots} -> {os.path.relpath(path, ART)}')
    if not ONLY:
        bake_textures()
    print('mine_parts: DONE')


main()
