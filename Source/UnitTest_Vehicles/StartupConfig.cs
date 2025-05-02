using DevTools;
using DevTools.UnitTesting;
using Vehicles;
using Verse;

namespace UnitTest_Vehicles;

[StaticConstructorOnStartup]
internal static class StartupConfig
{
  // Debugging with HarmonyMod's stack trace suppression for duplicates is a massive pain and for
  // some reason it isn't a mod setting, but rather a tweak value that has to be toggled off after
  // every startup! Just disable it permanently for debug builds, I'd rather not deal with this.
  private static readonly StackTraceCacheDisabler stcDisabler = new();

  static StartupConfig()
  {
    // Set test-specific states for main Vehicles project
    UnitTestManager.OnUnitTestStateChange +=
      isRunningTests => VehicleHarmony.RunningUnitTests = isRunningTests;
    UnitTestManager.OnUnitTestStateChange += SuppressDebugLogging;
  }

  private static void SuppressDebugLogging(bool value)
  {
    VehicleMod.settings.debug.debugLogging = false;
  }
}