"""Blender helpers shared by conform_car.py and conform_prop.py (run inside Blender's Python)."""
import bpy
from mathutils import Vector


def bbox(ob):
    xs = [v.co for v in ob.data.vertices]
    return Vector((min(v.x for v in xs), min(v.y for v in xs), min(v.z for v in xs))), \
        Vector((max(v.x for v in xs), max(v.y for v in xs), max(v.z for v in xs)))


def transform(ob, m):
    ob.data.transform(m)
    ob.data.update()


def apply_mod(ob, mod):
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.modifier_apply(modifier=mod.name)


def decimate(ob, target_tris, symmetric=True):
    ob.data.calc_loop_triangles()
    n = len(ob.data.loop_triangles)
    if n <= target_tris:
        return
    mod = ob.modifiers.new("dec", "DECIMATE")
    mod.ratio = target_tris / n
    mod.use_symmetry = symmetric
    mod.symmetry_axis = "X"
    mod.use_collapse_triangulate = True
    apply_mod(ob, mod)


def color_layer(me):
    return me.color_attributes.get("Col") or me.color_attributes.new("Col", "BYTE_COLOR", "CORNER")


# colours are stored as sRGB bytes; set via .color_srgb when available so the bytes equal the palette
def set_colors(ob, fn):
    me = ob.data
    col = color_layer(me)
    for p in me.polygons:
        c = fn(p)
        for li in p.loop_indices:
            if hasattr(col.data[li], "color_srgb"):
                col.data[li].color_srgb = c
            else:
                col.data[li].color = c
