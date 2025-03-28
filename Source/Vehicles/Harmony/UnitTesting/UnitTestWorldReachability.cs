using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Debugging;
using SmashTools.Pathfinding;
using Verse;
using WorldRegionGrid = Vehicles.WorldVehicleReachability.WorldRegionGrid;

namespace Vehicles.Testing
{
  internal class UnitTestWorldReachability : UnitTest
  {
    public override TestType ExecuteOn => TestType.GameLoaded;

    public override string Name => "WorldReachability";

    public override IEnumerable<UTResult> Execute()
    {
      World world = Find.World;
      Assert.IsNotNull(world);
      Map map = Find.CurrentMap;
      Assert.IsNotNull(map);

      WorldVehiclePathGrid pathGrid = WorldVehiclePathGrid.Instance;
      foreach (VehicleDef vehicleDef in GridOwners.World.AllOwners)
      {
        WorldRegionGrid regionGrid = pathGrid.reachability.GetRegionGrid(vehicleDef);
        yield return TestRegionFloodFill(pathGrid, regionGrid, vehicleDef);
        yield return TestRegionCorrectness(pathGrid, regionGrid, vehicleDef);
      }
    }

    // Entire grid was floodfilled
    private UTResult TestRegionFloodFill(WorldVehiclePathGrid pathGrid,
      WorldRegionGrid regionGrid, VehicleDef vehicleDef)
    {
      for (int tile = 0; tile < Find.WorldGrid.TilesCount; tile++)
      {
        if (regionGrid.GetRegionId(tile) == 0)
          return UTResult.For($"WorldReachability_{vehicleDef} (FloodFill)",
            UTResult.Result.Failed);
      }
      return UTResult.For($"WorldReachability_{vehicleDef} (FloodFill)", UTResult.Result.Passed);
    }

    // Regions only contain tiles of the same id and are surrounded by impassable tiles
    private UTResult TestRegionCorrectness(WorldVehiclePathGrid pathGrid,
      WorldRegionGrid regionGrid, VehicleDef vehicleDef)
    {
      UTResult result = new();

      WorldVehiclePathfinder pathfinder = WorldVehiclePathfinder.Instance;

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
        if (tiles.NullOrEmpty())
        {
          Assert.Fail("Region id registered with no tiles");
          result.Add("WorldReachability (TileLists)", UTResult.Result.Skipped);
          continue;
        }
        List<int> expectedTiles = bfs.FloodFill(tiles[0], Ext_World.GetTileNeighbors,
          canEnter: (t) => pathGrid.PassableFast(t, vehicleDef));
        result.Add("WorldReachability (Tile Ids)",
          tiles.All(tile => regionGrid.GetRegionId(tile) == id));
        // If validating impassable regions, make sure floodfill returned nothing and
        // all tiles have impassable costs.
        if (id == -1)
        {
          result.Add("WorldReachability (All Impassable)",
            expectedTiles.All(tile => !pathGrid.PassableFast(tile, vehicleDef)));
          result.Add("WorldReachability (Count)", expectedTiles.Count == 0);
        }
        else
        {
          result.Add("WorldReachability (Expected Ids)",
            expectedTiles.All(tile => regionGrid.GetRegionId(tile) == id));
          result.Add("WorldReachability (Count)", expectedTiles.Count == tiles.Count);
        }
        totalCount += tiles.Count;
      }
      Assert.IsTrue(totalCount == Find.WorldGrid.TilesCount);

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
          result.Add("WorldReachability (Pathfinding In)", path.Found == id > 0);
        }

        foreach ((int otherId, List<int> otherTiles) in regions)
        {
          if (id != otherId)
          {
            tile = tiles.RandomElement();
            int otherTile = otherTiles.RandomElement();
            using WorldPath path = pathfinder.FindPath(tile, otherTile, vehicleDefList);
            result.Add("WorldReachability (Pathfinding Out)", !path.Found);
          }
        }
      }
      return result;
    }
  }
}