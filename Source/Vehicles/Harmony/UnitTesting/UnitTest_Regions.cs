using System.Collections.Generic;
using System.Linq;
using DevTools;
using DevTools.UnitTesting;
using RimWorld;
using SmashTools;
using SmashTools.Performance;
using Verse;

namespace Vehicles.Testing;

[UnitTest(TestType.Playing)]
internal class UnitTest_Regions : UnitTest_MapTest
{
  private readonly HashSet<VehicleRegion> regions = [];

  protected override bool ShouldTest(VehicleDef vehicleDef)
  {
    // SizePadding will rarely be above 4, but there are mods out there adding incredibly large
    // vehicles, and region testing would be too expensive. Validating 4 and below should suffice.
    return vehicleDef.SizePadding <= 4 && PathingHelper.ShouldCreateRegions(vehicleDef);
  }

  protected override CellRect TestArea(VehicleDef vehicleDef)
  {
    return VehicleRegion.ChunkAt(root).ContractedBy(vehicleDef.SizePadding);
  }

  [Test]
  private void Generation()
  {
    foreach (VehiclePawn vehicle in vehicles)
    {
      using VehicleTestCase vtc = new(vehicle, this);

      VehicleDef vehicleDef = vehicle.VehicleDef;
      int padding = vehicleDef.SizePadding;

      CellRect testArea = TestArea(vehicleDef);

      ThingDef testDef = ThingDefOf.Wall;
      if (!PathingHelper.IsRegionEffector(vehicleDef, testDef))
      {
        testDef = DefDatabase<ThingDef>.AllDefsListForReading.RandomOrDefault(def =>
          def.building != null &&
          def.Size == IntVec2.One && PathingHelper.IsRegionEffector(vehicleDef, def) &&
          def is not VehicleBuildDef &&
          PathingHelper.regionEffectors[def].Contains(vehicleDef));
      }

      Assert.IsNotNull(testDef);

      VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
      VehicleRegionGrid regionGrid = mapping[vehicleDef].VehicleRegionGrid;
      VehicleRegionMaker regionMaker = mapping[vehicleDef].VehicleRegionMaker;
      Assert.IsFalse(mapping.ThreadAvailable);

      // Clear area region generation. The chunk should be completely empty, meaning
      // 1 region spanning the entirety of the chunk and there should be no neighboring
      // entities that might pad into the chunk we're testing.
      DebugHelper.DestroyArea(testArea.ExpandedBy(padding * 2), map);
      Assert.IsTrue(RegionsInArea(regionGrid, testArea) == 1);

      // Prewarm object pools, if spans change without invalidating the links, it will
      // not send the link to pool before requesting a new one. It will still utilize
      // the object pool, but the swap will fail the unit test if there are no objects
      // in the object pool before it occurs.
      const int PreWarmRegionCount = 9; // 3x3 grid surrounding root chunk
      const int PreWarmLinkCount = PreWarmRegionCount * 4; // 3x3 grid of 4 links each
      regionMaker.regionPool.PreWarm(PreWarmRegionCount);
      regionMaker.linkPool.PreWarm(PreWarmLinkCount);

      // Verify region is sent to pool and later retrieved when area is cleared
      // If Count is not 0, new objects were instantiated within this scope.
      using ObjectCountWatcher<VehicleRegion> ocwRegions = new();
      using ObjectCountWatcher<VehicleRegionLink> ocwLinks = new();

      // Full chunk filled with impassable entities leaves no invalid regions afterward
      SpawnThing(testArea);
      Expect.IsTrue("Set Impassable", RegionsInArea(regionGrid, testArea) == 0);
      Expect.IsFalse("No Invalid Regions", regionGrid.AnyInvalidRegions);
      Expect.IsTrue("PathGrid Enabled", mapping[vehicleDef].VehiclePathGrid.Enabled);

      // Clear
      ClearArea();
      Expect.IsTrue("Clear Impassable", ValidateArea(regionGrid, testArea, true));
      Expect.IsTrue("Unified Region", RegionsInArea(regionGrid, testArea) == 1);
      Expect.IsTrue("RegionLinks Generated", ValidateLinks(regionGrid, testArea));
      Expect.IsFalse("No Invalid Regions", regionGrid.AnyInvalidRegions);

      // 1 Block
      ClearArea();
      VehicleRegion region = regionGrid.GetValidRegionAt(root);
      Assert.IsNotNull(region);
      CellRect singleCell = CellRect.SingleCell(root);
      SpawnThing(singleCell);
      if (vehicleDef.SizePadding == 0)
      {
        // If there's no padding, then test valid edge cells instead
        Expect.IsTrue("1 Cell Removed From Region", ValidateArea(regionGrid, singleCell, false));
        Expect.IsTrue("No Padding Applied", singleCell.ExpandedBy(1).EdgeCells
         .All(cell => regionGrid.GetValidRegionAt(cell) != null));
      }
      else
      {
        CellRect paddedArea = CellRect.CenteredOn(root, vehicleDef.SizePadding);
        Expect.IsTrue("Padding Applied", ValidateArea(regionGrid, paddedArea, false));
      }

      Expect.IsFalse("No Invalid Regions", regionGrid.AnyInvalidRegions);

      // Region Reused
      ClearArea();
      Expect.IsTrue("Region Recycled", region == regionGrid.GetValidRegionAt(root));
      Expect.IsTrue("RegionLinks Generated", ValidateLinks(regionGrid, testArea));
      Expect.IsFalse("No Invalid Regions", regionGrid.AnyInvalidRegions);

      // Will always pass for non-debug builds since ObjectCounter will only increment for debug builds.
      // We really shouldn't add the overhead of counting object instantiations outside of a dev environment.
      Expect.IsTrue("No Regions Instantiated", ocwRegions.Count == 0);
      Expect.IsTrue("No RegionLinks Instantiated", ocwLinks.Count == 0);
      continue;

      void ClearArea()
      {
        DebugHelper.DestroyArea(testArea, map);
      }

      void SpawnThing(CellRect cellRect)
      {
        ThingDef stuffDef = testDef.MadeFromStuff ? GenStuff.DefaultStuffFor(testDef) : null;
        ClearArea();
        foreach (IntVec3 cell in cellRect)
        {
          GenSpawn.Spawn(ThingMaker.MakeThing(testDef, stuffDef), cell, map);
        }
      }
    }
  }

