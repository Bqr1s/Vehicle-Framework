using System;
using System.Threading;
using DevTools.UnitTesting;
using SmashTools;
using SmashTools.Performance;
using UnityEngine.Assertions;
using Verse;
using TestType = DevTools.UnitTesting.TestType;

namespace Vehicles.UnitTesting;

[UnitTest(TestType.Playing)]
internal sealed class UnitTest_DeferredGeneration : UnitTest_MapTest
{
  private const double MaxWaitTime = 5000; // ms

  protected override bool ShouldTest(VehicleDef vehicleDef)
  {
    return PathingHelper.ShouldCreateRegions(vehicleDef);
  }

  [Test]
  private void TestVehicle()
  {
    foreach (VehiclePawn vehicle in vehicles)
    {
      using VehicleTestCase vtc = new(vehicle, this);

      VehicleDef vehicleDef = vehicle.VehicleDef;

      VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
      VehicleMapping.VehiclePathData pathData = mapping[vehicleDef];

      Assert.IsNotNull(mapping.deferredGridGeneration);
      if (!mapping.ThreadAlive)
      {
        Test.Skip("Thread not available.");
        return;
      }

      mapping.deferredGridGeneration.DoPassExpectClear();
      Assert.IsTrue(pathData.Suspended);

      // We're specifically testing thread enqueueing with deferred grid generation, we need the 
      // dedicated thread available in order to test this. Will return to suspended after test
      // since we're running this as a UnitTestMapTest.
      using ThreadEnabler te = new();
      ManualResetEventSlim mres = new(false);

      GenSpawn.Spawn(vehicle, root, map);
      // Faction.OfPlayer
      Expect.IsTrue(vehicle.Spawned, "Spawned");
      Expect.AreEqual(DeferredGridGeneration.UrgencyFor(vehicle),
        DeferredGridGeneration.Urgency.Deferred, "Player Deferred");

      // We need to wait for the dedicated thread to finish generating vehicle's grids so we can
      // validate that every grid is initialized.
      AsyncLongOperationAction longOp = AsyncPool<AsyncLongOperationAction>.Get();
      longOp.OnInvoke += () => NotifyReadyToContinue(mres);
      mapping.dedicatedThread.Enqueue(longOp);
      Assert.IsTrue(mres.Wait(TimeSpan.FromMilliseconds(MaxWaitTime)));

      Expect.IsTrue(pathData.VehiclePathGrid.Enabled, "Player PathGrid Generated");
      Expect.IsTrue(pathData.VehicleRegionAndRoomUpdater.Enabled, "Player Regions Generated");
      Expect.IsFalse(pathData.Suspended, "Player PathData Suspended");

      vehicle.DeSpawn();
      Assert.IsFalse(vehicle.Spawned);
      mapping.deferredGridGeneration.DoPassExpectClear();
      Assert.IsTrue(pathData.Suspended);

      // Block dedicated thread without flagging as suspended so we can still validate that
      // grid generation is not being sent to the dedicated thread for deferred generation of
      // map grids. This is equivalent to clogging up the dedicated thread until we decide we're
      // ready or we hit the timeout threshold.
      mres.Reset();
      AsyncLongOperationAction blockingOp = AsyncPool<AsyncLongOperationAction>.Get();
      blockingOp.OnInvoke += () => WaitForSignal(mres);
      mapping.dedicatedThread.Enqueue(blockingOp);

      Assert.IsNotNull(Find.World.factionManager.OfAncientsHostile);
      vehicle.SetFactionDirect(Find.World.factionManager.OfAncientsHostile);
      GenSpawn.Spawn(vehicle, root, map);
      Expect.IsTrue(vehicle.Spawned, "Enemy Spawned");
      Expect.AreEqual(DeferredGridGeneration.UrgencyFor(vehicle),
        DeferredGridGeneration.Urgency.Urgent, "Enemy Spawn Urgent");

      Expect.IsTrue(pathData.VehicleRegionAndRoomUpdater.Enabled, "Enemy Regions Generated");
      Expect.IsTrue(pathData.VehiclePathGrid.Enabled, "Enemy PathGrid Generated");
      Expect.IsFalse(pathData.Suspended, "Enemy PathData Suspended");

      // Unblock dedicated thread
      mres.Set();
    }
  }

  [TearDown]
  private void RegenerateAllGrids()
  {
    VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
    mapping.deferredGridGeneration.DoPassExpectClear();
    mapping.RegenerateGrids(deferment: VehicleMapping.GridDeferment.Forced);
  }

  private static void WaitForSignal(ManualResetEventSlim mre)
  {
    mre.Wait(TimeSpan.FromMilliseconds(MaxWaitTime));
  }

  private static void NotifyReadyToContinue(ManualResetEventSlim mre)
  {
    mre.Set();
  }
}