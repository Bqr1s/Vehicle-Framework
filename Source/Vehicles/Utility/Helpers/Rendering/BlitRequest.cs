using System.Collections.Generic;
using JetBrains.Annotations;
using SmashTools;
using Verse;

namespace Vehicles.Rendering;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public struct BlitRequest
{
  public VehicleDef vehicleDef;
  public Rot8 rot;
  public PatternData patternData;
  public float scale = 1;

  public List<IBlitTarget> blitTargets = [];

  public BlitRequest(VehicleDef vehicleDef)
  {
    this.vehicleDef = vehicleDef;
    rot = vehicleDef.drawProperties.displayRotation;
    patternData =
      VehicleMod.settings.vehicles.defaultGraphics.TryGetValue(vehicleDef.defName,
        fallback: vehicleDef.graphicData);
    if (!VehicleMod.settings.main.useCustomShaders)
    {
      patternData.patternDef = PatternDefOf.Default;
    }
  }

  public BlitRequest(VehiclePawn vehicle) : this(vehicle.VehicleDef)
  {
    patternData = vehicle.patternData;
  }

  public static BlitRequest For(VehiclePawn vehicle)
  {
    VehicleDef vehicleDef = vehicle.VehicleDef;
    BlitRequest request = new(vehicleDef);
    request.blitTargets.Add(vehicleDef);
    if (vehicle.GetCachedComp<CompVehicleTurrets>() is { } compTurrets &&
      !compTurrets.turrets.NullOrEmpty())
    {
      request.blitTargets.AddRange(compTurrets.turrets);
    }
    if (!vehicle.DrawTracker.overlayRenderer.AllOverlaysListForReading.NullOrEmpty())
    {
      request.blitTargets.AddRange(vehicle.DrawTracker.overlayRenderer
       .AllOverlaysListForReading);
    }
    return request;
  }

  public static BlitRequest For(VehicleDef vehicleDef)
  {
    BlitRequest request = new(vehicleDef);
    request.blitTargets.Add(vehicleDef);
    if (vehicleDef.GetSortedCompProperties<CompProperties_VehicleTurrets>() is { } props)
    {
      request.blitTargets.AddRange(props.turrets);
    }
    if (!vehicleDef.drawProperties.overlays.NullOrEmpty())
    {
      request.blitTargets.AddRange(vehicleDef.drawProperties.overlays);
    }
    return request;
  }
}