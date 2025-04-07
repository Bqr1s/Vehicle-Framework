using SmashTools.UnitTesting;
using Verse;

namespace Vehicles.Testing;

internal abstract class UnitTest_VehicleTest : UnitTest
{
  public override void CleanUp()
  {
    EmptyWorldAndMapOfVehicles();
  }

  private static void EmptyWorldAndMapOfVehicles()
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