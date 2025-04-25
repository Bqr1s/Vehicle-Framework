using DevTools;
using DevTools.UnitTesting;
using RimWorld;
using SmashTools;
using Verse;

namespace Vehicles.UnitTesting;

[UnitTest(TestType.Playing)]
internal sealed class UnitTest_VehicleHandler : UnitTest_MapTest
{
  protected override bool ShouldTest(VehicleDef vehicleDef)
  {
    return vehicleDef.properties.roles.NotNullAndAny(role => role.Slots > 0);
  }

  [Test]
  private void BoardingUnboarding()
  {
    foreach (VehiclePawn vehicle in vehicles)
    {
      using VehicleTestCase vtc = new(vehicle, this);

      GenSpawn.Spawn(vehicle, root, map);
      Assert.IsTrue(vehicle.Spawned);
      Expect.IsEqual(vehicle.Position, root, "Position not shifted");

      // Colonists can board
      int total = vehicle.SeatsAvailable;
      for (int i = 0; i < total; i++)
      {
        Pawn colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        Assert.IsNotNull(colonist);
        Assert.IsEqual(colonist.Faction, Faction.OfPlayer);
        Expect.IsTrue(vehicle.TryAddPawn(colonist), $"Boarded {i + 1}/{total}");
      }

      // Colonist cannot board full vehicle
      Pawn failColonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
      Assert.IsNotNull(failColonist);
      Assert.IsEqual(failColonist.Faction, Faction.OfPlayer);
      Expect.IsFalse(vehicle.TryAddPawn(failColonist), "Reject boarding (Full Capacity)");

      failColonist.Destroy();
      vehicle.DestroyPawns();

      Pawn animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Alphabeaver, Faction.OfPlayer);
      Assert.IsNotNull(animal);
      Assert.IsEqual(animal.Faction, Faction.OfPlayer);
      Expect.IsTrue(vehicle.TryAddPawn(animal), "Boarded animal");

      vehicle.DestroyPawns();

      if (ModsConfig.BiotechActive)
      {
        Pawn mechanoid =
          PawnGenerator.GeneratePawn(PawnKindDefOf.Mech_Warqueen, Faction.OfPlayer);
        Assert.IsNotNull(mechanoid);
        Assert.IsEqual(mechanoid.Faction, Faction.OfPlayer);
        Expect.IsTrue(vehicle.TryAddPawn(mechanoid), "Boarded mech");
      }
    }
  }
}