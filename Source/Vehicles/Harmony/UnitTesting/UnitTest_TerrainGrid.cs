﻿using DevTools;
using DevTools.UnitTesting;
using SmashTools;
using Verse;

namespace Vehicles.Testing
{
  [UnitTest(TestType.Playing)]
  internal class UnitTest_TerrainGrid : UnitTest_MapTest
  {
    protected override CellRect TestArea(VehicleDef vehicleDef)
    {
      return CellRect.CenteredOn(root, 5);
    }

    [Test]
    private void TerrainGrid()
    {
      foreach (VehiclePawn vehicle in vehicles)
      {
        using VehicleTestCase vtc = new(vehicle, this);

        VehicleDef vehicleDef = vehicle.VehicleDef;
        VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
        VehicleRegionGrid regionGrid = mapping[vehicleDef].VehicleRegionGrid;
        VehicleMapping.VehiclePathData pathData = mapping[vehicleDef];

        CellRect testArea = TestArea(vehicleDef);
        CellRect terrainArea = testArea.ContractedBy(vehicleDef.SizePadding);
        DebugHelper.DestroyArea(testArea.ExpandedBy(vehicleDef.SizePadding), map);

        TerrainDef terrainOrig = map.terrainGrid.TerrainAt(root);
        TerrainDef passableTerrain = DefDatabase<TerrainDef>.AllDefsListForReading
         .FirstOrDefault(def =>
            def != terrainOrig && VehiclePathGrid.PassableTerrainCost(vehicleDef, def, out _));
        TerrainDef impassableTerrain = DefDatabase<TerrainDef>.AllDefsListForReading
         .FirstOrDefault(def =>
            def != terrainOrig && !VehiclePathGrid.PassableTerrainCost(vehicleDef, def, out _));

        Assert.IsNotNull(terrainOrig);
        Assert.IsNotNull(passableTerrain);
        Assert.IsNotNull(impassableTerrain);

        // VehiclePathGrid costs should take terrain into account
        VehiclePathGrid pathGrid = pathData.VehiclePathGrid;

        // Terrain cost updates
        SetArea(in terrainArea, passableTerrain);
        Expect.IsTrue("PathGrid Updated",
          AreaCost(vehicleDef, pathGrid, in terrainArea, passableTerrain));

        // Terrain becomes impassable
        SetArea(in terrainArea, impassableTerrain);
        Expect.IsTrue("PathGrid Updated",
          AreaCost(vehicleDef, pathGrid, in terrainArea, impassableTerrain));
        Expect.IsFalse("PathGrid Impassable",
          VehiclePathGrid.PassableTerrainCost(vehicleDef, impassableTerrain, out _));

        if (PathingHelper.ShouldCreateRegions(vehicleDef) && mapping.GridOwners.IsOwner(vehicleDef))
        {
          // Impassable terrain invalidates regions
          Expect.IsTrue("RegionGrid Updated", Regions(regionGrid, in testArea, false));
          Expect.IsFalse("No Invalid Regions", regionGrid.AnyInvalidRegions);

          // Impassable terrain removal invalidates regions
          SetArea(in terrainArea, terrainOrig);
          Expect.IsTrue("RegionGrid Updated", Regions(regionGrid, in testArea, true));
          Expect.IsFalse("No Invalid Regions", regionGrid.AnyInvalidRegions);
        }
      }
    }

    private void SetArea(ref readonly CellRect cellRect, TerrainDef replaceTerrain = null)
    {
      DebugHelper.DestroyArea(cellRect, map, replaceTerrain: replaceTerrain);
    }

    private static bool AreaCost(VehicleDef vehicleDef, VehiclePathGrid pathGrid,
      ref readonly CellRect cellRect, TerrainDef terrainDef)
    {
      int expected = VehiclePathGrid.TerrainCostAt(vehicleDef, terrainDef);
      foreach (IntVec3 cell in cellRect)
      {
        if (pathGrid.CalculatedCostAt(cell) != expected) return false;
      }

      return true;
    }

    private static bool Regions(VehicleRegionGrid regionGrid, ref readonly CellRect cellRect,
      bool valid)
    {
      foreach (IntVec3 cell in cellRect)
      {
        VehicleRegion region = regionGrid.GetValidRegionAt(cell);
        if ((region is not null) != valid) return false;
      }

      return true;
    }
  }
}