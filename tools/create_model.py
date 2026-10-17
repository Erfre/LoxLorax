"""Original Lorax fan model. Run with Blender --background --python tools/create_model.py.

Coordinates in the generator are Unity coordinates: X right, Y up, Z forward.
The editable .blend and the game mesh are exported from exactly the same geometry.
No reference-image pixels or third-party geometry are embedded in the model.
"""
import bpy
import os
import json
import math
import random
import struct
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'assets'
ASSETS.mkdir(exist_ok=True)
IS_VILE = os.environ.get('LOXLORAX_VARIANT') == 'vile'
OUTPUT = ASSETS / 'vile' if IS_VILE else ASSETS
OUTPUT.mkdir(exist_ok=True)
random.seed(20261016)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

PALETTE = [
    ('Burnt orange', (0.83, 0.225, 0.020)),
    ('Orange fur', (1.0, 0.335, 0.025)),
    ('Sunlit fur', (1.0, 0.41, 0.040)),
    ('Warm fur', (0.94, 0.275, 0.018)),
    ('Moustache gold', (0.98, 0.71, 0.25)),
    ('Moustache light', (1.0, 0.83, 0.40)),
    ('Moustache shadow', (0.91, 0.58, 0.18)),
    ('Eye white', (0.96, 0.97, 0.88)),
    ('Iris outer', (0.09, 0.23, 0.21)),
    ('Iris teal', (0.23, 0.49, 0.43)),
    ('Pupil', (0.018, 0.027, 0.023)),
    ('Eye glint', (1.0, 1.0, 1.0)),
    ('Nose warm', (0.88, 0.31, 0.065)),
    ('Nostril', (0.31, 0.11, 0.032)),
]
if IS_VILE:
    colors=[(.20,.16,.10),(.35,.38,.23),(.56,.32,.13),(.22,.28,.18),
            (.56,.47,.20),(.70,.61,.30),(.30,.31,.17),(.55,.60,.43),
            (.20,.035,.25),(1,.10,.72),(.72,.22,.82),(1,.76,.96),
            (.38,.20,.12),(.065,.025,.07)]
    PALETTE=[(name,color) for (name,_),color in zip(PALETTE,colors)]
    PALETTE += [('Exposed bone',(.70,.68,.48)),('Decayed patches',(.15,.105,.13))]

GROUPS = [('Body', (0, 0, 0)), ('Front left leg', (-.20, .48, .32)), ('Front right leg', (.20, .48, .32)), ('Rear left leg', (-.20, .48, -.45)), ('Rear right leg', (.20, .48, -.45)), ('Head', (0, .64, .46))]
BUCKETS = {}


def mesh_add(vertices, faces, material, group=0):
    bucket = BUCKETS.setdefault((group, material), [[], []])
    offset = len(bucket[0])
    bucket[0].extend(tuple(v) for v in vertices)
    bucket[1].extend(tuple(i + offset for i in f) for f in faces)


def ellipsoid(center, radii, material, group=0, segments=32, rings=18, tilt=0):
    vertices = []
    co, si = math.cos(tilt), math.sin(tilt)
    def point(x, y, z):
        return (center[0] + co*x-si*y, center[1]+si*x+co*y, center[2]+z)
    vertices.append(point(0, radii[1], 0))
    for i in range(1, rings):
        a = math.pi*i/rings
        for j in range(segments):
            b = 2*math.pi*j/segments
            vertices.append(point(radii[0]*math.sin(a)*math.cos(b), radii[1]*math.cos(a), radii[2]*math.sin(a)*math.sin(b)))
    vertices.append(point(0, -radii[1], 0))
    faces=[]
    for j in range(segments):
        faces.append((0, 1+(j+1)%segments, 1+j))
    for i in range(rings-2):
        for j in range(segments):
            a=1+i*segments+j; b=1+i*segments+(j+1)%segments
            faces.extend([(a,b,b+segments),(a,b+segments,a+segments)])
    last=len(vertices)-1
    for j in range(segments):
        faces.append((last, last-segments+j, last-segments+(j+1)%segments))
    mesh_add(vertices, faces, material, group)


def bezier(points, n):
    p=[Vector(v) for v in points]
    return [(1-t)**3*p[0]+3*(1-t)**2*t*p[1]+3*(1-t)*t*t*p[2]+t**3*p[3] for t in [i/(n-1) for i in range(n)]]


