"""Conform a raw Hunyuan3D shape into a CarsGame car body that satisfies Tools/art/spec.json.

blender --background --factory-startup --python-exit-code 1 --python conform_car.py -- \
    <shape_raw.glb> <reference.blend> <out_dir> [body_tris=4200]

Steps (all deterministic):
  1. import, find the long axis and the nose (the end without the tall rear wing), scale uniformly to the
     reference length, put the lowest point on the ground and centre it like the reference;
  2. pre-decimate, cut the fused Hunyuan wheels out with cylinders (this also opens the wheel arches),
     mirror the left half onto the right (exact X symmetry), decimate to the body budget;
  3. flat shading + per-face vertex colours transferred from the reference materials (nearest surface):
     alpha 1 = paint (tinted in game by CarPaint), 0 = fixed colour, 0.5 = fixed + emissive (lights);
  4. add the turret mount (merged into the body, fixed colour), a separate recoiling barrel node with a
     Muzzle empty, and four clean low-poly wheels under Wheel_FL/FR/RL/RR pivots;
  5. export FBX (Unity axes: +Z forward, +Y up, metres), write validation.json, render a contact sheet.
Modelling space here: X right, Y forward, Z up.
"""
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, str(Path(__file__).resolve().parent))
from artlib import apply_mod, bbox, color_layer, decimate, set_colors, transform  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, REF, OUT = Path(argv[0]).resolve(), Path(argv[1]).resolve(), Path(argv[2]).resolve()
BODY_TRIS = int(argv[3]) if len(argv) > 3 else 4200
OUT.mkdir(parents=True, exist_ok=True)
SPEC = json.loads((Path(__file__).resolve().parent / "spec.json").read_text())
PAL = {k: tuple(v) for k, v in SPEC["vertex_palette"].items()}
WHEEL_R, WHEEL_W = SPEC["wheels"]["radius"], SPEC["wheels"]["width"]
# Unity (x, y, z) -> modelling (x, z, y)
WHEELS = {k: Vector((v[0], v[2], v[1])) for k, v in SPEC["wheels"]["centres"].items()}
report = {"source": str(SRC), "reference": str(REF), "body_tris_target": BODY_TRIS}

# ------------------------------------------------------------------ reference (for colour transfer)
bpy.ops.wm.open_mainfile(filepath=str(REF))
REF_MAT_TO_PAL = {"paint": "paint", "glass": "glass", "dark": "trim_dark", "tyre": "trim_dark",
                  "rim": "trim_dark", "head": "headlight", "tail": "taillight"}
ref_tris, ref_tags = [], []
ref_min, ref_max = Vector((9, 9, 9)), Vector((-9, -9, -9))
dg = bpy.context.evaluated_depsgraph_get()
for ob in bpy.data.objects:
    if ob.type != "MESH":
        continue
    me = ob.evaluated_get(dg).to_mesh()
    me.calc_loop_triangles()
    mw = ob.matrix_world
    for lt in me.loop_triangles:
        mat = ob.material_slots[lt.material_index].material.name if ob.material_slots else "paint"
        ref_tris.append([mw @ me.vertices[i].co for i in lt.vertices])
        ref_tags.append(REF_MAT_TO_PAL.get(mat, "trim_dark"))
    for v in me.vertices:
        w = mw @ v.co
        ref_min = Vector(map(min, ref_min, w))
        ref_max = Vector(map(max, ref_max, w))
    ob.evaluated_get(dg).to_mesh_clear()
ref_verts, ref_faces = [], []
for t in ref_tris:
    ref_faces.append((len(ref_verts), len(ref_verts) + 1, len(ref_verts) + 2))
    ref_verts.extend(t)
REF_BVH = BVHTree.FromPolygons(ref_verts, ref_faces)
report["reference_bbox"] = [list(ref_min), list(ref_max)]

