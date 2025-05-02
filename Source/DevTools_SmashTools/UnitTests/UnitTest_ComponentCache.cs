using DevTools.UnitTesting;

namespace SmashTools.UnitTesting;

[UnitTest(TestType.PostGameExit)]
[TestDescription("Game, World, and Map component cache.")]
internal class UnitTest_ComponentCache
{
  [Test]
  private void Clear()
  {
    Expect.AreEqual(ComponentCache.gameComps.Count, 0, "GameComps");
    Expect.AreEqual(ComponentCache.worldComps.Count, 0, "WorldComps");
    Expect.AreEqual(MapComponentCache.CountAll(), 0, "MapComps");
  }
}