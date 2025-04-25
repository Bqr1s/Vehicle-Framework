using JetBrains.Annotations;
using UnityEngine;
using Verse;

namespace Vehicles
{
  [UsedImplicitly]
  public class VehicleBuilding : Building
  {
    public VehiclePawn vehicle;

    public VehicleDef VehicleDef
    {
      get
      {
        VehicleBuildDef buildDef = def as VehicleBuildDef;
        return buildDef?.thingToSpawn;
      }
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
      if (vehicle != null)
      {
        vehicle.DrawNowAt(drawLoc, flip);
        vehicle.CompVehicleTurrets?.PostDraw();
        return;
      }
      Log.ErrorOnce($"VehicleReference for building {LabelShort} is null.", GetHashCode());
      base.DrawAt(drawLoc, flip);
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
      base.SpawnSetup(map, respawningAfterLoad);
      if (vehicle is null && VehicleDef != null)
      {
        vehicle = VehicleSpawner.GenerateVehicle(VehicleDef, Faction);
      }
      vehicle?.CompVehicleTurrets?.RevalidateTurrets();
    }

    public override void ExposeData()
    {
      base.ExposeData();
      Scribe_References.Look(ref vehicle, nameof(vehicle), true);
    }
  }
}