using System;
using RimWorld;
using SmashTools.Rendering;
using UnityEngine;
using Verse;

namespace Vehicles;

public sealed class VehicleRenderer
{
  // values pulled from RimWorld pawn offsets
  //public const float SubInterval = 0.003787879f;
  //public const float YOffset_Body = 0.007575758f;
  public const float YOffset_Damage = 0.018939395f;
  public const float YOffset_CoveredInOverlay = 0.033301156f;

  private readonly VehiclePawn vehicle;

  //public VehicleGraphicSet graphics;

  //private Graphic_DynamicShadow shadowGraphic;

  //private PawnFirefoamDrawer firefoamOverlays;

  public VehicleRenderer(VehiclePawn vehicle)
  {
    this.vehicle = vehicle;
    //graphics = new VehicleGraphicSet(vehicle);

    //firefoamOverlays = new PawnFirefoamDrawer(vehicle);
  }

  [Obsolete("Not currently implemented, still WIP. Do not reference.", error: true)]
  public PawnFirefoamDrawer FirefoamOverlays => throw new NotImplementedException();

  public void RenderVehicle(ref readonly TransformData transform)
  {
    RenderVehicleWithOverlays(in transform);
    vehicle.VehicleGraphic.ShadowGraphic?.Draw(transform.position, vehicle.FullRotation, vehicle);
    if (vehicle.Spawned && !vehicle.Dead)
    {
      vehicle.vehiclePather.PatherDraw();
    }
  }

  private void RenderVehicleWithOverlays(ref readonly TransformData transform)
  {
    Vector3 aboveBodyPos = RenderVehicleInternal(in transform);
    vehicle.DrawExplosiveWicks(aboveBodyPos, transform.orientation);
    vehicle.overlayRenderer.DrawOverlays(in transform);
  }

  // Vehicle body rendering
  private Vector3 RenderVehicleInternal(ref readonly TransformData transform)
  {
    Quaternion quaternion = Quaternion.AngleAxis(
      (transform.orientation.AsRotationAngle + transform.rotation) *
      (vehicle.NorthSouthRotation ? -1 : 1), Vector3.up);

    Vector3 aboveBodyPos =
      transform.position + vehicle.VehicleGraphic.DrawOffset(transform.orientation);
    //aboveBodyPos.y += YOffset_Body;
    Mesh mesh = vehicle.VehicleGraphic.MeshAtFull(transform.orientation);
    Material material = vehicle.VehicleGraphic.MatAtFull(transform.orientation);

    GenDraw.DrawMeshNowOrLater(mesh, aboveBodyPos, quaternion, material, false);
    //aboveBodyPos.y += SubInterval;

    Vector3 drawLoc = transform.position;
    drawLoc.y += YOffset_Damage;
    // TODO - Firefoam overlays for vehicle
    //if (firefoamOverlays.IsCoveredInFoam)
    //{
    //	Vector3 overlayPos = rootLoc;
    //	overlayPos.y += YOffset_CoveredInOverlay;
    //	firefoamOverlays.RenderPawnOverlay(overlayPos, mesh, quaternion, flags.FlagSet(PawnRenderFlags.DrawNow), PawnOverlayDrawer.OverlayLayer.Body, bodyFacing);
    //}

    // TODO - pack graphics
    //if (vehicle.inventory != null && vehicle.inventory.innerContainer.Count > 0 && graphics.packGraphic != null)
    //{
    //	Graphics.DrawMesh(mesh, drawLoc, quaternion, graphics.packGraphic.MatAt(bodyFacing, null), 0);
    //}
    return aboveBodyPos;
  }

  public void ProcessPostTickVisuals(int ticksPassed)
  {
  }
}