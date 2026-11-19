"""Build the four game shells from the TripoSR silhouette guides. Run inside Blender.

blender -b -t 2 --python-exit-code 1 --python Tools/art/retopo_tripo_cars.py -- OUTPUT_DIRECTORY
Coordinates are metres, X right, Y towards the nose, Z up. The wheel uses the rig's X axle.
"""
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from artlib import apply_mod


# Measured outer half-width at thirteen equally spaced stations on the aligned TripoSR meshes.
# The cabin, wheel openings and light recesses are rebuilt as separate, clean panels.
GUIDES = {
    'Sport':   (.76, .95, .94, .98, .99, .97, .97, .98, .95, .99, .99, .97, .66),
    'Buggy':   (.79, .98, .97, .98, .99, .96, .97, .98, .96, .93, .92, .88, .65),
    'Armored': (.73, .90, .99, .96, .98, .99, .94, .93, .88, .85, .87, .82, .64),
    'Tank':    (.80, .99, .96, .95, .98, .96, .99, .98, .97, .96, .93, .79, .65),
}
PAINT = None
CARBON = (29, 34, 40, 0)
GRAPHITE = (55, 66, 76, 0)
ALLOY = (112, 131, 143, 0)
RUBBER = (19, 23, 27, 0)
RED = (247, 56, 37, 190)
WHITE = (246, 239, 203, 150)
AMBER = (255, 152, 48, 120)


def interp(rows, y):
    for (a, va), (b, vb) in zip(rows, rows[1:]):
        if y <= b:
            t = max(0., (y - a) / (b - a))
            return va + (vb - va) * t
    return rows[-1][1]


def glass(point):
    t = min(1., max(0., (point.z - .53) / .46))
    return (25 + 29*t, 39 + 37*t, 50 + 42*t, 0)


