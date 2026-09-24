"""Build a stylized arcade sports car blockout in headless Blender and render the RGBA reference
image that Hunyuan3D-2 turns into a shape. Deterministic (no randomness).

blender --background --factory-startup --python-exit-code 1 --python make_reference.py -- <out_dir>
Writes <out_dir>/reference.png (768x768, transparent background, 3/4 front-left view) and
<out_dir>/reference_side.png (side view, for the contact sheet only).

Modelling space: X right, Y forward, Z up, metres; dimensions follow RaceCar.prefab
(wheels r=0.34 at x=+-0.94, y=+-1.25).
"""
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

out_dir = Path(sys.argv[sys.argv.index("--") + 1]).resolve()
out_dir.mkdir(parents=True, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene


def material(name, rgb, rough=0.45, metal=0.0, emit=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*rgb, 1)
    p.inputs["Roughness"].default_value = rough
    p.inputs["Metallic"].default_value = metal
    if emit > 0:
        p.inputs["Emission Color"].default_value = (*rgb, 1)
        p.inputs["Emission Strength"].default_value = emit
    return m


M_PAINT = material("paint", (0.75, 0.05, 0.03), 0.3)
M_GLASS = material("glass", (0.03, 0.05, 0.08), 0.1)
M_DARK = material("dark", (0.025, 0.025, 0.03), 0.6)
M_TYRE = material("tyre", (0.02, 0.02, 0.02), 0.9)
M_RIM = material("rim", (0.55, 0.57, 0.6), 0.3, 0.8)
M_HEAD = material("head", (1.0, 0.95, 0.8), 0.2, 0, 3.0)
M_TAIL = material("tail", (0.9, 0.05, 0.03), 0.2, 0, 2.0)


def superellipse_ring(y, hw, zb, zt, n=16, p=4.0, upper_only=False):
    zc, hh = (zb + zt) * 0.5, (zt - zb) * 0.5
    pts = []
    for i in range(n):
        a = 2 * math.pi * i / n
        c, s = math.cos(a), math.sin(a)
        x = hw * math.copysign(abs(c) ** (2 / p), c)
        z = zc + hh * math.copysign(abs(s) ** (2 / p), s)
        pts.append(Vector((x, y, z)))
    return pts


def loft(name, rings, mat, subsurf=2):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    vr = [[bm.verts.new(p) for p in ring] for ring in rings]
    n = len(rings[0])
    for a, b in zip(vr, vr[1:]):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(list(reversed(vr[0])))
    bm.faces.new(vr[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    ob.data.materials.append(mat)
    if subsurf:
        mod = ob.modifiers.new("sub", "SUBSURF")
        mod.levels = mod.render_levels = subsurf
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.modifier_apply(modifier="sub")
    for f in ob.data.polygons:
        f.use_smooth = True
    return ob


def box(name, center, size, mat, rot_x_deg=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = size
    ob.rotation_euler = (math.radians(rot_x_deg), 0, 0)
    ob.data.materials.append(mat)
    return ob


def cylinder_x(name, center, radius, depth, mat, verts=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=center,
                                        rotation=(0, math.radians(90), 0))
    ob = bpy.context.active_object
    ob.name = name
    ob.data.materials.append(mat)
    for f in ob.data.polygons:
        f.use_smooth = True
    return ob


# ---- body: (y, half width, bottom z, top z)
BODY = [(2.18, 0.70, 0.22, 0.40), (2.00, 0.84, 0.17, 0.50), (1.65, 0.93, 0.15, 0.58),
        (1.25, 0.96, 0.15, 0.62), (0.70, 0.90, 0.15, 0.64), (0.00, 0.88, 0.15, 0.66),
        (-0.70, 0.92, 0.15, 0.68), (-1.25, 0.97, 0.15, 0.71), (-1.72, 0.94, 0.16, 0.71),
        (-2.08, 0.82, 0.22, 0.66)]
body = loft("Body", [superellipse_ring(*r) for r in BODY], M_PAINT, subsurf=2)

# ---- greenhouse: (y, base half width, base z, roof z)
CABIN = [(1.02, 0.74, 0.58, 0.62), (0.55, 0.74, 0.58, 0.97), (0.00, 0.72, 0.60, 1.06),
         (-0.60, 0.70, 0.62, 1.05), (-1.10, 0.66, 0.64, 0.92), (-1.55, 0.60, 0.66, 0.70)]


def cabin_ring(y, hw, zb, zr):
    k = 0.72
    return [Vector(v) for v in ((-hw, y, zb - 0.08), (-hw, y, zb), (-hw * 0.93, y, zb + 0.55 * (zr - zb)),
                                (-hw * k, y, zr), (0, y, zr + 0.025), (hw * k, y, zr),
                                (hw * 0.93, y, zb + 0.55 * (zr - zb)), (hw, y, zb), (hw, y, zb - 0.08),
                                (0, y, zb - 0.10))]


cabin = loft("Cabin", [cabin_ring(*r) for r in CABIN], M_GLASS, subsurf=1)
# painted roof panel + pillars read better than an all-glass bubble
roof = loft("Roof", [[p + Vector((0, 0, 0.012)) for p in cabin_ring(y, hw * 0.78, zr - 0.05, zr)[2:8]] +
                     [Vector((hw * 0.6, y, zr - 0.06)), Vector((-hw * 0.6, y, zr - 0.06))]
                     for (y, hw, zb, zr) in CABIN[1:5]], M_PAINT, subsurf=1)

# ---- wheel wells then wheels
WHEELS = {"FL": (-0.94, 1.25), "FR": (0.94, 1.25), "RL": (-0.94, -1.25), "RR": (0.94, -1.25)}
for key, (x, y) in WHEELS.items():
    cutter = cylinder_x("cut_" + key, (x * 1.05, y, 0.34), 0.41, 0.55, M_DARK)
    mod = body.modifiers.new("well_" + key, "BOOLEAN")
    mod.object = cutter
    mod.operation = "DIFFERENCE"
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter)
    cylinder_x("Tyre_" + key, (x, y, 0.34), 0.34, 0.24, M_TYRE)
    cylinder_x("Rim_" + key, (x + math.copysign(0.03, x), y, 0.34), 0.22, 0.2, M_RIM, 10)

# ---- aero, lights
box("Splitter", (0, 2.08, 0.12), (1.60, 0.30, 0.04), M_DARK)
box("Diffuser", (0, -2.02, 0.20), (1.50, 0.30, 0.08), M_DARK, -12)
box("Grille", (0, 2.13, 0.30), (0.90, 0.08, 0.10), M_DARK)
box("Wing", (0, -1.86, 0.93), (1.72, 0.36, 0.04), M_DARK, -7)
for s in (-1, 1):
    box(f"Strut{s}", (s * 0.52, -1.84, 0.80), (0.05, 0.12, 0.26), M_DARK)
    box(f"Endplate{s}", (s * 0.87, -1.86, 0.93), (0.03, 0.42, 0.20), M_DARK)
    box(f"Head{s}", (s * 0.58, 1.98, 0.46), (0.34, 0.12, 0.07), M_HEAD, -20)
    box(f"Tail{s}", (s * 0.56, -2.06, 0.58), (0.42, 0.06, 0.07), M_TAIL)
    box(f"Skirt{s}", (s * 0.93, 0, 0.17), (0.10, 1.8, 0.06), M_DARK)

# ---- lighting + render settings
world = bpy.data.worlds.new("w")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.8, 0.82, 0.86, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 3.5
sun.rotation_euler = (math.radians(40), math.radians(10), math.radians(-35))
scene.collection.objects.link(sun)

scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 48
scene.cycles.use_denoising = True
scene.render.film_transparent = True
scene.render.resolution_x = scene.render.resolution_y = 768
scene.render.image_settings.file_format = "PNG"
scene.render.image_settings.color_mode = "RGBA"
scene.view_settings.view_transform = "Standard"

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
scene.collection.objects.link(cam)
scene.camera = cam


def shoot(path, loc, lens=50, target=(0, 0, 0.5)):
    cam.location = loc
    cam.data.lens = lens
    d = Vector(target) - Vector(loc)
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


shoot(out_dir / "reference.png", (-5.6, 6.4, 2.7), 55, (0, 0.1, 0.45))
shoot(out_dir / "reference_side.png", (-9.0, 0, 0.8), 50, (0, 0, 0.55))
bpy.ops.wm.save_as_mainfile(filepath=str(out_dir / "reference.blend"))
print("REFERENCE OK", out_dir / "reference.png")