# ------------------------------------------------------------------ fresh scene + import
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
bpy.ops.import_scene.gltf(filepath=str(SRC))
meshes = [o for o in scene.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
body = bpy.context.active_object
body.name = "Body"
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(scene.objects):
    if o != body:
        bpy.data.objects.remove(o)
report["faces_raw"] = len(body.data.polygons)


# 1. orientation: long horizontal axis -> Y, nose -> +Y (the rear end carries the tall wing)
lo, hi = bbox(body)
ext = hi - lo
if ext.z > min(ext.x, ext.y):
    raise SystemExit(f"unexpected up axis, extents {tuple(ext)}")
if ext.x > ext.y:
    transform(body, Matrix.Rotation(math.radians(90), 4, "Z"))
    lo, hi = bbox(body)
L = hi.y - lo.y
front_top = max((v.co.z for v in body.data.vertices if v.co.y > hi.y - 0.12 * L), default=0)
rear_top = max((v.co.z for v in body.data.vertices if v.co.y < lo.y + 0.12 * L), default=0)
if front_top > rear_top:
    transform(body, Matrix.Rotation(math.radians(180), 4, "Z"))
report["orientation"] = {"front_end_top": front_top, "rear_end_top": rear_top, "swapped_long_axis": bool(ext.x > ext.y)}
lo, hi = bbox(body)
# Uniform scale to the reference length, unless that breaks the width or height limits (Hunyuan bodies
# come out chunkier than the reference): then the tightest limit wins, with 2 % headroom.
_b = SPEC["bounds_m"]
s = min(min(ref_max.y - ref_min.y, _b["length_max"] * 0.98) / (hi.y - lo.y),
        _b["width_max"] * 0.98 / (hi.x - lo.x),
        _b["height_without_turret_max"] * 0.98 / (hi.z - lo.z))
transform(body, Matrix.Scale(s, 4))
lo, hi = bbox(body)
ref_c = (ref_min + ref_max) * 0.5
transform(body, Matrix.Translation(Vector((-(lo.x + hi.x) * 0.5, ref_c.y - (lo.y + hi.y) * 0.5, -lo.z))))
lo, hi = bbox(body)
report["scaled_bbox"] = [list(lo), list(hi)]
report["scale_factor"] = s


def cylinder_x(name, center, radius, depth, verts):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=center,
                                        rotation=(0, math.radians(90), 0))
    ob = bpy.context.active_object
    ob.name = name
    return ob


# 2. clean-up: pre-decimate, cut wheels, mirror, final decimate
decimate(body, 60000, symmetric=False)
for key, c in WHEELS.items():
    side = math.copysign(1, c.x)
    cutter = cylinder_x("cut_" + key, (side * 0.97, c.y, c.z), WHEEL_R + 0.06, 0.62, 48)
    mod = body.modifiers.new("cut", "BOOLEAN")
    mod.object = cutter
    mod.operation = "DIFFERENCE"
    mod.solver = "EXACT"
    apply_mod(body, mod)
    bpy.data.objects.remove(cutter)

bpy.context.view_layer.objects.active = body
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.symmetrize(direction="NEGATIVE_X", threshold=0.002)
bpy.ops.mesh.remove_doubles(threshold=0.004)
bpy.ops.mesh.delete_loose()
bpy.ops.object.mode_set(mode="OBJECT")
decimate(body, BODY_TRIS, symmetric=True)
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.dissolve_degenerate(threshold=0.002)
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.mesh.quads_convert_to_tris()
bpy.ops.object.mode_set(mode="OBJECT")

# drop tiny disconnected islands left over by the cut (inner tyre stubs etc.)
bm = bmesh.new()
bm.from_mesh(body.data)
islands, seen = [], set()
for f in bm.faces:
    if f.index in seen:
        continue
    stack, isl = [f], []
    seen.add(f.index)
    while stack:
        g = stack.pop()
        isl.append(g)
        for e in g.edges:
            for h in e.link_faces:
                if h.index not in seen:
                    seen.add(h.index)
                    stack.append(h)
    islands.append(isl)
biggest = max(len(i) for i in islands)
dropped = [i for i in islands if len(i) < 0.02 * biggest]
bmesh.ops.delete(bm, geom=list({f for i in dropped for f in i}), context="FACES")
bm.to_mesh(body.data)
bm.free()
report["islands_dropped"] = len(dropped)


# 3. vertex colours from the nearest reference surface
def paint_faces(ob, fn):
    me = ob.data
    col = color_layer(me)
    for p in me.polygons:
        c = fn(p)
        for li in p.loop_indices:
            col.data[li].color_srgb = c if hasattr(col.data[li], "color_srgb") else c
            col.data[li].color = c


counts = {}


def classify(p):
    loc, _, idx, dist = REF_BVH.find_nearest(p.center)
    tag = ref_tags[idx] if idx is not None and dist < 0.25 else "trim_dark"
    if p.center.z < 0.14:                    # underside / sills never get paint
        tag = "trim_dark"
    counts[tag] = counts.get(tag, 0) + 1
    return PAL[tag]


set_colors(body, classify)
report["body_face_tags"] = counts


