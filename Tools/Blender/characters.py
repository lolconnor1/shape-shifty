"""Builds the low-poly character models and their animations, and exports each as an FBX
into Assets/_Project/Art/Models/Characters.

    blender --background --factory-startup --python Tools/Blender/characters.py -- Human Rat
    blender ... -- all --preview <folder>     (also renders a sheet of poses per character)

Characters face -Y here (Blender's front view) with their feet at 0. Speeds in the clip
comments are the ground speeds the cycles' strides match; the Unity side plays them faster
or slower to suit (see LocomotionAnimator).
"""
import math
import os
import sys

from mathutils import Vector  # noqa: E402

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import lowpoly  # noqa: E402
from lowpoly import Character, cycle  # noqa: E402

PROJECT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
OUT = os.path.join(PROJECT, 'Assets', '_Project', 'Art', 'Models', 'Characters')

SKIN = (0.86, 0.67, 0.52)
DARK = (0.06, 0.05, 0.05)


MASK = (0.03, 0.03, 0.035)
MASK_EYE = (0.95, 0.95, 0.93)


# ------------------------------------------------------------------------------------------
# The player's mark: every form the player takes wears a burglar's eye mask, so they can be
# told apart from the real animals. It's a separate object, "BurglarMask", that the model
# prefabs keep switched off and PlayerShapeshifter switches on for the player.
# ------------------------------------------------------------------------------------------
def mask_colours(c):
    c.colour('Mask', MASK)
    c.colour('MaskEye', MASK_EYE)


# ------------------------------------------------------------------------------------------
# Humans: the player's human form (a stripy burglar), and zoo keepers (cap and uniform).
# ------------------------------------------------------------------------------------------
def human(name, keeper):
    c = Character(name)
    c.colour('Skin', SKIN)
    c.colour('Eye', DARK)
    c.colour('Shoe', (0.16, 0.13, 0.11))
    if keeper:
        c.colour('Shirt', (0.13, 0.42, 0.38))
        c.colour('Pants', (0.55, 0.48, 0.32))
        c.colour('Hair', (0.25, 0.17, 0.1))
    else:
        c.colour('Shirt', (0.08, 0.08, 0.09))
        c.colour('Stripe', (0.9, 0.9, 0.88))
        c.colour('Pants', (0.13, 0.13, 0.15))
        c.colour('Glove', (0.05, 0.05, 0.06))
        mask_colours(c)

    c.bone('Root', (0, 0, 0), (0, 0, 0.3))
    c.bone('Hips', (0, 0, 0.92), (0, 0, 1.02), 'Root')
    c.bone('Spine', (0, 0, 1.02), (0, 0, 1.25), 'Hips')
    c.bone('Chest', (0, 0, 1.25), (0, 0, 1.5), 'Spine')
    c.bone('Head', (0, 0, 1.52), (0, 0, 1.8), 'Chest')
    for s, x in (('L', 1), ('R', -1)):
        c.bone(f'UpperArm.{s}', (0.24 * x, 0, 1.46), (0.26 * x, 0, 1.18), 'Chest')
        c.bone(f'Forearm.{s}', (0.26 * x, 0, 1.18), (0.27 * x, 0, 0.93), f'UpperArm.{s}')
        c.bone(f'Thigh.{s}', (0.1 * x, 0, 0.92), (0.1 * x, 0, 0.5), 'Hips')
        c.bone(f'Shin.{s}', (0.1 * x, 0, 0.5), (0.1 * x, 0, 0.09), f'Thigh.{s}')
        c.bone(f'Foot.{s}', (0.1 * x, 0, 0.09), (0.1 * x, -0.15, 0.04), f'Shin.{s}')

    # Body
    c.box('Hips', 'Pants', (0.34, 0.2, 0.17), at=(0, 0, 0.95), top=(1.02, 1))
    c.box('Spine', 'Shirt', (0.35, 0.21, 0.26), at=(0, 0, 1.13), top=(1.06, 1.02), bevel=0.015)
    c.box('Chest', 'Shirt', (0.4, 0.23, 0.27), at=(0, 0, 1.37), top=(1.12, 0.94), bevel=0.02)
    c.cyl('Chest', 'Skin', 0.055, 0.1, at=(0, 0, 1.53))
    # Head
    c.box('Head', 'Skin', (0.22, 0.24, 0.26), at=(0, 0, 1.68), bottom=(0.86, 0.9), bevel=0.025)
    c.box('Head', 'Skin', (0.04, 0.05, 0.06), at=(0, -0.13, 1.66), bottom=(1.2, 1.3))  # nose
    c.mirror(c.box, 'Head', 'Eye', (0.032, 0.01, 0.042), at=(0.055, -0.121, 1.7))
    c.mirror(c.box, 'Head', 'Skin', (0.03, 0.06, 0.07), at=(0.115, 0.0, 1.68))  # ears
    if keeper:
        c.box('Head', 'Hair', (0.23, 0.2, 0.06), at=(0, 0.04, 1.77))
        c.cyl('Head', 'Shirt', 0.135, 0.085, at=(0, 0.005, 1.83), sides=8, radius2=0.12)
        c.box('Head', 'Shirt', (0.2, 0.13, 0.02), at=(0, -0.16, 1.795), rot=(-8, 0, 0))
    else:
        # Beanie, stripy top
        # Beanie: roomy enough that the head's top corners stay inside it.
        c.cyl('Head', 'Shirt', 0.178, 0.125, at=(0, 0.005, 1.875), sides=8, radius2=0.11)
        c.cyl('Head', 'Shirt', 0.182, 0.07, at=(0, 0.005, 1.79), sides=8)
        c.ball('Head', 'Shirt', 0.04, at=(0, 0.005, 1.95))
        for z, w, d in ((1.08, 0.357, 0.211), (1.17, 0.364, 0.213)):
            c.box('Spine', 'Stripe', (w + 0.008, d + 0.008, 0.055), at=(0, 0, z))
        for z, w, d in ((1.3, 0.405, 0.222), (1.4, 0.42, 0.218)):
            c.box('Chest', 'Stripe', (w + 0.008, d + 0.008, 0.055), at=(0, 0, z))
        for z in (1.39, 1.28):
            c.mirror(c.box, 'UpperArm.X', 'Stripe', (0.12, 0.13, 0.04), at=(0.25, 0, z))
        c.mirror(c.box, 'Forearm.X', 'Stripe', (0.098, 0.108, 0.04), at=(0.265, 0, 1.08))
        # The mask: a band round the eyes with holes for them, knotted at the back.
        c.into('BurglarMask')
        c.box('Head', 'Mask', (0.245, 0.265, 0.075), at=(0, 0, 1.7), bevel=0.01)
        c.mirror(c.box, 'Head', 'MaskEye', (0.05, 0.012, 0.036), at=(0.055, -0.135, 1.702))
        c.mirror(c.box, 'Head', 'Eye', (0.022, 0.01, 0.026), at=(0.055, -0.142, 1.702))
        c.mirror(c.box, 'Head', 'Mask', (0.03, 0.09, 0.035), at=(0.035, 0.16, 1.68), rot=(25, 0, 20))
        c.into(None)
    # Arms
    c.mirror(c.box, 'UpperArm.X', 'Shirt', (0.11, 0.12, 0.3), at=(0.25, 0, 1.33), bottom=(0.9, 0.9), bevel=0.012)
    c.mirror(c.box, 'Forearm.X', 'Skin' if keeper else 'Shirt', (0.09, 0.1, 0.25), at=(0.265, 0, 1.06), bottom=(0.9, 0.9))
    c.mirror(c.box, 'Forearm.X', 'Skin' if keeper else 'Glove', (0.07, 0.1, 0.11), at=(0.27, -0.005, 0.88), bottom=(0.8, 0.9), bevel=0.01)
    # Legs
    c.mirror(c.box, 'Thigh.X', 'Pants', (0.15, 0.17, 0.44), at=(0.1, 0, 0.71), bottom=(0.85, 0.85))
    c.mirror(c.box, 'Shin.X', 'Pants', (0.12, 0.13, 0.42), at=(0.1, 0, 0.3), bottom=(0.9, 0.9))
    c.mirror(c.box, 'Foot.X', 'Shoe', (0.12, 0.26, 0.09), at=(0.1, -0.05, 0.045), top=(0.95, 0.9), bevel=0.012)

    c.clip('Idle', 60, {
        0: {'UpperArm.L': (0, -5, 0), 'UpperArm.R': (0, 5, 0), 'Forearm.L': (-8, 0, 0), 'Forearm.R': (-8, 0, 0)},
        30: {'UpperArm.L': (0, -7, 0), 'UpperArm.R': (0, 7, 0), 'Forearm.L': (-10, 0, 0), 'Forearm.R': (-10, 0, 0),
             'Chest': (-2, 0, 0), 'Head': (2, 0, 4), 'Hips@': (0, 0, 0.006)},
        60: {'UpperArm.L': (0, -5, 0), 'UpperArm.R': (0, 5, 0), 'Forearm.L': (-8, 0, 0), 'Forearm.R': (-8, 0, 0)},
    })
    # Walk: 1.8 m/s, one stride (two steps) every 0.8 s.
    c.clip('Walk', 24, cycle(24, {
        0: {'Thigh.L': (-24, 0, 0), 'Shin.L': (4, 0, 0), 'Foot.L': (-10, 0, 0),
            'Thigh.R': (20, 0, 0), 'Shin.R': (14, 0, 0), 'Foot.R': (16, 0, 0),
            'UpperArm.L': (18, -4, 0), 'Forearm.L': (-8, 0, 0), 'UpperArm.R': (-20, 4, 0), 'Forearm.R': (-25, 0, 0),
            'Hips': (0, 0, -5), 'Spine': (3, 0, 0), 'Chest': (0, 0, 9), 'Head': (0, 0, -4), 'Hips@': (0, 0, -0.03)},
        6: {'Thigh.L': (2, 0, 0), 'Shin.L': (3, 0, 0), 'Foot.L': (0, 0, 0),
            'Thigh.R': (-16, 0, 0), 'Shin.R': (42, 0, 0), 'Foot.R': (4, 0, 0),
            'UpperArm.L': (2, -4, 0), 'Forearm.L': (-12, 0, 0), 'UpperArm.R': (-2, 4, 0), 'Forearm.R': (-14, 0, 0),
            'Hips': (0, 3, 0), 'Spine': (3, 0, 0), 'Hips@': (0, 0, 0.02)},
    }))
    # Run: 5 m/s, one stride every 0.53 s.
    c.clip('Run', 16, cycle(16, {
        0: {'Thigh.L': (-36, 0, 0), 'Shin.L': (12, 0, 0), 'Foot.L': (-8, 0, 0),
            'Thigh.R': (30, 0, 0), 'Shin.R': (50, 0, 0), 'Foot.R': (22, 0, 0),
            'UpperArm.L': (38, -6, 0), 'Forearm.L': (-55, 0, 0), 'UpperArm.R': (-45, 6, 0), 'Forearm.R': (-80, 0, 0),
            'Hips': (0, 0, -8), 'Spine': (12, 0, 0), 'Chest': (0, 0, 12), 'Head': (-8, 0, -4), 'Hips@': (0, 0, -0.05)},
        4: {'Thigh.L': (8, 0, 0), 'Shin.L': (18, 0, 0), 'Foot.L': (14, 0, 0),
            'Thigh.R': (-55, 0, 0), 'Shin.R': (95, 0, 0), 'Foot.R': (2, 0, 0),
            'UpperArm.L': (8, -6, 0), 'Forearm.L': (-70, 0, 0), 'UpperArm.R': (-12, 6, 0), 'Forearm.R': (-75, 0, 0),
            'Spine': (12, 0, 0), 'Head': (-8, 0, 0), 'Hips@': (0, 0, 0.05)},
    }))
    air = {'Thigh.L': (-45, 0, 0), 'Shin.L': (65, 0, 0), 'Thigh.R': (12, 0, 0), 'Shin.R': (35, 0, 0),
           'UpperArm.L': (25, -30, 0), 'Forearm.L': (-35, 0, 0), 'UpperArm.R': (-35, 30, 0), 'Forearm.R': (-45, 0, 0),
           'Spine': (6, 0, 0), 'Head': (-6, 0, 0)}
    c.clip('Air', 10, {0: air, 10: air})
    return c


