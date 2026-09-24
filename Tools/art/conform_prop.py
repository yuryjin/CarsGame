"""Conform a raw Hunyuan3D shape into a small CarsGame prop described in Tools/art/props_spec.json.

blender --background --factory-startup --python-exit-code 1 --python conform_prop.py -- \
    <shape_raw.glb> <item name> <out_dir>

Steps (all deterministic):
  1. import and join; the optional spec 'rot' (Euler degrees, Blender axes X right, Y forward, Z up) turns
     the object into the game pose; 'mirror_x' symmetrises it across X;
  2. scale uniformly so it fits the spec bounds (Unity axes) and centre it in them;
  3. decimate to the item's triangle target, drop tiny islands;
  4. flat per-face vertex colours from the palette by the spec's zone rules (alpha = emission mask);
  5. export one mesh as FBX (Unity axes: +Z forward, +Y up, metres; no UVs, no materials used by the game),
     write validation.json (exit 1 when a check fails) and a 4-view contact sheet.
"""
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from artlib import bbox, decimate, set_colors, transform  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, NAME, OUT = Path(argv[0]).resolve(), argv[1], Path(argv[2]).resolve()
OUT.mkdir(parents=True, exist_ok=True)
SPEC_ALL = json.loads((Path(__file__).resolve().parent / "props_spec.json").read_text())
SPEC = SPEC_ALL["items"][NAME]
PAL = {k: tuple(v) for k, v in SPEC_ALL["palette"].items() if not k.startswith("_")}
BUDGET = SPEC_ALL["budget"][SPEC["kind"] + "_tris"]
MIRROR = bool(SPEC.get("mirror_x"))


def u2m(v):
    """Unity (x, y, z) -> modelling (x, z, y)."""
    return Vector((v[0], v[2], v[1]))


T_MIN, T_MAX = u2m(SPEC["bounds"]["min"]), u2m(SPEC["bounds"]["max"])
T_C, T_H = (T_MIN + T_MAX) * 0.5, (T_MAX - T_MIN) * 0.5
report = {"item": NAME, "source": str(SRC), "tris_target": SPEC["tris"], "budget": BUDGET}

# ------------------------------------------------------------------ import
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
ob = bpy.context.active_object
ob.name = NAME
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(scene.objects):
    if o != ob:
        bpy.data.objects.remove(o)
for uv in list(ob.data.uv_layers):
    ob.data.uv_layers.remove(uv)
for ca in list(ob.data.color_attributes):
    ob.data.color_attributes.remove(ca)
report["faces_raw"] = len(ob.data.polygons)

# 1. pose
rot = SPEC.get("rot", [0, 0, 0])
if any(rot):
    transform(ob, Euler([math.radians(a) for a in rot], "XYZ").to_matrix().to_4x4())
decimate(ob, 60000, symmetric=False)

# optional cuts (handles the game replaces with its extending arm): {"axis": "y", "below"|"above": v},
# v in the unit box (-1..1, Unity axes) of the posed shape; the cut is capped so the mesh stays closed
AXIS = {"x": 0, "y": 2, "z": 1}
for cut in SPEC.get("cut", []):
    lo, hi = bbox(ob)
    i = AXIS[cut["axis"]]
    below = "below" in cut
    v = cut["below"] if below else cut["above"]
    co = (lo + hi) * 0.5
    co[i] = co[i] + v * (hi[i] - lo[i]) * 0.5
    no = Vector((0, 0, 0))
    no[i] = 1.0
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no,
                                 clear_inner=below, clear_outer=not below)
    cut_edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    bmesh.ops.holes_fill(bm, edges=cut_edges, sides=0)
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()
report["cuts"] = SPEC.get("cut", [])


def fit():
    lo, hi = bbox(ob)
    ext = hi - lo
    s = min((T_MAX[i] - T_MIN[i]) / ext[i] for i in range(3))
    transform(ob, Matrix.Translation(-(lo + hi) * 0.5))
    transform(ob, Matrix.Scale(s, 4))
    transform(ob, Matrix.Translation(T_C))
    return s


