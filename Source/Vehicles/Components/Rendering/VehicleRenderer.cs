using System;
using RimWorld;
using SmashTools.Rendering;
using UnityEngine;
using Verse;
using PreRenderResults = Vehicles.Graphic_Rgb.PreRenderResults;

namespace Vehicles;

public sealed class VehicleRenderer : IParallelRenderer
{
  // values pulled from RimWorld pawn offsets
  //public const float SubInterval = 0.003787879f;
  //public const float YOffset_Body = 0.007575758f;
  //public const float YOffset_Damage = 0.018939395f;
  //public const float YOffset_CoveredInOverlay = 0.033301156f;

  private readonly VehiclePawn vehicle;

  private PreRenderResults results;

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

  public void DynamicDrawPhaseAt(DrawPhase phase, in TransformData transformData)
  {
    switch (phase)
    {
      case DrawPhase.EnsureInitialized:
        _ = vehicle.VehicleGraphic; // Ensure graphic has been created
        // TODO - generate combined mesh here and/or build list of things that should be batch rendered
        break;
      case DrawPhase.ParallelPreDraw:
        results = ParallelGetPreRenderResults(in transformData);
        break;
      case DrawPhase.Draw:
        // Out of phase drawing must immediately generate pre-render results for valid data.
        if (!results.valid)
          results = ParallelGetPreRenderResults(in transformData);
        Draw();
        results = default;
        break;
      default:
        throw new NotImplementedException();
    }
  }

  private PreRenderResults ParallelGetPreRenderResults(ref readonly TransformData transformData)
  {
    return vehicle.VehicleGraphic.ParallelGetPreRenderResults(in transformData);
  }

  private void Draw()
  {
    // TODO - use material property blocks
    Graphics.DrawMesh(results.mesh, results.position, results.quaternion, results.material, 0);
    vehicle.VehicleGraphic.ShadowGraphic?.Draw(results.position, vehicle.FullRotation, vehicle);

    if (vehicle.Spawned && !vehicle.Dead)
      vehicle.vehiclePather.PatherDraw();

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
  }
}