"""축제 표적·투석기 — 원점 기준 FBX로 뽑는다(투석기 사냥 2차 — 손맛 표적 9종).

실행:  blender -b -P Tools/Blender/festival_parts.py -- <LeagueOfPhysical-Client/Assets/Art 절대경로> [부품 이름 ...]
출력:  <Art>/Models/Festival/<부품>.fbx  (이름을 주면 그 부품만 — FBX는 뽑을 때마다 바이트가 달라진다)

좌표는 게임(유니티) 좌표: x 오른쪽, y 위, +z = 사수 쪽(앞면). 원점 = 판정 구 중심.
크기 규칙: 감싸는 구 반지름 = 판정 반지름 × 0.95~1.05 (보이는 크기 = 맞는 크기). 재질은 슬롯 이름만.
꾸밈 슬롯(String·Tassel)은 재지 않는다.
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
RADIUS = {'WaterBalloon': 1.0, 'Watermelon': 0.75, 'Plate': 0.85, 'Rocket': 0.45, 'GoldenBell': 0.8,
          'Pinata': 0.95, 'BirdNest': 0.75, 'CheerFlag': 0.8, 'KidBalloon': 0.8}
# 가는 꾸밈(풍선 줄·피냐타 술) — 감싸는 구에서 뺀다. 넣으면 몸이 줄 길이만큼 작아져 "안 맞을 것 같은데 맞음"이 된다.
DECOR = {'String', 'Tassel'}


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

    def sphere(self, c, r, mat, squash=(1, 1, 1), segs=16, rot_y_deg=0.0):
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=segs, v_segments=segs // 2, radius=r)
        verts = res['verts']
        bmesh.ops.scale(self.bm, vec=B(squash), verts=verts)
        if rot_y_deg:
            bmesh.ops.rotate(self.bm, matrix=Matrix.Rotation(math.radians(rot_y_deg), 4, 'Z'), verts=verts)  # 게임 y = 블렌더 z
        bmesh.ops.translate(self.bm, vec=B(c), verts=verts)
        self._mat(list({f for v in verts for f in v.link_faces}), mat)

    def prism(self, outline, depth, mat):
        """outline = 게임 xy 평면의 볼록하지 않아도 되는 다각형(반시계). z = ±depth/2로 뽑아 옆면을 잇는다."""
        front = [self.bm.verts.new(B((x, y, depth / 2))) for x, y in outline]
        back = [self.bm.verts.new(B((x, y, -depth / 2))) for x, y in outline]
        faces = [self.bm.faces.new(front), self.bm.faces.new(list(reversed(back)))]
        n = len(outline)
        for i in range(n):
            j = (i + 1) % n
            faces.append(self.bm.faces.new((front[i], back[i], back[j], front[j])))
        bmesh.ops.recalc_face_normals(self.bm, faces=faces)
        self._mat(faces, mat, smooth=False)

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
        """몸만 잰다 — 꾸밈 슬롯(DECOR) 면의 정점은 뺀다."""
        body = set()
        for f in self.bm.faces:
            if self.slots[f.material_index] not in DECOR:
                body.update(f.verts)
        return max((v.co.length for v in body), default=0.0)

    def decor_count(self):
        return sum(1 for f in self.bm.faces if self.slots[f.material_index] in DECOR)


def water_balloon():
    p = Part('WaterBalloon')   # 살짝 위아래로 긴 물방울 + 하이라이트 + 묶은 꼭지
    p.sphere((0, 0.02, 0), 0.5, 'Water', squash=(1, 1.08, 1), segs=24)
    p.sphere((-0.17, 0.22, 0.38), 0.12, 'WaterLight', squash=(0.8, 1.2, 0.4), segs=10)
    p.lathe([(0.0, -0.62), (0.07, -0.58), (0.03, -0.52)], 'WaterDark', segs=10)
    return p


def watermelon():
    p = Part('Watermelon')     # 옆으로 조금 긴 통수박 + 짙은 줄무늬(세로 타원 여섯 겹)
    p.sphere((0, 0, 0), 0.5, 'Melon', squash=(1.1, 1, 1), segs=24)
    for k in range(6):
        p.sphere((0, 0, 0), 0.505, 'MelonDark', squash=(0.13, 1.0, 1.1), segs=16, rot_y_deg=30 * k)
    return p


def plate():
    p = Part('Plate')          # 사수를 향한 얕은 접시(+z가 안쪽 면) + 파란 테
    p.lathe([(0.0, -0.03), (0.3, -0.03), (0.5, 0.02), (0.5, 0.05), (0.46, 0.05), (0.32, 0.0), (0.0, 0.0)],
            'Porcelain', segs=32, axis='z', smooth=False)
    p.lathe([(0.38, 0.012), (0.43, 0.035), (0.43, 0.04), (0.38, 0.017)], 'PorcelainBlue', segs=32, axis='z', caps=False)
    return p


def rocket():
    p = Part('Rocket')         # 세운 로켓: 몸통·흰 띠·뾰족 머리·날개 셋. 불꽃 꼬리는 연출(파티클)이 그린다
    p.lathe([(0.0, -0.3), (0.09, -0.3), (0.09, 0.2), (0.0, 0.2)], 'RocketRed', segs=16)
    p.lathe([(0.092, -0.02), (0.092, 0.05)], 'White', segs=16, caps=False)
    p.lathe([(0.09, 0.2), (0.0, 0.38)], 'Gold', segs=16)
    for x in (-0.12, 0.12):                     # 날개 넷 — box는 게임 z축으로만 돌리므로 x·z 방향으로 따로 세운다
        p.box((x, -0.25, 0), (0.08, 0.12, 0.015), 'Gold')
    for z in (-0.12, 0.12):
        p.box((0, -0.25, z), (0.015, 0.12, 0.08), 'Gold')
    return p


def golden_bell():
    p = Part('GoldenBell')     # 종 몸(아래가 벌어진 회전체) + 추 + 고리
    p.lathe([(0.0, 0.42), (0.14, 0.42), (0.22, 0.32), (0.25, 0.1), (0.3, -0.15), (0.45, -0.34), (0.48, -0.38),
             (0.44, -0.38), (0.0, -0.3)], 'Gold', segs=28)
    p.sphere((0, -0.4, 0), 0.08, 'GoldDark', segs=10)
    p.lathe([(0.06, 0.42), (0.08, 0.5), (0.06, 0.56), (0.03, 0.5)], 'GoldDark', segs=12, caps=False)
    return p


def pinata():
    p = Part('Pinata')         # 다섯 뿔 별(두께 0.3) + 뿔 끝 색 술(꾸밈)
    pts = []
    for i in range(10):
        a = math.pi / 2 + i * math.pi / 5
        r = 0.5 if i % 2 == 0 else 0.24
        pts.append((r * math.cos(a), r * math.sin(a)))
    p.prism(pts, 0.3, 'PinataOrange')
    for i, mat in enumerate(['PinataPink', 'PinataGreen', 'PinataBlue', 'PinataPink', 'PinataGreen']):
        a = math.pi / 2 + i * 2 * math.pi / 5
        p.box((0.36 * math.cos(a), 0.36 * math.sin(a), 0.155), (0.06, 0.2, 0.01), mat, rot_z_deg=math.degrees(a) - 90)
        tip = (0.5 * math.cos(a), 0.5 * math.sin(a), 0)
        p.tube([tip, (0.66 * math.cos(a + 0.1), 0.66 * math.sin(a + 0.1), 0)], 0.02, 'Tassel', sides=6)
    return p


def bird_nest():
    p = Part('BirdNest')       # 나뭇가지 둥지(얕은 그릇) + 하늘색 알 셋
    p.lathe([(0.0, -0.2), (0.4, -0.16), (0.5, 0.02), (0.44, 0.1), (0.36, 0.04), (0.0, -0.08)], 'NestBrown', segs=20, smooth=False)
    for x, z in ((-0.17, 0.0), (0.0, 0.08), (0.17, 0.0)):
        p.sphere((x, 0.08, z), 0.12, 'EggBlue', squash=(1, 1.3, 1), segs=12)
    for a in (20, 80, 140, 200, 260, 320):      # 테두리에 삐죽 나온 잔가지(작은 토막)
        p.box((0.46 * math.cos(math.radians(a)), 0.03, 0.46 * math.sin(math.radians(a))), (0.1, 0.03, 0.1), 'NestDark')
    return p


def cheer_flag():
    p = Part('CheerFlag')      # 막대 + 파랑·흰 줄무늬 천(네 줄) + 금 꼭지
    p.lathe([(0.03, -0.75), (0.03, 0.72)], 'WoodDark', segs=8, center=(-0.45, 0, 0))
    p.sphere((-0.45, 0.75, 0), 0.05, 'Gold', segs=10)
    for i in range(4):
        p.box((0.02, 0.62 - i * 0.14, 0.0), (0.9, 0.14, 0.02), 'FlagBlue' if i % 2 == 0 else 'White')
    return p


def kid_balloon():
    p = Part('KidBalloon')     # 노란 웃는 얼굴 풍선 + 줄(꾸밈)
    p.sphere((0, 0.05, 0), 0.5, 'KidYellow', segs=24)
    for x in (-0.15, 0.15):
        p.sphere((x, 0.13, 0.46), 0.05, 'Ink', squash=(1, 1.3, 0.5), segs=8)
    seg = 12
    p.tube([(0.22 * math.cos(math.pi * (1.15 + 0.7 * i / seg)), 0.0 + 0.22 * math.sin(math.pi * (1.15 + 0.7 * i / seg)), 0.47)
            for i in range(seg + 1)], 0.02, 'Ink', sides=6)
    p.tube([(0, -0.45, 0), (0.04, -0.7, 0), (-0.03, -0.95, 0), (0.02, -1.15, 0)], 0.01, 'String', sides=6)
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


PARTS = [water_balloon, watermelon, plate, rocket, golden_bell, pinata, bird_nest, cheer_flag, kid_balloon,
         catapult_frame, catapult_arm]


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
        r = part.bound_radius()
        flag = '' if want is None or 0.95 * want <= r <= 1.05 * want else '  <-- 크기 규칙 밖'
        decor = part.decor_count()   # export가 bmesh를 비우므로 먼저 센다
        path = export(part)
        print(f'PART {part.name:14s} tris={tris:5d} r={r:.3f} want={want} decor={decor}{flag}{"" if raw is None else f"  (손치수 {raw:.3f})"} -> {os.path.relpath(path, ART)}')
    print('festival_parts: DONE')


main()