class Car:
    def __init__(self, name):
        self.name = name
        self.parts = {'Hull': [], 'Trim': []}
        self.mats = {key: bpy.data.materials.new(mat) for key, mat in [('Hull', 'Paint'), ('Trim', 'Trim')]}
        self.stations = [(-1.90 + 3.8*i/12, w) for i, w in enumerate(GUIDES.get(name, ()))]

    def width(self, y):
        return interp(self.stations, y)

    def solid(self, name, vertices, faces, color, bevel=.012, segments=1):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(vertices, [], faces)
        mesh.update()
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
        bm.free()
        ob = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(ob)
        # Sub-centimetre trim gets its outline from the silhouette at chase-camera distance.
        if bevel >= .007:
            mod = ob.modifiers.new('Panel edge', 'BEVEL')
            mod.width, mod.segments = bevel, segments
            mod.limit_method = 'ANGLE'
            mod.angle_limit = math.radians(19)
            apply_mod(ob, mod)
        mesh = ob.data
        for face in mesh.polygons:
            face.use_smooth = True
        mesh.set_sharp_from_angle(angle=math.radians(38))
        normals = ob.modifiers.new('Panel normals', 'WEIGHTED_NORMAL')
        normals.keep_sharp = True
        apply_mod(ob, normals)
        key = 'Hull' if color is PAINT else 'Trim'
        ob.data.materials.append(self.mats[key])
        if color is not PAINT:
            palette = ob.data.color_attributes.new(name='Color', type='BYTE_COLOR', domain='POINT')
            for vertex, entry in zip(ob.data.vertices, palette.data):
                rgba = color(vertex.co) if callable(color) else color
                entry.color_srgb = tuple(v/255 for v in rgba)
            ob.data.color_attributes.active_color = palette
        self.parts[key].append(ob)

    def loft(self, name, rings, color, bevel=.012, segments=1):
        n, last = len(rings[0]), (len(rings)-1)*len(rings[0])
        faces = [tuple(reversed(range(n))), tuple(range(last, last+n))]
        for row in range(len(rings)-1):
            for i in range(n):
                a, b = row*n+i, row*n+(i+1)%n
                faces.append((a, b, b+n, a+n))
        self.solid(name, [p for ring in rings for p in ring], faces, color, bevel, segments)

    def prism(self, name, points, extrusion, color, bevel=.012, segments=1):
        self.loft(name, [points, [Vector(p)+Vector(extrusion) for p in points]], color, bevel, segments)

    def box(self, name, x0, x1, y0, y1, z0, z1, color, bevel=.012, segments=1):
        self.prism(name, [(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0)], (0,0,z1-z0), color, bevel, segments)

    def rod(self, name, a, b, radius, color, sides=10, bevel=.004):
        start, end = Vector(a), Vector(b)
        axis = (end-start).normalized()
        u = axis.orthogonal().normalized()*radius
        v = axis.cross(u)
        ring = [u*math.cos(i*math.tau/sides)+v*math.sin(i*math.tau/sides) for i in range(sides)]
        self.loft(name, [[p+delta for delta in ring] for p in (start,end)], color, bevel)

    def hull(self, name, rows, color=PAINT, bevel=.026):
        rings = []
        for y, width, bottom, top in rows:
            h = top-bottom
            side = [(width*.72,bottom),(width*.94,bottom+h*.16),(width,bottom+h*.59),
                    (width*.94,top-h*.13),(width*.77,top)]
            ring = [(-x,y,z) for x,z in reversed(side)] + [(x,y,z) for x,z in side]
            rings.append(ring)
        self.loft(name, rings, color, bevel, 2)

    def deck(self, name, x0, x1, y0, y1, height, thickness, color, bevel=.007):
        points = [(x,y,height(y)) for x,y in ((x0,y0),(x1,y0),(x1,y1),(x0,y1))]
        self.prism(name, points, (0,0,-thickness), color, bevel)

    def face(self, name, side, y, outline, depth, color, bevel=.008):
        self.prism(name, [(side*x,y,z) for x,z in outline], (0,depth,0), color, bevel)

    def cheek(self, name, side, x, outline, depth, color, bevel=.009):
        self.prism(name, [(side*x,y,z) for y,z in outline], (-side*depth,0,0), color, bevel)

    def louvers(self, side, x0, x1, y0, y1, height, count):
        self.deck('Vent recess', side*x0,side*x1,y0,y1, height,.025,CARBON)
        pitch = (y1-y0)/count
        for i in range(count):
            y = y0 + (i+.18)*pitch
            points = [(side*x,y+dy,height(y+dy)+rise) for x,dy,rise in
                      ((x0+.017,0,.011),(x1-.017,0,.011),(x1-.017,pitch*.46,.027),(x0+.017,pitch*.46,.027))]
            self.prism('Vent blade', points, (0,0,-.012), GRAPHITE, .003, 1)

    def fenders(self, axle, radius_y, hub, radius_z, tops, inner, separate=False, angular=False):
        arc_steps = 7 if angular else 10
        for side in (-1,1):
            intervals = [(-1.91,-.65),(.65,1.85)] if separate else [(-1.91,1.85)]
            for lo,hi in intervals:
                ys = {lo, hi}
                ys.update(y for y, _ in tops if lo < y < hi)
                for center in (-axle,axle):
                    for i in range(arc_steps+1):
                        y = center+radius_y*math.cos(i*math.pi/arc_steps)
                        if lo < y < hi:
                            ys.add(y)
                rings = []
                for y in sorted(ys):
                    d = min(abs(y-axle),abs(y+axle)) / radius_y
                    if d <= 1:
                        low = hub + radius_z * math.sqrt(max(0., 1-d*d))
                    else:
                        low = max(.145, hub-(d-1)*1.8)
                    high = max(interp(tops,y),low+.088)
                    w=self.width(y)
                    ring=[(inner-.04,low+.006),(w-.025,low),(w,low+.025),
                          (w,high-.086),(w-.067,high),(inner+.06,high+.012),(inner-.04,high-.048)]
                    rings.append([(side*x,y,z) for x,z in ring])
                self.loft('Wheel arch and shoulder',rings,PAINT,0)
            for center in (-axle,axle):
                rings=[]
                for i in range(arc_steps+1):
                    angle=i*math.pi/arc_steps
                    y = center + radius_y*math.cos(angle)
                    z = hub + radius_z*math.sin(angle)
                    w=self.width(y)+.003
                    rings.append([(side*(w+dx),y,z+dz) for dx,dz in
                                  ((-.083,0),(.001,0),(.002,.023 if not angular else .045),(-.083,.024))])
                self.loft('Arch lining',rings,GRAPHITE if angular else RUBBER,.004,1)

    def exhaust(self, x, y, z, radius):
        # An open metal lip around a recessed dark pipe, visible from the chase camera.
        n=14
        rings=[(radius,y+.10),(radius,y),(radius*.73,y-.002),(radius*.73,y+.055)]
        v=[(x+r*math.cos(i*math.tau/n),p,z+r*math.sin(i*math.tau/n)) for r,p in rings for i in range(n)]
        faces=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(3) for i in range(n)]
        faces.append(tuple(range(3*n,4*n)))
        self.solid('Exhaust lip',v,faces,ALLOY,.003,1)
        self.rod('Exhaust interior',(x,y+.030,z),(x,y+.044,z),radius*.73,CARBON,14,0)

    def export(self, out, budget):
        count=0
        for key, parts in self.parts.items():
            if not parts:
                continue
            bpy.ops.object.select_all(action='DESELECT')
            for ob in parts:
                ob.select_set(True)
            bpy.context.view_layer.objects.active=parts[0]
            bpy.ops.object.join()
            ob=parts[0]
            ob.name=ob.data.name=key
            ob.data.calc_loop_triangles()
            count+=len(ob.data.loop_triangles)
        if not budget[0] <= count <= budget[1]:
            raise ValueError(f'{self.name} has {count} triangles, expected {budget}')
        print(self.name, count, 'triangles', flush=True)
        bpy.ops.export_scene.fbx(filepath=str(out/(self.name+'.fbx')),object_types={'MESH'},
            apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='Z',axis_up='Y',
            bake_space_transform=True,mesh_smooth_type='FACE',colors_type='SRGB',prioritize_active_color=True,
            add_leaf_bones=False,bake_anim=False,use_custom_props=False,path_mode='STRIP',use_tspace=False)