# ------------------------------------------------------------------------------------------
# Rat: the form the player starts in. Fits a collider 0.32 m tall.
# ------------------------------------------------------------------------------------------
def rat():
    c = Character('Rat')
    c.colour('Fur', (0.42, 0.38, 0.36))
    c.colour('FurLight', (0.62, 0.57, 0.53))
    c.colour('Pink', (0.9, 0.6, 0.6))
    c.colour('Eye', DARK)

    c.bone('Root', (0, 0, 0), (0, 0, 0.08))
    c.bone('Body', (0, 0.1, 0.12), (0, -0.09, 0.13), 'Root')
    c.bone('Head', (0, -0.09, 0.13), (0, -0.25, 0.12), 'Body')
    c.bone('Tail1', (0, 0.15, 0.1), (0, 0.29, 0.07), 'Body')
    c.bone('Tail2', (0, 0.29, 0.07), (0, 0.43, 0.035), 'Tail1')
    c.bone('Tail3', (0, 0.43, 0.035), (0, 0.57, 0.02), 'Tail2')
    for s, x in (('L', 1), ('R', -1)):
        c.bone(f'LegF.{s}', (0.055 * x, -0.07, 0.1), (0.06 * x, -0.08, 0.0), 'Body')
        c.bone(f'LegB.{s}', (0.07 * x, 0.08, 0.1), (0.07 * x, 0.06, 0.0), 'Body')

    c.ball('Body', 'Fur', 0.1, at=(0, 0.0, 0.125), scale=(0.95, 1.5, 0.85))
    c.ball('Body', 'Fur', 0.085, at=(0, 0.08, 0.12), scale=(1.15, 1.1, 0.95))
    c.ball('Body', 'FurLight', 0.08, at=(0, -0.02, 0.09), scale=(0.9, 1.4, 0.5))
    c.cyl('Head', 'Fur', 0.068, 0.16, at=(0, -0.17, 0.125), rot=(90, 0, 0), sides=6, radius2=0.02)
    c.ball('Head', 'Pink', 0.018, at=(0, -0.252, 0.122), detail=1)
    c.mirror(c.cyl, 'Head', 'Pink', 0.034, 0.012, at=(0.042, -0.14, 0.2), rot=(75, 0, 25), sides=7)
    c.mirror(c.ball, 'Head', 'Eye', 0.013, at=(0.04, -0.185, 0.152))
    mask_colours(c)
    c.into('BurglarMask')
    # A domino mask, since a rat's eyes are on the sides of its snout: a patch over each,
    # joined over the top.
    c.mirror(c.ball, 'Head', 'Mask', 0.027, at=(0.041, -0.186, 0.153), scale=(0.5, 1.2, 0.95), rot=(0, -34, 0))
    c.mirror(c.ball, 'Head', 'MaskEye', 0.014, at=(0.05, -0.188, 0.158), scale=(0.6, 1, 1), rot=(0, -34, 0))
    c.mirror(c.ball, 'Head', 'Eye', 0.0075, at=(0.056, -0.19, 0.162))
    c.box('Head', 'Mask', (0.07, 0.03, 0.014), at=(0, -0.19, 0.162))
    c.into(None)
    c.limb('Tail1', 'Pink', (0, 0.14, 0.1), (0, 0.29, 0.07), 0.018, 0.014)
    c.limb('Tail2', 'Pink', (0, 0.29, 0.07), (0, 0.43, 0.035), 0.014, 0.01)
    c.limb('Tail3', 'Pink', (0, 0.43, 0.035), (0, 0.57, 0.02), 0.01, 0.004)
    c.mirror(c.limb, 'LegF.X', 'Fur', a=(0.055, -0.07, 0.1), b=(0.06, -0.078, 0.02), radius=0.022, radius2=0.016)
    c.mirror(c.box, 'LegF.X', 'Pink', (0.032, 0.05, 0.016), at=(0.06, -0.09, 0.008))
    c.mirror(c.ball, 'LegB.X', 'Fur', 0.045, at=(0.06, 0.08, 0.1), scale=(0.7, 1.1, 1))
    c.mirror(c.limb, 'LegB.X', 'Fur', a=(0.07, 0.08, 0.08), b=(0.07, 0.065, 0.02), radius=0.02, radius2=0.015)
    c.mirror(c.box, 'LegB.X', 'Pink', (0.034, 0.07, 0.016), at=(0.07, 0.05, 0.008))

    c.clip('Idle', 60, {
        0: {},
        8: {'Head': (-8, 0, 6), 'Tail2': (0, 0, 8)},
        14: {'Head': (2, 0, 8), 'Tail2': (0, 0, 10)},
        20: {'Head': (-8, 0, 8), 'Tail2': (0, 0, 12), 'Tail3': (0, 0, 8)},
        34: {'Head': (0, 0, -6), 'Tail2': (0, 0, -6), 'Tail3': (0, 0, -10)},
        46: {'Head': (-5, 0, -8), 'Tail2': (0, 0, -8)},
        60: {},
    })
    # Scurry (a trot, diagonal legs together): 1.2 m/s.
    c.clip('Walk', 12, cycle(12, {
        0: {'LegF.L': (-32, 0, 0), 'LegB.R': (-28, 0, 0), 'LegF.R': (30, 0, 0), 'LegB.L': (32, 0, 0),
            'Body': (0, 4, 0), 'Head': (0, 0, -4), 'Tail1': (0, 0, 10), 'Tail2': (0, 0, 8)},
        3: {'LegF.L': (0, 0, 0), 'LegB.R': (0, 0, 0), 'LegF.R': (-6, 0, 0), 'LegB.L': (-6, 0, 0),
            'Body@': (0, 0, 0.012), 'Tail1': (0, 0, 2), 'Tail2': (0, 0, 0)},
    }))
    # Bound (front legs, then back legs): 3.5 m/s.
    bound = {
        0: {'LegF.L': (-48, 0, 0), 'LegF.R': (-42, 0, 0), 'LegB.L': (42, 0, 0), 'LegB.R': (36, 0, 0),
            'Body': (-6, 0, 0), 'Head': (4, 0, 0), 'Tail1': (-8, 0, 0), 'Body@': (0, 0, 0.025)},
        3: {'LegF.L': (8, 0, 0), 'LegF.R': (2, 0, 0), 'LegB.L': (-10, 0, 0), 'LegB.R': (-4, 0, 0),
            'Body': (8, 0, 0), 'Head': (-6, 0, 0), 'Tail1': (6, 0, 0), 'Body@': (0, 0, 0.0)},
        5: {'LegF.L': (40, 0, 0), 'LegF.R': (34, 0, 0), 'LegB.L': (-48, 0, 0), 'LegB.R': (-42, 0, 0),
            'Body': (2, 0, 0), 'Head': (-2, 0, 0), 'Tail1': (10, 0, 0), 'Body@': (0, 0, -0.008)},
        8: {'LegF.L': (-20, 0, 0), 'LegF.R': (-14, 0, 0), 'LegB.L': (30, 0, 0), 'LegB.R': (24, 0, 0),
            'Body': (-4, 0, 0), 'Head': (2, 0, 0), 'Tail1': (-4, 0, 0), 'Body@': (0, 0, 0.02)},
    }
    bound[10] = bound[0]
    c.clip('Run', 10, bound)
    air = {'LegF.L': (-40, -10, 0), 'LegF.R': (-40, 10, 0), 'LegB.L': (40, -10, 0), 'LegB.R': (40, 10, 0),
           'Body': (-8, 0, 0), 'Tail1': (-15, 0, 0), 'Tail2': (-10, 0, 0)}
    c.clip('Air', 10, {0: air, 10: air})
    return c


