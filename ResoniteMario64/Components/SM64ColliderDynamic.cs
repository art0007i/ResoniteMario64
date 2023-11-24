using Elements.Core;
using FrooxEngine;
using System.Collections.Generic;
using System.Security.Principal;

namespace ResoniteMario64;

[Category("SM64")]
[DefaultUpdateOrder(888888)]
public class SM64ColliderDynamic : Component {

    private readonly Sync<SM64TerrainType> terrainType;
    private readonly Sync<SM64SurfaceType> surfaceType;

    // private Sync<bool> ignoreForSpawner;

    protected override void OnAttach()
    {
        base.OnAttach();
        terrainType.Value = SM64TerrainType.Grass;
        surfaceType.Value = SM64SurfaceType.Default;
        //ignoreForSpawner.Value = true;
    }

    private uint _surfaceObjectId;

    // Threading
    private readonly object _lock = new();

    private float3 LastPosition { get; set; }
    private floatQ LastRotation { get; set; }

    private bool HasChanges { get; set; }

    private bool _enabled; // nonserialized
    private bool _started; // nonserialized

    protected override void OnStart() 
    {
        base.OnStart();
        _started = true;
        OnEnabled();
    }

    protected override void OnEnabled()
    {
        base.OnEnabled();
        _enabled = true;
        Initialize();
    }

    private void Initialize() {

        // Only initialize when both Start and OnEnable ran
        if (!_started || !_enabled) return;

        // Check if the collider is inside of a mario we control, and ignore if that's the case
        // if (ignoreForSpawner) {
        //     var parentMario = GetComponentInParent<CVRSM64Mario>();
        //     if (parentMario != null && parentMario.IsMine()) {
        //         ResoniteMario64.Msg($"[{nameof(CVRSM64ColliderDynamic)}] Ignoring collider {gameObject.name} because it's on our own mario!");
        //         Destroy(this);
        //         return;
        //     }
        // }

        if (!SM64Context.RegisterSurfaceObject(this))
        {
            return;
        }

        LastPosition = Slot.GlobalPosition;
        LastRotation = Slot.GlobalRotation;

        var col = Slot.GetComponent<Collider>();

        var lst = Pool.BorrowList<Interop.SM64Surface>(); 

        var surfaces = Utils.GetScaledSurfaces(col, lst, surfaceType, terrainType, true);

        _surfaceObjectId = Interop.SurfaceObjectCreate(Slot.GlobalPosition, Slot.GlobalRotation, surfaces.ToArray());

        #if DEBUG
        ResoniteMario64.Msg($"[CVRSM64ColliderDynamic] [{_surfaceObjectId}] {Slot.Name} Enabled! Surface Count: {surfaces.Count}");
        #endif
        
        Pool.Return(ref lst);
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        OnDisabled();
    }

    protected override void OnDisabled() {

        if (!_started || !_enabled) return;

        _enabled = false;

        if (Interop.isGlobalInit) {
            SM64Context.UnregisterSurfaceObject(this);
            Interop.SurfaceObjectDelete(_surfaceObjectId);
        }

        #if DEBUG
        ResoniteMario64.Msg($"[CVRSM64ColliderDynamic] [{_surfaceObjectId}] {Slot.Name} Disabled!");
        #endif
    }

    internal void UpdateCurrentPositionData() {
        lock (_lock) {
            if (Slot.GlobalPosition != LastPosition || Slot.GlobalRotation != LastRotation) {
                LastPosition = Slot.GlobalPosition;
                LastRotation = Slot.GlobalRotation;
                HasChanges = true;
            }
        }
    }

    internal void ConsumeCurrentPosition() {
        lock (_lock) {
            if (HasChanges) {
                Interop.SurfaceObjectMove(_surfaceObjectId, LastPosition, LastRotation);
                HasChanges = false;
            }
        }
    }

    internal void ContextFixedUpdateSynced() {
        if (Slot.GlobalPosition != LastPosition || Slot.GlobalRotation != LastRotation) {
            LastPosition = Slot.GlobalPosition;
            LastRotation = Slot.GlobalRotation;

            Interop.SurfaceObjectMove(_surfaceObjectId, Slot.GlobalPosition, Slot.GlobalRotation);
        }
    }
}