def sport(c):
    c.hull('Monocoque',[(-1.94,.81,.15,.52),(-1.75,.86,.115,.59),(-1.33,.79,.09,.64),
        (-.88,.75,.09,.65),(-.42,.72,.075,.59),(.3,.71,.075,.54),(.8,.70,.085,.49),
        (1.32,.68,.095,.40),(1.76,.63,.10,.33),(1.95,.51,.13,.275)])
    c.hull('Floor and splitter',[(-1.99,.78,.063,.16),(-1.75,.82,.045,.19),(-.6,.77,.040,.13),
        (.65,.77,.040,.13),(1.67,.81,.037,.14),(2.,.66,.055,.13)],CARBON,.016)
    c.fenders(1.20,.437,.23,.373,[(-1.91,.52),(-1.63,.62),(-1.22,.73),(-.87,.66),(-.57,.42),
        (-.14,.40),(.53,.42),(.85,.58),(1.20,.70),(1.55,.54),(1.85,.315)],.57)

    # Glazed teardrop and painted roof form different closed surfaces with a crisp window seal.
    canopy=[(-1.12,.43,.627,.659),(-.78,.565,.610,.842),(-.56,.565,.605,.958),
            (-.28,.558,.595,.991),(.015,.54,.584,.972),(.21,.53,.572,.912),(.76,.449,.495,.545)]
    c.hull('Canopy',canopy,glass,.021)
    c.hull('Roof',[(-.61,.414,.926,.958),(-.50,.432,.95,.982),(-.30,.44,.976,1.0),
                   (-.06,.423,.963,.989),(.09,.394,.928,.953)],PAINT,.012)
    for s in (-1,1):
        for a,b in [((.350,.736,.542),(.454,.16,.926)),((.454,.16,.926),(.476,-.45,.962)),
                    ((.476,-.45,.962),(.432,-1.07,.663))]:
            c.rod('Window surround',(s*a[0],a[1],a[2]),(s*b[0],b[1],b[2]),.014,GRAPHITE,8,.002)
        c.rod('Window pillar',(s*.532,-.36,.68),(s*.476,-.46,.952),.019,CARBON,8,.004)
        c.cheek('Side intake',s,.928,[(-.61,.19),(.50,.20),(.44,.34),(-.24,.43),(-.54,.38)],.085,CARBON)
        c.cheek('Sill blade',s,.947,[(-.61,.147),(.60,.135),(.53,.19),(-.51,.205)],.20,GRAPHITE)
        c.deck('Rear deck shoulder',s*.40,s*.69,-1.89,-1.02,lambda y:.636+.09*(y+1.89),.085,PAINT,.022)
        c.louvers(s,.26,.48,-1.75,-1.19,lambda y:.631+.08*(y+1.75),6)
        c.louvers(s,.36,.55,.83,1.33,lambda y:.513-.177*(y-.83),4)
        c.face('Headlamp bezel',s,1.866,[(.54,.16),(.82,.17),(.88,.254),(.58,.27)],-.115,CARBON,.012)
        c.face('Headlamp signature',s,1.879,[(.568,.194),(.792,.197),(.848,.238),(.585,.237)],-.020,WHITE,.005)
        c.face('Rear lamp surround',s,-1.956,[(.20,.369),(.75,.357),(.836,.394),(.832,.486),(.225,.485)],.112,CARBON)
        c.face('Rear light blade',s,-1.970,[(.222,.433),(.730,.418),(.808,.452),(.807,.472),(.223,.465)],.024,RED,.004)
        c.face('Rear light return',s,-1.969,[(.716,.388),(.761,.385),(.808,.414),(.808,.432)],.022,RED,.003)
        c.exhaust(s*.56,-1.991,.215,.063)
    c.face('Rear grille',1,-1.952,[(-.43,.196),(.43,.196),(.53,.306),(.45,.354),(-.45,.354),(-.53,.306)],.09,CARBON)
    for z in (.239,.281,.323): c.box('Grille fin',-.398,.398,-1.962,-1.940,z,z+.012,GRAPHITE,.003,1)
    for x in (-.69,-.33,.33,.69):
        c.cheek('Diffuser vane',1,x,[(-1.997,.034),(-1.995,.14),(-1.51,.17),(-1.63,.084)],.022,GRAPHITE,.004)


