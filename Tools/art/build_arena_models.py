"""Bake the Arena flyover and industrial skyline. Metres, X right, Y forward, Z up.

blender -b -noaudio -t 2 --factory-startup --python-exit-code 1 --python Tools/art/build_arena_models.py -- OUTPUT
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector, geometry

sys.path.insert(0, str(Path(__file__).resolve().parent))
from artlib import set_colors


IRON = (54, 65, 67)
SOOT = (37, 42, 40)
STEEL = (115, 136, 136)
PETROL = (72, 99, 101)
PALE = (207, 199, 166)
YELLOW = (231, 160, 63)
OXIDE = (153, 76, 46)
RUST = (98, 61, 46)
GLASS = (65, 123, 139)
CEMENT = (155, 144, 122)
PLASTER = (186, 174, 144)
SPALL = (99, 93, 80)


class Model:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.colors = []
        self.frame = Matrix.Identity(4)

    def loft(self, rings, color, bands=()):
        """Skin equal-sized sections. Bands tint individual stretches of the skin."""
        rings = [[Vector(p) for p in ring] for ring in rings]
        if geometry.normal(rings[0]).dot(rings[-1][0] - rings[0][0]) < 0:
            rings = [ring[::-1] for ring in rings]
        first, n = len(self.vertices), len(rings[0])
        self.vertices.extend(self.frame @ p for ring in rings for p in ring)
        self.faces.append(tuple(reversed(range(first, first + n))))
        self.colors.append(color)
        for row in range(len(rings) - 1):
            for i in range(n):
                a = first + row*n + i
                b = first + row*n + (i + 1) % n
                self.faces.append((a, b, b + n, a + n))
                self.colors.append(bands[row] if bands else color)
        last = first + (len(rings) - 1)*n
        self.faces.append(tuple(range(last, last + n)))
        self.colors.append(color)

    def prism(self, points, offset, color):
        self.loft([points, [Vector(p) + Vector(offset) for p in points]], color)

    def box(self, x, y, z, width, depth, height, color):
        self.prism([(x + sx*width/2, y + sy*depth/2, z)
                    for sx, sy in [(-1,-1), (1,-1), (1,1), (-1,1)]], (0, 0, height), color)

    def beam(self, start, end, radius, color, sides=4):
        start, end = Vector(start), Vector(end)
        axis = (end - start).normalized()
        u = axis.orthogonal().normalized() * radius
        v = axis.cross(u)
        ring = [u*math.cos(i*math.tau/sides) + v*math.sin(i*math.tau/sides) for i in range(sides)]
        self.loft([[p + d for d in ring] for p in (start, end)], color)

    def export(self, out, name, max_triangles):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update()
        assert not mesh.validate(), f"{name} contains invalid geometry"
        mesh.calc_loop_triangles()
        assert 0 < len(mesh.loop_triangles) <= max_triangles, (name, len(mesh.loop_triangles))
        ob = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(ob)
        mesh.materials.append(bpy.data.materials.new("ArenaPaint"))
        set_colors(ob, lambda face: (*[c/255 for c in self.colors[face.index]], 0.0))
        mesh.color_attributes.active_color = mesh.color_attributes[0]
        bpy.ops.export_scene.fbx(filepath=str(out / (name + ".fbx")), object_types={'MESH'},
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='Z', axis_up='Y',
            bake_space_transform=True, mesh_smooth_type='FACE', colors_type='SRGB', prioritize_active_color=True,
            use_triangles=True, add_leaf_bones=False, bake_anim=False, use_custom_props=False, path_mode='STRIP')
        print(f"{name}: {len(mesh.loop_triangles)} triangles", flush=True)


def bomber(m):
    def hull(rows, color, bands=()):
        sections = []
        for y, w, bottom, top in rows:
            h = top - bottom
            sections.append([(w*x, y, bottom + h*z) for x, z in
                             [(-.54,0), (.54,0), (.95,.22), (1,.6), (.74,.90), (.39,1),
                              (-.39,1), (-.74,.90), (-1,.6), (-.95,.22)]])
        m.loft(sections, color, bands)

    hull([(-8.4,.28,.24,.95), (-6.9,.66,-.02,1.25), (-3.8,1.27,-.94,1.50),
          (-1.6,1.57,-1.35,1.58), (2.75,1.56,-1.29,1.39), (4.9,1.34,-1.06,1.06),
          (6.55,.92,-.67,.66), (7.5,.38,-.31,.28)], PETROL,
         [OXIDE, PETROL, STEEL, PETROL, PETROL, IRON, PALE])
    hull([(2.18,.78,1.14,1.44), (2.95,1.08,1.12,2.11), (3.85,1.02,.97,2.09),
          (4.05,.98,.96,2.03), (5.68,.66,.66,1.26), (6.05,.42,.60,.85)], GLASS,
         [IRON, PETROL, PALE, GLASS, IRON])
    hull([(-6.74,.36,1.04,1.16), (-4.75,.51,1.25,1.71), (-2.9,.48,1.32,1.79),
          (-2.28,.29,1.42,1.58)], OXIDE)
    for s in (-1, 1):
        # A thick, tapered airfoil; the salvage stripes wrap onto the underside.
        def wings(rows, colors):
            rings = []
            for x, lead, trail, z, thick in rows:
                chord = lead - trail
                rings.append([(s*x, lead - t*chord, z + h*thick) for t, h in
                              [(0,0), (.16,.68), (.50,.78), (1,.03), (.96,-.12), (.25,-.4), (.04,-.29)]])
            m.loft(rings, colors[-1], colors)

        wings([(.83,3.05,-3.82,.42,.77), (3.55,2.66,-3.62,.34,.69), (6.68,1.30,-3.64,.41,.53),
               (8.45,.42,-3.80,.52,.38), (9.32,.02,-3.83,.61,.34), (9.78,-.18,-3.84,.65,.31),
               (11.60,-1.08,-3.78,.87,.22), (12.75,-1.91,-3.50,1.05,.13)],
              [PETROL, PETROL if s < 0 else STEEL, PETROL, YELLOW, SOOT, PETROL, PALE])
        wings([(.18,-5.4,-8.1,.68,.40), (2.8,-5.83,-8.35,.78,.29), (4.62,-6.49,-8.26,.88,.15)],
              [PETROL, PALE])
        fin = [(s*(3.55 + z*.18), y, z) for y, z in
               [(-8.22,.80), (-8.35,3.52), (-7.71,4.05), (-7.14,3.97), (-5.65,1.02)]]
        m.prism(fin, (s*.24,0,0), OXIDE)
        m.prism([(s*(3.80 + z*.18),y,z) for y,z in
                 [(-8.20,2.83),(-8.24,3.16),(-7.02,3.16),(-6.86,2.83)]], (s*.02,0,0), PALE)
        m.box(s*.61, .04, -1.40, 1.08, 3.95, .14, IRON)
        for y in (-1.16, .28, 1.42):
            m.box(s*.62, y, -1.455, 1.03, .12, .07, STEEL)
        for x, front, r in [(3.25,3.79,.83), (6.47,2.16,.69)]:
            def ring(y, radius):
                return [(s*x + radius*math.cos(math.tau*i/12), y, -.31 + radius*math.sin(math.tau*i/12))
                        for i in range(12)]

            # The recessed dark faces stay readable head-on and from underneath.
            m.loft([ring(y, r*k) for y,k in
                    [(front-4.15,.51),(front-3.74,.69),(front-3.31,.78),(front-1.1,1.05),
                     (front-.39,1.04),(front,1),(front+.015,.80),(front-.25,.72)]],
                   SOOT, [IRON, OXIDE, PETROL, IRON, OXIDE, PALE, IRON])
            m.beam((s*x,front-.26,-.31), (s*x,front+.07,-.31), .20*r, STEEL, 8)
            for i in range(6):
                a = i*math.tau/6
                blade = [(s*x + k*r*math.cos(a+b), front-.20, -.31 + k*r*math.sin(a+b))
                         for k,b in [(.16,0),(.66,.16),(.67,.39),(.21,.35)]]
                m.prism(blade, (0,.06,0), PETROL)


def rubble(m, x, y, size):
    for dx, dy, w, h, skew in [(-.42,.12,.56,.28,.32), (.32,-.08,.60,.38,-.13),
                                (.03,.50,.48,.19,.24), (-.18,-.28,.41,.17,-.30)]:
        points = [(x+size*(dx+a*w), y+size*(dy+b*w), .10 + size*(h+skew*a))
                  for a,b in [(-.6,-.4),(.24,-.58),(.67,-.16),(.3,.47),(-.48,.37)]]
        m.prism(points, (0,0,-size*h), CEMENT if w > .5 else SPALL)


def bridgehead(m):
    # A sealed service chamber fills the old 17 x 12 x 24 m obstacle.
    def course(z, x, y, cut):
        return [(a,b,z) for a,b in [(-x+cut,-y),(x-cut,-y),(x,-y+cut),(x,y-cut),
                                     (x-cut,y),(-x+cut,y),(-x,y-cut),(-x,-y+cut)]]
    m.loft([course(0,8.4,11.9,1.35),course(1.08,8.4,11.9,1.35),course(1.45,8.1,11.6,1.45),
            course(7.32,7.35,10.83,1.4),course(7.75,7.34,10.81,1.4),course(9.58,7.17,10.6,1.30)],
           CEMENT,[SPALL,CEMENT,PLASTER,OXIDE,CEMENT])
    for side in (-1,1):
        for y in (-6.65, .1, 6.9):
            rib = [(side*x,y,z) for x,z in [(7.28,.1),(8.45,.1),(8.45,2.1),(7.56,8.89),(7.02,9.1)]]
            m.prism(rib,(0,1.30,0),SPALL)
        # Shutters are braced shut so the rectangular blocker reads correctly.
        def panel(xz, lift, depth, color):
            m.prism([(x,side*(11.81-.12*z+lift),z) for x,z in xz], (0,side*depth,0), color)
        panel([(-3.7,.5),(3.7,.5),(3.7,5.64),(-3.7,5.64)],.07,.09,IRON)
        for row in range(5):
            z = .65 + row*.94
            panel([(-3.51,z),(3.50,z),(3.5,z+.72),(-3.51,z+.72)],.18,.04,PETROL)
        panel([(-3.36,1.46),(-2.94,1.36),(2.95,5.10),(2.46,5.16)],.25,.09,RUST)
        panel([(-4.47,5.66),(4.47,5.66),(4.47,6.83),(-4.47,6.83)],.1,.21,IRON)
        for x in (-3.5,-1.55,.40,2.35):
            panel([(x,5.8),(x+.77,5.8),(x+1.29,6.68),(x+.52,6.68)],.34,.025,YELLOW)
    cap = [(-8.22,-10.8),(-6.94,-11.78),(6.92,-11.78),(8.15,-10.67),(8.15,.73),
           (6.91,.91),(7.27,2.18),(6.34,3.00),(7.86,4.18),(7.67,8.60),(5.93,11.49),
           (-6.12,11.74),(-8.20,10.78)]
    m.prism([(x,y,9.58) for x,y in cap],(0,0,.88),PLASTER)
    for y, end in [(-7.82,5.65),(6.5,.5)]:
        beam = [(-6.89,y,10.45),(end,y,10.45),(end+.55,y,11.10),(end-.45,y,11.25),
                (end+.15,y,11.84),(-6.89,y,11.84)]
        m.prism(beam,(0,1.38,0),SPALL)
        for x in (-5.7, -3.4):
            m.beam((x,y+.60,11.5),(x+.38,y+.50,12.7),.13,OXIDE)
    m.beam((6.99,1.8,9.98),(8.26,2.13,9.57),.11,OXIDE)
    assert all(abs(p.x) <= 8.5 and abs(p.y) <= 12 for p in m.vertices)


def tenement(m):
    # The floors recede into a missing corner; window bays are actual holes.
    for floor, end in enumerate([12.1, 12.0, 6.35, .67, -4.7]):
        z = floor * 4.65
        outline = [(-11.8,-6.2),(end-1.3,-6.2),(end-.48,-5.86),(end-1.02,-4.9),
                   (end+.32,-4.28),(end-.40,-2.84),(end+1.28,-1.31),(end+.66,.06),
                   (end+2.10,1.15),(end+1.51,3.36),(end+2.52,4.54),(end+2.42,6.2),(-11.8,6.2)]
        m.prism([(x,y,z) for x,y in outline], (0,0,.58), CEMENT)
        if floor == 4:
            break
        for x in [-11.1,-5.6,.05,5.7,11.35]:
            if x > end-.38:
                continue
            for y in [-5.57,5.57]:
                m.box(x,y,z+.54,1.22,1.24,4.12, PLASTER if y < 0 else SPALL)
        width = end + 11.3
        for y in [-5.61,5.61]:
            m.box(-11.65+width/2, y, z+.52, width,.70,1.15, OXIDE if floor == 2 else PLASTER)
        m.box(-11.12,0,z+.56,1.12,10,4.1, CEMENT)
        # A splinter of exterior wall and exposed steel at the torn floor edge.
        wall = [(end-.9,-5.75,z+.5),(end+.38,-5.75,z+.5),(end+.04,-5.75,z+2.55),
                (end-.31,-5.75,z+2.21),(end-.9,-5.75,z+3.67)]
        m.prism(wall, (0,.58,0), PLASTER)
        for dy in (-3.4,1.2):
            m.beam((end-.45,dy,z+.32), (end+2.0,dy+.4,z+.03), .14,RUST)
    for x, y, h in [(-11,-5.4,4.4),(-5.7,5.5,2.9),(-9.5,5.4,3.8)]:
        m.box(x,y,18.6,.80,.92,h*.42,CEMENT)
        for off in (-.26,.26):
            m.beam((x+off,y,19), (x+off+.35,y-.27,18.6+h), .10,RUST)
    m.prism([(2,-5.75,9.1),(7.1,-5.35,7.5),(6.8,2.0,6.6),(4.7,3,7.4),(1.6,.8,8.8)],
            (0,0,-.42), SPALL)
    rubble(m, 10,-3.8,7.5)


def hangar(m):
    def arch(y, i):
        a = math.pi*i/8
        return (13*math.cos(a), y, 6.2 + 8.4*math.sin(a))

    bays = [-10,-5,0,5,10]
    for j, y in enumerate(bays):
        stop = [8,8,6,4,2][j]
        for i in range(stop):
            m.beam(arch(y,i), arch(y,i+1), .34, OXIDE)
            if j < 4:
                m.beam(arch(y,i), arch(y+5,i), .20, IRON)
                if (j,i) in [(2,5),(3,3),(0,6),(1,2)]:
                    continue
                p = [arch(y+.08,i),arch(y+.08,i+1),arch(y+4.92,i+1),arch(y+4.92,i)]
                m.prism(p, (0,0,-.16), [PETROL,STEEL,RUST][(i+j//2)%3])
        for x in (-13,13):
            m.box(x,y,0,.53,.57,6.4,RUST)
        if j < 3:
            m.beam((-13,y,1), (-13,y+5,6.1), .22, IRON)
    for y in (-7.5,-2.5,2.5):
        m.box(13,y,.15,.38,4.9,5.93,PETROL)
        m.box(-13,y,.15,.52,4.9,1.68,CEMENT)
    # An open, chipped stack gives the low roof a tall counterweight.
    x, y = 15.2, 6
    def chimney(z, r, chipped=False):
        return [(x+r*math.cos(i*math.tau/12),y+r*math.sin(i*math.tau/12),
                 z+([0,-.7,.26,-.18,-1.35,-1.9,-.86,.16,-.34,.47,.12,-.22][i] if chipped else 0))
                for i in range(12)]
    m.loft([chimney(0,2.13),chimney(12,1.75),chimney(15,1.64),chimney(17.1,1.58),
            chimney(20.1,1.49),chimney(23.8,1.40,True),chimney(23.8,1.08,True),chimney(20.5,1.08)],
           SOOT,[OXIDE,PLASTER,OXIDE,PLASTER,OXIDE,SPALL,SOOT])
    rubble(m,-10,6.2,5.8)


def water_tower(m):
    for x, y in [(-1,-1),(1,-1),(1,1),(-1,1)]:
        m.box(x*4.25,y*4.25,0,1.65,1.65,1.15,CEMENT)
        m.beam((x*4.25,y*4.25,.8), (x*3.1,y*3.1,15.4), .37,OXIDE)
        for z, span, next_z, next_span in [(3,4.08,9,3.61),(9,3.61,14.7,3.15)]:
            m.beam((x*span,y*span,z), (-x*next_span,y*next_span,next_z), .18,RUST)
            m.beam((x*span,y*span,z), (x*next_span,-y*next_span,next_z), .18,RUST)
    def drum(z, radius):
        return [(radius*math.cos(i*math.tau/16),radius*math.sin(i*math.tau/16),z) for i in range(16)]
    m.loft([drum(z,r) for z,r in [(12,.9),(12.7,1.4),(15.8,5.1),(16.25,5.34),(17,5.34),
                                   (20.1,5.26),(21.63,5.24),(23.2,5.2),(23.6,5.31)]],
           IRON,[RUST,OXIDE,IRON,PETROL,PETROL,PALE,PETROL,RUST])
    for i in range(16):
        a,b = i*math.tau/16,(i+1)*math.tau/16
        if i not in (2,3,4,5):
            top = [(5.34*math.cos(a),5.34*math.sin(a),23.61),(5.34*math.cos(b),5.34*math.sin(b),23.61),
                   (.8+.65*math.cos(b),.2+.65*math.sin(b),25.9),(.8+.65*math.cos(a),.2+.65*math.sin(a),25.9)]
            m.prism(top,(0,0,-.18),PETROL if i%4 else OXIDE)
        if i%2 == 0:
            m.beam((5.38*math.cos(a),5.38*math.sin(a),16.3),
                   (5.26*math.cos(a),5.26*math.sin(a),23.5),.09,IRON)
    for x in (-.48,.48):
        m.beam((x,-5.52,2.2),(x,-5.52,24.2),.105,OXIDE)
    for z in range(3,25):
        m.beam((-.48,-5.52,z),(.48,-5.52,z),.075,STEEL)
    rubble(m,4.3,1.5,4.7)


def ruined_works(m):
    # Offsets are Arena-local X/Z. The lowest course meets the sand outside the 120 m rim.
    sites = [(tenement,-47,142,163,1.46), (hangar,-5,143,-9,1.06), (water_tower,48,136,22,1.18),
             (tenement,141,43,95,1.19), (hangar,145,-35,85,1.28),
             (water_tower,77,-137,128,.98), (tenement,28,-147,13,1.53), (hangar,-35,-145,15,1.02),
             (tenement,-142,-62,-76,1.13), (water_tower,-138,-12,-17,1.30), (hangar,-144,47,-91,1.16)]
    for build, x, z, yaw, scale in sites:
        m.frame = Matrix.Translation((x, z, -2)) @ Matrix.Rotation(math.radians(yaw),4,'Z') @ Matrix.Scale(scale,4)
        first = len(m.vertices)
        build(m)
        across = 0 if abs(x) > abs(z) else 1
        assert min(abs(p[across]) for p in m.vertices[first:]) >= 121.5, "ruins extend through the arena rim"


if __name__ == '__main__':
    output = Path(sys.argv[sys.argv.index('--')+1]).resolve()
    output.mkdir(parents=True, exist_ok=True)
    for name, make, budget in [('Bomber',bomber,2000), ('RuinedWorks',ruined_works,18000),
                               ('RuinedBridgehead',bridgehead,1200)]:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        model = Model()
        make(model)
        model.export(output, name, budget)
