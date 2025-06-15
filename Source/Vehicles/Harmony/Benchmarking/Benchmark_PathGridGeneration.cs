using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevTools.Benchmarking;
using LudeonTK;
using SmashTools;
using Verse;

namespace Vehicles.Testing;

[BenchmarkClass("Path Grid Generation", AllowedGameStates = AllowedGameStates.PlayingOnMap)]
internal class Benchmark_PathGridGeneration
{
  private const int VehicleTestCount = 5;

  [Benchmark]
  private static void PathGridGen_Parallel(ref PathGridContext context)
  {
    VehicleMapping mapping = context.mapping;
    // delegate will add a little bit of overhead from but this is how the original method
    // is written so it's accurate.
    Parallel.ForEach(context.vehicleDefs, delegate(VehicleDef vehicleDef)
    {
      VehicleMapping.VehiclePathData vehiclePathData = mapping[vehicleDef];
      vehiclePathData.VehiclePathGrid.RecalculateAllPerceivedPathCosts();
    });
  }

  [Benchmark]
  private static void PathGridGen_Partitioned(ref PathGridContext context)
  {
    VehicleMapping mapping = context.mapping;
    List<VehicleDef> vehicleDefs = context.vehicleDefs;
    // delegate will add a little bit of overhead from but this is how the original method
    // is written so it's accurate.
    Parallel.ForEach(Partitioner.Create(0, context.vehicleDefs.Count), (range, _) =>
    {
      for (int i = range.Item1; i < range.Item2; i++)
      {
        VehicleMapping.VehiclePathData vehiclePathData = mapping[vehicleDefs[i]];
        vehiclePathData.VehiclePathGrid.RecalculateAllPerceivedPathCosts();
      }
    });
  }

  [Benchmark]
  private static void PathGridGen_Sequential(ref PathGridContext context)
  {
    VehicleMapping mapping = context.mapping;

    foreach (VehicleDef vehicleDef in context.vehicleDefs)
    {
      VehicleMapping.VehiclePathData vehiclePathData = mapping[vehicleDef];
      vehiclePathData.VehiclePathGrid.RecalculateAllPerceivedPathCosts();
    }
  }

  private readonly struct PathGridContext
  {
    public readonly VehicleMapping mapping;
    public readonly List<VehicleDef> vehicleDefs;

    public PathGridContext()
    {
      this.mapping = Find.CurrentMap.GetCachedMapComponent<VehicleMapping>();
      this.vehicleDefs =
        DefDatabase<VehicleDef>.AllDefsListForReading.Take(VehicleTestCount).ToList();
    }
  }
}