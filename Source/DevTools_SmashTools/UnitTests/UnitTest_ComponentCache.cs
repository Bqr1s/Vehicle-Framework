using DevTools.UnitTesting;

namespace SmashTools.UnitTesting;

[UnitTest(TestType.PostGameExit)]
[TestDescription("Game, World, and Map component cache.")]
internal class UnitTest_ComponentCache
{
  [Test]
  private void Clear()
  {
    Expect.AreEqual(MapComponentCache.CountAll(), 0, "MapComps");
  }
}