# ------------------------------------------------------------------------------------------
# Penguin: 0.8 m. Belly slides and swims are the player's body tipping over (PlayerController).
# ------------------------------------------------------------------------------------------
def penguin():
    c = Character('Penguin')
    c.colour('Black', (0.13, 0.15, 0.2))
    c.colour('White', (0.93, 0.94, 0.95))
    c.colour('Orange', (0.98, 0.55, 0.12))
    c.colour('Eye', DARK)

    c.bone('Root', (0, 0, 0), (0, 0, 0.08))
    c.bone('Body', (0, 0, 0.1), (0, 0, 0.56), 'Root')
    c.bone('Head', (0, 0, 0.56), (0, 0, 0.82), 'Body')
    for s, x in (('L', 1), ('R', -1)):
        c.bone(f'Flipper.{s}', (0.18 * x, 0, 0.52), (0.25 * x, 0, 0.27), 'Body')
        c.bone(f'Foot.{s}', (0.08 * x, 0.0, 0.05), (0.08 * x, -0.12, 0.02), 'Root')

    c.ball('Body', 'Black', 0.21, at=(0, 0.02, 0.36), scale=(1, 0.95, 1.45), detail=2)
    c.ball('Body', 'White', 0.18, at=(0, -0.07, 0.33), scale=(0.88, 0.75, 1.3), detail=2)
    c.cyl('Body', 'Black', 0.07, 0.14, at=(0, 0.2, 0.12), rot=(-120, 0, 0), sides=5, radius2=0.01)  # tail
    c.ball('Head', 'Black', 0.135, at=(0, 0.0, 0.66), scale=(1, 1, 0.95), detail=2)
    c.mirror(c.ball, 'Head', 'White', 0.045, at=(0.06, -0.09, 0.68), scale=(0.7, 0.6, 1))
    c.mirror(c.ball, 'Head', 'Eye', 0.018, at=(0.062, -0.115, 0.69))
    c.cyl('Head', 'Orange', 0.035, 0.1, at=(0, -0.17, 0.635), rot=(95, 0, 0), sides=4, radius2=0.004)
    mask_colours(c)
    c.into('BurglarMask')
    c.cyl('Head', 'Mask', 0.147, 0.065, at=(0, 0, 0.69), sides=10)
    c.mirror(c.ball, 'Head', 'MaskEye', 0.032, at=(0.062, -0.13, 0.693), scale=(1, 0.5, 1.1))
    c.mirror(c.ball, 'Head', 'Eye', 0.016, at=(0.064, -0.145, 0.695))
    c.mirror(c.box, 'Head', 'Mask', (0.03, 0.08, 0.03), at=(0.03, 0.17, 0.68), rot=(25, 0, 20))
    c.into(None)
    c.mirror(c.box, 'Flipper.X', 'Black', (0.035, 0.11, 0.28), at=(0.2, 0.0, 0.4), rot=(0, -18, 0), top=(1, 1), bottom=(0.6, 0.55))
    c.mirror(c.box, 'Foot.X', 'Orange', (0.09, 0.15, 0.03), at=(0.08, -0.07, 0.015), top=(1, 0.9), bottom=(1.15, 1))
    c.mirror(c.cyl, 'Foot.X', 'Orange', 0.025, 0.06, at=(0.08, 0.0, 0.05), sides=5)

    c.clip('Idle', 60, {
        0: {},
        15: {'Head': (0, 0, 20), 'Flipper.L': (0, -6, 0), 'Flipper.R': (0, 6, 0)},
        25: {'Head': (-6, 0, 22)},
        40: {'Head': (0, 0, -18), 'Flipper.L': (0, -4, 0), 'Flipper.R': (0, 4, 0)},
        50: {'Head': (4, 0, -12)},
        60: {},
    })
    # Waddle: 1 m/s. The body rocks onto the standing foot while the other lifts.
    c.clip('Walk', 16, cycle(16, {
        0: {'Body': (0, 9, -5), 'Head': (0, -5, 4), 'Foot.R': (-15, 0, 0), 'Foot.R@': (0, -0.05, 0.05),
            'Flipper.L': (0, -22, 0), 'Flipper.R': (0, 8, 0)},
        4: {'Body': (0, 0, 0), 'Flipper.L': (0, -10, 0), 'Flipper.R': (0, 10, 0), 'Foot.R@': (0, -0.03, 0.0)},
    }))
    # Hurried waddle: 2.6 m/s, leaning in with the flippers out.
    c.clip('Run', 12, cycle(12, {
        0: {'Body': (12, 13, -7), 'Head': (-12, -6, 5), 'Foot.R': (-20, 0, 0), 'Foot.R@': (0, -0.08, 0.07),
            'Flipper.L': (0, -50, 0), 'Flipper.R': (0, 25, 0)},
        3: {'Body': (12, 0, 0), 'Head': (-12, 0, 0), 'Flipper.L': (0, -30, 0), 'Flipper.R': (0, 40, 0), 'Foot.R@': (0, -0.04, 0.0)},
    }))
    air = {'Flipper.L': (0, -75, 0), 'Flipper.R': (0, 75, 0), 'Foot.L': (20, 0, 0), 'Foot.R': (20, 0, 0),
           'Foot.L@': (0, 0.03, 0.03), 'Foot.R@': (0, 0.03, 0.03), 'Head': (-10, 0, 0)}
    c.clip('Air', 10, {0: air, 10: air})
    return c


