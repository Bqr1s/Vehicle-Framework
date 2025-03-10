using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace Vehicles
{
  public static class ThingDefGenerator_Skyfallers
  {
    public static bool GenerateImpliedSkyfallerDef(VehicleDef vehicleDef,
      out ThingDef skyfallerLeavingImpliedDef, out ThingDef skyfallerIncomingImpliedDef,
      out ThingDef skyfallerCrashingImpliedDef, bool hotReload)
    {
      skyfallerLeavingImpliedDef = null;
      skyfallerIncomingImpliedDef = null;
      skyfallerCrashingImpliedDef = null;
      if (vehicleDef.GetCompProperties<CompProperties_VehicleLauncher>() is
        CompProperties_VehicleLauncher comp)
      {
        if (comp.skyfallerLeaving == null)
        {
          string defName = $"{vehicleDef.defName}Leaving";
          skyfallerLeavingImpliedDef = !hotReload ?
            new ThingDef() :
            DefDatabase<ThingDef>.GetNamed(defName, false) ?? new ThingDef();
          skyfallerLeavingImpliedDef.defName = defName;
          skyfallerLeavingImpliedDef.label = $"{vehicleDef.defName}Leaving";
          skyfallerLeavingImpliedDef.thingClass = typeof(VehicleSkyfaller_Leaving);
          skyfallerLeavingImpliedDef.category = ThingCategory.Ethereal;
          skyfallerLeavingImpliedDef.useHitPoints = false;
          skyfallerLeavingImpliedDef.drawOffscreen = true;
          skyfallerLeavingImpliedDef.tickerType = TickerType.Normal;
          skyfallerLeavingImpliedDef.altitudeLayer = AltitudeLayer.Skyfaller;
          skyfallerLeavingImpliedDef.drawerType = DrawerType.RealtimeOnly;
          skyfallerLeavingImpliedDef.skyfaller = new SkyfallerProperties()
          {
            shadow =
              "Things/Skyfaller/SkyfallerShadowDropPod",
            shadowSize = vehicleDef.Size.ToVector2(),
          };
          comp.skyfallerLeaving = skyfallerLeavingImpliedDef;
        }

        if (comp.skyfallerIncoming == null)
        {
          string defName = $"{vehicleDef.defName}Incoming";
          skyfallerIncomingImpliedDef = !hotReload ?
            new ThingDef() :
            DefDatabase<ThingDef>.GetNamed(defName, false) ?? new ThingDef();
          skyfallerIncomingImpliedDef.defName = defName;
          skyfallerIncomingImpliedDef.label = $"{vehicleDef.defName}Incoming";
          skyfallerIncomingImpliedDef.thingClass = typeof(VehicleSkyfaller_Arriving);
          skyfallerIncomingImpliedDef.category = ThingCategory.Ethereal;
          skyfallerIncomingImpliedDef.useHitPoints = false;
          skyfallerIncomingImpliedDef.drawOffscreen = true;
          skyfallerIncomingImpliedDef.tickerType = TickerType.Normal;
          skyfallerIncomingImpliedDef.altitudeLayer = AltitudeLayer.Skyfaller;
          skyfallerIncomingImpliedDef.drawerType = DrawerType.RealtimeOnly;
          skyfallerIncomingImpliedDef.skyfaller = new SkyfallerProperties()
          {
            shadow =
              "Things/Skyfaller/SkyfallerShadowDropPod",
            shadowSize = vehicleDef.Size.ToVector2()
          };
          comp.skyfallerIncoming = skyfallerIncomingImpliedDef;
        }

        if (comp.skyfallerCrashing == null)
        {
          string defName = $"{vehicleDef.defName}Crashing";
          skyfallerCrashingImpliedDef = !hotReload ?
            new ThingDef() :
            DefDatabase<ThingDef>.GetNamed(defName, false) ?? new ThingDef();
          skyfallerCrashingImpliedDef.defName = defName;
          skyfallerCrashingImpliedDef.label = $"{vehicleDef.defName}Crashing";
          skyfallerCrashingImpliedDef.thingClass = typeof(VehicleSkyfaller_Crashing);
          skyfallerCrashingImpliedDef.category = ThingCategory.Ethereal;
          skyfallerCrashingImpliedDef.useHitPoints = false;
          skyfallerCrashingImpliedDef.drawOffscreen = true;
          skyfallerCrashingImpliedDef.tickerType = TickerType.Normal;
          skyfallerCrashingImpliedDef.altitudeLayer = AltitudeLayer.Skyfaller;
          skyfallerCrashingImpliedDef.drawerType = DrawerType.RealtimeOnly;
          skyfallerCrashingImpliedDef.skyfaller = new SkyfallerProperties()
          {
            shadow = "Things/Skyfaller/SkyfallerShadowDropPod",
            shadowSize = vehicleDef.Size.ToVector2(),
            movementType = SkyfallerMovementType.ConstantSpeed,
            explosionRadius = Mathf.Max(vehicleDef.Size.x, vehicleDef.Size.z) * 1.5f,
            explosionDamage = DamageDefOf.Bomb,
            rotateGraphicTowardsDirection = vehicleDef.rotatable,
            speed = 2,
            ticksToImpactRange = new IntRange(300, 350)
          };
          comp.skyfallerCrashing = skyfallerCrashingImpliedDef;
        }

        return true;
      }

      return false;
    }
  }
}