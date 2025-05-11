using DevTools.UnitTesting;
using Verse;

namespace Vehicles;

[StaticConstructorOnStartup]
internal static class StartupConfig
{
  static StartupConfig()
  {
    UnitTestManager.OnUnitTestStateChange += SuppressDebugLogging;
  }

  private static void SuppressDebugLogging(bool value)
  {
    VehicleMod.settings.debug.debugLogging = false;
  }
}