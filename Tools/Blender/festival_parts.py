"""축제 표적·조각·투석기 — 원점 기준 FBX로 뽑는다(투석기 사냥 슬라이스 2).

실행:  blender -b -P Tools/Blender/festival_parts.py -- <LeagueOfPhysical-Client/Assets/Art 절대경로> [부품 이름 ...]
출력:  <Art>/Models/Festival/<부품>.fbx  (이름을 주면 그 부품만 — FBX는 뽑을 때마다 바이트가 달라진다)

좌표는 게임(유니티) 좌표: x 오른쪽, y 위, +z = 사수 쪽(앞면). 원점 = 판정 구 중심.
크기 규칙: 감싸는 구 반지름 = 판정 반지름 × 0.95~1.05 (보이는 크기 = 맞는 크기). 재질은 슬롯 이름만.
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector, Matrix

ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
ART = ARGS[0] if ARGS else None
ONLY = set(ARGS[1:])
if not ART or not os.path.isdir(ART):
    raise SystemExit('usage: blender -b -P festival_parts.py -- <Assets/Art 절대경로>')
OUT = os.path.join(ART, 'Models', 'Festival')
AXIS_FORWARD, AXIS_UP = 'Z', 'Y'
PARENT = {'ShieldHalf': 'shield', 'AppleHalf': 'apple'}   # 조각은 원본과 같은 비율로 축척한다
RADIUS = {'Shield': 1.2, 'Helmet': 0.9, 'Apple': 0.6, 'Grail': 0.5, 'KingFlag': 0.85, 'FlowerBasket': 0.85}


def B(p):
    return Vector((p[0], p[2], p[1]))


class Part:
    def __init__(self, name):
        self.name, self.bm, self.slots = name, bmesh.new(), []

    def _mat(self, faces, mat, smooth=True):
        if mat not in self.slots:
            self.slots.append(mat)
        for f in faces:
            f.material_index = self.slots.index(mat)
            f.smooth = smooth

    def lathe(self, profile, mat, segs=24, center=(0, 0, 0), axis='y', smooth=True, caps=True):
        """profile = [(반지름, 높이)] 아래→위. axis 'y'면 세로, 'z'면 앞뒤(원판·방패).
        caps=False면 양 끝을 막지 않는다 — 고리(방패 테두리)가 안쪽 면을 원판으로 덮지 않게."""
        rings = []
        for (r, h) in profile:
            ring = []
            for i in range(segs):
                a = 2 * math.pi * i / segs
                if axis == 'y':
                    p = (center[0] + r * math.cos(a), center[1] + h, center[2] + r * math.sin(a))
                else:
                    p = (center[0] + r * math.cos(a), center[1] + r * math.sin(a), center[2] + h)
                ring.append(self.bm.verts.new(B(p)))
            rings.append(ring)
        faces = []
        for k in range(len(rings) - 1):
            a, b = rings[k], rings[k + 1]
            for i in range(segs):
                faces.append(self.bm.faces.new((a[i], a[(i + 1) % segs], b[(i + 1) % segs], b[i])))
        if caps and profile[0][0] > 1e-4:
            faces.append(self.bm.faces.new(list(reversed(rings[0]))))
        if caps and profile[-1][0] > 1e-4:
            faces.append(self.bm.faces.new(rings[-1]))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._mat(faces, mat, smooth)

    def box(self, c, s, mat, rot_z_deg=0.0):
        m = Matrix.Rotation(math.radians(rot_z_deg), 4, 'Y')   # 게임 z축 = 블렌더 y축
        res = bmesh.ops.create_cube(self.bm, size=1.0)
        verts = res['verts']
        bmesh.ops.scale(self.bm, vec=B(s), verts=verts)
        bmesh.ops.rotate(self.bm, matrix=m, verts=verts)
        bmesh.ops.translate(self.bm, vec=B(c), verts=verts)
        faces = list({f for v in verts for f in v.link_faces})
        self._mat(faces, mat, smooth=False)

    def sphere(self, c, r, mat, squash=(1, 1, 1), segs=16):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=segs // 2, radius=r)
        verts = res['verts']
        bmesh.ops.scale(self.bm, vec=B(squash), verts=verts)
        bmesh.ops.translate(self.bm, vec=B(c), verts=verts)
        self._mat(list({f for v in verts for f in v.link_faces}), mat)

    def tube(self, path, r, mat, sides=8):
        """path = 게임 좌표 점 목록(xy 평면 곡선). 반지름 r 관을 끊김 없이 훑고 양 끝을 막는다."""
        rings = []
        for i, c in enumerate(path):
            a, b = path[max(i - 1, 0)], path[min(i + 1, len(path) - 1)]
            t = Vector((b[0] - a[0], b[1] - a[1], 0)).normalized()
            n = Vector((-t.y, t.x, 0))
            ring = []
            for k in range(sides):
                q = 2 * math.pi * k / sides
                o = n * (r * math.cos(q)) + Vector((0, 0, r * math.sin(q)))
                ring.append(self.bm.verts.new(B((c[0] + o.x, c[1] + o.y, c[2] + o.z))))
            rings.append(ring)
        faces = []
        for j in range(len(rings) - 1):
            a, b = rings[j], rings[j + 1]
            for k in range(sides):
                faces.append(self.bm.faces.new((a[k], a[(k + 1) % sides], b[(k + 1) % sides], b[k])))
        faces.append(self.bm.faces.new(list(reversed(rings[0]))))
        faces.append(self.bm.faces.new(rings[-1]))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._mat(faces, mat)

    def bound_radius(self):
        return max((v.co.length for v in self.bm.verts), default=0.0)


def shield():
    p = Part('Shield')
    p.lathe([(0.0, -0.06), (1.14, -0.06), (1.14, 0.06), (0.0, 0.06)], 'Wood', segs=32, axis='z', smooth=False)
    p.lathe([(1.08, -0.07), (1.16, -0.07), (1.16, 0.08), (1.08, 0.08)], 'Metal', segs=32, axis='z', caps=False)
    p.sphere((0, 0, 0.08), 0.24, 'Metal', squash=(1, 1, 0.6))
    for x in (-0.38, 0.38):
        p.box((x, 0, 0.065), (0.04, 2.1, 0.01), 'WoodDark')
    return p


def shield_half():
    p = Part('ShieldHalf')   # 오른쪽 반(x ≥ 0). 왼쪽 반은 유니티에서 x 배율 −1.
    segs = 16
    prof = [(1.14 * math.cos(math.pi * (i / segs - 0.5)), 1.14 * math.sin(math.pi * (i / segs - 0.5))) for i in range(segs + 1)]
    front = [p.bm.verts.new(B((x, y, 0.06))) for x, y in prof]
    back = [p.bm.verts.new(B((x, y, -0.06))) for x, y in prof]
    faces = [p.bm.faces.new(front), p.bm.faces.new(list(reversed(back)))]
    for i in range(segs):
        faces.append(p.bm.faces.new((front[i], back[i], back[i + 1], front[i + 1])))
    faces.append(p.bm.faces.new((front[-1], back[-1], back[0], front[0])))
    bmesh.ops.recalc_face_normals(p.bm, faces=faces)
    p._mat(faces, 'Wood', smooth=False)
    return p


def helmet():
    p = Part('Helmet')   # 기사 통투구 — 세로 원기둥 + 둥근 꼭대기, 앞(+z)에 눈 틈
    p.lathe([(0.0, -0.62), (0.55, -0.62), (0.58, 0.2), (0.5, 0.48), (0.3, 0.64), (0.0, 0.68)], 'Metal')
    p.box((0, 0.12, 0.56), (0.7, 0.08, 0.06), 'MetalDark')
    p.box((0, -0.15, 0.57), (0.06, 0.4, 0.04), 'MetalDark')
    p.box((0, 0.66, 0), (0.08, 0.14, 0.7), 'Red')   # 볏
    return p


def apple():
    p = Part('Apple')
    p.sphere((0, -0.02, 0), 0.55, 'Red', squash=(1, 0.92, 1), segs=20)
    p.box((0, 0.55, 0), (0.04, 0.12, 0.04), 'WoodDark')
    p.sphere((0.1, 0.55, 0), 0.08, 'Green', squash=(1.6, 0.3, 0.8), segs=8)
    return p


def apple_half():
    p = Part('AppleHalf')   # 쪼갠 반쪽 — 자른 면(x=0)이 흰 속살
    p.lathe([(0.0, -0.5), (0.3, -0.42), (0.5, -0.1), (0.5, 0.15), (0.32, 0.45), (0.0, 0.5)], 'Red', segs=20)
    bmesh.ops.remove_doubles(p.bm, verts=p.bm.verts[:], dist=1e-5)   # 극점(반지름 0 고리)을 한 점으로
    bmesh.ops.bisect_plane(p.bm, geom=p.bm.verts[:] + p.bm.edges[:] + p.bm.faces[:], plane_co=(0, 0, 0),
                           plane_no=(1, 0, 0), clear_inner=True)
    # 자른 단면 = 잘린 윤곽 그대로 메운다(사각 판이면 옆에서 사과 밖으로 삐져나온다)
    rim = [e for e in p.bm.edges if e.is_boundary]
    cut = bmesh.ops.holes_fill(p.bm, edges=rim, sides=0)['faces']
    bmesh.ops.recalc_face_normals(p.bm, faces=p.bm.faces[:])
    p._mat(cut, 'White', smooth=False)
    return p


def grail():
    p = Part('Grail')   # 받침 → 기둥 → 잔. 높이 0.9, 감싸는 구 ≈ 0.48
    p.lathe([(0.0, -0.45), (0.28, -0.45), (0.3, -0.4), (0.08, -0.32), (0.06, -0.05), (0.12, 0.0),
             (0.3, 0.2), (0.32, 0.4), (0.27, 0.42), (0.0, 0.3)], 'Gold')
    p.lathe([(0.1, -0.2), (0.12, -0.2), (0.12, -0.14), (0.1, -0.14)], 'GoldDark')
    return p


def coin():
    p = Part('Coin')
    p.lathe([(0.0, -0.012), (0.08, -0.012), (0.08, 0.012), (0.0, 0.012)], 'Gold', segs=12, axis='z')
    return p


def king_flag():
    p = Part('KingFlag')   # 깃대 + 왕관 문양 천. 금지 표시는 유니티에서 따로(회색 판과 같은 "forbid")
    p.lathe([(0.03, -0.8), (0.03, 0.75)], 'WoodDark', segs=8, center=(-0.45, 0, 0))
    p.sphere((-0.45, 0.78, 0), 0.06, 'Gold')
    p.box((0.0, 0.42, 0.0), (0.86, 0.56, 0.02), 'Cloth')
    p.box((0.0, 0.42, 0.015), (0.3, 0.14, 0.01), 'Gold')
    for x in (-0.12, 0.0, 0.12):
        p.box((x, 0.53, 0.015), (0.05, 0.1, 0.01), 'Gold')
    return p


def flower_basket():
    p = Part('FlowerBasket')
    p.lathe([(0.0, -0.4), (0.36, -0.4), (0.5, -0.05), (0.52, 0.0), (0.48, 0.0), (0.0, -0.36)], 'Straw', smooth=False)
    seg = 24   # 손잡이 = 끊김 없는 반타원 관. 양 끝은 테두리 안으로 조금 묻는다
    arc = [math.pi * (-0.04 + 1.08 * i / seg) for i in range(seg + 1)]
    p.tube([(0.44 * math.cos(a), 0.02 + 0.66 * math.sin(a), 0) for a in arc], 0.03, 'Rope')
    for k, (x, z) in enumerate([(-0.25, 0.1), (0.0, -0.15), (0.22, 0.12), (-0.05, 0.25), (0.28, -0.1)]):
        p.sphere((x, 0.06, z), 0.11, 'Petal' if k % 2 == 0 else 'White', segs=8)
    return p


def catapult_frame():
    p = Part('CatapultFrame')   # 원점 = 바닥 가운데. 축 높이 1.2 m, +z = 던지는 쪽
    for x in (-0.7, 0.7):
        p.box((x, 0.15, 0), (0.18, 0.18, 2.6), 'Wood')
        p.box((x, 0.7, -0.2), (0.16, 1.2, 0.16), 'Wood')
        p.box((x, 0.75, 0.35), (0.14, 1.3, 0.14), 'WoodDark', rot_z_deg=0)
    p.box((0, 1.2, -0.2), (1.6, 0.14, 0.14), 'Metal')
    p.box((0, 1.6, 0.6), (1.5, 0.16, 0.16), 'WoodDark')   # 멈추개
    for z in (-1.1, 1.1):
        for x in (-0.8, 0.8):
            p.lathe([(0.32, -0.06), (0.32, 0.06)], 'WoodDark', segs=12, center=(x, 0.32, z), axis='z')
    return p


def catapult_arm():
    p = Part('CatapultArm')   # 원점 = 축. 팔은 뒤(−z)로 누운 자세가 0도, 바구니가 끝에
    p.box((0, 0, -1.2), (0.14, 0.14, 2.6), 'Wood')
    p.lathe([(0.0, 0.0), (0.32, 0.08), (0.34, 0.24)], 'Rope', segs=12, center=(0, 0.05, -2.45))
    return p


PARTS = [shield, shield_half, helmet, apple, apple_half, grail, coin, king_flag, flower_basket, catapult_frame, catapult_arm]


def export(part):
    me = bpy.data.meshes.new(part.name)
    part.bm.to_mesh(me)
    part.bm.free()
    for s in part.slots:
        me.materials.append(bpy.data.materials.get(s) or bpy.data.materials.new(s))
    ob = bpy.data.objects.new(part.name, me)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    path = os.path.join(OUT, part.name + '.fbx')
    # mine_parts.py와 같은 인수 — 유니티 임포트 축척이 갈리지 않게
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'},
                             axis_forward=AXIS_FORWARD, axis_up=AXIS_UP, apply_scale_options='FBX_SCALE_ALL',
                             bake_space_transform=True, use_mesh_modifiers=False, mesh_smooth_type='FACE',
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)
    return path


def parent_scale(make):
    whole = make()
    k = RADIUS[whole.name] / whole.bound_radius()
    whole.bm.free()
    return k


def main():
    os.makedirs(OUT, exist_ok=True)
    for make in PARTS:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        part = make()
        if ONLY and part.name not in ONLY:
            part.bm.free()
            continue
        tris = sum(len(f.verts) - 2 for f in part.bm.faces)
        want = RADIUS.get(part.name)
        raw = None
        if want is not None:
            # 보이는 크기 = 맞는 크기: 손치수를 판정 반지름에 맞춰 통째로 늘이거나 줄인다
            raw = part.bound_radius()
            k = want / raw
            bmesh.ops.scale(part.bm, vec=(k, k, k), verts=part.bm.verts[:])
        elif part.name in PARENT:
            # 깨진 조각이 원본보다 크거나 작아 보이지 않게 원본과 같은 비율을 쓴다
            k = parent_scale(globals()[PARENT[part.name]])
            bmesh.ops.scale(part.bm, vec=(k, k, k), verts=part.bm.verts[:])
        r = part.bound_radius()
        flag = '' if want is None or 0.95 * want <= r <= 1.05 * want else '  <-- 크기 규칙 밖'
        path = export(part)
        print(f'PART {part.name:14s} tris={tris:5d} r={r:.3f} want={want}{flag}{"" if raw is None else f"  (손치수 {raw:.3f})"} -> {os.path.relpath(path, ART)}')
    print('festival_parts: DONE')


main()
