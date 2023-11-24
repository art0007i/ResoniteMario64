using FrooxEngine;
using System.Collections.Generic;

namespace ResoniteMario64;

[Category("SM64")]
public class SM64LevelModifier : Component {

    public enum ModifierType {
        Water,
        Gas,
    }

    private readonly SyncRefList<Animator> animators;
    private readonly Sync<ModifierType> modifierType;


    protected override void OnAttach()
    {
        base.OnAttach();
        modifierType.Value = ModifierType.Water;
    }

    // nonserial startt
    private static float _lastLevel = float.MinValue;
    private static ModifierType _lastModifierType = ModifierType.Water;
    private static readonly List<SM64LevelModifier> LevelModifierObjects = new();

    private static bool _forceUpdate = false;
    // nonserial end

    private enum LocalParameterNames {
        IsActive,
        HasMod,
    }
    /*
     * TODO: Fix the code instead of just commenting it dummy.
    private static readonly Dictionary<LocalParameterNames, int> LocalParameters = new() {
        { LocalParameterNames.IsActive, Animator.StringToHash(nameof(LocalParameterNames.IsActive)) },
        { LocalParameterNames.HasMod, Animator.StringToHash(nameof(LocalParameterNames.HasMod)) },
    };

    protected override void OnStart() {

        // Check the animators
        var toNuke = new HashSet<Animator>();
        foreach (var animator in animators) {
            if (animator == null || animator.runtimeAnimatorController == null) {
                toNuke.Add(animator);
            }
            else {
                animator.SetBool(LocalParameters[LocalParameterNames.HasMod], true);
            }
        }
        foreach (var animatorToNuke in toNuke) animators.Remove(animatorToNuke);
        if (toNuke.Count > 0) {
            var animatorsToNukeStr = toNuke.Select(animToNuke => animToNuke.Slot.Name);
            ResoniteMario64.Warn($"[{nameof(CVRSM64LevelModifier)}] Removing animators: {string.Join(", ", animatorsToNukeStr)} because they were null or had no controllers slotted.");
        }
    }

    public static void MarkForUpdate() {
        _forceUpdate = true;
    }

    public static void ContextTick(List<CVRSM64Mario> marios) {

        if (LevelModifierObjects.Count == 0) {
            if (MathX.Approximately(_lastLevel, float.MinValue)) return;
            lock (marios) {
                foreach (var mario in marios) {
                    Interop.SetLevelModifier(mario.MarioId, _lastModifierType, float.MinValue);
                }
            }
            _lastLevel = float.MinValue;
            return;
        }

        // Get the highest level
        var maxLevelModifier = LevelModifierObjects[0];
        foreach (var levelModifier in LevelModifierObjects) {
            if (levelModifier.Slot.GlobalPosition.y > maxLevelModifier.Slot.GlobalPosition.y) {
                maxLevelModifier = levelModifier;
            }
        }
        var highestLevel = maxLevelModifier.Slot.GlobalPosition.y;

        // Highest level hasn't changed, lets ignore (unless we're forcing the update)
        if (maxLevelModifier.modifierType == _lastModifierType && MathX.Approximately(highestLevel, _lastLevel) && !_forceUpdate) {
            return;
        }

        // Reset the old type
        if (maxLevelModifier.modifierType != _lastModifierType) {
            lock (marios) {
                foreach (var mario in marios) {
                    Interop.SetLevelModifier(mario.MarioId, _lastModifierType, float.MinValue);
                }
            }
        }

        _lastModifierType = maxLevelModifier.modifierType;
        _lastLevel = highestLevel;
        _forceUpdate = false;

        lock (marios) {
            foreach (var mario in marios) {
                Interop.SetLevelModifier(mario.MarioId, _lastModifierType, highestLevel);
            }
        }

        // Update animators
        foreach (var levelModifier in LevelModifierObjects) {
            foreach (var animator in levelModifier.animators) {
                animator.SetBool(LocalParameters[LocalParameterNames.IsActive], levelModifier == maxLevelModifier);
            }
        }
    }

    protected override void OnEnabled() {
        if (LevelModifierObjects.Contains(this)) return;
        LevelModifierObjects.Add(this);
        #if DEBUG
        ResoniteMario64.Msg($"[{nameof(CVRSM64LevelModifier)}] {Slot.Name} Enabled! Type: {modifierType.ToString()}");
        #endif
    }

    protected override void OnDisabled() {
        if (!LevelModifierObjects.Contains(this)) return;
        LevelModifierObjects.Remove(this);
        #if DEBUG
        ResoniteMario64.Msg($"[{nameof(CVRSM64LevelModifier)}] {Slot.Name} Disabled!");
        #endif
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        OnDisabled();
    }*/
}
