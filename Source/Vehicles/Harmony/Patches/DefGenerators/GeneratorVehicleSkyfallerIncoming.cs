using Verse;

namespace Vehicles;

internal class GeneratorVehicleSkyfallerIncoming : IVehicleDefGenerator<ThingDef>
{
  bool IVehicleDefGenerator<ThingDef>.TryGenerateImpliedDef(VehicleDef vehicleDef,
    out ThingDef skyfallerIncomingImpliedDef, bool hotReload)
  {
    skyfallerIncomingImpliedDef = null;

    if (vehicleDef.GetCompProperties<CompProperties_VehicleLauncher>() is not { } comp)
      return false;

    if (comp.skyfallerIncoming is not null)
      return false;

    string defName = $"{vehicleDef.defName}Incoming";
    skyfallerIncomingImpliedDef = !hotReload ?
      new ThingDef() :
      DefDatabase<ThingDef>.GetNamed(defName, false) ?? new ThingDef();
    skyfallerIncomingImpliedDef.defName = defName;
    skyfallerIncomingImpliedDef.modContentPack = vehicleDef.modContentPack;
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
    return true;
  }
}