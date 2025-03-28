using System;
using System.Threading;
using SmashTools;
using SmashTools.Debugging;
using SmashTools.Performance;
using Verse;

namespace Vehicles.Testing
{
  internal class UnitTestDeferredGeneration : UnitTestMapTest
  {
    private const double MaxWaitTime = 5000; // ms

    public override string Name => "DeferredGridGeneration";

    protected override bool ShouldTest(VehicleDef vehicleDef)
    {
      return PathingHelper.ShouldCreateRegions(vehicleDef);
    }

    protected override UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root)
    {
      VehicleDef vehicleDef = vehicle.VehicleDef;

      UTResult result = new();

      VehicleMapping mapping = TestMap.GetCachedMapComponent<VehicleMapping>();
      VehicleMapping.VehiclePathData pathData = mapping[vehicleDef];

      if (mapping.deferredGridGeneration == null || !mapping.ThreadAlive)
        return UTResult.For("DeferredGeneration", UTResult.Result.Skipped);

      mapping.deferredGridGeneration.DoPassExpectClear();
      Assert.IsTrue(pathData.Suspended);

      // We're specifically testing thread enqueueing with deferred grid generation, we need the 
      // dedicated thread available in order to test this. Will return to suspended after test
      // since we're running this as a UnitTestMapTest.
      using ThreadEnabler te = new();
      ManualResetEventSlim mres = new(false);

      GenSpawn.Spawn(vehicle, root, TestMap);
      // Faction.OfPlayer
      result.Add($"DeferredGeneration_{vehicleDef} (Spawned)", vehicle.Spawned);
      result.Add("DeferredGeneration (Player Deferred)",
        DeferredGridGeneration.UrgencyFor(vehicle) == DeferredGridGeneration.Urgency.Deferred);

      // We need to wait for the dedicated thread to finish generating vehicle's grids so we can
      // validate that every grid is initialized.
      AsyncLongOperationAction longOp = AsyncPool<AsyncLongOperationAction>.Get();
      longOp.OnInvoke += () => NotifyReadyToContinue(mres);
      mapping.dedicatedThread.Enqueue(longOp);
      mres.Wait(TimeSpan.FromMilliseconds(MaxWaitTime));

      result.Add("DeferredGeneration (Player PathGrid Generated)",
        pathData.VehiclePathGrid.Enabled);
      result.Add("DeferredGeneration (Player Regions Generated)",
        pathData.VehicleRegionAndRoomUpdater.Enabled);
      result.Add("DeferredGeneration (Player PathData Status)", !pathData.Suspended);

      vehicle.DeSpawn();
      Assert.IsTrue(!vehicle.Spawned);
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
      GenSpawn.Spawn(vehicle, root, TestMap);
      result.Add($"DeferredGeneration_{vehicleDef} (Spawned Enemy)", vehicle.Spawned);
      result.Add($"DeferredGeneration (Enemy Urgent)",
        DeferredGridGeneration.UrgencyFor(vehicle) == DeferredGridGeneration.Urgency.Urgent);

      result.Add($"DeferredGeneration (Enemy Regions Generated)",
        pathData.VehicleRegionAndRoomUpdater.Enabled);
      result.Add($"DeferredGeneration (Enemy PathGrid Generated)",
        pathData.VehiclePathGrid.Enabled);
      result.Add($"DeferredGeneration (Enemy PathData Status)", !pathData.Suspended);

      // Unblock dedicated thread
      mres.Set();

      return result;
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
}