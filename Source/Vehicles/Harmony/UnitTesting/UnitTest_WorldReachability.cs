using System.Collections.Generic;
using System.Linq;
using DevTools;
using DevTools.UnitTesting;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Pathfinding;
using Verse;
using WorldRegionGrid = Vehicles.WorldVehicleReachability.WorldRegionGrid;

namespace Vehicles.Testing
{
  [UnitTest(TestType.Playing)]
  internal class UnitTest_WorldReachability
  {
    [Test]
    private void RegionFloodFill()
    {
      WorldVehiclePathGrid pathGrid = WorldVehiclePathGrid.Instance;
      foreach (VehicleDef vehicleDef in GridOwners.World.AllOwners)
      {
        using Test.Group group = new(vehicleDef.defName);

        WorldRegionGrid regionGrid = pathGrid.reachability.GetRegionGrid(vehicleDef);
        bool allValid = true;
        for (int tile = 0; tile < Find.WorldGrid.TilesCount; tile++)
        {
          // Verify that every tile has been processed
          if (regionGrid.GetRegionId(tile) == 0)
          {
            allValid = false;
            break;
          }
        }
        Expect.IsTrue("Region Floodfill", allValid);
      }
    }

    [Test]
    private void RegionGeneration()
    {
      WorldVehiclePathGrid pathGrid = WorldVehiclePathGrid.Instance;
      WorldVehiclePathfinder pathfinder = WorldVehiclePathfinder.Instance;
      foreach (VehicleDef vehicleDef in GridOwners.World.AllOwners)
      {
        using Test.Group group = new(vehicleDef.defName);

        WorldRegionGrid regionGrid = pathGrid.reachability.GetRegionGrid(vehicleDef);

        Dictionary<int, List<int>> regions = [];
        for (int tile = 0; tile < Find.WorldGrid.TilesCount; tile++)
        {
          int id = regionGrid.GetRegionId(tile);
          regions.AddOrInsert(id, tile);
        }

        int totalCount = 0;
        BFS<int> bfs = new();
        // Regions are homogenous
        foreach ((int id, List<int> tiles) in regions)
        {
          Assert.IsFalse(tiles.NullOrEmpty(), "Region id registered with no tiles");

          List<int> expectedTiles = bfs.FloodFill(tiles[0], Ext_World.GetTileNeighbors,
            canEnter: (t) => pathGrid.PassableFast(t, vehicleDef));
          Expect.IsTrue($"Homogenous Region: {id}",
            tiles.All(tile => regionGrid.GetRegionId(tile) == id));
          // If validating impassable regions, make sure floodfill returned nothing and
          // all tiles have impassable costs.
          if (id == -1)
          {
            Expect.IsTrue("All Impassable",
              expectedTiles.All(tile => !pathGrid.PassableFast(tile, vehicleDef)));
            Expect.IsTrue("Tile Count", expectedTiles.Count == 0);
          }
          else
          {
            Expect.IsTrue("Id Assignment",
              expectedTiles.All(tile => regionGrid.GetRegionId(tile) == id));
            Expect.IsTrue("Tile Count", expectedTiles.Count == tiles.Count);
          }
          totalCount += tiles.Count;
        }
        Expect.IsTrue("All Tiles Registered", totalCount == Find.WorldGrid.TilesCount);

        List<VehicleDef> vehicleDefList = [vehicleDef];
        // Regions cannot pathfind to each other
        foreach ((int id, List<int> tiles) in regions)
        {
          int tile = tiles.FirstOrDefault();
          if (tiles.Count > 1)
          {
            int tileDest = tiles.Skip(1).RandomElement();
            using WorldPath path = pathfinder.FindPath(tile, tileDest, vehicleDefList);
            // If id = -1, it should immediately fail to find path
            Expect.IsTrue("Pathfinding Within Region", path.Found == id > 0);
          }

          foreach ((int otherId, List<int> otherTiles) in regions)
          {
            if (id != otherId)
            {
              tile = tiles.RandomElement();
              int otherTile = otherTiles.RandomElement();
              using WorldPath path = pathfinder.FindPath(tile, otherTile, vehicleDefList);
              Expect.IsFalse("Pathfinding Outside Region", path.Found);
            }
          }
        }
      }
    }
  }
}