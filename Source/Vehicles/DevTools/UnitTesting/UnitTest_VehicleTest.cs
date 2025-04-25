using DevTools.UnitTesting;
using Verse;

namespace Vehicles.UnitTesting;

internal abstract class UnitTest_VehicleTest
{
  [TearDown, ExecutionPriority(Priority.Last)]
  protected void EmptyWorldAndMapOfVehicles()
  {
    foreach (Pawn pawn in Find.World.worldPawns.AllPawnsAliveOrDead)
    {
      if (pawn is VehiclePawn vehicle)
        Find.WorldPawns.RemoveAndDiscardPawnViaGC(vehicle);
    }
    foreach (Map map in Find.Maps)
    {
      foreach (Pawn pawn in map.mapPawns.AllPawns)
      {
        if (pawn is VehiclePawn { Destroyed: false } vehicle)
          vehicle.Destroy();
      }
    }
  }
}