  private int RegionsInArea(VehicleRegionGrid regionGrid, CellRect cellRect)
  {
    foreach (IntVec3 cell in cellRect)
    {
      VehicleRegion validRegion = regionGrid.GetValidRegionAt(cell);
      if (validRegion is not null)
        regions.Add(validRegion);
    }
    int count = regions.Count;
    regions.Clear();
    return count;
  }

  private static bool ValidateLinks(VehicleRegionGrid regionGrid, CellRect cellRect)
  {
    foreach (IntVec3 cell in cellRect.EdgeCells)
    {
      VehicleRegion validRegion = regionGrid.GetValidRegionAt(cell);
      if (validRegion is null) continue;

      // i = 0 would start at center, we want 4 cardinal neighbors
      for (int i = 1; i <= 4; i++)
      {
        IntVec3 cardinal = cell + GenRadial.ManualRadialPattern[i];
        VehicleRegion neighbor = regionGrid.GetValidRegionAt(cardinal);
        if (neighbor is null || neighbor == validRegion)
          continue;

        VehicleRegionLink regionLink = validRegion.Links.items.FirstOrDefault(link =>
          link.LinksRegions(validRegion, neighbor));
        VehicleRegionLink neighborLink = neighbor.Links.items.FirstOrDefault(link =>
          link.LinksRegions(validRegion, neighbor));

        if (regionLink is null || neighborLink is null || regionLink != neighborLink)
          return false;
      }
    }
    return true;
  }

  private static bool ValidateArea(VehicleRegionGrid regionGrid, CellRect cellRect, bool expected)
  {
    foreach (IntVec3 cell in cellRect)
    {
      VehicleRegion validRegion = regionGrid.GetValidRegionAt(cell);
      if (validRegion is not null != expected) return false;
    }
    return true;
  }
}