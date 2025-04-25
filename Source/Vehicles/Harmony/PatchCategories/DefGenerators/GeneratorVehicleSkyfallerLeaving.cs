using Verse;

namespace Vehicles;

internal class GeneratorVehicleSkyfallerLeaving : IVehicleDefGenerator<ThingDef>
{
  bool IVehicleDefGenerator<ThingDef>.TryGenerateImpliedDef(VehicleDef vehicleDef,
    out ThingDef skyfallerLeavingImpliedDef, bool hotReload)
  {
    skyfallerLeavingImpliedDef = null;

    if (vehicleDef.GetCompProperties<CompProperties_VehicleLauncher>() is not { } comp)
      return false;

    if (comp.skyfallerLeaving is not null)
      return false;

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
    return true;
  }
}