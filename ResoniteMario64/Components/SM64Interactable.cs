using Elements.Core;
using FrooxEngine;
using System.Collections.Generic;

namespace ResoniteMario64;

[Category("SM64")]
public class SM64Interactable : Component {

    private enum InteractableType {
        VanishCap,
        MetalCap,
        WingCap,
    }

    private readonly Sync<InteractableType> interactableType;

    protected override void OnAttach()
    {
        base.OnAttach();
        interactableType.Value = InteractableType.MetalCap;
    }

    private static readonly List<SM64Interactable> InteractableObjects = new();

    public static void HandleInteractables(SM64Mario mario, uint currentStateFlags) {

        // Trigger Caps if is close enough to the proper interactable (if it's already triggered will be ignored)
        foreach (var interactable in InteractableObjects) {
            if (MathX.Distance(interactable.Slot.GlobalPosition, mario.Slot.GlobalPosition) > 0.1) continue;
            if (interactable.interactableType == InteractableType.VanishCap) {
                mario.WearCap(currentStateFlags, Utils.MarioCapType.VanishCap, true);
            }
            if (interactable.interactableType == InteractableType.MetalCap) {
                mario.WearCap(currentStateFlags, Utils.MarioCapType.MetalCap, true);
            }
            if (interactable.interactableType == InteractableType.WingCap) {
                mario.WearCap(currentStateFlags, Utils.MarioCapType.WingCap, true);
            }
        }
    }

    protected override void OnStart()
    {
        base.OnStart();
        OnEnabled();
    }

    protected override void OnEnabled() {
        if (InteractableObjects.Contains(this)) return;
        InteractableObjects.Add(this);
        #if DEBUG
        ResoniteMario64.Msg($"[{nameof(SM64Interactable)}] {Slot.Name} Enabled! Type: {interactableType.ToString()}");
        #endif
    }

    protected override void OnDisabled() {
        if (!InteractableObjects.Contains(this)) return;
        InteractableObjects.Remove(this);
        #if DEBUG
        ResoniteMario64.Msg($"[{nameof(SM64Interactable)}] {Slot.Name} Disabled!");
        #endif
    }

    protected override void OnDestroy() {
        OnDisabled();
    }
}
