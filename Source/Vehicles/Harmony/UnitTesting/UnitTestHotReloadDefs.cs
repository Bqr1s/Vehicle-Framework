using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using SmashTools;
using SmashTools.Debugging;
using SmashTools.Performance;
using Verse;

namespace Vehicles.Testing;

internal class UnitTestHotReloadDefs : UnitTest
{
  public override string Name => "Hot Reload Defs";

  public override TestType ExecuteOn => TestType.GameLoaded;

  public override IEnumerable<UTResult> Execute()
  {
    UTResult result = new();

    int countBefore = VehicleHarmony.VehicleMCP.AllDefs.Count();
    Assert.IsTrue(countBefore > 0);
    int targetsBefore = RGBMaterialPool.Count;
    int materialsBefore = RGBMaterialPool.TotalMaterials;

    PlayDataLoader.HotReloadDefs();

    int countAfter = VehicleHarmony.VehicleMCP.AllDefs.Count();
    int targetsAfter = RGBMaterialPool.Count;
    int materialsAfter = RGBMaterialPool.TotalMaterials;

    result.Add("HotReloadDefs (Def Count)", countBefore == countAfter);
    result.Add("HotReloadDefs (CacheTargets Count)", targetsBefore == targetsAfter);
    result.Add("HotReloadDefs (Material Count)", materialsBefore == materialsAfter);
    yield return result;
  }
}