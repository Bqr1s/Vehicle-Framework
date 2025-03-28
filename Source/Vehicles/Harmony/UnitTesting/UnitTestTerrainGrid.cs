using SmashTools;
using SmashTools.Debugging;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Vehicles.Testing
{
  internal class UnitTestTerrainGrid : UnitTestMapTest
  {
    public override string Name => "TerrainGrid";

    protected override CellRect TestArea(VehicleDef vehicleDef, IntVec3 root)
    {
      return CellRect.CenteredOn(root, 5);
    }

    protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
    {
      UTResult result = new();

      VehicleDef vehicleDef = vehicle.VehicleDef;
      VehicleMapping mapping = TestMap.GetCachedMapComponent<VehicleMapping>();
      VehicleRegionGrid regionGrid = mapping[vehicleDef].VehicleRegionGrid;
      VehicleMapping.VehiclePathData pathData = mapping[vehicleDef];

      CellRect testArea = TestArea(vehicleDef, root);
      CellRect terrainArea = testArea.ContractedBy(vehicleDef.SizePadding);
      DebugHelper.DestroyArea(testArea.ExpandedBy(vehicleDef.SizePadding), TestMap);

      TerrainDef terrainOrig = TestMap.terrainGrid.TerrainAt(root);
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
      bool success = AreaCost(in terrainArea, passableTerrain);
      result.Add($"TerrainGrid_{vehicleDef} (PathCost)", success);

      // Terrain becomes impassable
      SetArea(in terrainArea, impassableTerrain);
      success = AreaCost(in terrainArea, impassableTerrain) &&
        !VehiclePathGrid.PassableTerrainCost(vehicleDef, impassableTerrain, out _);
      result.Add($"TerrainGrid_{vehicleDef} (ImpassableCost)", success);

      if (PathingHelper.ShouldCreateRegions(vehicleDef) && mapping.GridOwners.IsOwner(vehicleDef))
      {
        // Impassable terrain invalidates regions
        success = Regions(in testArea, false);
        result.Add($"TerrainGrid_{vehicleDef} (Removed Regions)", success);
        success = !regionGrid.AnyInvalidRegions;
        result.Add($"TerrainGrid_{vehicleDef} (Invalid Regions)", success);

        // Impassable terrain removal invalidates regions
        SetArea(in terrainArea, terrainOrig);
        success = Regions(in testArea, true);
        result.Add($"TerrainGrid_{vehicleDef} (Created Regions)", success);
        success = !regionGrid.AnyInvalidRegions;
        result.Add($"TerrainGrid_{vehicleDef} (Invalid Regions)", success);
      }

      return result;

      bool AreaCost(ref readonly CellRect cellRect, TerrainDef terrainDef)
      {
        int expected = VehiclePathGrid.TerrainCostAt(vehicleDef, terrainDef);
        foreach (IntVec3 cell in cellRect)
        {
          if (pathGrid.CalculatedCostAt(cell) != expected) return false;
        }

        return true;
      }

      bool Regions(ref readonly CellRect cellRect, bool valid)
      {
        foreach (IntVec3 cell in cellRect)
        {
          VehicleRegion region = mapping[vehicleDef].VehicleRegionGrid.GetValidRegionAt(cell);
          if ((region is not null) != valid) return false;
        }

        return true;
      }
    }

    private void SetArea(ref readonly CellRect cellRect, TerrainDef replaceTerrain = null)
    {
      DebugHelper.DestroyArea(cellRect, TestMap, replaceTerrain: replaceTerrain);
    }
  }
}