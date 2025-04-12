using DevTools;
using DevTools.UnitTesting;
using RimWorld;
using SmashTools;
using Verse;

namespace Vehicles.Testing
{
  [UnitTest(TestType.Playing)]
  internal class UnitTest_VehicleHandler : UnitTest_MapTest
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
        Expect.IsTrue("Spawn Position", vehicle.Position == root);

        // Colonists can board
        int total = vehicle.SeatsAvailable;
        for (int i = 0; i < total; i++)
        {
          Pawn colonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
          Assert.IsNotNull(colonist);
          Assert.IsTrue(colonist.Faction == Faction.OfPlayer);
          Expect.IsTrue($"Boarded {i + 1}/{total}", vehicle.TryAddPawn(colonist));
        }

        // Colonist cannot board full vehicle
        Pawn failColonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        Assert.IsNotNull(failColonist);
        Assert.IsTrue(failColonist.Faction == Faction.OfPlayer);
        Expect.IsFalse("Reject Boarding (Full Capacity)", vehicle.TryAddPawn(failColonist));

        failColonist.Destroy();
        vehicle.DestroyPawns();

        Pawn animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Alphabeaver, Faction.OfPlayer);
        Assert.IsNotNull(animal);
        Assert.IsTrue(animal.Faction == Faction.OfPlayer);
        Expect.IsTrue("Boarded Animal", vehicle.TryAddPawn(animal));

        vehicle.DestroyPawns();

        if (ModsConfig.BiotechActive)
        {
          Pawn mechanoid =
            PawnGenerator.GeneratePawn(PawnKindDefOf.Mech_Warqueen, Faction.OfPlayer);
          Assert.IsNotNull(mechanoid);
          Assert.IsTrue(mechanoid.Faction == Faction.OfPlayer);
          Expect.IsTrue("Boarded Mech", vehicle.TryAddPawn(mechanoid));
        }
      }
    }
  }
}