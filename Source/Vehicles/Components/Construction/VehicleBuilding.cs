using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using SmashTools;
using UnityEngine;
using Verse;

namespace Vehicles
{
  [UsedImplicitly]
  public class VehicleBuilding : Building, IInspectable
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
      }
      else
      {
        Log.ErrorOnce(
          $"VehicleReference for building {LabelShort} is null. This should not happen unless " +
          $"spawning VehicleBuildings in DevMode.", GetHashCode());
        base.DrawAt(drawLoc, flip);
      }
    }

    public virtual float DoInspectPaneButtons(float x)
    {
      Rect rect = new Rect(x, 0f, Extra.IconBarDim, Extra.IconBarDim);
      float usedWidth = 0;

      if (Prefs.DevMode)
      {
        //rect.x -= rect.width;
        //usedWidth += rect.width;
        //TODO - add devmode options related to constructions
      }

      return usedWidth;
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