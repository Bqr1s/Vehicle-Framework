﻿using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Debugging;
using Verse;

namespace Vehicles.Testing
{
  internal class UnitTestAerialVehicleWorldHandling : UnitTest
  {
    public override TestType ExecuteOn => TestType.GameLoaded;

    public override string Name => "AerialVehicle WorldHandling";

    public override IEnumerable<UTResult> Execute()
    {
      CameraJumper.TryShowWorld();
      Map map = Find.CurrentMap;
      World world = Find.World;
      Assert.IsNotNull(world);
      Assert.IsNotNull(map);

      VehicleDef vehicleDef = DefDatabase<VehicleDef>.AllDefsListForReading.RandomOrDefault(def => def.vehicleType == VehicleType.Air);
      Assert.IsNotNull(vehicleDef, "No aerial vehicle for testing.");

      if (world == null || map == null || vehicleDef == null)
      {
        yield return UTResult.For("UnitTest Setup", false);
        yield break;
      }

      VehiclePawn vehicle = VehicleSpawner.GenerateVehicle(vehicleDef, Faction.OfPlayer);
      AerialVehicleInFlight aerialVehicle = AerialVehicleInFlight.Create(vehicle, map.Tile);

      Pawn colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
      Assert.IsTrue(colonist != null && colonist.Faction == Faction.OfPlayer, "Unable to generate colonist");
      Pawn animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Alphabeaver, Faction.OfPlayer);
      Assert.IsTrue(animal != null && animal.Faction == Faction.OfPlayer, "Unable to generate pet");

      VehicleHandler handler = vehicle.handlers.FirstOrDefault();
      Assert.IsNotNull(handler, "Testing with aerial vehicle which has no roles");
      Assert.IsTrue(vehicle.TryAddPawn(colonist, handler));
      Assert.IsTrue(vehicle.inventory.innerContainer.TryAddOrTransfer(animal, canMergeWithExistingStacks: false), "Unable to add pet to vehicle inventory");
      Assert.IsTrue(!vehicle.Destroyed && !vehicle.Discarded);

      Find.WorldPawns.PassToWorld(vehicle);
      foreach (Pawn pawn in vehicle.AllPawnsAboard)
      {
        Assert.IsTrue(!pawn.Destroyed && !pawn.Discarded);
        if (!pawn.IsWorldPawn())
        {
          Find.WorldPawns.PassToWorld(pawn);
        }
      }
      foreach (Thing thing in vehicle.inventory.innerContainer)
      {
        if (thing is Pawn pawn && !pawn.IsWorldPawn())
        {
          Assert.IsTrue(!pawn.Destroyed && !pawn.Discarded);
          Find.WorldPawns.PassToWorld(pawn);
        }
      }
      bool result = vehicle.ParentHolder is AerialVehicleInFlight aerialWorldObject && aerialWorldObject == aerialVehicle;
      yield return UTResult.For("Vehicle ParentHolder", result);

      result = vehicle.ParentHolder != null;
      foreach (Pawn pawn in vehicle.AllPawnsAboard)
      {
        result &= pawn.GetVehicle() == vehicle;
      }
      yield return UTResult.For("Passenger ParentHolder", result);

      result = vehicle.ParentHolder != null;
      foreach (Thing thing in vehicle.inventory.innerContainer)
      {
        if (thing is Pawn pawn)
        {
          result &= pawn.ParentHolder is Pawn_InventoryTracker inventoryTracker && inventoryTracker.pawn == vehicle;
        }
      }
      yield return UTResult.For("Pet in trunk ParentHolder", result);

      Find.WorldPawns.gc.CancelGCPass();
      _ = Find.WorldPawns.gc.PawnGCPass();

      string output = Find.WorldPawns.gc.PawnGCDebugResults();
      if (vehicle.Destroyed || vehicle.Discarded)
      {
        Find.WorldPawns.gc.LogGC();
        yield return UTResult.For($"Vehicle WorldPawnGC", false);
      }
      foreach (Pawn pawn in vehicle.AllPawnsAboard)
      {
        if (pawn.Destroyed || pawn.Discarded)
        {
          Find.WorldPawns.gc.LogGC();
          yield return UTResult.For($"Passenger WorldPawnGC", false);
        }
      }
      foreach (Thing thing in vehicle.inventory.innerContainer)
      {
        if (thing is Pawn pawn && (pawn.Destroyed || pawn.Discarded))
        {
          Find.WorldPawns.gc.LogGC();
          yield return UTResult.For($"Pet in trunk WorldPawnGC", false);
        }
      }
      yield return UTResult.For("WorldPawnGC", true);
    }
  }
}
