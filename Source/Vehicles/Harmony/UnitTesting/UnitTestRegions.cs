using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SmashTools;
using SmashTools.Debugging;
using SmashTools.Performance;
using UnityEngine;
using Verse;

namespace Vehicles.Testing
{
  internal class UnitTestRegions : UnitTestMapTest
  {
    private readonly HashSet<VehicleRegion> regions = [];

    public override string Name => "Regions";

    public override bool ShouldTest(VehicleDef vehicleDef)
    {
      return PathingHelper.ShouldCreateRegions(vehicleDef) && GridOwners.IsOwner(vehicleDef);
    }

    public override CellRect TestArea(VehicleDef vehicleDef, IntVec3 root)
    {
      return VehicleRegion.ChunkAt(root);
    }

    protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
    {
      int padding = vehicle.VehicleDef.SizePadding;
      UTResult result;

      CellRect testArea = TestArea(vehicle.VehicleDef, root);

      ThingDef testDef = DefDatabase<ThingDef>.AllDefsListForReading.RandomOrDefault(def => def.building != null &&
        def.Size == IntVec2.One && PathingHelper.IsRegionEffector(def) &&
        PathingHelper.regionEffectors[def].Contains(vehicle.VehicleDef));
      Assert.IsNotNull(testDef);

      VehicleMapping mapping = TestMap.GetCachedMapComponent<VehicleMapping>();
      VehicleRegionGrid regionGrid = mapping[vehicle.VehicleDef].VehicleRegionGrid;

      // Verify area is ready for further testing. The chunk should be completely empty,
      // meaning 1 region spanning the entirety of the chunk and there should be no
      // neighboring entities that might pad into the chunk we're testing.
      ClearArea(testArea.ExpandedBy(padding * 2));
      Assert.IsTrue(RegionsInArea(testArea) == 1);

      {
        // Verify region is sent to pool and later retrieved when area is cleared
        ObjectCountWatcher<VehicleRegion> ocwRegions = new();
        ObjectCountWatcher<VehicleRegionLink> ocwLinks = new();

        // Full chunk filled with impassable entities leaves no invalid regions afterward
        SetArea(testArea, testDef);
        result.Add($"100% Impassable (No Invalid Regions)", RegionsInArea(testArea) == 0);

        // Clear
        ClearArea(testArea);
        result.Add($"Clear Impassable", ValidateArea(testArea, true) && RegionsInArea(testArea) == 1);

        // Will always pass for non-debug builds since ObjectCounter will only increment for debug builds.
        // We really shouldn't add the overhead of counting object instantiations outside of a dev environment.
        result.Add($"Region Pool", ocwRegions.Count == 0);
      }

      // 1 Block

      // Region Pooling

      // Fetch from Pool

      return result;

      int RegionsInArea(CellRect cellRect)
      {
        foreach (IntVec3 cell in cellRect)
        {
          VehicleRegion region = regionGrid.GetRegionAt(cell);
          if (region is not null) regions.Add(region);
        }
        int count = regions.Count;
        regions.Clear();
        return count;
      }

      bool ValidateArea(CellRect cellRect, bool expected)
      {
        foreach (IntVec3 cell in testArea)
        {
          VehicleRegion region = regionGrid.GetRegionAt(cell);
          if ((region is not null) != expected) return false;
        }
        return true;
      }
    }

    private void ClearArea(CellRect cellRect)
    {
      DebugHelper.DestroyArea(cellRect, TestMap);
    }

    private void SetArea(CellRect cellRect, ThingDef thingDef)
    {
      ThingDef stuffDef = thingDef.MadeFromStuff ? GenStuff.DefaultStuffFor(thingDef) : null;
      ClearArea(cellRect);
      foreach (IntVec3 cell in cellRect)
      {
        GenSpawn.Spawn(ThingMaker.MakeThing(thingDef, stuffDef), cell, TestMap);
      }
    }
  }
}
