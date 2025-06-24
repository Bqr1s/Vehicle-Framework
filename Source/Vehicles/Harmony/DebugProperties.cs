using SmashTools;
using Verse;

namespace Vehicles;

internal static class DebugProperties
{
  // Enhanced debugging state which will enable many costly debugging features.
  internal static readonly bool debug = false;

  internal static readonly bool drawPaths = false;

  internal static readonly bool drawAllRegions = false;

  private static readonly (string defName, DebugRegionType regionType) regionDebugging =
    ("VF_TestMarshal", DebugRegionType.Regions | DebugRegionType.Links);

  internal static void Init()
  {
    // Debug settings cannot be allowed in release builds, as there is no way for
    // a user to unset them. Set everything to default as a fail safe, but we should
    // still verify it's not enabled.
#if RELEASE
    Trace.IsFalse(debug);
    typeof(DebugProperties).SetStaticFieldsDefault();
#else
    if (!debug)
    {
      typeof(DebugProperties).SetStaticFieldsDefault();
      return;
    }

    DebugHelper.Local.VehicleDef =
      DefDatabase<VehicleDef>.GetNamedSilentFail(regionDebugging.defName);
    if (DebugHelper.Local.VehicleDef != null)
    {
      DebugHelper.Local.DebugType = regionDebugging.regionType;
    }
#endif
  }
}