/*
 * TODO: maybe impl this with connector or sth. cuz it uses unity particals
using FrooxEngine;
using System.Collections.Generic;

namespace ResoniteMario64;

public class CVRSM64InteractableParticles : Component {

    public enum ParticleType {
        GoldCoin,
        BlueCoin,
        RedCoin,
    }

    private Sync<ParticleType> particleType;

    private const string MarioParticleTargetName = "[CVRSM64InteractableParticlesTarget]";

    // Internal (NonSerialized)
    private ParticleSystem _particleSystem;
    private readonly List<ParticleCollisionEvent> _collisionEvents = new();

    protected override void OnAttach()
    {
        base.OnAttach();
        particleType.Value = ParticleType.GoldCoin;
    }

    private void Start() {
        _particleSystem = GetComponent<ParticleSystem>();

        if (_particleSystem == null) {
            ResoniteMario64.Error($"[{nameof(CVRSM64InteractableParticles)}] This component requires to be next to a particle system!");
            Destroy();
            return;
        }
    }

    private void OnParticleCollision(GameObject other) {
        if (other.name != MarioParticleTargetName) return;
        var marioTarget = other.GetComponentInParent<CVRSM64Mario>();
        if (marioTarget == null || !marioTarget.IsMine()) return;

        var numCollisionEvents = _particleSystem.GetCollisionEvents(other, _collisionEvents);
        for (var i = 0; i < numCollisionEvents; i++) {
            marioTarget.PickupCoin(particleType);
        }
    }
}
*/