def tube(points, radii, material, group=0, sides=8, flatten=1.0):
    points=[Vector(p) for p in points]
    vertices=[]
    for i,p in enumerate(points):
        direction=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized()
        reference=Vector((0,0,1)) if abs(direction.z)<.9 else Vector((0,1,0))
        u=direction.cross(reference).normalized()
        v=direction.cross(u).normalized()
        for j in range(sides):
            a=2*math.pi*j/sides
            vertices.append(p+radii[i]*(u*math.cos(a)+v*math.sin(a)*flatten))
    faces=[]
    for i in range(len(points)-1):
        for j in range(sides):
            a=i*sides+j; b=i*sides+(j+1)%sides
            faces.extend([(a,b,b+sides),(a,b+sides,a+sides)])
    vertices.extend([points[0],points[-1]])
    for j in range(sides):
        faces.extend([(len(vertices)-2,(j+1)%sides,j), (len(vertices)-1,(len(points)-1)*sides+j,(len(points)-1)*sides+(j+1)%sides)])
    mesh_add(vertices,faces,material,group)


def strand(path, radius, material, group=0, sides=3):
    # Two-sided tapered ribbons retain the wispy silhouette at half the triangle cost.
    points=[Vector(p) for p in path]; vertices=[]; faces=[]
    previous_across=None
    for i,p in enumerate(points):
        direction=(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized()
        normal=Vector((p.x,0,p.z)).normalized()
        across=direction.cross(normal).normalized()
        # Keep the ribbon frame continuous when a bent wisp crosses its radial axis.
        if previous_across is not None and across.dot(previous_across)<0:
            across=-across
        previous_across=across
        r=radius*(1-i/len(points))
        vertices.extend([p-across*r,p+across*r])
    for i in range(len(points)-1):
        a=i*2
        faces.extend([(a,a+1,a+3),(a,a+3,a+2)])
    # Separate back vertices preserve normals instead of cancelling the front normals.
    offset=len(vertices); vertices.extend(list(vertices))
    faces.extend([tuple(i+offset for i in reversed(f)) for f in list(faces)])
    mesh_add(vertices,faces,material,group)


# Preserve the recognizable Lorax head; the quadruped body is built below.
PROFILE=[(.13,.018,.022),(.18,.095,.085),(.27,.178,.132),(.42,.218,.153),(.55,.205,.140),(.66,.158,.113),(.74,.148,.105),(.84,.153,.108),(.90,.119,.088),(.935,.069,.051),(.95,.003,.003)]
def profile(y):
    for i in range(len(PROFILE)-1):
        if y<=PROFILE[i+1][0]:
            p0=PROFILE[max(0,i-1)]; p1=PROFILE[i]; p2=PROFILE[i+1]; p3=PROFILE[min(len(PROFILE)-1,i+2)]
            t=(y-p1[0])/(p2[0]-p1[0])
            return tuple(.5*((2*p1[k])+(-p0[k]+p2[k])*t+(2*p0[k]-5*p1[k]+4*p2[k]-p3[k])*t*t+(-p0[k]+3*p1[k]-3*p2[k]+p3[k])*t**3) for k in (1,2))
    return PROFILE[-1][1:]

vertices=[]; faces=[]; rows=64; cols=64
for i in range(rows):
    y=.70+(.95-.70)*i/(rows-1)
    rx,rz=profile(y)
    for j in range(cols):
        a=j*2*math.pi/cols
        ripple=1+.004*math.sin(a*21+y*123)
        vertices.append((rx*math.cos(a)*ripple,y,rz*math.sin(a)*ripple))
for i in range(rows-1):
    for j in range(cols):
        a=i*cols+j; b=i*cols+(j+1)%cols
        faces.extend([(a,a+cols,b+cols),(a,b+cols,b)])
mesh_add(vertices,faces,1)

# Fur wisps bend downward; multiple restrained orange shades break up the surface.
for i in range(1600):
    y=random.uniform(.70,.945); a=random.uniform(0,2*math.pi)
    rx,rz=profile(y)
    p=Vector((rx*math.cos(a),y,rz*math.sin(a)))
    # Keep eye/muzzle areas clean, and avoid burying the folded hands in fur.
    if p.z>.045 and .765<y<.89: continue
    n=Vector((math.cos(a),.08,math.sin(a))).normalized()
    length=random.uniform(.015,.034)
    flow=Vector((.12*math.cos(a),-1,.12*math.sin(a))).normalized()
    path=[p-n*.001,p+n*.0015+flow*length*.30,p+n*.0015+flow*length*.70,p+n*.001+flow*length]
    strand(path,random.uniform(.00025,.00055),random.choices([0,1,2,3],[1,5,2,3])[0])

# Large heavy-lidded eyes, teal irises, and small physically modeled catchlights.
for side in (-1,1):
    x=side*.066
    ellipsoid((x,.843,.099),(.052,.041,.024),0,segments=40,rings=22,tilt=-side*.12)
    ellipsoid((x,.843,.123),(.047,.035,.017),7,segments=40,rings=22,tilt=-side*.15)
    ix=x-side*.006
    ellipsoid((ix,.849,.140),(.020,.023,.0065),8,segments=32,rings=18)
    ellipsoid((ix,.849,.145),(.0156,.019,.004),9,segments=32,rings=18)
    ellipsoid((ix,.849,.1485),(.0083,.0128,.003),10,segments=24,rings=16)
    ellipsoid((ix-.0045,.858,.1515),(.0046,.0046,.002),11,segments=16,rings=10)
    ellipsoid((ix+.004,.843,.151),(.0018,.0018,.001),11,segments=12,rings=8)
    # Upper eyelids arch over the eyeball; lower rims soften the face.
    for upper in (True,False):
        path=[]
        for j in range(17):
            t=math.pi*j/16
            path.append((x+.047*math.cos(t),.843+(.030 if upper else -.028)*math.sin(t),.128+.008*math.sin(t)))
        tube(path,[.007 if upper else .003]*17,1,0,7)

# Orange button nose, tiny nostrils and a shadowed mouth below the moustache.
ellipsoid((0,.790,.162),(.036,.027,.034),12,segments=36,rings=22)
for side in (-1,1):
    ellipsoid((side*.018,.778,.185),(.007,.004,.004),13,segments=16,rings=10)
ellipsoid((0,.743,.113),(.024,.006,.006),13,segments=24,rings=12)

# Each moustache is a thick curved fan made from overlapping tapering locks.
# Fine surface wisps follow the same flow, so the fan reads as hair from all angles.
for side in (-1,1):
    for k in range(14):
        q=k/13
        path=bezier([(side*.008,.770-q*.014,.167+q*.010),
                     (side*(.110-q*.020),.800-q*.105,.209+q*.006),
                     (side*(.310-q*.140),.885-q*.235,.207-q*.010),
                     (side*(.345-q*.175),.665-q*.045,.162-q*.005)],20)
        radius=.023+.009*math.sin(math.pi*q)
        tube(path,[radius*(math.sin(math.pi*(.12+.875*i/19))**.65)+.0004 for i in range(20)],4 if k%3 else 6,0,9,.82)
    for k in range(400):
        q=random.random(); jitter=random.uniform(-.007,.007)
        path=bezier([(side*.010,.773-q*.016+jitter,.193+q*.010),
                     (side*(.110-q*.020),.805-q*.105+jitter,.241+q*.006),
                     (side*(.310-q*.140),.890-q*.235+jitter,.238-q*.010),
                     (side*(.346-q*.175+random.uniform(-.008,.010)),.665-q*.045+random.uniform(-.012,.010),.168-q*.005)],9)
        strand(path,random.uniform(.00045,.0010),random.choices([4,5,6],[3,6,1])[0])
    # Swept bushy brows, with the inner tip lifted for a skeptical expression.
    for k in range(6):
        q=k/5
        path=bezier([(side*.018,.901+q*.022,.105), (side*.051,.944+q*.016,.132), (side*.108,.931-q*.014,.139), (side*.141,.879-q*.015,.116)],14)
        tube(path,[.023*(1-i/14)+.001 for i in range(14)],4,0,9,.72)
    for k in range(110):
        q=random.random(); j=random.uniform(-.003,.003)
        path=bezier([(side*.018,.903+q*.024,.118+j), (side*.050,.950+q*.016,.150+j), (side*.108,.935-q*.014,.158+j), (side*.145,.879-q*.015,.120+j)],8)
        strand(path,random.uniform(.0004,.0008),5 if k%3 else 4)


def linear(c):
    return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4

# Bring the face forward above the forelegs, keeping eyes and moustache upright.
for bucket in BUCKETS.values():
    bucket[0]=[(x*1.35,(y-.70)*1.35+.60,z*1.35+.42) for x,y,z in bucket[0]]

BUCKETS={(5,material):bucket for (group,material),bucket in BUCKETS.items()}

# Broad horizontal torso, raised shoulders and short neck: a planted lox stance.
ellipsoid((0,.53,-.12),(.32,.30,.58),1,segments=56,rings=32)
ellipsoid((0,.60,.27),(.29,.27,.28),1,segments=40,rings=24)
ellipsoid((0,.64,.43),(.205,.20,.22),1,segments=40,rings=24)
for i in range(4200):
    a=random.uniform(0,2*math.pi); t=random.uniform(-.98,.98)
    radial=math.sqrt(1-t*t)
    p=Vector((.32*radial*math.cos(a),.53+.30*radial*math.sin(a),-.12+.58*t))
    n=Vector((radial*math.cos(a)/.32,radial*math.sin(a)/.30,t/.58)).normalized()
    flow=Vector((n.x*.16,-.8,-.35)).normalized()
    length=random.uniform(.015,.035)
    strand([p-n*.001,p+n*.003+flow*length*.25,p+n*.005+flow*length*.65,p+n*.002+flow*length],random.uniform(.00035,.0007),random.choices([0,1,2,3],[1,5,2,3])[0])

# Four separate weight-bearing limbs with broad paws touching a common ground plane.
for group,(name,(x,y,z)) in enumerate(GROUPS[1:5],1):
    side=-1 if x<0 else 1
    path=bezier([(x,y,z),(x+side*.012,.34,z-.015),(x+side*.018,.18,z+.01),(x+side*.02,.075,z+.035)],14)
    tube(path,[.105-.042*i/13 for i in range(14)],1,group,18)
    ellipsoid((x+side*.02,.057,z+.075),(.095,.057,.135),1,group,32,16)
    for k in range(3):
        ellipsoid((x+side*.02+(k-1)*.052,.036,z+.165-abs(k-1)*.012),(.036,.035,.052),1,group,20,12)

# Vile-inspired decay is additional original geometry; the Lorax face is retained.
if IS_VILE:
    for side in (-1,1):
        for k in range(6):
            z=-.44+k*.105
            section=math.sqrt(max(.12,1-((z+.12)/.58)**2))
            rib=[]
            for j in range(20):
                angle=.08+2.15*j/19
                rib.append((side*.335*section*math.sin(angle),.53+.315*section*math.cos(angle),z+.012*math.sin(angle*2)))
            tube(rib,[.013+.004*math.sin(math.pi*j/19) for j in range(20)],14,sides=9)
        for z,y,ry,rz in [(-.22,.56,.12,.18),(-.48,.54,.08,.10),(.07,.58,.065,.10)]:
            x=.32*math.sqrt(max(.1,1-((z+.12)/.58)**2))
            ellipsoid((side*x,y,z),(.022,ry,rz),15,segments=24,rings=16)
    for k in range(8):
        z=-.52+k*.095
        y=.53+.30*math.sqrt(max(.1,1-((z+.12)/.58)**2))
        spike=bezier([(0,y-.012,z),(0,y+.045,z-.015),(.018*(-1 if k%2 else 1),y+.10,z-.035),(0,y+.14+(k%3)*.015,z-.06)],10)
        tube(spike,[.028*(1-j/10)+.001 for j in range(10)],14,sides=8)

# True overall normalization keeps the configured height and grounded paws accurate.
points=[v for bucket in BUCKETS.values() for v in bucket[0]]
base=min(v[1] for v in points)
normalization=max(v[1] for v in points)-base
for bucket in BUCKETS.values():
    bucket[0]=[(x/normalization,(y-base)/normalization,z/normalization) for x,y,z in bucket[0]]
GROUPS=[(name,(x/normalization,(y-base)/normalization,z/normalization)) for name,(x,y,z) in GROUPS]
materials=[]
fur_image=bpy.data.images.load(str(ASSETS/'fur.png'))
fur_image.pack()
gold_image=bpy.data.images.load(str(ASSETS/'gold-fur.png'))
gold_image.pack()
for name,color in PALETTE:
    m=bpy.data.materials.new(name)
    m.diffuse_color=tuple(linear(c) for c in color)+(1,)
    m.use_nodes=True
    bsdf=m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=m.diffuse_color
    if IS_VILE and len(materials) in (9,10):
        bsdf.inputs['Emission Color'].default_value=m.diffuse_color
        bsdf.inputs['Emission Strength'].default_value=2.0
    bsdf.inputs['Roughness'].default_value=.78 if 'Eye' not in name and 'Iris' not in name and name!='Pupil' else .22
    if len(materials)<7:
        tex=m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=fur_image if len(materials)<4 else gold_image
        multiply=m.node_tree.nodes.new('ShaderNodeMixRGB'); multiply.blend_type='MULTIPLY'
        multiply.inputs[0].default_value=1
        multiply.inputs[1].default_value=m.diffuse_color
        m.node_tree.links.new(tex.outputs['Color'],multiply.inputs[2])
        m.node_tree.links.new(multiply.outputs[0],bsdf.inputs['Base Color'])
    materials.append(m)

objects=[]
for (group,material),(verts,faces) in sorted(BUCKETS.items()):
    mesh=bpy.data.meshes.new(PALETTE[material][0])
    mesh.from_pydata([(x,-z,y) for x,y,z in verts],[],faces)
    mesh.update()
    uv=mesh.uv_layers.new(name='Fur flow')
    for loop in mesh.loops:
        x,y,z=verts[loop.vertex_index]
        uv.data[loop.index].uv=(y*2,abs(x)*2+(y-.8)*.3) if 4<=material<=6 else (math.atan2(z,x)/(2*math.pi)+.5,y)
    obj=bpy.data.objects.new(GROUPS[group][0]+' / '+PALETTE[material][0],mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(materials[material])
    obj['animation_group']=group
    for polygon in mesh.polygons: polygon.use_smooth=True
    objects.append(obj)

# Exact mesh export with normalized smooth normals, shared materials and limb pivots.
out=OUTPUT/'lorax.llmesh'
with out.open('wb') as f:
    f.write(b'LOX2')
    f.write(struct.pack('<i',len(PALETTE)))
    for index,(name,color) in enumerate(PALETTE):
        texture=(ASSETS/('fur.png' if index<4 else 'gold-fur.png')).read_bytes() if index<7 else b''
        f.write(struct.pack('<4fi',*color,1,len(texture)))
        f.write(texture)
    f.write(struct.pack('<i',len(GROUPS)))
    for name,pivot in GROUPS:
        name=name.encode('utf8'); f.write(struct.pack('<i',len(name))); f.write(name)
        f.write(struct.pack('<3f',*pivot))
    f.write(struct.pack('<i',len(objects)))
    for obj,((group,material),(verts,faces)) in zip(objects,sorted(BUCKETS.items())):
        f.write(struct.pack('<3i',material,group,len(verts)))
        pivot=Vector(GROUPS[group][1])
        for v,source in zip(obj.data.vertices,verts):
            p=Vector(source)-pivot; n=v.normal
            uv=(source[1]*2,abs(source[0])*2+(source[1]-.8)*.3) if 4<=material<=6 else (math.atan2(source[2],source[0])/(2*math.pi)+.5,source[1])
            f.write(struct.pack('<8f',*p,n.x,n.z,-n.y,*uv))
        f.write(struct.pack('<i',len(faces)*3))
        for face in faces: f.write(struct.pack('<3i',*face))

stats={'vertices':sum(len(v[0]) for v in BUCKETS.values()),'triangles':sum(len(v[1]) for v in BUCKETS.values()),'materials':len(PALETTE),'mesh_parts':len(objects),'groups':[g[0] for g in GROUPS], 'height_metres_at_scale_1':1.0,'format':'LOX2'}
(OUTPUT/'model-info.json').write_text(json.dumps(stats,indent=2)+'\n')
print('MODEL_STATS',json.dumps(stats),flush=True)

# Export reusable, editable assets before adding the preview studio.
bpy.ops.object.select_all(action='DESELECT')
for obj in objects: obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.gltf(filepath=str(OUTPUT/'lorax.glb'),use_selection=True,export_format='GLB')

world=bpy.data.worlds.new('Soft blue studio')
bpy.context.scene.world=world
world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.065,.085,.105,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.45
floor=bpy.data.materials.new('Studio ground'); floor.diffuse_color=(.022,.038,.047,1)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.006))
bpy.context.object.data.materials.append(floor)

