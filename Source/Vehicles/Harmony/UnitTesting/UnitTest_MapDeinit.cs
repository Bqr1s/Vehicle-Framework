using DevTools;
using DevTools.UnitTesting;
using SmashTools;
using SmashTools.Performance;
using Verse;

namespace Vehicles.Testing
{
  [UnitTest(TestType.Playing)]
  internal class UnitTest_MapDeinit
  {
    [Test]
    private void ReleaseThreads()
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
      Expect.IsFalse("Created", ThreadManager.AllThreadsTerminated);

      // Validate all threads terminate and Thread::Join wait handles don't time out.
      ThreadManager.ReleaseThreadsAndClearCache();
      Expect.IsTrue("Terminated", ThreadManager.AllThreadsTerminated);
    }
  }
}