report["scale_factor"] = fit()
if MIRROR:
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.symmetrize(direction="NEGATIVE_X", threshold=0.001)
    bpy.ops.mesh.remove_doubles(threshold=0.001)
    bpy.ops.object.mode_set(mode="OBJECT")
    fit()

# 2. budget
decimate(ob, SPEC["tris"], symmetric=MIRROR)
bpy.context.view_layer.objects.active = ob
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.dissolve_degenerate(threshold=0.001)
bpy.ops.mesh.delete_loose()
bpy.ops.mesh.normals_make_consistent(inside=False)
bpy.ops.mesh.quads_convert_to_tris()
bpy.ops.object.mode_set(mode="OBJECT")

bm = bmesh.new()
bm.from_mesh(ob.data)
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
dropped = [i for i in islands if len(i) < 0.03 * biggest]
bmesh.ops.delete(bm, geom=list({f for i in dropped for f in i}), context="FACES")
bm.to_mesh(ob.data)
bm.free()
report["islands_dropped"] = len(dropped)
fit()   # dropping islands can shrink the box: refit so the prop fills its slot

# 3. colours by zones (rules speak Unity axes). Decimation leaves big faces (a whole crate side can be two
# triangles), so first cut the mesh along every axis boundary the rules use: the zones then follow the
# edges exactly (a cross, a band) for a few extra triangles.
RULES = SPEC["colors"]["rules"]
# zones are relative to the fitted mesh's own box (uniform scaling leaves slack on the non-limiting axes)
_lo, _hi = bbox(ob)
Z_C, Z_H = (_lo + _hi) * 0.5, (_hi - _lo) * 0.5
planes = sorted({(k, b) for r in RULES for k in ("x", "y", "z") if k in r for b in r[k] if -0.999 < b < 0.999})
bm = bmesh.new()
bm.from_mesh(ob.data)
for k, b in planes:
    i = AXIS[k]
    co = Z_C.copy()
    co[i] = Z_C[i] + b * Z_H[i]
    no = Vector((0, 0, 0))
    no[i] = 1.0
    bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no)
bmesh.ops.triangulate(bm, faces=bm.faces[:])
bm.to_mesh(ob.data)
bm.free()
ob.data.update()
report["zone_planes"] = len(planes)
counts = {}


def within(val, rng):
    return rng[0] <= val <= rng[1]


def zone(p):
    c, n = p.center, p.normal
    ux, uy, uz = ((c.x - Z_C.x) / Z_H.x, (c.z - Z_C.z) / Z_H.z, (c.y - Z_C.y) / Z_H.y)
    q = {"x": ux, "y": uy, "z": uz, "nx": n.x, "ny": n.z, "nz": n.y,
         "rxy": math.hypot(ux, uy), "rxz": math.hypot(ux, uz), "ryz": math.hypot(uy, uz)}
    for r in RULES:
        if all(within(q[k], v) for k, v in r.items() if k != "c"):
            return r["c"]
    return SPEC["colors"]["default"]


tags = list(PAL)
face_tag = [tags.index(zone(p)) for p in ob.data.polygons]
report["faces_before_dissolve"] = len(face_tag)

# merge coplanar neighbours of the same colour back into big faces (the zone cuts only stay where the
# colour changes): one material slot per colour so the dissolve is delimited by colour, then retriangulate
for t in tags:
    ob.data.materials.append(bpy.data.materials.new("tag_" + t))
for p, ti in zip(ob.data.polygons, face_tag):
    p.material_index = ti
bpy.context.view_layer.objects.active = ob
bpy.ops.object.mode_set(mode="EDIT")
bpy.ops.mesh.select_all(action="SELECT")
bpy.ops.mesh.dissolve_limited(angle_limit=math.radians(2.0), delimit={"MATERIAL"})
bpy.ops.mesh.quads_convert_to_tris(quad_method="BEAUTY", ngon_method="BEAUTY")
bpy.ops.object.mode_set(mode="OBJECT")


def by_slot(p):
    tag = tags[p.material_index]
    counts[tag] = counts.get(tag, 0) + 1
    return PAL[tag]


set_colors(ob, by_slot)
report["face_colors"] = counts

# 4. validation + export
for p in ob.data.polygons:
    p.use_smooth = False
