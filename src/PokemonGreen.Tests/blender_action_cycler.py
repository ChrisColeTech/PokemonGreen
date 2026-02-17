"""
Blender Action Cycler

Usage:
1) Open Blender -> Scripting.
2) Load this file and Run Script.
3) Select your armature object.
4) In 3D View press:
   - Ctrl + Shift + Right Arrow: next action
   - Ctrl + Shift + Left Arrow: previous action
"""

import bpy

_registered_keymaps = []


def _ensure_anim_data(obj):
    if obj.animation_data is None:
        obj.animation_data_create()


def _all_actions():
    return sorted(list(bpy.data.actions), key=lambda a: a.name.lower())


def _active_action_index(actions, current):
    if not actions:
        return -1
    if current is None:
        return -1
    for i, act in enumerate(actions):
        if act == current:
            return i
    return -1


class ACTIONCYCLER_OT_next(bpy.types.Operator):
    bl_idname = "actioncycler.next_action"
    bl_label = "Next Action"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        obj = context.object
        if obj is None:
            self.report({"WARNING"}, "No active object selected.")
            return {"CANCELLED"}

        actions = _all_actions()
        if not actions:
            self.report({"WARNING"}, "No actions found in this file.")
            return {"CANCELLED"}

        _ensure_anim_data(obj)
        current = obj.animation_data.action
        idx = _active_action_index(actions, current)
        next_idx = 0 if idx < 0 else (idx + 1) % len(actions)

        obj.animation_data.action = actions[next_idx]
        context.scene.frame_set(int(context.scene.frame_start))
        self.report({"INFO"}, f"Action: {actions[next_idx].name} ({next_idx + 1}/{len(actions)})")
        return {"FINISHED"}


class ACTIONCYCLER_OT_prev(bpy.types.Operator):
    bl_idname = "actioncycler.prev_action"
    bl_label = "Previous Action"
    bl_options = {"REGISTER", "UNDO"}

    def execute(self, context):
        obj = context.object
        if obj is None:
            self.report({"WARNING"}, "No active object selected.")
            return {"CANCELLED"}

        actions = _all_actions()
        if not actions:
            self.report({"WARNING"}, "No actions found in this file.")
            return {"CANCELLED"}

        _ensure_anim_data(obj)
        current = obj.animation_data.action
        idx = _active_action_index(actions, current)
        prev_idx = len(actions) - 1 if idx < 0 else (idx - 1) % len(actions)

        obj.animation_data.action = actions[prev_idx]
        context.scene.frame_set(int(context.scene.frame_start))
        self.report({"INFO"}, f"Action: {actions[prev_idx].name} ({prev_idx + 1}/{len(actions)})")
        return {"FINISHED"}


def register_keymaps():
    wm = bpy.context.window_manager
    kc = wm.keyconfigs.addon
    if kc is None:
        return

    km = kc.keymaps.new(name="3D View", space_type="VIEW_3D")
    kmi_next = km.keymap_items.new(ACTIONCYCLER_OT_next.bl_idname, "RIGHT_ARROW", "PRESS", ctrl=True, shift=True)
    kmi_prev = km.keymap_items.new(ACTIONCYCLER_OT_prev.bl_idname, "LEFT_ARROW", "PRESS", ctrl=True, shift=True)

    _registered_keymaps.append((km, kmi_next))
    _registered_keymaps.append((km, kmi_prev))


def unregister_keymaps():
    for km, kmi in _registered_keymaps:
        try:
            km.keymap_items.remove(kmi)
        except Exception:
            pass
    _registered_keymaps.clear()


classes = (
    ACTIONCYCLER_OT_next,
    ACTIONCYCLER_OT_prev,
)


def register():
    for c in classes:
        bpy.utils.register_class(c)
    register_keymaps()
    print("Action Cycler registered. Ctrl+Shift+Right/Left to cycle actions.")


def unregister():
    unregister_keymaps()
    for c in reversed(classes):
        bpy.utils.unregister_class(c)
    print("Action Cycler unregistered.")


# Safe re-register when script is run multiple times.
try:
    unregister()
except Exception:
    pass

register()