# ------------------------------------------------------------------------------------------
# Wolf: about 0.85 m at the shoulder. Its Head is never animated, for code to tip back to
# howl or down to sniff (Wolf, PlayerHowl), and its Mouth holds a carried bone (Wolf, PlayerCarry).
# ------------------------------------------------------------------------------------------
def wolf():
    c = Character('Wolf')
    c.colour('Fur', (0.46, 0.46, 0.48))
    c.colour('FurDark', (0.27, 0.27, 0.3))
    c.colour('FurLight', (0.8, 0.78, 0.74))
    c.colour('Nose', (0.08, 0.07, 0.07))
    c.colour('Eye', (0.95, 0.75, 0.2))

    c.bone('Root', (0, 0, 0), (0, 0, 0.2))
    c.bone('Hips', (0, 0.36, 0.64), (0, 0.0, 0.66), 'Root')
    c.bone('Chest', (0, 0.0, 0.66), (0, -0.32, 0.7), 'Hips')
    c.bone('Neck', (0, -0.3, 0.74), (0, -0.44, 0.88), 'Chest')
    # Rolled so that in Unity, turning it about its X axis by a negative angle lifts the nose
    # (as Wolf and PlayerHowl expect).
    c.bone('Head', (0, -0.44, 0.88), (0, -0.72, 0.82), 'Neck', free=True, roll=180)
    c.bone('Mouth', (0, -0.68, 0.76), (0, -0.74, 0.76), 'Head', free=True)
    c.bone('Tail1', (0, 0.4, 0.66), (0, 0.62, 0.56), 'Hips')
    c.bone('Tail2', (0, 0.62, 0.56), (0, 0.82, 0.42), 'Tail1')
    for s, x in (('L', 1), ('R', -1)):
        c.bone(f'UpperF.{s}', (0.13 * x, -0.27, 0.58), (0.13 * x, -0.29, 0.32), 'Chest')
        c.bone(f'LowerF.{s}', (0.13 * x, -0.29, 0.32), (0.13 * x, -0.28, 0.05), f'UpperF.{s}')
        c.bone(f'UpperB.{s}', (0.13 * x, 0.3, 0.6), (0.13 * x, 0.38, 0.33), 'Hips')
        c.bone(f'LowerB.{s}', (0.13 * x, 0.38, 0.33), (0.13 * x, 0.32, 0.05), f'UpperB.{s}')

    # Body
    c.box('Hips', 'Fur', (0.3, 0.42, 0.3), at=(0, 0.2, 0.64), top=(1, 1), bottom=(0.85, 0.9), bevel=0.04)
    c.box('Hips', 'FurDark', (0.2, 0.36, 0.05), at=(0, 0.18, 0.8))
    c.box('Chest', 'Fur', (0.34, 0.4, 0.38), at=(0, -0.16, 0.68), top=(1.05, 1), bottom=(0.8, 0.85), bevel=0.05)
    c.box('Chest', 'FurLight', (0.24, 0.12, 0.3), at=(0, -0.33, 0.62), bottom=(0.7, 1))  # chest ruff
    c.box('Chest', 'FurDark', (0.22, 0.34, 0.05), at=(0, -0.14, 0.87))
    c.limb('Neck', 'Fur', (0, -0.28, 0.72), (0, -0.46, 0.88), 0.13, 0.11, sides=6)
    # Head (free bone)
    c.box('Head', 'Fur', (0.25, 0.22, 0.2), at=(0, -0.5, 0.88), bevel=0.03)
    c.box('Head', 'Fur', (0.13, 0.2, 0.1), at=(0, -0.66, 0.83), top=(0.8, 0.9), bottom=(0.9, 1))  # snout
    c.box('Head', 'FurLight', (0.11, 0.18, 0.04), at=(0, -0.65, 0.77), bottom=(0.9, 1))  # jaw
    c.box('Head', 'Nose', (0.06, 0.05, 0.05), at=(0, -0.765, 0.86))
    c.mirror(c.cyl, 'Head', 'Fur', 0.055, 0.12, at=(0.08, -0.47, 1.02), rot=(-8, 12, 0), sides=4, radius2=0.005)
    c.mirror(c.box, 'Head', 'Eye', (0.04, 0.012, 0.025), at=(0.065, -0.61, 0.915), rot=(0, 0, 18))
    c.mirror(c.box, 'Head', 'FurLight', (0.07, 0.1, 0.08), at=(0.11, -0.52, 0.83))  # cheeks
    mask_colours(c)
    c.into('BurglarMask')
    c.box('Head', 'Mask', (0.27, 0.245, 0.07), at=(0, -0.5, 0.915), bevel=0.01)
    c.mirror(c.box, 'Head', 'MaskEye', (0.055, 0.012, 0.035), at=(0.065, -0.628, 0.917), rot=(0, 0, 18))
    c.mirror(c.box, 'Head', 'Eye', (0.03, 0.01, 0.022), at=(0.065, -0.635, 0.917), rot=(0, 0, 18))
    c.mirror(c.box, 'Head', 'Mask', (0.035, 0.1, 0.04), at=(0.04, -0.36, 0.92), rot=(25, 0, 20))
    c.into(None)
    # Tail
    c.limb('Tail1', 'Fur', (0, 0.38, 0.66), (0, 0.62, 0.56), 0.06, 0.08, sides=6)
    c.limb('Tail2', 'FurDark', (0, 0.62, 0.56), (0, 0.84, 0.41), 0.08, 0.02, sides=6)
    # Legs
    c.mirror(c.limb, 'UpperF.X', 'Fur', a=(0.13, -0.27, 0.62), b=(0.13, -0.29, 0.32), radius=0.075, radius2=0.05)
    c.mirror(c.limb, 'LowerF.X', 'FurLight', a=(0.13, -0.29, 0.33), b=(0.13, -0.28, 0.05), radius=0.045, radius2=0.04)
    c.mirror(c.box, 'LowerF.X', 'FurLight', (0.08, 0.11, 0.05), at=(0.13, -0.31, 0.025))
    c.mirror(c.limb, 'UpperB.X', 'Fur', a=(0.13, 0.26, 0.64), b=(0.13, 0.38, 0.33), radius=0.1, radius2=0.055)
    c.mirror(c.limb, 'LowerB.X', 'FurLight', a=(0.13, 0.38, 0.34), b=(0.13, 0.32, 0.05), radius=0.045, radius2=0.04)
    c.mirror(c.box, 'LowerB.X', 'FurLight', (0.08, 0.11, 0.05), at=(0.13, 0.29, 0.025))

    c.clip('Idle', 60, {
        0: {'Tail1': (12, 0, 0)},
        20: {'Tail1': (12, 0, 10), 'Tail2': (0, 0, 8), 'Neck': (-3, 0, 4), 'Chest': (-1, 0, 0), 'Hips@': (0, 0, 0.004)},
        40: {'Tail1': (12, 0, -8), 'Tail2': (0, 0, -6), 'Neck': (0, 0, -3)},
        60: {'Tail1': (12, 0, 0)},
    })
    canine_gaits(c)
    return c


