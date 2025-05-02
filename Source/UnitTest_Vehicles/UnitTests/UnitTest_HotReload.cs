using System.Linq;
using DevTools.UnitTesting;
using Verse;

namespace Vehicles.UnitTesting;

[UnitTest(TestType.Playing)]
internal sealed class UnitTest_HotReload
{
  private int countBefore;
  private int targetsBefore;
  private int materialsBefore;

  [SetUp]
  private void CacheCounts()
  {
    countBefore = VehicleHarmony.VehicleMCP.AllDefs.Count();
    targetsBefore = RGBMaterialPool.Count;
    materialsBefore = RGBMaterialPool.TotalMaterials;

    PlayDataLoader.HotReloadDefs();
  }

  [Test]
  private void VehicleDefs()
  {
    int countAfter = VehicleHarmony.VehicleMCP.AllDefs.Count();
    Expect.AreEqual(countBefore, countAfter, "Def Count");
  }

  [Test]
  private void MaterialPool()
  {
    Expect.AreEqual(targetsBefore, RGBMaterialPool.Count, "MaterialPool Targets");
    Expect.AreEqual(materialsBefore, RGBMaterialPool.TotalMaterials, "Total Material Count");
  }
}