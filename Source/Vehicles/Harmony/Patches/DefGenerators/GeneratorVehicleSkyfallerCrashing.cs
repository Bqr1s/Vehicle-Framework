using RimWorld;
using UnityEngine;
using Verse;

namespace Vehicles;

internal class GeneratorVehicleSkyfallerCrashing : IVehicleDefGenerator<ThingDef>
{
  bool IVehicleDefGenerator<ThingDef>.TryGenerateImpliedDef(VehicleDef vehicleDef,
    out ThingDef skyfallerCrashingImpliedDef, bool hotReload)
  {
    skyfallerCrashingImpliedDef = null;
    if (vehicleDef.GetCompProperties<CompProperties_VehicleLauncher>() is not { } comp)
      return false;

    if (comp.skyfallerCrashing is not null)
      return false;

    string defName = $"{vehicleDef.defName}Crashing";
    skyfallerCrashingImpliedDef = !hotReload ?
      new ThingDef() :
      DefDatabase<ThingDef>.GetNamed(defName, false) ?? new ThingDef();
    skyfallerCrashingImpliedDef.defName = defName;
    skyfallerCrashingImpliedDef.modContentPack = vehicleDef.modContentPack;
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

    return true;
  }
}