def canine_gaits(c):
    """Walk, Trot, Run (gallop) and Air for a four-legged rig with the wolf's bones: Hips,
    Chest, Neck, Tail1/2 and UpperF/LowerF/UpperB/LowerB.L/R. Poses are in armature space,
    so they suit any dog of about the wolf's size."""
    # Walk (diagonal legs together): 1.8 m/s.
    c.clip('Walk', 20, cycle(20, {
        0: {'UpperF.L': (-26, 0, 0), 'LowerF.L': (6, 0, 0), 'UpperB.R': (-22, 0, 0), 'LowerB.R': (-4, 0, 0),
            'UpperF.R': (22, 0, 0), 'LowerF.R': (16, 0, 0), 'UpperB.L': (22, 0, 0), 'LowerB.L': (12, 0, 0),
            'Hips': (0, 3, -3), 'Chest': (0, -3, 4), 'Neck': (4, 0, -3), 'Tail1': (10, 0, 6), 'Hips@': (0, 0, -0.012)},
        5: {'UpperF.L': (2, 0, 0), 'LowerF.L': (2, 0, 0), 'UpperB.R': (0, 0, 0), 'LowerB.R': (0, 0, 0),
            'UpperF.R': (-12, 0, 0), 'LowerF.R': (55, 0, 0), 'UpperB.L': (-8, 0, 0), 'LowerB.L': (40, 0, 0),
            'Neck': (0, 0, 0), 'Tail1': (12, 0, 0), 'Hips@': (0, 0, 0.012)},
    }))
    # Trot: 4.5 m/s, the same diagonal pairs as the walk (so the two blend) with a moment
    # in the air between steps.
    c.clip('Trot', 14, cycle(14, {
        0: {'UpperF.L': (-34, 0, 0), 'LowerF.L': (4, 0, 0), 'UpperB.R': (-30, 0, 0), 'LowerB.R': (-6, 0, 0),
            'UpperF.R': (30, 0, 0), 'LowerF.R': (30, 0, 0), 'UpperB.L': (30, 0, 0), 'LowerB.L': (18, 0, 0),
            'Hips': (0, 2, -2), 'Chest': (0, -2, 3), 'Neck': (6, 0, -2), 'Tail1': (8, 0, 4), 'Hips@': (0, 0, -0.025)},
        3: {'UpperF.L': (8, 0, 0), 'LowerF.L': (2, 0, 0), 'UpperB.R': (6, 0, 0), 'LowerB.R': (0, 0, 0),
            'UpperF.R': (-18, 0, 0), 'LowerF.R': (70, 0, 0), 'UpperB.L': (-14, 0, 0), 'LowerB.L': (50, 0, 0),
            'Neck': (0, 0, 0), 'Tail1': (12, 0, 0), 'Hips@': (0, 0, 0.03)},
    }))
    # Gallop: 9 m/s. Stretched out in the air, then gathered up with the back legs under.
    # Its legs don't line up with the trot's, so the game switches between them rather than
    # blending (LocomotionAnimator).
    gallop = {
        0: {'UpperF.L': (-48, 0, 0), 'LowerF.L': (-8, 0, 0), 'UpperF.R': (-40, 0, 0), 'LowerF.R': (-4, 0, 0),
            'UpperB.L': (48, 0, 0), 'LowerB.L': (10, 0, 0), 'UpperB.R': (40, 0, 0), 'LowerB.R': (6, 0, 0),
            'Hips': (-4, 0, 0), 'Chest': (2, 0, 0), 'Neck': (-6, 0, 0), 'Tail1': (-10, 0, 0), 'Hips@': (0, 0, 0.06)},
        3: {'UpperF.L': (2, 0, 0), 'LowerF.L': (4, 0, 0), 'UpperF.R': (-12, 0, 0), 'LowerF.R': (0, 0, 0),
            'UpperB.L': (12, 0, 0), 'LowerB.L': (50, 0, 0), 'UpperB.R': (20, 0, 0), 'LowerB.R': (40, 0, 0),
            'Hips': (4, 0, 0), 'Chest': (6, 0, 0), 'Neck': (6, 0, 0), 'Tail1': (0, 0, 0), 'Hips@': (0, 0, -0.01)},
        6: {'UpperF.L': (45, 0, 0), 'LowerF.L': (40, 0, 0), 'UpperF.R': (30, 0, 0), 'LowerF.R': (20, 0, 0),
            'UpperB.L': (-45, 0, 0), 'LowerB.L': (-5, 0, 0), 'UpperB.R': (-50, 0, 0), 'LowerB.R': (-8, 0, 0),
            'Hips': (12, 0, 0), 'Chest': (-6, 0, 0), 'Neck': (-4, 0, 0), 'Tail1': (8, 0, 0), 'Hips@': (0, 0, -0.04)},
        9: {'UpperF.L': (-20, 0, 0), 'LowerF.L': (70, 0, 0), 'UpperF.R': (-6, 0, 0), 'LowerF.R': (80, 0, 0),
            'UpperB.L': (10, 0, 0), 'LowerB.L': (0, 0, 0), 'UpperB.R': (0, 0, 0), 'LowerB.R': (4, 0, 0),
            'Hips': (2, 0, 0), 'Chest': (0, 0, 0), 'Neck': (2, 0, 0), 'Tail1': (-4, 0, 0), 'Hips@': (0, 0, 0.02)},
    }
    gallop[12] = gallop[0]
    c.clip('Run', 12, gallop)
    air = {'UpperF.L': (-45, 0, 0), 'LowerF.L': (30, 0, 0), 'UpperF.R': (-38, 0, 0), 'LowerF.R': (40, 0, 0),
           'UpperB.L': (40, 0, 0), 'LowerB.L': (8, 0, 0), 'UpperB.R': (35, 0, 0), 'LowerB.R': (12, 0, 0),
           'Neck': (-10, 0, 0), 'Tail1': (-12, 0, 0)}
    c.clip('Air', 10, {0: air, 10: air})


