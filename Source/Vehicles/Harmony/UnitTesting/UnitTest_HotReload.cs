using System.Linq;
using DevTools;
using DevTools.UnitTesting;
using Verse;

namespace Vehicles.Testing;

[UnitTest(TestType.Playing)]
internal class UnitTest_HotReload
{
  [Test]
  private void Defs()
  {
    int countBefore = VehicleHarmony.VehicleMCP.AllDefs.Count();
    Assert.IsTrue(countBefore > 0);
    int targetsBefore = RGBMaterialPool.Count;
    int materialsBefore = RGBMaterialPool.TotalMaterials;

    PlayDataLoader.HotReloadDefs();

    int countAfter = VehicleHarmony.VehicleMCP.AllDefs.Count();
    int targetsAfter = RGBMaterialPool.Count;
    int materialsAfter = RGBMaterialPool.TotalMaterials;

    Expect.IsTrue("HotReloadDefs (Def Count)", countBefore == countAfter);
    Expect.IsTrue("HotReloadDefs (CacheTargets Count)", targetsBefore == targetsAfter);
    Expect.IsTrue("HotReloadDefs (Material Count)", materialsBefore == materialsAfter);
  }
}