def light(name,location,power,size,color):
    data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.shape='DISK'; data.size=size; data.color=color
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj); obj.location=location
    obj.rotation_euler=(Vector((0,0,.5))-obj.location).to_track_quat('-Z','Y').to_euler()
light('Large warm key',(-1.5,-2,2.7),90,2.2,(1,.86,.69))
light('Cool fill',(1.8,-1,1.5),40,2.0,(.67,.84,1))
light('Golden rim',(.5,1.3,1.8),95,1.4,(1,.74,.39))
data=bpy.data.cameras.new('Portrait camera'); cam=bpy.data.objects.new('Portrait camera',data)
bpy.context.collection.objects.link(cam); bpy.context.scene.camera=cam
cam.location=(2.8,-3.6,1.8); target=Vector((0,0,.49))
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
data.type='ORTHO'; data.ortho_scale=1.85; data.lens=70
scene=bpy.context.scene
scene.render.engine='CYCLES'; scene.cycles.samples=48; scene.cycles.use_denoising=True
scene.render.resolution_x=1100; scene.render.resolution_y=1100; scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
scene.render.image_settings.file_format='PNG'
bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT/'lorax.blend'))
scene.render.filepath=str(OUTPUT/'preview.png')
bpy.ops.render.render(write_still=True)
cam.location=(0,-4,.65); cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(OUTPUT/'front.png')
bpy.ops.render.render(write_still=True)
