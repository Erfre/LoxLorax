"""Validate the actual binary consumed by Unity, independently of its exporter."""
import io
import sys
import json
import struct
from pathlib import Path
import numpy as np
from PIL import Image

root=Path(__file__).resolve().parents[1]
assets=Path(sys.argv[1]).resolve() if len(sys.argv)>1 else root/'assets'
stream=io.BytesIO((assets/'lorax.llmesh').read_bytes())
def integer():
    return struct.unpack('<i',stream.read(4))[0]
def floats(count):
    return np.frombuffer(stream.read(count*4),dtype='<f4')

assert stream.read(4)==b'LOX2'
materials=integer()
assert 1<=materials<=128
for _ in range(materials):
    color=floats(4)
    assert len(color)==4 and np.isfinite(color).all() and (color>=0).all() and (color<=1).all()
    size=integer()
    assert 0<=size<=16*1024*1024
    if size:
        image=Image.open(io.BytesIO(stream.read(size)))
        image.verify()
groups=[]
for _ in range(integer()):
    length=integer()
    assert 1<=length<=128
    name=stream.read(length).decode('utf8')
    pivot=floats(3)
    assert np.isfinite(pivot).all()
    groups.append((name,pivot))
assert [name for name,_ in groups]==['Body','Front left leg','Front right leg','Rear left leg','Rear right leg','Head']
counts={'vertices':0,'triangles':0,'reversed_triangles':0,'degenerate_triangles':0}
bounds=[]
limb_bounds={}
for _ in range(integer()):
    material,group,count=integer(),integer(),integer()
    assert 0<=material<materials and 0<=group<len(groups) and 3<=count<=200000
    vertices=floats(count*8).reshape(count,8)
    assert np.isfinite(vertices).all()
    normals=vertices[:,3:6]
    lengths=np.linalg.norm(normals,axis=1)
    assert np.allclose(lengths,1,atol=.002), ('Invalid normals',float(lengths.min()))
    index_count=integer()
    assert 3<=index_count<=1500000 and index_count%3==0
    indices=np.frombuffer(stream.read(index_count*4),dtype='<i4').reshape(-1,3)
    assert indices.min()>=0 and indices.max()<count
    p=vertices[:,:3]
    crosses=np.cross(p[indices[:,1]]-p[indices[:,0]],p[indices[:,2]]-p[indices[:,0]])
    areas=np.linalg.norm(crosses,axis=1)
    counts['degenerate_triangles']+=int((areas<1e-13).sum())
    average_normals=normals[indices].mean(axis=1)
    agreement=np.einsum('ij,ij->i',crosses,average_normals)
    counts['reversed_triangles']+=int((agreement< -1e-10).sum())
    counts['vertices']+=count; counts['triangles']+=len(indices)
    bounds.append(p+groups[group][1])
    if 1<=group<=4: limb_bounds.setdefault(group,[]).append(p+groups[group][1])
assert stream.read()==b'', 'Trailing data'
assert counts['vertices']<=500000
assert counts['degenerate_triangles']==0, counts
assert counts['reversed_triangles']==0, counts
all_positions=np.concatenate(bounds)
minimum=all_positions.min(axis=0); maximum=all_positions.max(axis=0)
assert -.01<=minimum[1]<=.01, ('Feet off floor',minimum)
assert .98<=maximum[1]<=1.05, ('Height normalization',maximum)
assert maximum[0]<.55 and minimum[0]>-.55
assert maximum[2]-minimum[2]>1.3, 'Torso must have the length of a quadruped'
for group,parts in limb_bounds.items():
    foot=np.concatenate(parts).min(axis=0)
    assert abs(foot[1])<.002, ('Paw not planted',groups[group][0],foot)
assert len(limb_bounds)==4, 'Four weight-bearing limbs required'
stats=json.loads((assets/'model-info.json').read_text())
assert stats['triangles']==counts['triangles'] and stats['vertices']==counts['vertices']
print('PASS:',json.dumps(counts))
print('Bounds:',minimum.tolist(),maximum.tolist())