# 4a. turret mount on the roof (merged into the body) + separate barrel
def roof_z(x, y):
    hit, _, _, _ = BVHTree.FromObject(body, bpy.context.evaluated_depsgraph_get()).ray_cast(Vector((x, y, 5)), Vector((0, 0, -1)))
    return hit.z if hit else None


TURRET_Y = -0.45                          # same place CarCannon uses on RaceCar (turretLocalPosition.z)
rz = roof_z(0, TURRET_Y)
report["roof_z_at_turret"] = rz
turret_z = (rz if rz is not None else 1.07) + 0.05


def add_prism(bm, center, radius, h, sides, rot_y=0.0):
    ret = bmesh.ops.create_cone(bm, cap_ends=True, segments=sides, radius1=radius, radius2=radius, depth=h)
    bmesh.ops.translate(bm, verts=ret["verts"], vec=center)
    return ret["verts"]


def add_box(bm, center, size):
    ret = bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=size, verts=ret["verts"])
    bmesh.ops.translate(bm, verts=ret["verts"], vec=center)
    return ret["verts"]


tm = bpy.data.meshes.new("turret_tmp")
bm = bmesh.new()
add_prism(bm, Vector((0, TURRET_Y, turret_z - 0.05)), 0.29, 0.07, 10)            # base ring
add_box(bm, Vector((0, TURRET_Y, turret_z + 0.08)), Vector((0.34, 0.46, 0.20)))  # housing
bm.to_mesh(tm)
bm.free()
tob = bpy.data.objects.new("turret_tmp", tm)
scene.collection.objects.link(tob)
set_colors(tob, lambda p: PAL["gun"])
bpy.ops.object.select_all(action="DESELECT")
tob.select_set(True)
body.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
body = bpy.context.active_object


def empty(name, loc, parent=None):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.15
    scene.collection.objects.link(e)
    if parent:
        e.parent = parent
    e.location = loc
    return e


