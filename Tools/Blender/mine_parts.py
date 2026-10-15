"""Flappy 광산 부품 — 원점 기준 단위 부품을 FBX로, 나무결·바위 무늬를 512² 타일 PNG로 뽑는다.

실행:  blender -b -P Tools/Blender/mine_parts.py -- <LeagueOfPhysical-Client/Assets/Art 절대경로> [부품 이름 ...]
출력:  <Art>/Models/Mine/<부품>.fbx,  <Art>/Textures/Mine/{wood_grain,rock}.png
       부품 이름을 주면 그 부품 FBX만 다시 뽑는다(텍스처는 안 건드린다) — FBX는 뽑을 때마다 바이트가 달라져
       안 바뀐 부품까지 커밋에 섞이지 않게. 이름에 'strata'를 넣으면 지층 띠 텍스처 strata.png만 함께 뽑는다.

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
UV_TILE = {'Plank': 1.0, 'WoodDark': 1.0, 'Wood': 1.0, 'Tie': 1.0, 'Rock': 2.0, 'RockDark': 2.0,
           'Timber': 1.0, 'SignWood': 1.0, 'Strata': 1.0, 'TimberDim': 1.0}

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
        self.tile = {}           # 슬롯별 UV_TILE 덮어쓰기(이 부품만) — 축척해 쓰는 부품이 단위 크기에 맞춘다

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

    def slab(self, outline, z0, z1, mat, uvo=0.0):
        """xy 외곽선(오목 가능)을 z0~z1로 두께를 준 판. 앞뒤 면은 삼각형으로 쪼갠다."""
        bm = self.bm
        v0 = [bm.verts.new(B((p[0], p[1], z0))) for p in outline]
        v1 = [bm.verts.new(B((p[0], p[1], z1))) for p in outline]
        n = len(outline)
        sides = [bm.faces.new((v0[i], v0[(i + 1) % n], v1[(i + 1) % n], v1[i])) for i in range(n)]
        front = bm.faces.new(v0); back = bm.faces.new(list(reversed(v1)))
        tri = bmesh.ops.triangulate(bm, faces=[front, back])['faces']
        return self._faces(sides + tri, mat, uvo)

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
            slot = self.slots[f.material_index]
            tile = self.tile.get(slot, UV_TILE.get(slot, 1.0))
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


# ──────────── 펼치기 1차(10-10) — 갱 입구·수직 갱·계단 내리막·고수 갈림길 ────────────
# "x 축척"·"y 축척" 부품은 그 축 길이가 1 m인 단위 조각이다. 축척하면 그 축 방향으로 늘어나므로
# 그 축을 따라 끝없이 이어지는 모양(각재·띠)만 넣고, 점 같은 장식(못·쇠띠·돌)은 넣지 않는다 — 늘어나 타원이 된다.
# 지층 띠(Strata)는 띠가 v(= y)로만 바뀌고 u(= x)로는 거의 같은 무늬라 x로 늘리거나 좁혀도 그대로 읽힌다.

PORTAL_HALF = 7.28      # 평지 통로 반 높이(14.56 / 2)
PORTAL_RIM = 1.72       # 문틀이 통로 위·아래 밖으로 덮는 높이 — 18 = 14.56 + 2 × 1.72
ENT_Z0, ENT_Z1 = 0.06, 0.66   # 입구 문틀 깊이: 판정면(z 0) 뒤 0.6 m
BORE_HALF = 1.55        # 굴 틈 3.1의 반
ROCK_Z0, ROCK_Z1 = -1.3, 1.3  # 바위 몸통 깊이(CeilingRock과 같은 앞면 −1.3)


def wedge(p, a, b, c, z0, z1, mat, uvo=0.0):
    """xy 삼각형 a·b·c를 z0~z1로 뽑은 쐐기."""
    return p._ring_prism([(q[0], q[1], z0) for q in (a, b, c)], [(q[0], q[1], z1) for q in (a, b, c)], mat, uvo)


def entrance_frame():
    """EntranceFrame — 갱 입구 문틀. 원점 = 문턱(바닥선) 가운데: x ∈ [−2.5, 2.5], y ∈ [−1.72, 16.28]
    (통로 14.56 = y 0~14.56, 위·아래로 1.72씩 밖을 덮는다), z ∈ [0.06, 0.66](전부 판정면 뒤).
    기둥 0.6 × 0.6 둘(x ±2.2), 문턱 밑 깔목, 상인방 두 겹, 위 모서리 버팀목, 쐐기, 쇠띠, 볼트."""
    p = Part('EntranceFrame')
    zc, d = (ENT_Z0 + ENT_Z1) / 2, ENT_Z1 - ENT_Z0
    top = 2 * PORTAL_HALF                              # 천장선 y 14.56
    lo, hi = -PORTAL_RIM, top + PORTAL_RIM             # −1.72, 16.28
    p.prism_x(chamfer_rect(lo + 0.35, zc, 0.7, d, 0.05), -2.5, 2.5, 'Timber')               # 문턱 깔목 y −1.72~−1.02
    for sx in (-1, 1):
        p.prism_y(chamfer_rect(sx * 2.2, zc, 0.6, d, 0.05), lo + 0.7, top + 0.06, 'Timber', uvo=0.31 * (sx > 0))
    p.prism_x(chamfer_rect(top + 0.06 + 0.4, zc, 0.8, d, 0.05), -2.5, 2.5, 'Timber', uvo=0.17)   # 상인방 y 14.62~15.42
    p.prism_x(chamfer_rect((top + 0.86 + hi) / 2, zc, hi - top - 0.86, d, 0.05), -2.5, 2.5, 'Timber', uvo=0.53)  # 머리목 ~16.28
    for sx in (-1, 1):
        p.strut((sx * 1.9, top - 2.0), (sx * 0.8, top + 0.06), 0.34, 0.4, zc + 0.04, 'Timber', uvo=0.4)   # 위 모서리 버팀목
        ix = sx * 1.9                                                                                  # 기둥 안쪽 면
        wedge(p, (ix, top + 0.06), (ix, top - 0.42), (ix - sx * 0.36, top + 0.06), zc - 0.2, zc + 0.2, 'Timber')
        wedge(p, (ix, lo + 0.7), (ix, lo + 1.18), (ix - sx * 0.36, lo + 0.7), zc - 0.2, zc + 0.2, 'Timber')
        for by in (top - 0.5, -0.45):
            p.box((sx * 2.19, by, zc), (0.62, 0.14, d + 0.04), 'Strap')                                # 쇠띠 z 0.04~0.68, 바깥 면은 기둥과 같게(폭 5.0)
    for bx in (-1.8, -0.6, 0.6, 1.8):
        p.cyl((bx, top + 0.46, ENT_Z0 - 0.0), 0.07, 0.06, 'z', 8, 'Iron')                             # 볼트 z 0.03~0.09
    return p


def entrance_sign():
    """EntranceSign — 갱 입구 간판(글자 없음). 가운데가 원점: x ∈ [−1.5, 1.5], y ∈ [−0.5, 0.5], 판 z ∈ [−0.06, 0.06].
    앞(−z)에 그림 홈: 왼쪽 곡괭이, 오른쪽 광차(SignInlay, z −0.085~−0.06). 양끝 쇠띠."""
    p = Part('EntranceSign')
    for i, yc in enumerate((-1 / 3, 0.0, 1 / 3)):
        p.prism_x(chamfer_rect(yc, 0.0, 1 / 3 - 0.012, 0.12, 0.025), -1.5, 1.5, 'SignWood', uvo=0.29 * i)
    for sx in (-1, 1):
        p.box((sx * 1.28, 0.0, 0.0), (0.12, 1.0, 0.14), 'Iron')
    z0, z1 = -0.085, -0.06
    # 곡괭이: 자루 + 휜 머리
    a, b = (-0.98, -0.36), (-0.4, 0.28)
    p.strut(a, b, 0.08, z1 - z0, (z0 + z1) / 2, 'SignInlay')
    L = math.hypot(b[0] - a[0], b[1] - a[1]); dx, dy = (b[0] - a[0]) / L, (b[1] - a[1]) / L
    px, py = -dy, dx
    head = [(b[0] + s * 0.34 * px - s * s * 0.1 * dx, b[1] + s * 0.34 * py - s * s * 0.1 * dy) for s in (-1, -0.5, 0, 0.5, 1)]
    for q0, q1 in zip(head, head[1:]):
        p.strut(q0, q1, 0.09, z1 - z0, (z0 + z1) / 2, 'SignInlay')
    # 광차: 몸통 사다리꼴 + 바퀴 + 레일 줄
    p.slab([(0.3, -0.12), (0.98, -0.12), (1.1, 0.26), (0.18, 0.26)], z0, z1, 'SignInlay')
    for wx in (0.45, 0.83):
        p.cyl((wx, -0.22, (z0 + z1) / 2), 0.1, z1 - z0, 'z', 10, 'SignInlay', smooth=False)
    p.strut((0.1, -0.35), (1.18, -0.35), 0.04, z1 - z0, (z0 + z1) / 2, 'SignInlay')
    return p


LANTERN_SAG = 0.35


def lantern_string():
    """LanternString — 처진 줄 + 작은 랜턴 4개. 원점 = 왼끝(거는 점): x ∈ [0, 1](x 축척으로 늘린다), 오른끝도 y 0.
    줄은 y = −0.35·4t(1−t)로 처지고, 랜턴은 t = 0.2·0.4·0.6·0.8에 매달려 아래 끝 y ≈ −0.66.
    주의: x로 늘리면 랜턴도 그만큼 넓어진다(작은 랜턴 폭 0.2)."""
    p = Part('LanternString')
    n = 16
    pts = [(i / n, -LANTERN_SAG * 4 * (i / n) * (1 - i / n)) for i in range(n + 1)]
    for q0, q1 in zip(pts, pts[1:]):
        p.strut(q0, q1, 0.035, 0.035, 0.0, 'Rope')
    for t in (0.2, 0.4, 0.6, 0.8):
        y = -LANTERN_SAG * 4 * t * (1 - t)
        p.cyl((t, y - 0.06, 0.0), 0.012, 0.12, 'y', 6, 'Iron')
        p.cyl((t, y - 0.14, 0.0), 0.085, 0.05, 'y', 8, 'Iron')
        p.cyl((t, y - 0.27, 0.0), 0.1, 0.22, 'y', 8, 'Glass')
        p.cyl((t, y - 0.4, 0.0), 0.075, 0.04, 'y', 8, 'Iron')
    return p


def shaft_timber():
    """ShaftTimber — 갱도 벽 가로 갱목 한 줄. 원점 = 왼끝 아래: x ∈ [0, 1](x 축척), y ∈ [0, 0.4], z ∈ [−0.5, 0.5].
    앞뒤 모서리만 깎아(x 방향 이음 없음) 층층이 쌓으면 줄 사이에 홈이 보인다(간격 > 0.4일 때 틈)."""
    p = Part('ShaftTimber')
    p.prism_x(chamfer_rect(0.2, 0.0, 0.4, 1.0, 0.06), 0.0, 1.0, 'Timber')
    return p


def cage_rail():
    """CageRail — 승강 케이지 안내 레일 두 줄 + 줄. 원점 = 아래 끝 가운데: y ∈ [0, 1](y 축척으로 늘린다),
    안내 각재 x ±0.95(케이지 폭 1.6 바로 밖), 안쪽 면에 쇠 레일, 가운데 매단 줄. z ∈ [−0.11, 0.11]."""
    p = Part('CageRail')
    for sx in (-1, 1):
        p.prism_y(chamfer_rect(sx * 0.95, 0.0, 0.16, 0.22, 0.03), 0.0, 1.0, 'Timber', uvo=0.4 * (sx > 0))
        p.box((sx * 0.855, 0.5, 0.0), (0.03, 1.0, 0.08), 'Rail')
    p.cyl((0.0, 0.5, 0.0), 0.025, 1.0, 'y', 6, 'Rope')
    return p


def cage():
    """Cage — 나무·쇠 승강 케이지. 가운데가 원점: x ∈ [−0.8, 0.8], y ∈ [−1, 1], z ∈ [−0.6, 0.6].
    바닥·지붕 판, 쇠 모서리 기둥, 아래 반은 판자 벽, 위 반은 쇠창살, 가로 쇠띠."""
    p = Part('Cage')
    for yy in (-0.93, 0.93):
        p.box((0.0, yy, 0.0), (1.6, 0.14, 1.2), 'Wood', uvo=0.2 * (yy > 0))
    for sx in (-1, 1):
        for sz in (-1, 1):
            p.box((sx * 0.76, 0.0, sz * 0.56), (0.08, 1.72, 0.08), 'Iron')
        p.box((sx * 0.76, -0.55, 0.0), (0.06, 0.62, 1.04), 'Wood')
    for sz in (-1, 1):
        p.box((0.0, -0.55, sz * 0.56), (1.44, 0.62, 0.06), 'Wood')
        for i in range(7):
            p.box((-0.6 + 0.2 * i, 0.27, sz * 0.56), (0.04, 1.04, 0.04), 'Iron')
        for by in (-0.22, 0.8):
            p.box((0.0, by, sz * 0.57), (1.56, 0.08, 0.07), 'Strap')
    return p


BENCH_DROP = 4.5


def bench_face():
    """BenchFace — 계단식 바위 단(한 단). 원점 = 단 윗면 앞 모서리의 왼끝(윗면 = 바닥선): x ∈ [0, 1](x 축척 = 단 길이),
    y ∈ [−4.5, 0](한 단 높이 4.5 = 아랫단 바닥선까지), 몸통 z ∈ [−1.3, 1.3], 위 밝은 띠(RockEdge) y −0.18~0 · z −1.35~1.3.
    앞면은 지층 띠(Strata, 4.5 m에 띠 4줄 — 아랫단도 4.5 아래라 무늬가 이어진다). 천장 쪽 단은 z축 180° 돌려 쓴다."""
    p = Part('BenchFace')
    p.tile['Strata'] = BENCH_DROP
    p.prism_x(chamfer_rect(-0.09, -0.025, 0.18, 2.65, 0.05), 0.0, 1.0, 'RockEdge')
    p.box((0.5, -(BENCH_DROP + 0.18) / 2, 0.0), (1.0, BENCH_DROP - 0.18, ROCK_Z1 - ROCK_Z0), 'Strata')
    return p


def short_scaffold():
    """ShortScaffold — 단 앞면에 거는 짧은 비계 + 사다리. 원점 = 아래 가운데: x ∈ [−1.2, 1.2], y ∈ [0, 2.5], z ∈ [−0.35, 0.35].
    기둥 둘(x ±1.05), 윗판(y 2.3~2.5), 가운데 띠장, 엇댄 버팀목, 앞(−z)에 사다리(x −0.65~−0.25)."""
    p = Part('ShortScaffold')
    for sx in (-1, 1):
        p.prism_y(chamfer_rect(sx * 1.05, 0.0, 0.2, 0.2, 0.02), 0.0, 2.3, 'Wood', uvo=0.3 * (sx > 0))
    p.prism_x(chamfer_rect(2.4, 0.0, 0.2, 0.7, 0.03), -1.2, 1.2, 'Wood', uvo=0.6)
    p.box((0.0, 1.15, 0.1), (1.9, 0.14, 0.12), 'Wood')
    p.strut((-0.95, 0.1), (0.95, 2.25), 0.12, 0.08, 0.18, 'Wood')
    for xr in (-0.65, -0.25):
        p.box((xr, 1.25, -0.22), (0.07, 2.5, 0.07), 'Wood')
    y = 0.25
    while y < 2.45:
        p.box((-0.45, y, -0.22), (0.4, 0.05, 0.05), 'Wood')
        y += 0.3
    return p


def rock_mass():
    """RockMass — 지층 무늬 바위 덩어리 조각. 원점 = 왼끝 아래: x ∈ [0, 1], y ∈ [0, 1](x·y 축척), z ∈ [−1.3, 1.3].
    UV는 단위 상자에 텍스처 한 장(띠 4줄) — 축척한 높이에 띠 4줄. 테두리는 없다(경계 띠는 MergeSlope·따로)."""
    p = Part('RockMass')
    p.box((0.5, 0.5, 0.0), (1.0, 1.0, ROCK_Z1 - ROCK_Z0), 'Strata')
    return p


def bore_frame():
    """BoreFrame — 굴 입구 문틀(갱목). 원점 = 굴 입구 중심(틈 가운데): x ∈ [−0.3, 1.0], y ∈ [−1.75, 1.75](틈 ±1.55 바로 밖에
    0.2 두께 상인방·문턱), 상인방·문턱 z ∈ [−1.6, 0.5](바위 앞면 −1.3보다 0.3 앞). 틈을 가로지르는 문설주는 판정면 뒤
    (x −0.3~0.1, z 0.2~0.5)라 새를 가리지 않는다. 쇠띠·볼트."""
    p = Part('BoreFrame')
    for sy in (-1, 1):
        yc = sy * (BORE_HALF + 0.1)
        p.prism_x(chamfer_rect(yc, -0.55, 0.2, 2.1, 0.03), -0.3, 1.0, 'Timber', uvo=0.3 * (sy > 0))
        p.box((0.45, yc, -0.55), (0.1, 0.21, 2.14), 'Strap')
        for bx in (-0.12, 0.8):
            p.cyl((bx, yc, -1.62), 0.05, 0.06, 'z', 8, 'Iron')
    p.prism_y(chamfer_rect(-0.1, 0.35, 0.4, 0.3, 0.03), -BORE_HALF, BORE_HALF, 'Timber', uvo=0.7)
    return p


def bolt_glyph(s, cx=0.0, cy=0.0):
    """굵은 ⚡ 외곽선(높이 s, 가운데 정렬)."""
    pts = [(-0.05, 0.5), (0.30, 0.5), (0.08, 0.08), (0.28, 0.08), (-0.12, -0.5), (0.0, -0.06), (-0.22, -0.06)]
    return [(cx + (x - 0.04) * s, cy + y * s) for x, y in pts]


def bolt_sign():
    """BoltSign — 판자 위 굵은 번개(⚡). 가운데가 원점: x ∈ [−0.95, 0.95], y ∈ [−0.55, 0.55], 판 z ∈ [−0.06, 0.06].
    어두운 테두리(BoltRim, z −0.08~−0.06, 높이 1.09) 위에 노란 면(BoltGlow — 빛 재질, z −0.11~−0.08, 높이 0.95)."""
    p = Part('BoltSign')
    for i, yc in enumerate((-0.3667, 0.0, 0.3667)):
        p.prism_x(chamfer_rect(yc, 0.0, 0.36, 0.12, 0.02), -0.95, 0.95, 'SignWood', uvo=0.23 * i)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.cyl((sx * 0.8, sy * 0.3667, -0.07), 0.04, 0.04, 'z', 8, 'Iron')
    p.slab(bolt_glyph(0.95 * 1.15), -0.08, -0.06, 'BoltRim')
    p.slab(bolt_glyph(0.95), -0.11, -0.08, 'BoltGlow')
    return p


def pad_glow():
    """PadGlow — ⚡ 발판. 가운데가 원점: x ∈ [−0.6, 0.6], y ∈ [−0.15, 0.15], z ∈ [−0.67, 0.65].
    어두운 틀(PadFrame, y −0.15~0.11) 위에 빛 판(PadGlow, y 0.11~0.15), 앞면(z −0.67~−0.65)에 > > > (PadGlow)."""
    p = Part('PadGlow')
    p.box((0.0, -0.02, 0.0), (1.2, 0.26, 1.3), 'PadFrame')
    p.box((0.0, 0.13, 0.0), (1.0, 0.04, 1.1), 'PadGlow')
    for cx in (-0.33, 0.0, 0.33):
        p.strut((cx - 0.08, 0.07), (cx + 0.07, -0.02), 0.045, 0.02, -0.66, 'PadGlow')
        p.strut((cx - 0.08, -0.11), (cx + 0.07, -0.02), 0.045, 0.02, -0.66, 'PadGlow')
    return p


DIV_HALF = 0.6


def divider_wall():
    """DividerWall — 두꺼운 갱목 칸막이 1 m. 원점 = 왼끝 가운데: x ∈ [0, 1](x 축척), y ∈ [−0.6, 0.6](칸막이 높이 1.2 그대로),
    위·아래 밝은 테(Band, y ±0.42~±0.6, z ±0.75 — 닿는 면) 사이에 각재 두 줄(Timber, z ±0.7). 쇠띠는 x로 늘어나므로 넣지 않는다."""
    p = Part('DividerWall')
    for sy in (-1, 1):
        p.prism_x(chamfer_rect(sy * (DIV_HALF - 0.09), 0.0, 0.18, 1.5, 0.03), 0.0, 1.0, 'Band')
        p.prism_x(chamfer_rect(sy * 0.21, 0.0, 0.4, 1.4, 0.05), 0.0, 1.0, 'Timber', uvo=0.35 * (sy > 0))
    return p


def merge_slope():
    """MergeSlope — 비스듬히 열리는 바위 쐐기. 원점 = 왼끝 아래: 1 × 1(x·y 축척), 바위는 빗변 (0,0)→(1,1)의 위쪽(왼쪽 위 삼각형),
    빗변 쪽에 밝은 띠(RockEdge, 두께 0.13 수직, z −1.35~1.3). 천장이 오른쪽으로 갈수록 올라가며 열린다.
    바닥 쪽은 y 축척을 음수로(위아래 뒤집기). 몸통 z ∈ [−1.3, 1.3], 지층 띠는 RockMass와 같은 단위 UV."""
    p = Part('MergeSlope')
    a = 0.13 * math.sqrt(2)
    p.slab([(0.0, a), (1.0 - a, 1.0), (0.0, 1.0)], ROCK_Z0, ROCK_Z1, 'Strata')
    p.slab([(0.0, 0.0), (1.0, 1.0), (1.0 - a, 1.0), (0.0, a)], ROCK_Z0 - 0.05, ROCK_Z1, 'RockEdge')
    return p


NEW_PARTS = [entrance_frame, entrance_sign, lantern_string, shaft_timber, cage_rail, cage, bench_face, short_scaffold,
             rock_mass, bore_frame, bolt_sign, pad_glow, divider_wall, merge_slope]


# ──────────── 펼치기 2차(10-12) — 출구·결승선 ────────────
# 크기·깊이는 MineDressingLayout 상수(CliffEdgeZ/CliffEdgeHalfDepth, FinishFlagZ)와 레이아웃 테스트 PartDepth 표가 가정한 값 그대로.

def exit_sign():
    """ExitSign — 갱 출구 간판(글자 없음, EntranceSign과 짝). 가운데가 원점: x ∈ [−1.5, 1.5], y ∈ [−0.5, 0.5], 판 z ∈ [−0.06, 0.06],
    쇠띠 z ±0.07. 앞(−z)에 그림 홈(SignInlay, z −0.085~−0.06): 왼쪽 지평선 위 해(반원 + 빛살), 오른쪽 → 화살표. 슬롯은 EntranceSign과 같다."""
    p = Part('ExitSign')
    for i, yc in enumerate((-1 / 3, 0.0, 1 / 3)):
        p.prism_x(chamfer_rect(yc, 0.0, 1 / 3 - 0.012, 0.12, 0.025), -1.5, 1.5, 'SignWood', uvo=0.37 * i + 0.11)
    for sx in (-1, 1):
        p.box((sx * 1.28, 0.0, 0.0), (0.12, 1.0, 0.14), 'Iron')
    z0, z1 = -0.085, -0.06
    zc, d = (z0 + z1) / 2, z1 - z0
    # 해: 지평선(가로 줄) 위 반원 + 빛살 다섯
    cx, cy, r = -0.68, -0.16, 0.24
    p.strut((-1.1, cy), (-0.26, cy), 0.045, d, zc, 'SignInlay')
    p.slab([(cx + r * math.cos(math.pi * k / 12), cy + r * math.sin(math.pi * k / 12)) for k in range(13)], z0, z1, 'SignInlay')
    for k in range(5):
        a = math.pi * (k + 1) / 6
        p.strut((cx + 0.31 * math.cos(a), cy + 0.31 * math.sin(a)), (cx + 0.43 * math.cos(a), cy + 0.43 * math.sin(a)), 0.05, d, zc, 'SignInlay')
    # 화살표(→ 출구 밖): 몸통 + 머리
    p.strut((0.22, 0.0), (0.84, 0.0), 0.13, d, zc, 'SignInlay')
    p.slab([(0.78, 0.27), (1.12, 0.0), (0.78, -0.27)], z0, z1, 'SignInlay')
    return p


CLIFF_HALF = 1.0   # 절벽 몸통 반 깊이 = MineDressingLayout.CliffEdgeHalfDepth
# 오른끝(절벽 가장자리) 단면 — 위 끝 (1, 0)에서 아래로 갈수록 조금씩 안으로 들어가는 울퉁불퉁한 바위 면(단위 좌표, x·y 축척).
CLIFF_FACE = [(1.0, 0.0), (0.986, -0.025), (0.962, -0.06), (0.97, -0.11), (0.945, -0.18), (0.952, -0.26),
              (0.92, -0.36), (0.9, -0.5), (0.91, -0.62), (0.87, -0.78), (0.84, -1.0)]


def cliff_edge():
    """CliffEdge — 출구 밖 산이 끝나는 절벽 끝 바위. 원점 = 위 끝 왼쪽: x ∈ [0, 1], y ∈ [−1, 0](x·y 축척), z ∈ [−1.0, 1.0].
    몸통은 바위 덩어리(Strata — 굽기가 월드 UV로 rock_paint를 깐다, RockMass와 같다). 오른끝은 아래로 갈수록 안으로 들어가는 울퉁불퉁한 절벽 면.
    윗면(y 0, 위를 보는 면 하나)만 CliffSun — 노을 빛을 받은 따뜻한 바위 색(밝은 테 아님: 앞을 보는 두께가 없어 선으로 안 보인다)."""
    p = Part('CliffEdge')
    outline = [(0.0, 0.0)] + CLIFF_FACE + [(0.0, -1.0)]
    faces = p.slab(outline, -CLIFF_HALF, CLIFF_HALF, 'Strata')
    sun = p._slot('CliffSun')
    p.bm.normal_update()
    for f in faces:
        if f.normal.z > 0.99:          # 블렌더 +Z = 게임 +y(위)
            f.material_index = sun
    return p


FLAG_COLS, FLAG_ROWS = 6, 4
FLAG_X, FLAG_Y0, FLAG_Y1 = 0.45, 1.86, 2.72   # 체크 깃발 x ±0.45, 아래 1.86 ~ 가로대 2.72
FLAG_Z0, FLAG_Z1 = -0.1, -0.075               # 깃발 천은 기둥 앞(카메라 쪽)


def finish_flag():
    """FinishFlag — 결승선 깃발대. 원점 = 아래 가운데(기둥 밑): x ∈ [−0.5, 0.5], y ∈ [0, 3.0], z ∈ [−0.1, 0.1].
    나무 기둥(Timber, r 0.06, 결승선 x = 원점) + 꼭대기 가로대에 매단 체크 깃발 6 × 4(FlagBrown·FlagCream — 따뜻한 갈색·크림, 형광 없음)
    + 밑받침(Iron, z ±0.1). 깃발 천은 기둥 앞 z −0.1~−0.075."""
    p = Part('FinishFlag')
    p.box((0.0, 0.06, 0.0), (0.36, 0.12, 0.2), 'Iron')
    p.cyl((0.0, 1.47, 0.0), 0.06, 2.82, 'y', 10, 'Timber')
    p.cyl((0.0, 2.94, 0.0), 0.085, 0.12, 'y', 10, 'Timber')
    p.box((0.0, 2.76, 0.0), (0.98, 0.08, 0.1), 'Timber')
    cw, ch = 2 * FLAG_X / FLAG_COLS, (FLAG_Y1 - FLAG_Y0) / FLAG_ROWS
    for i in range(FLAG_COLS):
        for j in range(FLAG_ROWS):
            slot = 'FlagCream' if (i + j) % 2 == 0 else 'FlagBrown'
            p.box((-FLAG_X + (i + 0.5) * cw, FLAG_Y0 + (j + 0.5) * ch, (FLAG_Z0 + FLAG_Z1) / 2), (cw, ch, FLAG_Z1 - FLAG_Z0), slot)
    return p


SPREAD2_PARTS = [exit_sign, cliff_edge, finish_flag]


# ════════════════════════ 바위 면 장식(10-10 Task 7 Fix 1b) ════════════════════════
#  사용자 10-10 "차분해지긴 했는데 너무 휑하다": 판정 고체 바위 면 위에 붙이는 납작한 장식. 전부 뒷면 z = 0, 앞면 −두께(얇다 —
#  원근으로 옆·윗면이 밝은 띠로 드러나지 않게). 밝은 테두리·볼트·점 무늬는 없다(밝은 것 = 닿는 것, 판정선 위 얇은 테만).
#  재질 슬롯은 어두운 전용 슬롯(Seam·StrataPatch·TimberDim·OreDim·RailDim) — 색은 FlappyMineMaterials가 바위 색에서 정한다.
DECOR_T = 0.01   # 무늬(지층 렌즈·금·광맥) 두께 — 0.04는 원근으로 윗면이 1~2 px 밝은 선으로 보였다(cap3 1차)
PROP_T = 0.12    # 박힌 갱목·레일 조각 두께


def _ribbon(center, half_width):
    """중심선 점들(x, y)과 점마다 반폭 → 위 변 + 거꾸로 아래 변 외곽선."""
    top = [(x, y + w) for (x, y), w in zip(center, half_width)]
    bot = [(x, y - w) for (x, y), w in zip(center, half_width)]
    return top + list(reversed(bot))


def strata_lens():
    """StrataLens — 바위 면의 은은한 지층 렌즈(물결 진 긴 띠). 원점 = 왼끝 가운데: x ∈ [0, 1](x 축척 = 길이), y ∈ [−0.5, 0.5](y 축척 = 두께),
    z ∈ [−0.01, 0]. 양끝이 뾰족하게 가늘어져 바위 무늬처럼 녹아든다. 슬롯 StrataPatch(굽기가 층에 따라 조금 어둡게/밝게 갈아 끼운다)."""
    p = Part('StrataLens')
    n = 28
    top, bot = [], []
    for i in range(n + 1):
        t = i / n
        s = math.sin(math.pi * t) ** 0.7
        top.append((t, 0.5 * s * (1 + 0.18 * math.sin(3 * math.pi * t + 0.4))))
        bot.append((t, -0.5 * s * (1 + 0.12 * math.sin(5 * math.pi * t + 1.3))))
    outline = top + list(reversed(bot[1:-1]))
    p.slab(outline, -DECOR_T, 0.0, 'StrataPatch')
    return p


def rock_seam():
    """RockSeam — 바위 면의 큰 금(이음매). 원점 = 왼끝 가운데: x ∈ [0, 1](x 축척 = 길이), y는 지그재그 ±0.1 + 갈래 −0.28·+0.2(축척 1),
    z ∈ [−0.01, 0]. 끝이 가늘어지는 리본 + 짧은 갈래 둘. 슬롯 Seam(바위보다 조금 어둡다)."""
    p = Part('RockSeam')
    ts = [0.0, 0.12, 0.25, 0.37, 0.5, 0.63, 0.76, 0.88, 1.0]
    ys = [0.0, 0.06, -0.04, 0.08, 0.02, -0.06, 0.03, -0.05, 0.0]
    ws = [0.006 + 0.032 * math.sin(math.pi * t) ** 0.5 for t in ts]
    p.slab(_ribbon(list(zip(ts, ys)), ws), -DECOR_T, 0.0, 'Seam')
    p.slab([(0.49, 0.0), (0.53, 0.02), (0.72, -0.28)], -DECOR_T, 0.0, 'Seam')
    p.slab([(0.24, -0.03), (0.27, -0.05), (0.34, 0.2)], -DECOR_T, 0.0, 'Seam')
    return p


def timber_set():
    """TimberSet — 바위에 박힌 갱목 받침(기둥 둘 + 상인방 + 귀 받침). 원점 = 아래 가운데: x ∈ [−1.1, 1.1], y ∈ [0, 2.46],
    z ∈ [−0.12, 0]. 상인방 아랫면 y = 2.2(랜턴을 거는 자리). 슬롯 TimberDim(어두운 갱목)."""
    p = Part('TimberSet')
    zc = -PROP_T / 2
    for i, px in enumerate((-0.9, 0.9)):
        p.box((px, 1.1, zc), (0.22, 2.2, PROP_T), 'TimberDim', uvo=0.31 * i)
    p.box((0.0, 2.33, zc), (2.2, 0.26, PROP_T), 'TimberDim', uvo=0.17)
    for sx in (-1, 1):
        p.strut((sx * 0.79, 1.72), (sx * 0.42, 2.2), 0.12, PROP_T * 0.8, zc, 'TimberDim')
    return p


def cribbing():
    """Cribbing — 바위에 박힌 갱목 쌓기(가로 통나무 + 마구리 둘을 번갈아 네 켜). 원점 = 아래 가운데: x ∈ [−0.6, 0.6], y ∈ [0, 1.6],
    z ∈ [−0.12, 0]. 슬롯 TimberDim."""
    p = Part('Cribbing')
    zc = -PROP_T / 2
    for i in range(4):
        y = i * 0.4
        p.box((0.0, y + 0.1, zc), (1.2, 0.2, PROP_T), 'TimberDim', uvo=0.23 * i)
        for ex in (-0.45, 0.45):
            p.box((ex, y + 0.3, zc), (0.2, 0.2, PROP_T), 'TimberDim', uvo=0.11 * i)
    return p


def ore_vein():
    """OreVein — 바위 면의 가는 광맥 + 작은 광석 조각 셋. 원점 = 왼끝 가운데: x ∈ [0, 1](x 축척), y ±0.12, z ∈ [−0.015, 0].
    광맥은 Seam, 조각은 OreDim(어두운 구리빛 — 반짝이지 않는다)."""
    p = Part('OreVein')
    ts = [0.0, 0.2, 0.4, 0.6, 0.8, 1.0]
    ys = [0.0, 0.03, -0.02, 0.04, -0.01, 0.0]
    ws = [0.004 + 0.022 * math.sin(math.pi * t) ** 0.5 for t in ts]
    p.slab(_ribbon(list(zip(ts, ys)), ws), -DECOR_T, 0.0, 'Seam')
    for cx, cy, r, rot in ((0.27, 0.0, 0.07, 0.3), (0.55, 0.035, 0.09, 1.1), (0.78, -0.01, 0.06, 2.0)):
        pts = [(cx + r * (1 + 0.25 * math.sin(3 * k)) * math.cos(rot + 2 * math.pi * k / 5),
                cy + r * 0.8 * (1 + 0.25 * math.sin(3 * k)) * math.sin(rot + 2 * math.pi * k / 5)) for k in range(5)]
        p.slab(pts, -0.015, 0.0, 'OreDim')
    return p


def rail_scrap():
    """RailScrap — 바위에 묻힌 끊긴 레일 조각(침목 넷 + 레일, 끝이 휘어 들림). 원점 = 왼끝 아래: x ∈ [0, 1.6], y ∈ [0, ~0.3],
    z ∈ [−0.1, 0]. 슬롯 RailDim·TimberDim."""
    p = Part('RailScrap')
    zc = -0.05
    for i, tx in enumerate((0.15, 0.55, 0.95, 1.35)):
        p.box((tx, 0.05, zc), (0.18, 0.1, 0.1), 'TimberDim', uvo=0.19 * i)
    p.box((0.65, 0.135, zc), (1.3, 0.07, 0.08), 'RailDim')
    p.strut((1.28, 0.135), (1.6, 0.3), 0.07, 0.08, zc, 'RailDim')
    return p


DECOR_PARTS = [strata_lens, rock_seam, timber_set, cribbing, ore_vein, rail_scrap]


# ════════════════════════ 펼치기 3차(10-13) — 움직이는 지형 ════════════════════════
#  승강기·매단 광차·낙석·무너진 받침목. 닿는 부품(LiftPlank·LiftLash·LiftCap·HangingCart·FallingRock·CollapsedProp)은 경계가 판정 상자 그대로
#  (MineDressingLayout 상수 옆 주석과 레이아웃 테스트가 이 크기를 가정한다). y로 늘려 쓰는 부품(LiftFrame·CartChain)은
#  세로로 끝없이 이어지는 모양만 — 가로 띠·볼트는 늘어나 두꺼워지므로 넣지 않는다. 나무결은 늘어난 뒤에 알맞게 타일을 잘게 잡는다.
LIFT_HALF = 0.975       # 파이프 폭 1.95의 반(GatePlank와 같다)
LIFT_DEPTH = 0.65       # 관문 판자와 같은 깊이(z ±0.65)


def lift_plank():
    """LiftPlank — 승강기 기둥 판자 칸(10-15 "L": 짙은 붉은 갈색 나무 + 밧줄). GatePlank와 같은 틀: 원점 = 아래 끝 가운데,
    x ∈ [−0.975, 0.975], y ∈ [0, 1](1 m씩 쌓고 마지막 칸만 y 축척), z ∈ [−0.65, 0.65].
    세로 판자 넉 장(WoodDark) 사이 홈 셋 너머로 어두운 안판(WoodDarkGroove)이 보이고, 앞면에 세로 밧줄 두 가닥(RopeLight)이
    양옆에서 안쪽으로 들어와 기둥 끝까지 달린다(칸마다 이어져 끊김이 없다). 위아래로 깎지 않아 쌓아도 이음매가 없다.
    틈 쪽 끝 테(LiftCap)와 밧줄 감기(LiftLash)는 굽기가 따로 얹는다(감기는 y로 늘면 굵어지므로)."""
    p = Part('LiftPlank')
    n, groove = 4, 0.03
    w = (2 * LIFT_HALF - (n - 1) * groove) / n
    for i in range(n):
        cx = -LIFT_HALF + w / 2 + i * (w + groove)
        p.prism_y(chamfer_rect(cx, 0.0, w, 2 * LIFT_DEPTH, 0.035), 0.0, 1.0, 'WoodDark', uvo=0.29 * i)
    p.box((0.0, 0.5, 0.0), (2 * LIFT_HALF - 0.1, 1.0, 2 * LIFT_DEPTH - 0.2), 'WoodDarkGroove')   # 안판 x ±0.925, z ±0.55
    for sx in (-1, 1):
        p.cyl((sx * 0.55, 0.5, -LIFT_DEPTH - 0.015), 0.035, 1.0, 'y', 8, 'RopeLight')                               # 밧줄 z −0.70~−0.63
    return p


LASH_WRAPS = 4
LASH_PITCH = 0.075


def lift_lash():
    """LiftLash — 밧줄 감기(GateStrap 자리). 가운데 원점: 네 바퀴 y ∈ [−0.15, 0.15], x ∈ [−0.98, 0.98](판자 옆면 ±0.975와 한 평면이 되지 않게
    5 mm 밖 — 그림 = 판정 ± 0.01 안), z ∈ [−0.72, 0.72](판자 앞뒤로 0.07, 앞면 밧줄 가닥 위로 감긴다). 바퀴마다 밝은·어두운 밧줄이 번갈아."""
    p = Part('LiftLash')
    for k in range(LASH_WRAPS):
        y = (k - (LASH_WRAPS - 1) / 2) * LASH_PITCH
        p.prism_x(chamfer_rect(y, 0.0, LASH_PITCH - 0.005, 1.44, 0.028), -0.98, 0.98, 'RopeLight' if k % 2 == 0 else 'RopeDark')
    return p


def lift_cap():
    """LiftCap — 승강기 틈 쪽 끝 테(GateCap과 같은 틀). 위 끝이 원점: y ∈ [−0.4, 0], x ∈ [−0.975, 0.975], z ∈ [−0.73, 0.73].
    보통 관문 쇠테보다 조금 따뜻한 크림색(CapCream). 쇠 볼트 대신 짙은 나무 못 둘(앞 −z). 위 기둥에는 z축 180°로 돌려 쓴다."""
    p = Part('LiftCap')
    p.prism_x(chamfer_rect(-0.2, 0.0, 0.4, 1.46, 0.05), -LIFT_HALF, LIFT_HALF, 'CapCream')
    for bx in (-0.7, 0.7):
        p.cyl((bx, -0.2, -0.75), 0.06, 0.1, 'z', 8, 'WoodDarkGroove')
    return p


def lift_frame():
    """LiftFrame — 승강기 안내 틀(판정면 뒤, 판정 없음). 원점 = 아래 가운데: x ∈ [−1.3, 1.3], y ∈ [0, 1](y 축척 = 통로 높이 + 진폭 × 2),
    z ∈ [0.3, 0.9]. 파이프(±0.975) 바로 바깥 양쪽에 갱목 기둥(x ±1.0~±1.3)과 안쪽 면 쇠 레일. 가로 버팀목은 통로를 가로질러 장애물로 읽히므로 없다."""
    p = Part('LiftFrame')
    p.tile['Timber'] = 0.06                       # 통로 높이 18 m 안팎으로 늘어난다
    for sx in (-1, 1):
        p.prism_y(chamfer_rect(sx * 1.15, 0.6, 0.3, 0.6, 0.04), 0.0, 1.0, 'Timber', uvo=0.3 * (sx > 0))
        p.prism_y(chamfer_rect(sx * 0.985, 0.6, 0.03, 0.14, 0.005), 0.0, 1.0, 'Iron')
    return p


CART_HALF_W, CART_HALF_H, CART_HALF_D = 0.8, 0.6, 0.6


def hanging_cart():
    """HangingCart — 매단 작은 광차. 가운데가 원점: x ∈ [−0.8, 0.8], y ∈ [−0.6, 0.6](광차 1.6 × 1.2), z ∈ [−0.6, 0.6].
    아래가 조금(0.08) 좁은 나무 통(Wood) + 위 쇠테(Iron, y 0.45~0.6 — 판정 상자 위 끝) + 앞 세로 쇠띠 둘 + 아래 모서리 작은 바퀴 둘.
    위에 줄을 거는 가로대·고리(Iron)는 테 안쪽(y ≤ 0.6) — 경계가 CartBox 그대로다. 줄(CartChain)은 판정면 뒤에서 내려와 고리에 닿는다."""
    p = Part('HangingCart')
    w, h, d = CART_HALF_W, CART_HALF_H, CART_HALF_D
    p.slab([(-0.72, -h), (0.72, -h), (0.77, 0.45), (-0.77, 0.45)], -0.55, 0.55, 'Wood')   # 아래가 조금만 좁다 — 판정 상자 모서리를 거의 다 채운다
    p.box((0.0, 0.525, 0.0), (2 * w, 0.15, 2 * d), 'Iron')
    for sx in (-1, 1):
        p.strut((sx * 0.44, -h + 0.1), (sx * 0.5, 0.45), 0.09, 0.03, -0.565, 'Strap')
        p.cyl((sx * 0.48, -h + 0.13, 0.0), 0.13, 2 * d, 'z', 12, 'Iron')   # 12각 — 아래 끝이 꼭짓점이라 y −0.6에 정확히
    p.cyl((0.0, 0.5, 0.0), 0.04, 1.1, 'z', 6, 'Iron')                       # 줄 거는 가로대(앞뒤로)
    p.cyl((0.0, 0.55, 0.25), 0.05, 0.1, 'z', 8, 'Iron')                      # 고리 — 줄이 내려오는 깊이(z 0.25)
    for i, (lx, lz) in enumerate(((-0.35, -0.2), (0.05, 0.15), (0.38, -0.15))):
        p.lump((lx, 0.43, lz), 0.14, (1.0, 0.8, 1.0), 'RockDark', seed=120 + i)   # 실은 광석 — 테 위로 안 나온다(y ≤ 0.57)
    return p


def cart_chain():
    """CartChain — 광차를 매단 줄(판정 없음). 원점 = 위 끝(진자 축): y ∈ [−1, 0](y 축척 = 줄 길이 − 광차 반 높이 0.6), z ∈ [0.2, 0.3](판정면 뒤).
    진자 루트에 달아 줄째 흔들린다. 가는 줄 하나(Rope) — 마디는 늘어나 타원이 되므로 넣지 않는다."""
    p = Part('CartChain')
    p.cyl((0.0, -0.5, 0.25), 0.05, 1.0, 'y', 6, 'Rope')
    return p


ROCK_HALF = 0.6


def falling_rock():
    """FallingRock — 떨어지는 바위 덩이. 가운데가 원점: x, y ∈ [−0.6, 0.6](RockBox 1.2 그대로), z ∈ [−0.6, 0.6].
    모서리를 조금(0.07) 깎은 사각 덩이(RockDark, z −0.55~0.6) 앞면(z −0.6~−0.55)에 각진 면 조각 다섯(Rock). 실루엣이 상자 그대로라 그림 = 판정."""
    p = Part('FallingRock')
    r, c = ROCK_HALF, 0.07
    body = [(-r + c, -r), (r - c, -r), (r, -r + c), (r, r - c), (r - c, r), (-r + c, r), (-r, r - c), (-r, -r + c)]
    #  앞면을 각진 면 다섯으로 나누고(P0·P1·P2 안쪽 꼭짓점을 같이 쓴다) 면마다 가운데 쪽으로 0.025 줄인다 — 사이로 보이는 어두운 몸통이 금이 된다.
    p.slab(body, -0.55, r, 'RockDark')
    P0, P1, P2 = (-0.1, -0.15), (0.08, 0.27), (0.15, -0.25)
    facets = [[(-r, -0.05), P0, P1, (-0.25, r), (-r + c, r), (-r, r - c)],
              [(-r, -r + c), (-r + c, -r), (0.25, -r), P2, P0, (-r, -0.05)],
              [(0.25, -r), (r - c, -r), (r, -r + c), (r, 0.3), P1, P2],
              [P0, P2, P1],
              [P1, (r, 0.3), (r, r - c), (r - c, r), (-0.25, r)]]
    for k, pts in enumerate(facets):
        cx = sum(q[0] for q in pts) / len(pts); cy = sum(q[1] for q in pts) / len(pts)
        shrunk = []
        for qx, qy in pts:
            L = math.hypot(cx - qx, cy - qy)
            shrunk.append((qx + 0.025 * (cx - qx) / L, qy + 0.025 * (cy - qy) / L))
        p.slab(shrunk, -r, -0.55, 'Rock', uvo=0.21 * k)
    return p


def ceiling_crack():
    """CeilingCrack — 낙석 줄 위 천장 바위의 금 간 자리(장식, 늘 같은 모양 — 박자 예고가 아니다). 원점 = 아래 가운데(= 천장선):
    x ∈ [−1.0, 1.0], y ∈ [0.13, 0.6](천장 밝은 테 0~0.12는 가리지 않는다), z ∈ [−0.3, −0.29](판정면 앞, 바위 띠·테 바로 앞).
    가운데 어두운 들쭉날쭉한 구멍 자국 + 양옆·위로 뻗는 가는 금(전부 RockDark)."""
    p = Part('CeilingCrack')
    z0, z1 = -0.3, -0.29
    p.slab([(-0.5, 0.13), (0.48, 0.13), (0.42, 0.22), (0.5, 0.3), (0.3, 0.38), (0.12, 0.34), (-0.05, 0.44),
            (-0.22, 0.36), (-0.42, 0.33), (-0.38, 0.24)], z0, z1, 'RockDark')
    for pts in (([(-0.48, 0.2), (-0.75, 0.22), (-1.0, 0.16)], [0.018, 0.012, 0.003]),
                ([(0.47, 0.24), (0.72, 0.3), (1.0, 0.27)], [0.018, 0.012, 0.003]),
                ([(-0.05, 0.42), (-0.12, 0.52), (-0.04, 0.597)], [0.016, 0.01, 0.003]),
                ([(0.3, 0.36), (0.42, 0.47), (0.55, 0.52)], [0.014, 0.009, 0.003])):
        p.slab(_ribbon(*pts), z0, z1, 'RockDark')
    return p


PROP_W, PROP_H, PROP_D = 0.6, 0.8, 0.35


def collapsed_prop():
    """CollapsedProp — 무너진 갱도의 부러진 받침목. 원점 = 왼끝 붙은 쪽(갱도 벽): x ∈ [0, 0.6], y ∈ [0, 0.8](끝이 통로 쪽),
    z ∈ [−0.35, 0.35]. 위에서 튀어나오는 받침목은 Z축 180° 돌려 쓴다. 비스듬히 부러진 두 동강(Timber) — 사이 틈 0.04 말고는
    판정 사각형을 빈틈없이 채운다(끝도 반듯 — 들쭉날쭉 쪼개진 끝은 판정과 그림이 어긋난다). 붙은 쪽 쇠띠(Strap)·볼트(Iron), 부러진 자리 앞 쐐기 조각."""
    p = Part('CollapsedProp')
    w, h, d = PROP_W, PROP_H, PROP_D
    p.slab([(0.0, 0.0), (w, 0.0), (w, 0.42), (0.0, 0.56)], -d + 0.02, d, 'Timber')
    p.slab([(0.0, 0.6), (w, 0.46), (w, h), (0.0, h)], -d + 0.02, d, 'Timber', uvo=0.37)
    p.box((w / 2, 0.11, -d + 0.0125), (w, 0.1, 0.015), 'Strap')                 # z −0.345~−0.33
    for bx in (0.15, 0.45):
        p.cyl((bx, 0.11, -d + 0.0025), 0.03, 0.005, 'z', 8, 'Iron')            # 볼트 z −0.35~−0.345
    for a, b, c in (((0.05, 0.55), (0.2, 0.5), (0.12, 0.66)), ((0.3, 0.5), (0.45, 0.46), (0.38, 0.62)), ((0.42, 0.44), (0.56, 0.42), (0.5, 0.34))):
        wedge(p, a, b, c, -d, -d + 0.02, 'Timber', uvo=0.6)
    return p


SPREAD3_PARTS = [lift_plank, lift_lash, lift_cap, lift_frame, hanging_cart, cart_chain, falling_rock, ceiling_crack, collapsed_prop]


PARTS = [gate_plank, gate_strap, gate_cap, trestle_bay, rail_span, ceiling_rock, cave_mouth, beam,
         bg_frame, ladder, walkway, bg_trestle_bay, mine_cart, lantern, silhouette_strip] + NEW_PARTS + DECOR_PARTS + SPREAD2_PARTS \
    + SPREAD3_PARTS


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


# 지층 띠 기본 4색(sRGB) — 시안 mine_sections.py strata_fill의 strA~D. 위에서부터가 아니라 v(아래→위) 순서.
STRATA_COLORS = ('#7a5646', '#6a4838', '#8a644e', '#5e4034')


def bake_strata():
    """strata.png — 따뜻한 갈색 4색 가로 띠 + 결. 색을 텍스처에 굽는다(재질 기본색은 흰색으로 둔다).
    띠 경계와 결은 u(x) 방향 주파수가 1~2뿐이라 x로 크게 늘리거나 좁혀도(축척 부품) 가로 지층으로 읽힌다.
    양방향 이어짐(정수 주파수), 띠 4줄 = 텍스처 한 장."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    u, v = np.meshgrid(np.arange(N) / N, np.arange(N) / N)
    rng = np.random.default_rng(23)

    def wob(fu, fv, n):
        out = np.zeros_like(u)
        for _ in range(n):
            i = int(rng.integers(0, fu + 1)); j = int(rng.integers(1, fv + 1)) if fv else 0
            if i == 0 and j == 0:
                continue
            out += np.sin(2 * np.pi * (i * u + j * v) + rng.uniform(0, 2 * np.pi)) / math.hypot(i, j)
        return out / (np.abs(out).max() + 1e-9)

    # 띠 경계: v를 낮은 u 주파수로 살짝 비튼다(경계가 물결)
    vv = (v + 0.035 * wob(2, 1, 6)) % 1.0
    k = np.floor(vv * 4).astype(int) % 4
    cols = np.array([[int(c[i:i + 2], 16) / 255.0 for i in (1, 3, 5)] for c in STRATA_COLORS])
    rgb = cols[k]
    # 띠 안 명암: 위쪽이 조금 밝게(층 윗면이 빛 받는 느낌) + 경계 바로 아래 어두운 금
    f = (vv * 4) % 1.0
    shade = 0.92 + 0.1 * f - 0.22 * np.clip((f - 0.96) / 0.04, 0, 1) - 0.12 * np.clip((0.03 - f) / 0.03, 0, 1)
    streak = wob(2, 48, 40)      # 가로 결(세로 주파수만 높다)
    grain = wob(1, 96, 30)
    m = shade * (1.0 + 0.06 * streak + 0.03 * grain)
    out = np.clip(rgb * m[..., None], 0, 1).astype(np.float32)
    rgba = np.concatenate([out, np.ones_like(out[..., :1])], axis=-1)
    img = bpy.data.images.new('strata', N, N, alpha=False)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = os.path.join(TEX_DIR, 'strata.png')
    img.file_format = 'PNG'
    img.save()
    print(f'TEX strata.png {N}x{N} mean_rgb={tuple(round(float(x), 3) for x in out.reshape(-1, 3).mean(0))} '
          f'seam_u={float(np.abs(out[:, 0] - out[:, -1]).mean()):.4f} seam_v={float(np.abs(out[0] - out[-1]).mean()):.4f}')


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
    if not ONLY or 'strata' in ONLY:
        bake_strata()
    print('mine_parts: DONE')


main()
