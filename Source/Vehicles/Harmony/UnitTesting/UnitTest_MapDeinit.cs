using System.Collections.Generic;
using DevTools;
using SmashTools;
using SmashTools.Performance;
using SmashTools.UnitTesting;
using Verse;

namespace Vehicles.Testing
{
  internal class UnitTest_MapDeinit : UnitTest
  {
    public override TestType ExecuteOn => TestType.Playing;

    public override ExecutionPriority Priority => ExecutionPriority.Last;

    public override string Name => "Map Deinit";

    public override IEnumerable<UTResult> Execute()
    {
      Assert.IsNotNull(Current.Game);
      Assert.IsNotNull(Find.CurrentMap);

      VehicleMapping mapping = Find.CurrentMap.GetCachedMapComponent<VehicleMapping>();
      Assert.IsNotNull(mapping);

      // Create a few threads to validate that cleanup occurs
      ThreadManager.CreateNew();
      ThreadManager.CreateNew();
      ThreadManager.CreateNew();

      // Threads have been registered in thread manager.
      yield return UTResult.For("Threads Created", !ThreadManager.AllThreadsTerminated);

      // Validate all threads terminate and Thread::Join wait handles don't time out.
      ThreadManager.ReleaseThreadsAndClearCache();
      yield return UTResult.For("Threads Terminated", ThreadManager.AllThreadsTerminated);
    }
  }
}