#!/usr/bin/env python3
"""File-level Stage 1 checks; does not substitute for Unity import, physics or Play Mode.
Run from the repository root: python3 Tools/validate_level1.py (requires PyYAML).
"""
from pathlib import Path
import math
import re
import subprocess
import yaml

SCENE = Path('Assets/Scenes/Level1_PowerRestoration.unity')
text = SCENE.read_text()
blocks = {}
for match in re.finditer(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)', text, re.M | re.S):
    key = int(match[2])
    assert key not in blocks, f'Duplicate local ID {key}'
    blocks[key] = (int(match[1]), next(iter(yaml.safe_load(match[3]).values())))
assert blocks, 'Scene must contain serialized objects'
checks = []
def check(condition, message):
    assert condition, message
    checks.append(message)
    print('PASS:', message)

def walk(value):
    if isinstance(value, dict):
        if 'fileID' in value: yield value
        for child in value.values(): yield from walk(child)
    elif isinstance(value, list):
        for child in value: yield from walk(child)

asset_guids = {}
for root in ('Assets', 'Packages', 'Library/PackageCache'):
    for path in Path(root).rglob('*.meta'):
        match = re.search(r'^guid: (\w+)', path.read_text(errors='replace'), re.M)
        if match: asset_guids[match[1]] = path
references = [r for _, block in blocks.values() for r in walk(block)]
check(all(not r['fileID'] or 'guid' in r or r['fileID'] in blocks for r in references), 'All local object/component references resolve')
check(all('guid' not in r or str(r['guid']).startswith('0000000000000000') or r['guid'] in asset_guids for r in references), 'All external asset/script GUIDs resolve (including cached Unity packages)')
objects = {i: b for i, (c, b) in blocks.items() if c == 1}
transforms = {i: b for i, (c, b) in blocks.items() if c in (4, 224)}
def named(name):
    return next(i for i, b in objects.items() if b['m_Name'] == name)
def components(go, cls):
    return [(r['component']['fileID'], blocks[r['component']['fileID']][1]) for r in objects[go]['m_Component'] if blocks[r['component']['fileID']][0] == cls]
def transform(go):
    return next((i, b) for i, b in transforms.items() if b['m_GameObject']['fileID'] == go)
for go, obj in objects.items():
    for comp in obj['m_Component']:
        assert blocks[comp['component']['fileID']][1]['m_GameObject']['fileID'] == go
for i, tr in transforms.items():
    for child in tr['m_Children']:
        assert transforms[child['fileID']]['m_Father']['fileID'] == i
roots = {r['fileID'] for c, b in blocks.values() if c == 1660057539 for r in b['m_Roots']}
check(roots == {i for i, t in transforms.items() if not t['m_Father']['fileID']}, 'Hierarchy ownership, parent links and scene roots are consistent')

def vec(value): return tuple(value[k] for k in ('x','y','z'))
def world(tid):
    t = transforms[tid]; p = vec(t['m_LocalPosition']); s = vec(t['m_LocalScale'])
    q = t['m_LocalRotation']; angle = 2 * math.atan2(q['y'], q['w'])
    parent = t['m_Father']['fileID']
    if not parent: return p, s, angle
    pp, ps, pa = world(parent)
    x,y,z = (p[i]*ps[i] for i in range(3))
    return (pp[0]+x*math.cos(pa)+z*math.sin(pa), pp[1]+y, pp[2]-x*math.sin(pa)+z*math.cos(pa)), tuple(s[i]*ps[i] for i in range(3)), angle+pa

bounds=[]
for go,obj in objects.items():
    if obj['m_Layer'] != 3: continue
    for cls in (65,64):
        for _, collider in components(go, cls):
            if not collider['m_Enabled'] or collider['m_IsTrigger']: continue
            p,s,a = world(transform(go)[0])
            # Cylinders use their conservative bounding box for these file-level clearance checks.
            size = vec(collider['m_Size']) if cls == 65 else (1,2,1)
            half = tuple(abs(s[i])*size[i]/2 for i in range(3))
            extent=(abs(math.cos(a))*half[0]+abs(math.sin(a))*half[2], half[1], abs(math.sin(a))*half[0]+abs(math.cos(a))*half[2])
            bounds.append((obj['m_Name'], tuple(p[i]-extent[i] for i in range(3)), tuple(p[i]+extent[i] for i in range(3))))