turret = empty("Cannon_Turret", Vector((0, TURRET_Y, turret_z)))
BARREL_LEN = SPEC["turret"]["barrel_length"]
bme = bpy.data.meshes.new("Cannon_Barrel")
bm = bmesh.new()
tube = bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.055, radius2=0.055, depth=BARREL_LEN)["verts"]
bmesh.ops.rotate(bm, verts=tube, cent=Vector(), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
bmesh.ops.translate(bm, verts=tube, vec=Vector((0, BARREL_LEN * 0.5, 0)))
brake = bmesh.ops.create_cone(bm, cap_ends=True, segments=8, radius1=0.075, radius2=0.075, depth=0.16)["verts"]
bmesh.ops.rotate(bm, verts=brake, cent=Vector(), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
bmesh.ops.translate(bm, verts=brake, vec=Vector((0, BARREL_LEN - 0.08, 0)))
bm.to_mesh(bme)
bm.free()
barrel = bpy.data.objects.new("Cannon_Barrel", bme)
scene.collection.objects.link(barrel)
barrel.parent = turret
barrel.location = (0, 0.1, 0.1)          # same offsets as CarCannon.BuildTurret
set_colors(barrel, lambda p: PAL["gun"])
empty("Muzzle", Vector((0, BARREL_LEN + 0.25, 0)), barrel)


# 4b. wheels: pivot empty at the wheel centre -> one spinner mesh (tyre + rim), axle along X
def wheel_mesh(name, outward):
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    tyre = bmesh.ops.create_cone(bm, cap_ends=True, segments=14, radius1=WHEEL_R, radius2=WHEEL_R, depth=WHEEL_W)["verts"]
    bmesh.ops.rotate(bm, verts=tyre, cent=Vector(), matrix=Matrix.Rotation(math.radians(90), 3, "Y"))
    rim = bmesh.ops.create_cone(bm, cap_ends=True, segments=7, radius1=0.21, radius2=0.21, depth=0.03)["verts"]
    bmesh.ops.rotate(bm, verts=rim, cent=Vector(), matrix=Matrix.Rotation(math.radians(90), 3, "Y"))
    bmesh.ops.translate(bm, verts=rim, vec=Vector((outward * (WHEEL_W * 0.5 + 0.005), 0, 0)))
    rim_set = set(rim)
    bm.to_mesh(me)
    rim_idx = {v.index for v in rim_set}
    bm.free()
    return me, rim_idx


for key, c in WHEELS.items():
    outward = math.copysign(1, c.x)
    pivot = empty(key, c)
    me, rim_idx = wheel_mesh(key + "_Spinner", outward)
    sp = bpy.data.objects.new(key + "_Spinner", me)
    scene.collection.objects.link(sp)
    sp.parent = pivot
    sp.location = (0, 0, 0)
    set_colors(sp, lambda p, r=rim_idx: PAL["rim"] if all(v in r for v in p.vertices) else PAL["tyre"])

# 5. flat shading, materials (preview only; Unity assigns its own), validation, export
PREVIEW = {}


def preview_material(name, paint):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    attr = nt.nodes.new("ShaderNodeVertexColor")
    attr.layer_name = "Col"
    bsdf.inputs["Roughness"].default_value = 0.55
    if paint:   # alpha 1 -> red paint preview, else vertex rgb
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        gt = nt.nodes.new("ShaderNodeMath")
        gt.operation = "GREATER_THAN"
        gt.inputs[1].default_value = 0.75
        nt.links.new(attr.outputs["Alpha"], gt.inputs[0])
        nt.links.new(gt.outputs[0], mix.inputs["Factor"])
        nt.links.new(attr.outputs["Color"], mix.inputs["A"])
        mix.inputs["B"].default_value = (0.8, 0.06, 0.04, 1)
        nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    else:
        nt.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    return m


mat_paint = preview_material("M_HunyuanSample_CarPaint", True)
mat_trim = preview_material("M_HunyuanSample_Trim", False)
tris = {}
for ob in scene.objects:
    if ob.type != "MESH":
        continue
    ob.data.materials.clear()
    ob.data.materials.append(mat_paint if ob == body else mat_trim)
    for p in ob.data.polygons:
        p.use_smooth = False
    ob.data.calc_loop_triangles()
    tris[ob.name] = len(ob.data.loop_triangles)

lo, hi = bbox(body)
renderers = [o.name for o in scene.objects if o.type == "MESH"]
report.update({
    "tris": tris, "tris_total": sum(tris.values()), "renderers": renderers, "renderer_count": len(renderers),
    "body_bbox_modelling": [list(lo), list(hi)],
    "body_bbox_unity": {"min": [lo.x, lo.z, lo.y], "max": [hi.x, hi.z, hi.y]},
    "turret_unity": [0, turret_z, TURRET_Y],
})
b = SPEC["bounds_m"]
bpy.context.view_layer.update()   # matrix_world of freshly parented wheel pivots is stale until the depsgraph runs
checks = {
    "body_tris_in_budget": SPEC["tri_budget"]["body_min"] <= tris["Body"] <= SPEC["tri_budget"]["body_max"],
    "total_tris_in_budget": report["tris_total"] <= SPEC["tri_budget"]["total_max"],
    "renderers_le_max": len(renderers) <= SPEC["renderers_max"],
    "width_ok": hi.x - lo.x <= b["width_max"],
    "length_ok": hi.y - lo.y <= b["length_max"],
    "height_ok": hi.z <= b["height_max"],
    # The wheels carry the car: the lowest point of any mesh (a tyre) sits on the ground, the body above it.
    "on_ground": abs(min((o.matrix_world @ v.co).z for o in scene.objects if o.type == "MESH" for v in o.data.vertices)) < 0.02,
    "symmetric": abs(lo.x + hi.x) < 0.01,
    "wheels_named": all(scene.objects.get(k) for k in WHEELS),
    "paint_faces_present": counts.get("paint", 0) > 0.3 * sum(counts.values()),
}
report["checks"] = checks
report["ok"] = all(checks.values())

scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
bpy.ops.object.select_all(action="SELECT")
fbx = OUT / "HunyuanSampleCar.fbx"
bpy.ops.export_scene.fbx(
    filepath=str(fbx), use_selection=False, object_types={"MESH", "EMPTY"},
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
    axis_forward="Z", axis_up="Y", bake_space_transform=True,
    mesh_smooth_type="FACE", use_mesh_modifiers=True, colors_type="SRGB", prioritize_active_color=True,
    add_leaf_bones=False, bake_anim=False, use_custom_props=False, path_mode="STRIP", embed_textures=False)
report["fbx"] = str(fbx)
report["fbx_bytes"] = fbx.stat().st_size
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "conformed.blend"))
(OUT / "validation.json").write_text(json.dumps(report, indent=2, default=float))
print("CONFORM", "OK" if report["ok"] else "FAILED", json.dumps({k: report[k] for k in ("tris", "renderer_count", "checks")}))
if not report["ok"]:
    sys.exit(1)
