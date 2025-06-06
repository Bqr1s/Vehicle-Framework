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
    // Back compatibility for maps that were not saved with these area types
    VehicleHarmony.Patch(original: AccessTools.Method(typeof(Map), nameof(Map.FinalizeInit)),
      postfix: new HarmonyMethod(typeof(ProjectSetup),
        nameof(BackfillVehicleAreas)));
  }

  private static void AddVehicleAreas(AreaManager __instance)
  {
    __instance.map.EnsureAreaInitialized<Area_Road>();
    __instance.map.EnsureAreaInitialized<Area_RoadAvoidal>();
  }

  private static void BackfillVehicleAreas(Map __instance)
  {
    __instance.EnsureAreaInitialized<Area_Road>();
    __instance.EnsureAreaInitialized<Area_RoadAvoidal>();
  }
}