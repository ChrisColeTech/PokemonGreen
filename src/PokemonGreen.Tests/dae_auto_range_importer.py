bl_info = {
    "name": "DAE Import + Auto Frame Range + Focus 3D View",
    "author": "ChatGPT",
    "version": (1, 1, 0),
    "blender": (3, 0, 0),
    "location": "File > Import > Collada (.dae) (Auto Range + Focus 3D)",
    "description": "Import Collada (.dae), set scene start/end to imported keyframes, then focus 3D Viewport.",
    "category": "Import-Export",
}

import bpy
from bpy_extras.io_utils import ImportHelper
from bpy.types import Operator
from pathlib import Path

_addon_keymaps = []


def _action_key_range(action):
    if not action or not action.fcurves:
        return None
    xs = []
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            xs.append(kp.co.x)
    return (min(xs), max(xs)) if xs else None


def _focus_3d_view(context, view_all=True, switch_workspace_to_layout=True):
    """
    Try to ensure the user ends up looking at a 3D Viewport after import.
    - If a VIEW_3D area exists, we focus it and optionally frame everything.
    - If none exists, we convert the current area into a VIEW_3D (best effort).
    """
    win = context.window
    screen = win.screen

    # Optionally switch to the "Layout" workspace if it exists (common default)
    if switch_workspace_to_layout:
        try:
            for ws in bpy.data.workspaces:
                if ws.name == "Layout":
                    win.workspace = ws
                    screen = win.screen  # refresh
                    break
        except Exception:
            pass

    area_3d = None
    for area in screen.areas:
        if area.type == "VIEW_3D":
            area_3d = area
            break

    # If no 3D viewport exists, convert the current area (best effort)
    if area_3d is None:
        if context.area is not None:
            try:
                context.area.type = "VIEW_3D"
                area_3d = context.area
            except Exception:
                return

    # Pick a WINDOW region inside the 3D View
    region_win = None
    for r in area_3d.regions:
        if r.type == "WINDOW":
            region_win = r
            break

    if region_win is None:
        return

    # Force Object Mode (optional nicety) and frame the view
    try:
        with context.temp_override(window=win, area=area_3d, region=region_win):
            if bpy.ops.object.mode_set.poll():
                bpy.ops.object.mode_set(mode="OBJECT")
            if view_all and bpy.ops.view3d.view_all.poll():
                bpy.ops.view3d.view_all(center=False)
    except Exception:
        # Even if framing fails, the import + range set is still fine
        pass


class IMPORT_OT_dae_auto_range_focus(Operator, ImportHelper):
    bl_idname = "import_scene.dae_auto_range_focus"
    bl_label = "Collada (.dae) (Auto Range + Focus 3D)"
    bl_options = {"REGISTER", "UNDO"}

    filename_ext = ".dae"
    filter_glob: bpy.props.StringProperty(default="*.dae", options={"HIDDEN"})

    set_preview_range: bpy.props.BoolProperty(
        name="Set Preview Range",
        description="Also set the timeline Preview Range to match the detected animation range",
        default=True,
    )

    focus_3d_after: bpy.props.BoolProperty(
        name="Focus 3D Viewport After Import",
        default=True,
    )

    view_all_after: bpy.props.BoolProperty(
        name="Frame All After Import",
        description="After switching to 3D Viewport, frame the scene (View All)",
        default=True,
    )

    switch_workspace_to_layout: bpy.props.BoolProperty(
        name="Switch Workspace to Layout",
        description="If a workspace named 'Layout' exists, switch to it before focusing the 3D viewport",
        default=True,
    )

    def execute(self, context):
        scene = context.scene

        # Snapshot BEFORE import
        actions_before = set(bpy.data.actions)
        objects_before = set(bpy.data.objects)

        # Import DAE
        bpy.ops.wm.collada_import(filepath=self.filepath)

        # Determine what was created
        new_actions = list(set(bpy.data.actions) - actions_before)
        imported_objects = list(set(bpy.data.objects) - objects_before)

        fmin = None
        fmax = None

        # Primary: scan *new actions* created by this import (most reliable for Collada)
        for act in new_actions:
            r = _action_key_range(act)
            if r:
                a, b = r
                fmin = a if fmin is None else min(fmin, a)
                fmax = b if fmax is None else max(fmax, b)

        # Fallback: scan assigned actions/NLA on imported objects (in case actions weren't "new")
        if fmin is None:
            def scan_id(id_block):
                nonlocal fmin, fmax
                ad = getattr(id_block, "animation_data", None)
                if not ad:
                    return

                if ad.action:
                    r = _action_key_range(ad.action)
                    if r:
                        a, b = r
                        fmin = a if fmin is None else min(fmin, a)
                        fmax = b if fmax is None else max(fmax, b)

                for tr in ad.nla_tracks or []:
                    for st in tr.strips:
                        act = getattr(st, "action", None)
                        r = _action_key_range(act) if act else None
                        if r:
                            a, b = r
                            fmin = a if fmin is None else min(fmin, a)
                            fmax = b if fmax is None else max(fmax, b)

            for obj in imported_objects:
                scan_id(obj)
                if getattr(obj, "data", None):
                    scan_id(obj.data)
                    sk = getattr(obj.data, "shape_keys", None)
                    if sk:
                        scan_id(sk)

        if fmin is None or fmax is None:
            self.report({"WARNING"}, "Imported DAE, but no keyframes detected; frame range unchanged.")
        else:
            start = int(round(fmin))
            end = int(round(fmax))
            scene.frame_start = start
            scene.frame_end = end

            if self.set_preview_range:
                scene.use_preview_range = True
                scene.frame_preview_start = start
                scene.frame_preview_end = end

            self.report({"INFO"}, f"Imported {Path(self.filepath).name}; set range {start}–{end}")

        if self.focus_3d_after:
            _focus_3d_view(
                context,
                view_all=self.view_all_after,
                switch_workspace_to_layout=self.switch_workspace_to_layout,
            )

        return {"FINISHED"}


def menu_func_import(self, context):
    self.layout.operator(
        IMPORT_OT_dae_auto_range_focus.bl_idname,
        text="Collada (.dae) (Auto Range + Focus 3D)",
    )


def register_keymap():
    # Register Ctrl+Shift+I by default (you can change/remove later in Preferences)
    wm = bpy.context.window_manager
    kc = wm.keyconfigs.addon
    if not kc:
        return

    km = kc.keymaps.new(name="Window", space_type="EMPTY")
    kmi = km.keymap_items.new(
        IMPORT_OT_dae_auto_range_focus.bl_idname,
        type="I",
        value="PRESS",
        ctrl=True,
        shift=True,
    )
    _addon_keymaps.append((km, kmi))


def unregister_keymap():
    for km, kmi in _addon_keymaps:
        try:
            km.keymap_items.remove(kmi)
        except Exception:
            pass
    _addon_keymaps.clear()


def register():
    bpy.utils.register_class(IMPORT_OT_dae_auto_range_focus)
    bpy.types.TOPBAR_MT_file_import.append(menu_func_import)
    register_keymap()


def unregister():
    unregister_keymap()
    bpy.types.TOPBAR_MT_file_import.remove(menu_func_import)
    bpy.utils.unregister_class(IMPORT_OT_dae_auto_range_focus)