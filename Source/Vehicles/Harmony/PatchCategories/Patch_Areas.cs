using HarmonyLib;
using SmashTools;
using Verse;

namespace Vehicles;

internal class Patch_Areas : IPatchCategory
{
  public void PatchMethods()
  {
    VehicleHarmony.Patch(
      original: AccessTools.Method(typeof(AreaManager), nameof(AreaManager.AddStartingAreas)),
      postfix: new HarmonyMethod(typeof(Patch_Areas),
        nameof(AddVehicleAreas)));
  }

  private static void AddVehicleAreas(AreaManager __instance)
  {
    __instance.map.TryAddAreas();
  }
}