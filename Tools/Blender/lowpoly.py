"""Helpers for building rigged, animated low-poly characters in Blender from a script.

A character is a set of simple parts (boxes, cylinders, spheres) each stuck rigidly to one
bone, so it moves like a jointed figure: no weight painting needed. Poses are given as
rotations in the armature's own axes, with the character facing -Y (Blender's front view):

    +X rotation pitches a bone nose-down: a leg or arm swings BACK, a spine leans FORWARD.
    +Y rotation rolls it to the character's left.    +Z rotation turns it to the left.

Used by the per-character scripts next to this file; see characters.py.
"""
import math
import os

import bmesh
import bpy
from mathutils import Euler, Matrix, Vector

FPS = 30


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.unit_settings.system = 'METRIC'


class Character:
    def __init__(self, name):
        self.name = name
        self.bones = []          # (name, head, tail, parent, roll)
        self.parts = []          # (bone, material, kind, kwargs, object)
        self.materials = {}      # name -> rgb
        self.clips = []          # (name, frames, keys)
        self.free = set()        # bones the clips never move
        self.target = None       # object new parts go in (None = the body)
        self.rigid = {}          # object -> bone, for objects that are stuck to one bone

    # --- skeleton ---------------------------------------------------------------------
    def bone(self, name, head, tail, parent=None, free=False, roll=0):
        """free: never keyed in any clip, for game code to turn (a wolf's Head tips back to
        howl). roll (degrees) turns the bone's own axes, which game code turns it about."""
        self.bones.append((name, Vector(head), Vector(tail), parent, roll))
        if free:
            self.free.add(name)

    def colour(self, name, rgb):
        self.materials[name] = rgb

    def into(self, name, bone=None):
        """Parts from here on go in a separate object of this name (None = back to the
        body), still following the rig: an extra the game can switch on and off, like the
        player's BurglarMask. Given a bone, it's a plain mesh parented to that bone rather
        than skinned, so the game can take it off (the Guerilla King's crown falls off)."""
        self.target = name
        if name and bone:
            self.rigid[name] = bone

    # --- parts: every one is placed in armature space and follows one bone -------------
    def box(self, bone, mat, size, at, top=(1, 1), bottom=(1, 1), rot=(0, 0, 0), bevel=0.0, shift=(0, 0)):
        """A box of size (x, y, z) centred on `at`. top/bottom scale the upper and lower
        faces in x and y (tapering), and shift slides the top face by (x, y)."""
        self.parts.append((bone, mat, 'box', dict(size=size, at=at, top=top, bottom=bottom, rot=rot, bevel=bevel, shift=shift), self.target))

    def cyl(self, bone, mat, radius, depth, at, rot=(0, 0, 0), sides=6, radius2=None):
        """A cylinder (or cone, with radius2) along its own Z, centred on `at`."""
        self.parts.append((bone, mat, 'cyl', dict(radius=radius, radius2=radius if radius2 is None else radius2,
                                                   depth=depth, at=at, rot=rot, sides=sides), self.target))

    def ball(self, bone, mat, radius, at, scale=(1, 1, 1), rot=(0, 0, 0), detail=1):
        """A faceted sphere (icosphere), squashed by scale."""
        self.parts.append((bone, mat, 'ball', dict(radius=radius, at=at, scale=scale, rot=rot, detail=detail), self.target))

    def limb(self, bone, mat, a, b, radius, radius2=None, sides=6):
        """A cylinder (tapering to radius2) from point a to point b."""
        a, b = Vector(a), Vector(b)
        rot = Vector((0, 0, 1)).rotation_difference(b - a).to_euler()
        self.cyl(bone, mat, radius, (b - a).length, at=tuple((a + b) / 2),
                 rot=tuple(math.degrees(r) for r in rot), sides=sides, radius2=radius2)

    def mirror(self, fn, *args, **kw):
        """Calls a part function twice: as given for .L, and mirrored in X for .R.
        Use 'X' in the bone name where L/R goes, and give `at` (or a and b) for the left side."""
        bone = args[0]
        for side, sx in (('L', 1), ('R', -1)):
            a = list(args)
            a[0] = bone.replace('X', side) if bone.endswith('X') else bone
            k = dict(kw)
            for key in ('at', 'a', 'b'):
                if key in k:
                    k[key] = (k[key][0] * sx, k[key][1], k[key][2])
            if 'rot' in k:
                r = k['rot']
                k['rot'] = (r[0], -r[1] if sx < 0 else r[1], -r[2] if sx < 0 else r[2])
            if 'shift' in k:
                k['shift'] = (k['shift'][0] * sx, k['shift'][1])
            fn(*a, **k)

    # --- animation --------------------------------------------------------------------
    def clip(self, name, frames, keys):
        """keys: {frame: pose}, a pose being {bone: (rx, ry, rz)} in degrees, plus optional
        {bone + '@': (x, y, z)} offsets in metres. Bones not named are at rest. Give the
        last frame the same pose as frame 0 for a seamless loop."""
        self.clips.append((name, frames, keys))

    # --- build ------------------------------------------------------------------------
    def build(self):
        arm_data = bpy.data.armatures.new(self.name + '_Rig')
        arm = bpy.data.objects.new(self.name + '_Rig', arm_data)
        bpy.context.collection.objects.link(arm)
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode='EDIT')
        for name, head, tail, parent, roll in self.bones:
            eb = arm_data.edit_bones.new(name)
            eb.head, eb.tail = head, tail
            eb.roll = math.radians(roll)
            if parent:
                eb.parent = arm_data.edit_bones[parent]
                eb.use_connect = False
        bpy.ops.object.mode_set(mode='OBJECT')
        arm_data.display_type = 'STICK'

        mats = {}
        for mname, rgb in self.materials.items():
            m = bpy.data.materials.new(mname)
            m.diffuse_color = (*rgb, 1)
            m.use_nodes = True
            bsdf = m.node_tree.nodes.get('Principled BSDF')
            bsdf.inputs['Base Color'].default_value = (*rgb, 1)
            bsdf.inputs['Roughness'].default_value = 0.85
            mats[mname] = m

        bone_names = [b[0] for b in self.bones]
        objects = []
        for target in dict.fromkeys(p[4] for p in self.parts):
            parts = [p for p in self.parts if p[4] == target]
            used = list(dict.fromkeys(p[1] for p in parts))  # only its own materials
            bm = bmesh.new()
            deform = bm.verts.layers.deform.verify()
            for bone, mat, kind, k, _ in parts:
                verts = self._make(bm, kind, k)
                faces = {f for v in verts for f in v.link_faces}
                for f in faces:
                    f.material_index = used.index(mat)
                    f.smooth = False
                gi = bone_names.index(bone)
                for v in verts:
                    v[deform][gi] = 1.0
            name = target or self.name
            mesh = bpy.data.meshes.new(name)
            bm.to_mesh(mesh)
            bm.free()
            for m in used:
                mesh.materials.append(mats[m])
            obj = bpy.data.objects.new(name, mesh)
            bpy.context.collection.objects.link(obj)
            if target in self.rigid:
                # Parented to the bone, which Blender measures from its tail, with its own
                # origin at its middle, so it tumbles about itself once the game lets it go.
                bone = arm.data.bones[self.rigid[target]]
                centre = sum((v.co for v in mesh.vertices), Vector()) / len(mesh.vertices)
                mesh.transform(Matrix.Translation(-centre))
                obj.parent = arm
                obj.parent_type = 'BONE'
                obj.parent_bone = bone.name
                obj.matrix_parent_inverse = (bone.matrix_local @ Matrix.Translation((0, bone.length, 0))).inverted()
                obj.matrix_basis = Matrix.Translation(centre)
            else:
                for n in bone_names:
                    obj.vertex_groups.new(name=n)
                obj.parent = arm
                mod = obj.modifiers.new('Armature', 'ARMATURE')
                mod.object = arm
            objects.append(obj)
        body = objects[0]
        self.extras = objects[1:]

        self.arm, self.body = arm, body
        self._animate()
        return arm, body

    def _make(self, bm, kind, k):
        rot = Euler([math.radians(a) for a in k['rot']]).to_matrix().to_4x4()
        place = Matrix.Translation(k['at']) @ rot
        if kind == 'box':
            sx, sy, sz = k['size']
            r = bmesh.ops.create_cube(bm, size=1.0)
            verts = r['verts']
            for v in verts:
                if v.co.z > 0:
                    v.co.x *= k['top'][0]; v.co.y *= k['top'][1]
                    v.co.x += k['shift'][0] / sx; v.co.y += k['shift'][1] / sy
                else:
                    v.co.x *= k['bottom'][0]; v.co.y *= k['bottom'][1]
                v.co.x *= sx; v.co.y *= sy; v.co.z *= sz
            if k['bevel'] > 0:
                edges = list({e for v in verts for e in v.link_edges})
                r = bmesh.ops.bevel(bm, geom=edges + verts, offset=k['bevel'], segments=1, affect='EDGES')
                verts = list({v for f in r['faces'] for v in f.verts} | set(v for v in verts if v.is_valid))
                verts = [v for v in verts if v.is_valid]
        elif kind == 'cyl':
            r = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=k['sides'],
                                      radius1=k['radius'], radius2=k['radius2'], depth=k['depth'])
            verts = r['verts']
        else:
            r = bmesh.ops.create_icosphere(bm, subdivisions=k['detail'], radius=k['radius'])
            verts = r['verts']
            for v in verts:
                v.co.x *= k['scale'][0]; v.co.y *= k['scale'][1]; v.co.z *= k['scale'][2]
        bmesh.ops.transform(bm, matrix=place, verts=verts)
        return verts

    def _animate(self):
        arm = self.arm
        arm.animation_data_create()
        for pb in arm.pose.bones:
            pb.rotation_mode = 'QUATERNION'
        rest = {b.name: b.matrix_local.to_3x3() for b in arm.data.bones}
        for name, frames, keys in self.clips:
            action = bpy.data.actions.new(name)
            arm.animation_data.action = action
            for frame, pose in sorted(keys.items()):
                for pb in arm.pose.bones:
                    if pb.name in self.free:
                        continue
                    m = rest[pb.name]
                    r = pose.get(pb.name, (0, 0, 0))
                    world = Euler([math.radians(a) for a in r], 'XYZ').to_matrix()
                    pb.rotation_quaternion = (m.inverted() @ world @ m).to_quaternion()
                    pb.location = m.inverted() @ Vector(pose.get(pb.name + '@', (0, 0, 0)))
                    pb.keyframe_insert('rotation_quaternion', frame=frame, group=pb.name)
                    pb.keyframe_insert('location', frame=frame, group=pb.name)
            action.use_fake_user = True
            track = arm.animation_data.nla_tracks.new()
            track.name = name
            arm.animation_data.action = None
            strip = track.strips.new(name, 0, action)
            strip.frame_end = frames
        arm.animation_data.action = None

    # --- output -----------------------------------------------------------------------
    def export(self, path):
        os.makedirs(os.path.dirname(path), exist_ok=True)
        bpy.ops.object.select_all(action='DESELECT')
        self.arm.select_set(True)
        self.body.select_set(True)
        for extra in self.extras:
            extra.select_set(True)
        bpy.context.view_layer.objects.active = self.arm
        # Unity reads Blender's front (-Y) as its back, so the rig is turned round for the
        # export: in Unity the model faces +Z, as the shapes expect.
        self.arm.rotation_euler = (0, 0, math.pi)
        bpy.ops.export_scene.fbx(
            filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
            apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
            mesh_smooth_type='FACE', add_leaf_bones=False, use_armature_deform_only=False,
            primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL',
            bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=True,
            bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
            bake_anim_use_all_bones=False, bake_anim_step=1.0)
        self.arm.rotation_euler = (0, 0, 0)

    def preview(self, path, clip_frames, size=360, view=(35, 25), focus=None):
        """Renders a sheet of frames: clip_frames = [(clip name, frame), ...], one per cell.
        focus = (bone, width in metres) frames that bone close up instead of the whole body."""
        scene = bpy.context.scene
        scene.render.engine = 'BLENDER_WORKBENCH'
        scene.display.shading.light = 'STUDIO'
        scene.display.shading.color_type = 'MATERIAL'
        scene.display.shading.show_object_outline = True
        scene.display.shading.show_shadows = True
        scene.render.resolution_x = scene.render.resolution_y = size
        scene.render.film_transparent = False
        scene.world = bpy.data.worlds.new('W') if scene.world is None else scene.world
        scene.display.shading.background_type = 'VIEWPORT'
        scene.display.shading.background_color = (0.75, 0.78, 0.82)

        lo = Vector((min(v.co.x for v in self.body.data.vertices), min(v.co.y for v in self.body.data.vertices), 0))
        hi = Vector((max(v.co.x for v in self.body.data.vertices), max(v.co.y for v in self.body.data.vertices),
                     max(v.co.z for v in self.body.data.vertices)))
        centre = (lo + hi) / 2
        extent = max(hi.x - lo.x, hi.y - lo.y, hi.z) * 1.35
        if focus:
            bone = self.arm.data.bones[focus[0]]
            centre = (bone.head_local + bone.tail_local) / 2
            extent = focus[1]

        if 'PreviewFloor' not in bpy.data.objects:
            bpy.ops.mesh.primitive_plane_add(size=extent * 8)
            floor = bpy.context.object
            floor.name = 'PreviewFloor'
            fm = bpy.data.materials.new('PreviewFloor')
            fm.diffuse_color = (0.55, 0.58, 0.55, 1)
            floor.data.materials.append(fm)
        cam_data = bpy.data.cameras.new('PreviewCam')
        cam_data.type = 'ORTHO'
        cam_data.ortho_scale = extent
        cam = bpy.data.objects.new('PreviewCam', cam_data)
        bpy.context.collection.objects.link(cam)
        yaw, pitch = math.radians(view[0]), math.radians(view[1])
        d = extent * 3
        cam.location = centre + Vector((-math.sin(yaw) * math.cos(pitch) * d, -math.cos(yaw) * math.cos(pitch) * d,
                                        math.sin(pitch) * d))
        cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
        scene.camera = cam

        import numpy as np
        cols = min(len(clip_frames), 4)
        rows = math.ceil(len(clip_frames) / cols)
        sheet = np.zeros((rows * size, cols * size, 4), dtype=np.float32)
        tmp = path + '.tmp.png'
        ad = self.arm.animation_data
        for i, (clip, frame) in enumerate(clip_frames):
            for t in ad.nla_tracks:
                t.mute = True
            ad.action = bpy.data.actions[clip] if clip else None
            if ad.action is not None and ad.action_slot is None:
                ad.action_slot = ad.action.slots[0]
            scene.frame_set(frame)
            scene.render.filepath = tmp
            bpy.ops.render.render(write_still=True)
            img = bpy.data.images.load(tmp)
            px = np.array(img.pixels[:], dtype=np.float32).reshape(size, size, 4)
            bpy.data.images.remove(img)
            r, c = i // cols, i % cols
            sheet[(rows - 1 - r) * size:(rows - r) * size, c * size:(c + 1) * size] = px
        os.remove(tmp)
        ad.action = None
        for t in ad.nla_tracks:
            t.mute = False
        out = bpy.data.images.new('Sheet', cols * size, rows * size, alpha=True)
        out.pixels = sheet.ravel()
        out.filepath_raw = path
        out.file_format = 'PNG'
        out.save()
        bpy.data.objects.remove(cam)


def mirror_pose(pose):
    """Swaps .L and .R in a pose and flips side-to-side rotations, for the second half of a cycle."""
    out = {}
    for k, v in pose.items():
        loc = k.endswith('@')
        name = k[:-1] if loc else k
        if name.endswith('.L'):
            name = name[:-2] + '.R'
        elif name.endswith('.R'):
            name = name[:-2] + '.L'
        if loc:
            out[name + '@'] = (-v[0], v[1], v[2])
        else:
            out[name] = (v[0], -v[1], -v[2])
    return out


def cycle(frames, half_keys):
    """A looping cycle from its first half: half_keys = {frame: pose} with frames in
    [0, frames/2). The second half is the same poses mirrored left to right."""
    half = frames // 2
    keys = {}
    for f, pose in half_keys.items():
        keys[f] = pose
        keys[f + half] = mirror_pose(pose)
    keys[frames] = half_keys[0]
    return keys
