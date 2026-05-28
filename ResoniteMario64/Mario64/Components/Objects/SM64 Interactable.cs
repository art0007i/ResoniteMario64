using FrooxEngine;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using ResoniteMario64.Mario64.Components.Context;
using ResoniteMario64.Mario64.Components.Interfaces;
using ResoniteMario64.Mario64.libsm64;
using static ResoniteMario64.Mario64.libsm64.SM64Constants;

namespace ResoniteMario64.Mario64.Components.Objects;

public sealed class SM64Interactable : ISM64Object
{
    public readonly SM64InteractableType Type;

    public readonly int TypeId;

    private readonly int _delete;

    private bool _runningSync;

    public bool Delete => _delete == 1;
    public bool HasValue => TypeId != -1;

    public World World { get; private set; }
    public SM64Context Context { get; private set; }
    public Collider Collider { get; private set; }
    public string OriginalTag { get; }

    public bool IsDisposed { get; private set; }

    public SM64Interactable(Collider col, SM64Context instance)
    {
        if (col is MeshCollider mc && (mc.Mesh.Target == null || !mc.Mesh.IsAssetAvailable))
        {
            if (Config.DebugEnabled.Value) Logger.Warn($"[Interactable{mc.GetType()}] {mc.Slot.Name} ({mc.ReferenceID}) Mesh is {(mc.Mesh.Target == null ? "null" : "non-readable")}");
            Dispose();
            return;
        }

        World = col.World;
        Context = instance;
        Collider = col;
        OriginalTag = col.Slot.Tag;

        string[] tagParts = col.Slot.Tag?.Split(',');
        Utils.ParseTagParts(tagParts, out _, out _, out Type, out TypeId, out _delete);
    }

    public void Handle(SM64Mario mario)
    {
        if (_runningSync || Collider?.Slot is not { IsActive: true }) return;

        if (!mario.IsInCollider(this)) return;

        bool disable = true;
        switch (Type)
        {
            case SM64InteractableType.GoldCoin:
                Interop.PlaySoundGlobal(Sounds.SOUND_GENERAL_COIN);
                mario.SyncedCoinCounter++;
                mario.Heal(1);
                break;
            case SM64InteractableType.BlueCoin:
                Interop.PlaySoundGlobal(Sounds.SOUND_GENERAL_COIN);
                mario.SyncedCoinCounter += 5;
                mario.Heal(5);
                break;
            case SM64InteractableType.RedCoin:
                int currentRedIndex = mario.SyncedRedCoinCounter;

                Sounds redSound = Utils.GetRedCoinSound(TypeId == -1 ? currentRedIndex : TypeId);

                Interop.PlaySoundGlobal(redSound);

                mario.SyncedRedCoinCounter = currentRedIndex == 7 ? 0 : currentRedIndex + 1;
                mario.SyncedCoinCounter += 2;
                mario.Heal(2);
                break;
            case SM64InteractableType.VanishCap:
                mario.WearCap(MarioCapType.VanishCap);
                break;
            case SM64InteractableType.MetalCap:
                mario.WearCap(MarioCapType.MetalCap);
                break;
            case SM64InteractableType.WingCap:
                mario.WearCap(MarioCapType.WingCap, 40f);
                break;
            case SM64InteractableType.NormalCap:
                mario.WearCap(MarioCapType.NormalCap);
                break;
            case SM64InteractableType.Star:
                Interop.PlaySoundGlobal(Sounds.Menu_StarSound);
                mario.SyncedStarCounter++;
                mario.Heal(8);
                mario.SetForwardVelocity(0f);
                mario.SetAction(ActionFlag.Freefall);
                break;
            case SM64InteractableType.OneUp:
                Interop.PlaySoundGlobal(Sounds.General_Collect1Up);
                mario.SyncedLives++;
                break;
            case SM64InteractableType.Damage:
                bool isLocalMarioCollider = Collider.Slot.IsChildOf(mario.MarioSlot);
                if (!isLocalMarioCollider)
                {
                    uint damage = TypeId == -1 || TypeId >= 8 ? 7 : (uint)TypeId;

                    mario.TakeDamage(Collider.Slot.GlobalPosition, damage);
                }

                disable = false;
                break;
            case SM64InteractableType.None:
                disable = false;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        Collider.Slot.RunSynchronously(() =>
        {
            _runningSync = true;

            DynamicImpulseHelper.Singleton.TriggerDynamicImpulseWithArgument(this.Collider.Slot.GetObjectRoot(), "MarioCollided", true, mario.MarioSlot);

            if (Delete) Collider.Slot.Destroy();

            Collider.Slot.ActiveSelf = !disable;

            _runningSync = false;
        });
    }

    ~SM64Interactable()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (IsDisposed) return;

        if (disposing)
        {
            Context.UnregisterInteractable(Collider);

            World = null;
            Context = null;
            Collider = null;
        }

        IsDisposed = true;
    }
}