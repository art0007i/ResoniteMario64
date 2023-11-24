/*
 * TODO: implement non-essential feature
using FrooxEngine;

namespace ResoniteMario64;

public class CVRSM64Teleporter : Component {

    [SerializeField] private bool isActive = false;
    [SerializeField] private bool isTwoWays = false;

    [SerializeField] private Transform sourcePoint = null;
    [SerializeField] private Transform targetPoint = null;

    [NonSerialized] private const float TriggerDistance = 0.05f;
    [NonSerialized] internal const float TeleportDuration = 1f;
    [NonSerialized] private const float TeleportCooldown = 5f + TeleportDuration;
    [NonSerialized] private static readonly HashSet<CVRSM64Teleporter> TeleporterObjects = new();

    protected override void OnAwake() {
        if (sourcePoint != null && targetPoint != null) return;
        ResoniteMario64.Warn($"[{nameof(CVRSM64Teleporter)}] Attempted to load a teleporter with source or target not defined! Destroying it...");
        Destroy();
    }

    public static void HandleTeleporters(CVRSM64Mario mario, uint flags, ref float startedTeleporting, ref Transform target) {
        var isTeleporting = Utils.IsTeleporting(flags);

        if (isTeleporting) {
            if (Time.time <= startedTeleporting + TeleportDuration || target == null) return;
            mario.SetPosition(target.position);
            mario.SetRotation(target.rotation);
            mario.TeleportEnd();
            #if DEBUG
            ResoniteMario64.Msg($"[{nameof(CVRSM64Teleporter)}] Finishing Teleporting a Mario to {target.position.ToString("F2")}!");
            #endif
            target = null;
        }
        else {

            // Wait for teleport cooldown
            if (Time.time < startedTeleporting + TeleportCooldown) return;

            var marioPos = mario.transform.position;

            foreach (var teleporter in TeleporterObjects) {
                if (!teleporter.isActive) continue;

                var triggeringSource = float3.Distance(teleporter.sourcePoint.position, marioPos) <= TriggerDistance;
                var triggeringTarget = teleporter.isTwoWays && float3.Distance(teleporter.targetPoint.position, marioPos) <= TriggerDistance;

                // If not triggering this teleporter, continue
                if (!triggeringSource && !triggeringTarget) continue;

                mario.TeleportStart();
                target = triggeringSource ? teleporter.targetPoint : teleporter.sourcePoint;
                #if DEBUG
                ResoniteMario64.Msg($"[{nameof(CVRSM64Teleporter)}][{teleporter.name}] Starting Teleporting a Mario to {target.position.ToString("F2")}!");
                #endif
                startedTeleporting = Time.time;
                break;
            }
        }
    }

    protected override void OnEnabled() {
        base.OnEnabled();
        TeleporterObjects.Add(this);
        #if DEBUG
        ResoniteMario64.Msg($"[{nameof(CVRSM64Teleporter)}] {gameObject.name} Enabled!");
        #endif
    }

    protected override void OnDisabled() {
        base.OnDisabled();
        TeleporterObjects.Remove(this);
        #if DEBUG
        ResoniteMario64.Msg($"[{nameof(CVRSM64Teleporter)}] {gameObject.name} Disabled!");
        #endif
    }

    protected override void OnDestroy() {
        base.OnDestroy();
        OnDisabled();
    }
}
*/