def buggy(c):
    c.hull('Tub',[(-1.84,.62,.18,.43),(-1.48,.68,.11,.54),(-.85,.68,.08,.49),
        (-.43,.66,.075,.31),(.30,.64,.075,.30),(.70,.60,.09,.39),(1.08,.56,.13,.43),
        (1.51,.51,.14,.38),(1.93,.35,.14,.27)],PAINT)
    c.hull('Skid pan',[(-1.93,.57,.07,.22),(-.83,.65,.035,.18),(.8,.58,.035,.16),
        (1.99,.37,.12,.19)],GRAPHITE,.018)
    c.fenders(1.29,.49,.09,.34,[(-1.91,.33),(-1.30,.55),(-.65,.38),(.65,.35),(1.29,.51),(1.85,.29)],.53,True)
    c.box('Cockpit floor',-.58,.58,-.77,.46,.278,.318,CARBON,.024)
    for s in (-1,1):
        c.cheek('Door cut-out',s,.660,[(-.75,.31),(.56,.28),(.39,.48),(-.62,.55)],.11,PAINT,.022)
        c.cheek('Sill insert',s,.667,[(-.49,.288),(.4,.28),(.30,.36),(-.44,.414)],.014,GRAPHITE,.007)
        cx=s*.26
        c.box('Seat cushion',cx-.17,cx+.17,-.57,.08,.308,.393,GRAPHITE,.050,2)
        c.prism('Seat back',[(cx-.164,-.56,.36),(cx+.164,-.56,.36),(cx+.132,-.72,.72),(cx-.132,-.72,.72)],(0,-.063,0),CARBON,.034,2)
        c.box('Headrest',cx-.108,cx+.108,-.77,-.67,.673,.79,GRAPHITE,.034,2)
        for dx in (-.067,.067):
            c.rod('Harness',(cx+dx,-.567,.42),(cx+dx,-.68,.682),.018,ALLOY,6,.002)
        cage=[(.62,.62,.345),(.48,.075,.924),(.453,-.56,.973),(.565,-1.23,.526)]
        for a,b in zip(cage,cage[1:]):
            c.rod('Roll hoop',(s*a[0],a[1],a[2]),(s*b[0],b[1],b[2]),.048,GRAPHITE,12,0)
        c.rod('Door brace',(s*.60,.42,.326),(s*.545,-.60,.68),.027,GRAPHITE,10)
        c.rod('Rear cage diagonal',(s*.46,-.57,.917),(-s*.54,-1.19,.531),.028,ALLOY,10)
        c.box('Square headlamp',s*.58-.084,s*.58+.084,1.75,1.886,.236,.395,CARBON,.023)
        c.box('Headlamp lens',s*.58-.052,s*.58+.052,1.88,1.898,.266,.363,WHITE,.012)
        c.box('Rear marker',s*.595-.097,s*.595+.097,-1.864,-1.799,.356,.459,CARBON,.017)
        c.box('Rear lens',s*.595-.071,s*.595+.071,-1.874,-1.851,.378,.435,RED,.014)
        c.louvers(s,.095,.39,-1.74,-1.19,lambda y:.55+.03*(y+1.74),5)
        c.rod('Rear crash hoop',(s*.64,-1.88,.21),(s*.57,-1.53,.34),.041,GRAPHITE)
        c.exhaust(s*.34,-1.95,.28,.075)
    for y,z,w in [(.075,.924,.48),(-.56,.973,.453),(-1.23,.526,.565)]:
        c.rod('Cage crossmember',(-w,y,z),(w,y,z),.042,GRAPHITE,12)
    c.hull('Roof mount',[(-.55,.24,.936,.985),(-.33,.30,.947,1.0),(.09,.25,.905,.963)],PAINT,.014)
    c.rod('Bumper',(-.64,-1.91,.214),(.64,-1.91,.214),.045,GRAPHITE,12)
    c.rod('Dash',(-.50,.45,.46),(.50,.45,.46),.076,CARBON,14)


