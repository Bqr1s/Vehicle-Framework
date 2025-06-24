using System.Linq;
using DevTools.UnitTesting;
using RimWorld;
using SmashTools;
using UnityEngine.Assertions;
using Verse;
using DescriptionAttribute = DevTools.UnitTesting.TestDescriptionAttribute;

namespace Vehicles.UnitTesting;

// Validation of vehicle functionality needs to occur before
[UnitTest(TestType.Playing), ExecutionPriority(Priority.AboveNormal)]
[TestCategory(TestCategoryNames.TickBehavior)]
[Description("VehicleRoleHandler behavior and all logic surrounding board and disembark.")]
internal sealed class UnitTest_VehicleRoleHandler
{
  [Test]
  private void BoardingUnboarding()
  {
    using VehicleGroup group = VehicleGroup.CreateBasicVehicleGroup(new VehicleGroup.MockSettings
    {
      drivers = 2,
      passengers = 2,
      animals = 2,
    });

    TestUtils.ForceSpawn(group.vehicle);
    Assert.IsTrue(group.vehicle.Spawned);

    // Colonists can board
    for (int i = 0; i < group.pawns.Count; i++)
    {
      Pawn pawn = group.pawns[i];
      Expect.IsTrue(group.vehicle.TryAddPawn(pawn), $"Boarded {i + 1}/{group.pawns.Count}");
    }
    Assert.IsTrue(group.pawns.All(pawn => pawn.IsInVehicle() && !pawn.Spawned));

    // Colonist cannot board full vehicle
    Pawn failColonist = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
    Assert.IsNotNull(failColonist);
    Assert.AreEqual(failColonist.Faction, Faction.OfPlayer);
    Expect.IsFalse(group.vehicle.TryAddPawn(failColonist));

    failColonist.Destroy();

    if (ModsConfig.BiotechActive)
    {
      group.DisembarkAll();
      Pawn mechanoid =
        PawnGenerator.GeneratePawn(PawnKindDefOf.Mech_Warqueen, Faction.OfPlayer);
      Assert.IsNotNull(mechanoid);
      Assert.AreEqual(mechanoid.Faction, Faction.OfPlayer);
      Expect.IsTrue(group.vehicle.TryAddPawn(mechanoid));
      group.vehicle.DisembarkPawn(mechanoid);
      mechanoid.Destroy();
    }
  }

  // TODO
  [Test]
  private void ReservationChecks()
  {
    using VehicleGroup group = VehicleGroup.CreateBasicVehicleGroup(new VehicleGroup.MockSettings
    {
      drivers = 1,
      passengers = 1,
      animals = 1,
    });

    TestUtils.ForceSpawn(group.vehicle);
    Assert.IsTrue(group.vehicle.Spawned);

    VehicleReservationManager reservationMgr =
      group.vehicle.Map.GetCachedMapComponent<VehicleReservationManager>();
    Assert.IsNotNull(reservationMgr);
    VehicleHandlerReservation reservation =
      reservationMgr.GetReservation<VehicleHandlerReservation>(group.vehicle);
    Assert.IsNull(reservation);
  }

  [Test]
  private void RoleTicking()
  {
    using VehicleGroup group = VehicleGroup.CreateBasicVehicleGroup(new VehicleGroup.MockSettings
    {
      drivers = 1
    });

    group.Spawn();

    // Vehicle parent
    {
      using TickObserver<VehiclePawn> observer = new(group.vehicle);
      Find.TickManager.DoSingleTick();
      Expect.AreEqual(observer.TickCount, 1);
    }

    // Internal roles
    {
      Pawn pawn = group.pawns.First();
      Assert.IsFalse(pawn.Spawned);
      Assert.IsTrue(pawn.IsInVehicle());
      using TickObserver<Pawn> observer = new(pawn);
      Find.TickManager.DoSingleTick();
      Expect.AreEqual(observer.TickCount, 1);
    }
  }
}