mat = bpy.data.materials.new("M_Props")
mat.use_nodes = True
attr = mat.node_tree.nodes.new("ShaderNodeVertexColor")
attr.layer_name = "Col"
mat.node_tree.links.new(attr.outputs["Color"], mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
for p in ob.data.polygons:
    p.material_index = 0
ob.data.materials.clear()
ob.data.materials.append(mat)
ob.data.calc_loop_triangles()
tris = len(ob.data.loop_triangles)
lo, hi = bbox(ob)
tol = 0.02
report.update({
    "tris": tris,
    "bbox_unity": {"min": [lo.x, lo.z, lo.y], "max": [hi.x, hi.z, hi.y]},
})
checks = {
    "tris_in_kind_budget": BUDGET[0] <= tris <= BUDGET[1],
    "inside_bounds": all(lo[i] >= T_MIN[i] - tol and hi[i] <= T_MAX[i] + tol for i in range(3)),
    "fills_bounds": max((hi[i] - lo[i]) / (T_MAX[i] - T_MIN[i]) for i in range(3)) > 0.97,
    "one_mesh": len([o for o in scene.objects if o.type == "MESH"]) == 1,
    "has_colors": len(ob.data.color_attributes) == 1,
    "no_uvs": len(ob.data.uv_layers) == 0,
    "symmetric": (not MIRROR) or abs(lo.x + hi.x) < 0.01,
}
report["checks"] = checks
report["ok"] = all(checks.values())

scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
bpy.ops.object.select_all(action="SELECT")
fbx = OUT / f"{NAME}.fbx"
bpy.ops.export_scene.fbx(
    filepath=str(fbx), use_selection=False, object_types={"MESH"},
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", global_scale=1.0,
    axis_forward="Z", axis_up="Y", bake_space_transform=True,
    mesh_smooth_type="FACE", use_mesh_modifiers=True, colors_type="SRGB", prioritize_active_color=True,
    add_leaf_bones=False, bake_anim=False, use_custom_props=False, path_mode="STRIP", embed_textures=False,
    use_tspace=False)
report["fbx"] = str(fbx)
report["fbx_bytes"] = fbx.stat().st_size


# 5. contact sheet: front (-Y looks at +Y... i.e. the camera sits in front of the +Unity Z face), side, top, 3/4
def render_sheet(path):
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light = "STUDIO"
    sh.color_type = "VERTEX"
    sh.show_object_outline = True
    scene.render.resolution_x = scene.render.resolution_y = 384
    scene.render.film_transparent = False
    scene.world = bpy.data.worlds.new("w")
    scene.world.color = (0.8, 0.8, 0.82)
    cam_data = bpy.data.cameras.new("cam")
    cam_data.type = "ORTHO"
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    centre = (lo + hi) * 0.5
    size = max(hi - lo) * 1.25
    cam_data.ortho_scale = size
    views = [("front", Vector((0, 1, 0))), ("side", Vector((1, 0, 0))), ("top", Vector((0, 0.001, 1))),
             ("three_quarter", Vector((0.8, 0.9, 0.6)))]
    tiles = []
    for name, d in views:
        d = d.normalized()
        cam.location = centre + d * size * 3
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        cam_data.clip_end = size * 10
        p = OUT / f"_view_{name}.png"
        scene.render.filepath = str(p)
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(str(p))
        px = np.array(img.pixels[:], dtype=np.float32).reshape(384, 384, 4)
        tiles.append(px)
        bpy.data.images.remove(img)
        p.unlink()
    sheet = np.concatenate([np.concatenate(tiles[2:], axis=1), np.concatenate(tiles[:2], axis=1)], axis=0)
    out = bpy.data.images.new("sheet", 768, 768)
    out.pixels = sheet.ravel()
    out.filepath_raw = str(path)
    out.file_format = "PNG"
    out.save()


render_sheet(OUT / f"{NAME}_sheet.png")
(OUT / f"{NAME}_validation.json").write_text(json.dumps(report, indent=2, default=float))
print("CONFORM", NAME, "OK" if report["ok"] else "FAILED", json.dumps({k: report[k] for k in ("tris", "face_colors", "checks")}))
if not report["ok"]:
    sys.exit(1)