def armored(c):
    c.hull('Armored chassis',[(-1.94,.78,.105,.52),(-1.68,.84,.09,.585),(-.94,.79,.08,.61),
        (-.18,.76,.08,.525),(.75,.75,.085,.46),(1.29,.69,.11,.45),(1.93,.62,.16,.37)])
    c.hull('Lower armor',[(-1.985,.85,.08,.245),(-1.51,.82,.045,.22),(.83,.75,.038,.23),
        (1.70,.815,.08,.26),(1.995,.72,.11,.22)],GRAPHITE,.024)
    c.fenders(1.28,.44,.20,.365,[(-1.91,.47),(-1.3,.73),(-.83,.58),(.68,.51),(1.28,.69),(1.85,.41)],.54,angular=True)
    c.hull('Cab shell',[(-1.68,.72,.53,.84),(-1.53,.735,.54,.947),(-.90,.714,.54,.979),
        (-.18,.685,.49,.982),(.20,.67,.48,.94),(.76,.65,.46,.535)],PAINT,.027)
    windshield=[(.036,.705,.566),(.567,.707,.558),(.559,.237,.907),(.036,.237,.924)]
    for s in (-1,1):
        c.prism('Windshield',[(s*x,y+.019,z+.020) for x,y,z in windshield],(0,-.033,-.019),glass,.014)
        c.cheek('Side glazing',s,.693,[(-.70,.642),(.405,.611),(.153,.886),(-.66,.927)],.058,glass,.015)
        c.cheek('Window divider',s,.703,[(-.20,.623),(-.157,.626),(-.145,.905),(-.197,.912)],.027,GRAPHITE,.004)
        c.cheek('Rear cabin panel',s,.725,[(-1.45,.60),(-.84,.615),(-.86,.894),(-1.40,.855)],.035,GRAPHITE,.012)
        for y in (-1.32,-1.19,-1.06):
            c.cheek('Cab panel louvre',s,.743,[(y,.667),(y+.042,.668),(y+.091,.819),(y+.048,.817)],.020,ALLOY,.004)
        c.cheek('Door armor',s,.886,[(-.70,.20),(.55,.20),(.46,.458),(-.59,.57)],.15,PAINT,.021)
        c.box('Door handle',s*.891-.015,s*.891+.015,-.564,-.374,.457,.483,GRAPHITE,.008)
        c.deck('Roof rail',s*.48,s*.58,-1.46,.08,lambda y:.974+.005*(y+1.46),.050,GRAPHITE,.012)
        c.face('Headlight casing',s,1.943,[(.46,.204),(.713,.197),(.722,.439),(.484,.439)],-.134,CARBON,.017)
        for z in (.24,.34):
            c.box('Headlight bar',s*.586-.098,s*.586+.098,1.94,1.958,z,z+.052,WHITE,.009)
        c.face('Stoplight guard',s,-1.959,[(.54,.265),(.725,.261),(.733,.51),(.55,.51)],.082,CARBON,.012)
        for z,col in ((.305,RED),(.412,AMBER)):
            c.box('Rear lamp',s*.636-.071,s*.636+.071,-1.972,-1.944,z,z+.061,col,.009)
        c.face('Rear pane',s,-1.689,[(.05,.652),(.563,.645),(.535,.805),(.053,.814)],.045,glass,.010)
        c.louvers(s,.18,.42,.83,1.30,lambda y:.485-.039*(y-.83),4)
    c.box('Rear step',-.57,.57,-2.,-1.80,.083,.168,CARBON,.019)
    for x in (-.40,-.20,0,.20,.40):
        c.box('Step tread',x-.043,x+.043,-1.985,-1.91,.161,.184,ALLOY,.004)