def clear(center, half):
    return all(not all(center[i]+half[i]>lo[i]+0.001 and center[i]-half[i]<hi[i]-0.001 for i in range(3)) for _,lo,hi in bounds)
player=named('Player'); player_pos=world(transform(player)[0])[0]
check(clear(player_pos,(.5,1,.5)), 'Serialized spawn capsule clears all room colliders')
camera_angle=world(transform(named('Main Camera'))[0])[2]
check(player_pos == (0,1.05,-8.2) and abs(camera_angle)<0.001, 'Spawn at entrance, facing the reactor (+Z camera)')
check(all(clear((0,1.05,-8.2+i*.2),(.5,1,.5)) for i in range(49)), 'Central walking lane clear up to reactor approach')
check(all(clear((6.5,1.05+i*.2,1.4),(.5,1,.5)) for i in range(65)), 'Zero-G shaft clear from floor to elevated repair area')
check(all(clear((-3.5,1.05,-7+i*.2),(.5,1,.5)) for i in range(50)), 'Left-side route to Module A clear')
check(all(objects[named(n)]['m_Layer']==3 and components(named(n),65) for n in ('Floor_PowerRoom','Ceiling','Left bulkhead','Right bulkhead','Reactor bulkhead','Entrance bulkhead')), 'Six enclosing room surfaces have Structure-layer solid box colliders')
repair=next((lo,hi) for n,lo,hi in bounds if n=='Repair panel')
max_jump_eye = 6**2/(2*14.715)+1.5
check(repair[0][1] > max_jump_eye+4, 'Repair panel lower edge exceeds floor jump eye-height plus 4m interaction range')
check(next(hi[1] for n,lo,hi in bounds if n=='Upper collar') > 3.5+6**2/(2*14.715), 'Upper reactor collar is beyond a jump from nearby cabinets (no reactor stepping route)')
script_guids=[b['m_Script']['guid'] for c,b in blocks.values() if c==114]
for name in ('PlayerController','ArtificialGravity','Look','GlobalStateCompanion','FutureDecayEnvironment','PowerRestorationPresentation','DistortableObject','Interact','StationGravity'):
    meta=next(Path('Assets/Scripts').rglob(name+'.cs.meta'))
    expected=re.search(r'^guid: (\w+)',meta.read_text(),re.M)[1]
    check(script_guids.count(expected)==1, 'Exactly one '+name)
check(sum(c==54 for c,_ in blocks.values())==1, 'Only player has a Rigidbody; no module pickup/repair physics introduced')
light=components(named('Activation effects anchor — reserved'),108)[0][1]
check(light['m_Enabled']==0 and light['m_Intensity']==0, 'Reactor light remains disabled and unpowered')
check(not subprocess.check_output(['git','diff','--','Assets/StartScene.unity','Assets/StartScene_Backup.unity','Assets/gravityfieldshadertest.unity','Assets/Scripts/Player','Assets/Scripts/GlobalState.cs','Assets/Scripts/Physics']), 'Existing scenes and core gameplay scripts unchanged')
settings=Path('ProjectSettings/EditorBuildSettings.asset').read_text()
check(settings.index('Assets/MainMenu.unity') < settings.index('Assets/StartScene.unity') < settings.index(str(SCENE)), 'Menu and tutorial ordering preserved; Power Restoration appended')
station=next(b for c,b in blocks.values() if c==114 and b.get('m_EditorClassIdentifier')=='Assembly-CSharp::StationGravity')
check(station['heightAboveDeck'] >= 15, 'Existing station gravity covers the full room height')
controller=blocks[1786953323][1]
check(controller['interactionController']['fileID']==1786953326 and controller['jumpSpeed']==6 and controller['cameraSensitivity']==0.2, 'Project 2 player settings and interaction reference retained')
interaction=blocks[1786953326][1]
check(interaction['rayOrigin']['fileID']==1047493168 and interaction['interactibleLayers']['m_Bits']==128, 'Project 2 interaction camera and layer wiring retained')
check(all('F SCAN' not in b.get('m_Name','') for b in objects.values()), 'Diagnostic label uses Project 2 G binding')
print(f'\n{len(checks)} file-level checks passed; {len(objects)} objects, {len(bounds)} colliders. Unity import, visual quality and runtime behavior remain separate checks.')