# ------------------------------------------------------------------------------------------
# The zoo's guard dogs: black-and-tan Dobermans in red spiked collars, so they never look
# like the player's grey wolf. The wolf's rig (and so its gaits), plus a Jaw that the clips
# never move, for GuardDog to snap open on each bark.
# ------------------------------------------------------------------------------------------
def guard_dog():
    c = Character('GuardDog')
    c.colour('Coat', (0.13, 0.1, 0.1))
    c.colour('Tan', (0.7, 0.42, 0.18))
    c.colour('Nose', (0.04, 0.035, 0.035))
    c.colour('Eye', (0.95, 0.62, 0.12))
    c.colour('Collar', (0.78, 0.1, 0.1))
    c.colour('Stud', (0.82, 0.82, 0.85))
    c.colour('Mouth', (0.45, 0.08, 0.1))
    c.colour('Tongue', (0.92, 0.42, 0.48))
    c.colour('Teeth', (0.96, 0.95, 0.9))

    c.bone('Root', (0, 0, 0), (0, 0, 0.2))
    c.bone('Hips', (0, 0.34, 0.68), (0, 0.0, 0.72), 'Root')
    c.bone('Chest', (0, 0.0, 0.72), (0, -0.3, 0.76), 'Hips')
    c.bone('Neck', (0, -0.28, 0.8), (0, -0.42, 0.98), 'Chest')
    c.bone('Head', (0, -0.42, 0.98), (0, -0.74, 0.94), 'Neck')
    c.bone('Jaw', (0, -0.5, 0.9), (0, -0.74, 0.88), 'Head', free=True)
    c.bone('Tail1', (0, 0.36, 0.72), (0, 0.48, 0.84), 'Hips')
    c.bone('Tail2', (0, 0.48, 0.84), (0, 0.56, 0.98), 'Tail1')
    for s, x in (('L', 1), ('R', -1)):
        c.bone(f'UpperF.{s}', (0.1 * x, -0.25, 0.62), (0.1 * x, -0.27, 0.33), 'Chest')
        c.bone(f'LowerF.{s}', (0.1 * x, -0.27, 0.33), (0.1 * x, -0.26, 0.05), f'UpperF.{s}')
        c.bone(f'UpperB.{s}', (0.1 * x, 0.27, 0.64), (0.1 * x, 0.37, 0.35), 'Hips')
        c.bone(f'LowerB.{s}', (0.1 * x, 0.37, 0.35), (0.1 * x, 0.31, 0.05), f'UpperB.{s}')

    # Body: a deep chest and a tucked-up waist
    c.box('Chest', 'Coat', (0.3, 0.42, 0.42), at=(0, -0.13, 0.68), bottom=(0.72, 0.75), bevel=0.05)
    c.box('Hips', 'Coat', (0.26, 0.42, 0.24), at=(0, 0.19, 0.72), bottom=(0.78, 0.85), bevel=0.04)
    c.mirror(c.box, 'Chest', 'Tan', (0.07, 0.04, 0.07), at=(0.065, -0.335, 0.72), bevel=0.01)  # chest spots
    c.limb('Neck', 'Coat', (0, -0.24, 0.76), (0, -0.46, 1.0), 0.11, 0.085, sides=6)
    # Red collar with silver spikes
    neck = Vector((0, -0.19, 0.22)).normalized()
    ring = Vector((0, -0.35, 0.86))
    c.limb('Neck', 'Collar', ring - neck * 0.035, ring + neck * 0.035, 0.122, sides=8)
    side, up = Vector((1, 0, 0)), neck.cross(Vector((1, 0, 0)))
    for i in range(8):
        a = math.radians(22.5 + 45 * i)
        out = side * math.cos(a) + up * math.sin(a)
        c.limb('Neck', 'Stud', ring + out * 0.115, ring + out * 0.175, 0.022, 0.002, sides=4)
    # Head: a long wedge with tan cheeks and brows, and tall cropped ears
    c.box('Head', 'Coat', (0.2, 0.2, 0.18), at=(0, -0.5, 0.99), top=(0.85, 0.9), bevel=0.03)
    c.box('Head', 'Coat', (0.12, 0.22, 0.1), at=(0, -0.68, 0.955), top=(0.8, 0.9), bottom=(0.95, 1))  # muzzle
    c.box('Head', 'Nose', (0.07, 0.05, 0.05), at=(0, -0.79, 0.975))
    c.mirror(c.box, 'Head', 'Tan', (0.05, 0.16, 0.05), at=(0.055, -0.66, 0.92))  # tan muzzle sides
    c.mirror(c.box, 'Head', 'Tan', (0.045, 0.025, 0.02), at=(0.055, -0.6, 1.06), rot=(0, 0, 18))  # brows
    c.mirror(c.box, 'Head', 'Eye', (0.04, 0.012, 0.022), at=(0.06, -0.605, 1.025), rot=(0, 0, 18))
    c.mirror(c.cyl, 'Head', 'Coat', 0.05, 0.19, at=(0.07, -0.47, 1.15), rot=(-6, 10, 0), sides=4, radius2=0.004)
    c.mirror(c.cyl, 'Head', 'Teeth', 0.012, 0.035, at=(0.04, -0.765, 0.9), rot=(180, 0, 0), sides=4, radius2=0.001)  # fangs
    # Lower jaw (snaps open to bark)
    c.box('Jaw', 'Tan', (0.1, 0.2, 0.045), at=(0, -0.66, 0.885), bottom=(0.9, 1))
    c.box('Jaw', 'Mouth', (0.08, 0.17, 0.01), at=(0, -0.65, 0.91))
    c.box('Jaw', 'Tongue', (0.06, 0.12, 0.012), at=(0, -0.64, 0.915))
    # A thin tail held up
    c.limb('Tail1', 'Coat', (0, 0.34, 0.71), (0, 0.48, 0.84), 0.04, 0.032, sides=5)
    c.limb('Tail2', 'Coat', (0, 0.48, 0.84), (0, 0.57, 1.0), 0.032, 0.01, sides=5)
    # Legs: black above, tan socks and paws
    c.mirror(c.limb, 'UpperF.X', 'Coat', a=(0.1, -0.25, 0.66), b=(0.1, -0.27, 0.32), radius=0.065, radius2=0.042)
    c.mirror(c.limb, 'LowerF.X', 'Coat', a=(0.1, -0.27, 0.33), b=(0.1, -0.265, 0.17), radius=0.038, radius2=0.035)
    c.mirror(c.limb, 'LowerF.X', 'Tan', a=(0.1, -0.265, 0.18), b=(0.1, -0.26, 0.05), radius=0.037, radius2=0.035)
    c.mirror(c.box, 'LowerF.X', 'Tan', (0.07, 0.1, 0.05), at=(0.1, -0.285, 0.025))
    c.mirror(c.limb, 'UpperB.X', 'Coat', a=(0.1, 0.22, 0.68), b=(0.1, 0.37, 0.34), radius=0.09, radius2=0.048)
    c.mirror(c.limb, 'LowerB.X', 'Coat', a=(0.1, 0.37, 0.35), b=(0.1, 0.34, 0.19), radius=0.04, radius2=0.036)
    c.mirror(c.limb, 'LowerB.X', 'Tan', a=(0.1, 0.34, 0.2), b=(0.1, 0.31, 0.05), radius=0.037, radius2=0.035)
    c.mirror(c.box, 'LowerB.X', 'Tan', (0.07, 0.1, 0.05), at=(0.1, 0.285, 0.025))

    # Idle: on guard, panting, tail going, looking about.
    idle = {}
    for f in range(0, 61, 5):
        wag = 22 if (f // 5) % 2 == 0 else -22
        pant = 0.006 if (f // 5) % 2 == 0 else -0.004
        idle[f] = {'Tail1': (0, 0, wag), 'Tail2': (0, 0, wag * 0.6), 'Chest@': (0, 0, pant)}
    idle[20]['Neck'] = (0, 0, 10)
    idle[25]['Neck'] = (0, 0, 10)
    idle[40]['Neck'] = (-4, 0, -8)
    idle[45]['Neck'] = (-4, 0, -8)
    c.clip('Idle', 60, idle)
    canine_gaits(c)
    return c


# ------------------------------------------------------------------------------------------
# Monkeys, 1.1 m: the Brown troop (also the player's monkey form), the Guerillas (grey, red
# headband; the Brute is the same model scaled up) and the Guerilla King (crown and cape;
# scaled up 2.4 in his prefab). ArmL and ArmR are shoulder pivots that the clips never move,
# for game code to swing (throwing, waving, beating his chest: Monkey, KingMonkey,
# PlayerThrower); the animated arm hangs inside each, so the two add together.
# ------------------------------------------------------------------------------------------
def monkey(name, kind):
    c = Character(name)
    if kind == 'brown':
        c.colour('Fur', (0.42, 0.27, 0.15))
        c.colour('Face', (0.86, 0.68, 0.52))
    elif kind == 'grey':
        c.colour('Fur', (0.47, 0.48, 0.52))
        c.colour('Face', (0.72, 0.66, 0.66))
        c.colour('Red', (0.78, 0.1, 0.08))
    else:
        c.colour('Fur', (0.32, 0.33, 0.37))
        c.colour('Face', (0.72, 0.66, 0.66))
        c.colour('Red', (0.78, 0.1, 0.08))
        c.colour('Gold', (1.0, 0.78, 0.22))
    c.colour('Eye', DARK)

    c.bone('Root', (0, 0, 0), (0, 0, 0.15))
    c.bone('Hips', (0, 0, 0.4), (0, 0, 0.5), 'Root')
    c.bone('Spine', (0, 0, 0.5), (0, 0, 0.78), 'Hips')
    c.bone('Head', (0, 0, 0.8), (0, 0, 1.12), 'Spine')
    c.bone('Tail1', (0, 0.12, 0.42), (0, 0.3, 0.36), 'Hips')
    c.bone('Tail2', (0, 0.3, 0.36), (0, 0.42, 0.48), 'Tail1')
    c.bone('Tail3', (0, 0.42, 0.48), (0, 0.42, 0.66), 'Tail2')
    for s, x in (('L', 1), ('R', -1)):
        # Rolled so that in Unity, a positive turn about X swings the arm back (as the code expects).
        c.bone(f'Arm{s}', (0.245 * x, 0, 0.8), (0.25 * x, 0, 0.74), 'Spine', free=True, roll=180)
        c.bone(f'UpperArm.{s}', (0.25 * x, 0, 0.78), (0.25 * x, 0, 0.52), f'Arm{s}')
        c.bone(f'Forearm.{s}', (0.25 * x, 0, 0.52), (0.25 * x, 0, 0.29), f'UpperArm.{s}')
        c.bone(f'Hand.{s}', (0.25 * x, 0, 0.29), (0.25 * x, 0, 0.19), f'Forearm.{s}')
        c.bone(f'Thigh.{s}', (0.11 * x, 0, 0.42), (0.12 * x, 0, 0.22), 'Hips')
        c.bone(f'Shin.{s}', (0.12 * x, 0, 0.22), (0.12 * x, 0, 0.05), f'Thigh.{s}')
        c.bone(f'Foot.{s}', (0.12 * x, 0, 0.05), (0.12 * x, -0.12, 0.02), f'Shin.{s}')

    # Body
    c.ball('Hips', 'Fur', 0.15, at=(0, 0.02, 0.44), scale=(1.2, 1, 0.8))
    c.ball('Spine', 'Fur', 0.2, at=(0, 0.01, 0.61), scale=(1.1, 0.9, 1.15))
    c.ball('Spine', 'Face', 0.14, at=(0, -0.09, 0.57), scale=(1, 0.6, 1.2))
    # Head
    c.ball('Head', 'Fur', 0.19, at=(0, 0, 0.97), scale=(1, 0.95, 0.95), detail=2)
    c.ball('Head', 'Face', 0.13, at=(0, -0.12, 1.0), scale=(1.05, 0.55, 0.62))
    c.ball('Head', 'Face', 0.1, at=(0, -0.14, 0.905), scale=(1.15, 0.8, 0.72))
    c.mirror(c.ball, 'Head', 'Eye', 0.028, at=(0.058, -0.185, 1.005))
    c.mirror(c.ball, 'Head', 'Eye', 0.011, at=(0.022, -0.218, 0.925))
    c.box('Head', 'Eye', (0.08, 0.01, 0.012), at=(0, -0.205, 0.872))
    c.mirror(c.ball, 'Head', 'Face', 0.068, at=(0.195, 0.005, 0.98), scale=(0.45, 0.85, 1))
    # Arms, legs, tail
    c.mirror(c.ball, 'UpperArm.X', 'Fur', 0.07, at=(0.24, 0, 0.77))
    c.mirror(c.limb, 'UpperArm.X', 'Fur', a=(0.25, 0, 0.79), b=(0.25, 0, 0.52), radius=0.055, radius2=0.048)
    c.mirror(c.limb, 'Forearm.X', 'Fur', a=(0.25, 0, 0.53), b=(0.25, 0, 0.29), radius=0.048, radius2=0.042)
    c.mirror(c.ball, 'Hand.X', 'Face', 0.055, at=(0.25, -0.01, 0.245), scale=(0.9, 0.8, 1.1))
    c.mirror(c.limb, 'Thigh.X', 'Fur', a=(0.11, 0, 0.44), b=(0.12, 0, 0.22), radius=0.068, radius2=0.055)
    c.mirror(c.limb, 'Shin.X', 'Fur', a=(0.12, 0, 0.23), b=(0.12, 0, 0.05), radius=0.05, radius2=0.045)
    c.mirror(c.box, 'Foot.X', 'Face', (0.1, 0.17, 0.05), at=(0.12, -0.05, 0.025), top=(0.9, 0.85), bevel=0.012)
    c.limb('Tail1', 'Fur', (0, 0.1, 0.43), (0, 0.3, 0.36), 0.032, 0.028)
    c.limb('Tail2', 'Fur', (0, 0.3, 0.36), (0, 0.42, 0.48), 0.028, 0.024)
    c.limb('Tail3', 'Fur', (0, 0.42, 0.48), (0, 0.42, 0.66), 0.024, 0.012)

    if kind == 'brown':
        mask_colours(c)
        c.into('BurglarMask')
        c.cyl('Head', 'Mask', 0.2, 0.07, at=(0, 0, 1.005), sides=10)
        c.box('Head', 'Mask', (0.25, 0.06, 0.075), at=(0, -0.185, 1.005), bevel=0.01)
        c.mirror(c.box, 'Head', 'MaskEye', (0.05, 0.012, 0.036), at=(0.058, -0.218, 1.005))
        c.mirror(c.box, 'Head', 'Eye', (0.022, 0.01, 0.026), at=(0.058, -0.225, 1.005))
        c.mirror(c.box, 'Head', 'Mask', (0.03, 0.09, 0.035), at=(0.035, 0.21, 0.99), rot=(25, 0, 20))
        c.into(None)
    elif kind == 'grey':
        c.cyl('Head', 'Red', 0.197, 0.04, at=(0, 0, 1.05), rot=(-6, 0, 0), sides=10)
        c.mirror(c.box, 'Head', 'Red', (0.04, 0.02, 0.14), at=(0.03, 0.21, 0.99), rot=(-20, 0, 20))
    else:
        c.box('Head', 'Fur', (0.22, 0.04, 0.03), at=(0, -0.175, 1.065), rot=(12, 0, 0))  # scowl
        c.box('Spine', 'Red', (0.5, 0.03, 0.62), at=(0, 0.2, 0.55), rot=(12, 0, 0), top=(0.85, 1))  # cape
        c.cyl('Spine', 'Red', 0.23, 0.03, at=(0, 0.015, 0.82), sides=10)
        c.into('Crown', bone='Head')
        c.cyl('Head', 'Gold', 0.13, 0.045, at=(0, 0, 1.13), sides=10)
        for i in range(6):
            a = math.radians(i * 60)
            c.cyl('Head', 'Gold', 0.03, 0.08, at=(math.sin(a) * 0.11, -math.cos(a) * 0.11, 1.19), sides=4, radius2=0.002)
        c.ball('Head', 'Red', 0.022, at=(0, -0.13, 1.135), scale=(1, 0.6, 1))
        c.into(None)

    c.clip('Idle', 60, {
        0: {'Tail1': (0, 0, 0), 'UpperArm.L': (0, -4, 0), 'UpperArm.R': (0, 4, 0)},
        20: {'Spine': (-3, 0, 0), 'Head': (0, 0, 12), 'Tail1': (0, 0, 12), 'Tail3': (0, 6, 0),
             'UpperArm.L': (0, -6, 0), 'UpperArm.R': (0, 6, 0), 'Hips@': (0, 0, 0.006)},
        40: {'Head': (4, 0, -10), 'Tail1': (0, 0, -10), 'Tail3': (0, -6, 0),
             'UpperArm.L': (0, -4, 0), 'UpperArm.R': (0, 4, 0)},
        60: {'Tail1': (0, 0, 0), 'UpperArm.L': (0, -4, 0), 'UpperArm.R': (0, 4, 0)},
    })
    # Walk: 1.6 m/s, rolling from foot to foot.
    c.clip('Walk', 20, cycle(20, {
        0: {'Thigh.L': (-26, 0, 0), 'Shin.L': (5, 0, 0), 'Foot.L': (-8, 0, 0),
            'Thigh.R': (20, 0, 0), 'Shin.R': (16, 0, 0), 'Foot.R': (16, 0, 0),
            'UpperArm.L': (20, -6, 0), 'Forearm.L': (-12, 0, 0), 'UpperArm.R': (-22, 6, 0), 'Forearm.R': (-25, 0, 0),
            'Spine': (5, 5, -6), 'Head': (0, -4, 4), 'Tail1': (0, 0, 10), 'Hips@': (0, 0, -0.02)},
        5: {'Thigh.L': (2, 0, 0), 'Shin.L': (3, 0, 0), 'Thigh.R': (-18, 0, 0), 'Shin.R': (48, 0, 0), 'Foot.R': (5, 0, 0),
            'UpperArm.L': (2, -6, 0), 'Forearm.L': (-15, 0, 0), 'UpperArm.R': (-2, 6, 0), 'Forearm.R': (-15, 0, 0),
            'Spine': (5, 0, 0), 'Tail1': (0, 0, 2), 'Hips@': (0, 0, 0.02)},
    }))
    # Run: 5.5 m/s, bounding, arms flung about.
    c.clip('Run', 14, cycle(14, {
        0: {'Thigh.L': (-48, 0, 0), 'Shin.L': (15, 0, 0), 'Foot.L': (-10, 0, 0),
            'Thigh.R': (40, 0, 0), 'Shin.R': (60, 0, 0), 'Foot.R': (20, 0, 0),
            'UpperArm.L': (50, -12, 0), 'Forearm.L': (-40, 0, 0), 'UpperArm.R': (-70, 12, 0), 'Forearm.R': (-50, 0, 0),
            'Spine': (16, 0, -8), 'Head': (-12, 0, 5), 'Tail1': (-15, 0, 10), 'Hips@': (0, 0, -0.04)},
        3: {'Thigh.L': (10, 0, 0), 'Shin.L': (20, 0, 0), 'Foot.L': (12, 0, 0),
            'Thigh.R': (-60, 0, 0), 'Shin.R': (100, 0, 0),
            'UpperArm.L': (10, -14, 0), 'Forearm.L': (-60, 0, 0), 'UpperArm.R': (-20, 14, 0), 'Forearm.R': (-60, 0, 0),
            'Spine': (16, 0, 0), 'Head': (-12, 0, 0), 'Tail1': (-10, 0, 0), 'Hips@': (0, 0, 0.06)},
    }))
    air = {'Thigh.L': (-50, 0, 0), 'Shin.L': (70, 0, 0), 'Thigh.R': (-20, 0, 0), 'Shin.R': (60, 0, 0),
           'UpperArm.L': (0, -70, 0), 'Forearm.L': (-30, 0, 0), 'UpperArm.R': (0, 70, 0), 'Forearm.R': (-30, 0, 0),
           'Spine': (-4, 0, 0), 'Tail1': (-20, 0, 0), 'Tail2': (-15, 0, 0)}
    c.clip('Air', 10, {0: air, 10: air})
    return c


# ------------------------------------------------------------------------------------------
# The Golden Goose, about 1.25 m (scaled in the level). She never walks: she sits on her
# nest, then rides on the player. Her wings are free for GoldenGoose to flap. Materials
# named Gold come in shiny (CharacterModelImporter).
# ------------------------------------------------------------------------------------------
def goose():
    c = Character('Goose')
    c.colour('Gold', (1.0, 0.78, 0.22))
    c.colour('GoldDeep', (0.86, 0.58, 0.12))
    c.colour('Orange', (0.98, 0.5, 0.1))
    c.colour('Eye', DARK)

    c.bone('Root', (0, 0, 0), (0, 0, 0.15))
    c.bone('Body', (0, 0.1, 0.42), (0, -0.25, 0.5), 'Root')
    c.bone('Neck', (0, -0.3, 0.6), (0, -0.42, 1.04), 'Body')
    c.bone('Head', (0, -0.42, 1.04), (0, -0.64, 1.1), 'Neck')
    c.bone('Tail', (0, 0.38, 0.52), (0, 0.6, 0.6), 'Body')
    for s, x in (('L', 1), ('R', -1)):
        c.bone(f'Wing{s}', (0.24 * x, -0.15, 0.62), (0.27 * x, 0.35, 0.58), 'Body', free=True)

    # Body
    c.ball('Body', 'Gold', 0.4, at=(0, 0.05, 0.45), scale=(0.75, 1.25, 0.7), detail=2)
    c.ball('Body', 'Gold', 0.25, at=(0, -0.26, 0.5), scale=(1, 1, 1.05))  # breast
    c.ball('Tail', 'GoldDeep', 0.17, at=(0, 0.5, 0.6), scale=(0.75, 1, 0.4), rot=(-18, 0, 0))
    # A long neck and a small head
    c.limb('Neck', 'Gold', (0, -0.27, 0.6), (0, -0.43, 1.07), 0.085, 0.062)
    c.ball('Head', 'Gold', 0.125, at=(0, -0.48, 1.11), scale=(0.85, 1.25, 0.9))
    c.limb('Head', 'Orange', (0, -0.57, 1.095), (0, -0.73, 1.07), 0.05, 0.02, sides=4)
    c.mirror(c.ball, 'Head', 'Eye', 0.022, at=(0.07, -0.53, 1.14))
    # Wings, folded along her sides with the tips lying over the tail
    c.mirror(c.ball, 'WingX', 'GoldDeep', 0.32, at=(0.28, 0.08, 0.53), scale=(0.25, 1.05, 0.55), rot=(-8, 0, 0))
    c.mirror(c.ball, 'WingX', 'GoldDeep', 0.2, at=(0.2, 0.42, 0.6), scale=(0.3, 1, 0.3), rot=(-10, 0, -12))
    # Legs and webbed feet
    c.mirror(c.limb, 'Root', 'Orange', a=(0.11, 0, 0.24), b=(0.11, -0.02, 0.03), radius=0.025)
    c.mirror(c.box, 'Root', 'Orange', (0.13, 0.17, 0.025), at=(0.11, -0.07, 0.012), top=(0.7, 0.6), bevel=0.005)

    # Looks about, pecks, and looks about again.
    c.clip('Idle', 90, {
        0: {},
        18: {'Neck': (-6, 0, 12), 'Head': (4, 0, 18), 'Tail': (0, 0, 8)},
        34: {'Neck': (-10, 0, 2), 'Head': (-12, 0, 0), 'Body@': (0, 0, 0.01)},
        52: {'Neck': (-4, 0, -12), 'Head': (2, 0, -20), 'Tail': (0, 0, -8)},
        66: {'Neck': (14, 0, -4), 'Head': (18, 0, 0)},
        72: {'Neck': (6, 0, -2), 'Head': (8, 0, 0)},
        78: {'Neck': (14, 0, -2), 'Head': (18, 0, 0)},
        90: {},
    })
    return c


# ------------------------------------------------------------------------------------------
CHARACTERS = {
    'Human': lambda: human('Human', keeper=False),
    'Keeper': lambda: human('Keeper', keeper=True),
    'Rat': rat,
    'Penguin': penguin,
    'Wolf': wolf,
    'GuardDog': guard_dog,
    'Monkey': lambda: monkey('Monkey', 'brown'),
    'GreyMonkey': lambda: monkey('GreyMonkey', 'grey'),
    'KingMonkey': lambda: monkey('KingMonkey', 'king'),
    'Goose': goose,
}

# Clip and how far through it, for the preview sheets.
PREVIEW_FRAMES = [('Idle', 0), ('Walk', 0), ('Walk', 0.25), ('Walk', 0.5),
                  ('Trot', 0), ('Trot', 0.25), ('Trot', 0.5), ('Trot', 0.75),
                  ('Run', 0), ('Run', 0.25), ('Run', 0.5), ('Run', 0.75)]


def main(argv):
    preview = None
    if '--preview' in argv:
        i = argv.index('--preview')
        preview = argv[i + 1]
        argv = argv[:i] + argv[i + 2:]
    names = list(CHARACTERS) if not argv or argv == ['all'] else argv
    for name in names:
        lowpoly.reset()
        c = CHARACTERS[name]()
        c.build()
        path = os.path.join(OUT, name + '.fbx')
        c.export(path)
        print('EXPORTED', path)
        if preview:
            lengths = {cl[0]: cl[1] for cl in c.clips}
            frames = [(n, round(t * lengths[n])) for n, t in PREVIEW_FRAMES if n in lengths]
            c.preview(os.path.join(preview, name + '.png'), frames)
            print('PREVIEW', os.path.join(preview, name + '.png'))
            head = next((b for b in c.bones if b[0] == 'Head'), None)
            if head:
                width = (head[2] - head[1]).length * 2.4
                for view, suffix in (((35, 10), '_Head'), ((0, 5), '_Face')):
                    c.preview(os.path.join(preview, name + suffix + '.png'), [('Idle', 0)], view=view, focus=('Head', width))


main(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