def tank(c):
    c.hull('Lower hull',[(-1.91,.67,.10,.55),(-1.59,.79,.045,.69),(1.10,.76,.035,.54),(1.92,.53,.13,.33)],GRAPHITE,.028)
    c.hull('Upper glacis',[(-1.956,.73,.36,.81),(-1.68,.84,.37,.89),(-.98,.87,.40,.92),
        (-.21,.85,.39,.933),(.59,.81,.37,.88),(1.36,.73,.30,.68),(1.97,.495,.19,.395)],PAINT,.033)
    for s in (-1,1):
        rings=[]
        for y,lo,hi in [(-1.91,.38,.70),(-1.61,.37,.82),(-.96,.38,.847),(.64,.35,.810),(1.43,.28,.66),(1.86,.19,.42)]:
            w=c.width(y)
            rings.append([(s*x,y,z) for x,z in ((.65,lo),(w-.057,lo),(w,lo+.071),(w,hi-.061),(w-.061,hi),(.65,hi+.002))])
        c.loft('Track deck',rings,PAINT,.025)
        c.louvers(s,.26,.62,-1.75,-.77,lambda y:.902+.036*(y+1.75),7)
        c.louvers(s,.39,.61,.69,1.32,lambda y:.888-.26*(y-.69),4)
        c.face('Tail cluster',s,-1.975,[(.466,.47),(.720,.435),(.731,.63),(.481,.665)],.115,CARBON,.014)
        for x in (.535,.65):
            c.box('Tail marker',s*x-.038,s*x+.038,-1.986,-1.96,.514,.591,RED,.010)
        c.face('Headlamp',s,1.89,[(.49,.295),(.67,.285),(.70,.40),(.53,.414)],-.1,CARBON,.009)
        c.face('Headlamp slit',s,1.904,[(.521,.321),(.655,.317),(.666,.363),(.536,.373)],-.022,WHITE,.005)
        for y in (-1.40,-.86,.77,1.16):
            c.rod('Deck fastener',(s*.75,y,.794 if y<.9 else .68),(s*.75,y,.815 if y<.9 else .70),.035,ALLOY,8,.003)
        c.exhaust(s*.28,-1.977,.56,.09)
    c.rod('Turret bearing',(0,-.10,.90),(0,-.10,.990),.407,GRAPHITE,36,.014)
    c.rod('Bearing top',(0,-.10,.970),(0,-.10,1.0),.370,CARBON,36,.007)
    c.box('Tow crossmember',-.62,.62,-1.976,-1.84,.276,.375,GRAPHITE,.025)


def wheel(c):
    # One section from the inner sidewall to the recessed brake face, around the local X axle.
    section = [(-.154,.293), (-.116,.338), (.112,.338), (.167,.308),
               (.175,.244), (.179,.226), (.135,.207)]
    sides = 20
    rings = [[(x, radius*math.cos(i*math.tau/sides), radius*math.sin(i*math.tau/sides))
              for i in range(sides)] for x, radius in section]

    def wheel_color(p):
        if p.x > .173:
            return ALLOY
        return GRAPHITE if .130 < p.x < .140 else RUBBER

    c.loft('Tyre and rim', rings, wheel_color, 0)
    for i in range(5):
        angle = i*math.tau/5
        spoke = []
        for radius, sweep in ((.055,-.032),(.195,-.034),(.230,-.004),(.227,.030),(.060,.022)):
            y = radius*math.cos(angle) - sweep*math.sin(angle)
            z = radius*math.sin(angle) + sweep*math.cos(angle)
            spoke.append((.180, y, z))
        c.prism('Alloy spoke', spoke, (-.032,0,0), ALLOY, 0)
    c.rod('Centre cap', (.148,0,0), (.185,0,0), .060, (154,166,171,0), 8, 0)


if __name__ == '__main__':
    output=Path(sys.argv[sys.argv.index('--')+1]).resolve()
    output.mkdir(parents=True,exist_ok=True)
    for name, build in [('Sport',sport),('Buggy',buggy),('Armored',armored),('Tank',tank),('Wheel',wheel)]:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        car=Car(name)
        build(car)
        car.export(output, (100, 480) if name == 'Wheel' else (3000, 6000))
