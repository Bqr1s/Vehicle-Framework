using DevTools.UnitTesting;
using Verse;

namespace SmashTools.UnitTesting;

[UnitTest(TestType.Disabled)]
[TestDescription("Synchronization util class for posting actions to the main thread.")]
internal class UnitTest_UnityThread
{
  [Test]
  private void UpdateLoop()
  {
  }

  [Test]
  private void ExecuteMainThreadNonBlocking()
  {
  }

  [Test]
  private void ExecuteMainThreadBlocking()
  {
  }

  private static void TestMethod()
  {
    Expect.IsTrue(UnityData.IsInMainThread, "Executing From MainThread");
  }
}