using Elements.Assets;
using Elements.Core;
using FrooxEngine;

namespace ResoniteMario64;

public static class ColliderShapeExtensions
{
    public static MeshX GetColliderMesh(this Collider c)
    {
        var meshx = new MeshX();
        switch (c)
        {
            case TriangleCollider col:
                var triSubMesh = meshx.AddSubmesh<TriangleSubmesh>();
                meshx.AddVertex(col.A + col.Offset.Value);
                meshx.AddVertex(col.B + col.Offset.Value);
                meshx.AddVertex(col.C + col.Offset.Value);
                triSubMesh.AddTriangle(0, 1, 2);
                return meshx;
            case CapsuleCollider col:
                //Utils.GetPrimitiveMesh(Utils.PrimitiveType.Capsule);
                var uvcapsule = new UVSphereCapsule(meshx, 8, 16, UVSphereCapsule.Shading.Flat, capsule: true);
                uvcapsule.Radius = col.Radius;
                uvcapsule.Height = col.Height;
                uvcapsule.Update();
                break;
            case ConeCollider col:
                var cone = new ConicalFrustum(meshx, 8, true, true);
                cone.Radius = col.Radius;
                cone.RadiusTop = 0;
                cone.Height = col.Height;
                cone.Update();
                break;
            case CylinderCollider col:
                var cylinder = new ConicalFrustum(meshx, 8, true, true);
                cylinder.Radius = col.Radius;
                cylinder.Height = col.Height;
                cylinder.Update();
                break;
            case BoxCollider col:
                var box = new Box(meshx);
                box.Size = col.Size;
                box.Update();
                break;
            case SphereCollider col:
                var uvsphere = new UVSphereCapsule(meshx, 8, 16, UVSphereCapsule.Shading.Flat);
                uvsphere.Radius = col.Radius;
                uvsphere.Update();
                break;
            case MeshCollider col:
                if (col.Mesh.IsAssetAvailable)
                {
                    return col.Mesh.Target.Asset.Data;
                }
                else
                {
#if DEBUG
                    ResoniteMario64.Warn($"[MeshCollider] {col.Slot.Name} Mesh is null or not readable, so we won't be able to use this as a collider for Mario :(");
#endif
                }
                break;
            case ConvexHullCollider col:
                if (col.Mesh.IsAssetAvailable)
                {
                    return col.Mesh.Target.Asset.Data;
                }
                else
                {
#if DEBUG
                    ResoniteMario64.Warn($"[ConvexHullCollider] {col.Slot.Name} Mesh is null or not readable, so we won't be able to use this as a collider for Mario :(");
#endif
                }
                break;
            default:
                break;
        }
        return meshx;
    }
}
