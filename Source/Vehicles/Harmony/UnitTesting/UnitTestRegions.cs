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

    public override TestType ExecuteOn => TestType.GameLoaded;

    protected override bool ShouldTest(VehicleDef vehicleDef)
    {
      // Should never be the case but some people are wild
      // so 8 wide vehicle isn't out of the question.
      if (vehicleDef.SizePadding > 4) return false;
      return PathingHelper.ShouldCreateRegions(vehicleDef) && GridOwners.IsOwner(vehicleDef);
    }

    protected override CellRect TestArea(VehicleDef vehicleDef, IntVec3 root)
    {
      return VehicleRegion.ChunkAt(root).ContractedBy(vehicleDef.SizePadding);
    }

    protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
    {
      VehicleDef vehicleDef = vehicle.VehicleDef;
      int padding = vehicleDef.SizePadding;
      UTResult result = new();

      CellRect testArea = TestArea(vehicleDef, root);

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

      VehicleMapping mapping = TestMap.GetCachedMapComponent<VehicleMapping>();
      VehicleRegionGrid regionGrid = mapping[vehicleDef].VehicleRegionGrid;
      VehicleRegionMaker regionMaker = mapping[vehicleDef].VehicleRegionMaker;
      Assert.IsFalse(mapping.ThreadAvailable);

      // Clear area region generation. The chunk should be completely empty, meaning
      // 1 region spanning the entirety of the chunk and there should be no neighboring
      // entities that might pad into the chunk we're testing.
      DebugHelper.DestroyArea(testArea.ExpandedBy(padding * 2), TestMap);
      Assert.IsTrue(RegionsInArea(testArea) == 1);

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
      result.Add($"{vehicleDef} (Set Impassable)", RegionsInArea(testArea) == 0);
      result.Add($"{vehicleDef} (Invalid Regions)", !regionGrid.AnyInvalidRegions);

      // Clear
      ClearArea();
      result.Add($"{vehicleDef} (Clear Impassable)",
        ValidateArea(testArea, true) && RegionsInArea(testArea) == 1);
      result.Add($"{vehicleDef} (Region Links)", ValidateLinks(testArea));
      result.Add($"{vehicleDef} (Invalid Regions)", !regionGrid.AnyInvalidRegions);

      // 1 Block
      ClearArea();
      VehicleRegion region = regionGrid.GetValidRegionAt(root);
      result.Add($"{vehicleDef} (Region Single)", region != null);
      CellRect singleCell = CellRect.SingleCell(root);
      SpawnThing(singleCell);
      if (vehicleDef.SizePadding == 0)
      {
        // If there's no padding, then test valid edge cells instead
        result.Add($"{vehicleDef} (Single Invalid)", ValidateArea(singleCell, false));
        result.Add($"{vehicleDef} (Edge Cells)", singleCell.ExpandedBy(1).EdgeCells
         .All(cell => regionGrid.GetValidRegionAt(cell) != null));
      }
      else
      {
        CellRect paddedArea = CellRect.CenteredOn(root, vehicleDef.SizePadding);
        result.Add($"{vehicleDef} (Padding)", ValidateArea(paddedArea, false));
      }

      result.Add($"{vehicleDef} (Invalid Regions)", !regionGrid.AnyInvalidRegions);

      // Region Reused
      ClearArea();
      result.Add($"{vehicleDef} (Region Reused)", region != null &&
                                                  region == regionGrid.GetValidRegionAt(root));
      result.Add($"{vehicleDef} (Region Links)", ValidateLinks(testArea));
      result.Add($"{vehicleDef} (Invalid Regions)", !regionGrid.AnyInvalidRegions);

      // Will always pass for non-debug builds since ObjectCounter will only increment for debug builds.
      // We really shouldn't add the overhead of counting object instantiations outside of a dev environment.
      result.Add($"{vehicleDef} (Region Pool)", ocwRegions.Count == 0);
      result.Add($"{vehicleDef} (RegionLink Pool)", ocwLinks.Count == 0);

      return result;

      int RegionsInArea(CellRect cellRect)
      {
        foreach (IntVec3 cell in cellRect)
        {
          VehicleRegion validRegion = regionGrid.GetValidRegionAt(cell);
          if (validRegion is not null) regions.Add(validRegion);
        }

        int count = regions.Count;
        regions.Clear();
        return count;
      }

      bool ValidateLinks(CellRect cellRect)
      {
        foreach (IntVec3 cell in cellRect.EdgeCells)
        {
          VehicleRegion validRegion = regionGrid.GetValidRegionAt(cell);
          if (validRegion is null) continue;

          // i = 0 would start at center, we want 4 cardinal neighbors
          for (int i = 1; i <= 4; i++)
          {
            IntVec3 cardinal = cell + GenRadial.ManualRadialPattern[i];
            VehicleRegion neighbor = regionGrid.GetValidRegionAt(cell);
            if (neighbor is null || neighbor == validRegion) continue;

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

      bool ValidateArea(CellRect cellRect, bool expected)
      {
        foreach (IntVec3 cell in cellRect)
        {
          VehicleRegion validRegion = regionGrid.GetValidRegionAt(cell);
          if (validRegion is not null != expected) return false;
        }

        return true;
      }

      void ClearArea()
      {
        DebugHelper.DestroyArea(testArea, TestMap);
      }

      void SpawnThing(CellRect cellRect)
      {
        ThingDef stuffDef = testDef.MadeFromStuff ? GenStuff.DefaultStuffFor(testDef) : null;
        ClearArea();
        foreach (IntVec3 cell in cellRect)
        {
          GenSpawn.Spawn(ThingMaker.MakeThing(testDef, stuffDef), cell, TestMap);
        }
      }
    }
  }
}