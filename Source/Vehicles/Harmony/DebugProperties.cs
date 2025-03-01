using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmashTools;
using Verse;

namespace Vehicles
{
  internal static class DebugProperties
  {
    // Enhanced debugging state which will enable many costly
    // debugging features.
    internal static readonly bool debug = true;

    internal static readonly bool drawPaths = false;

    internal static readonly bool drawAllRegions = false;

    internal static readonly (string defName, DebugRegionType regionType) regionDebugging =
      ("VF_TestMarshal", DebugRegionType.Regions | DebugRegionType.Links);

    [Conditional("DEBUG")]
    internal static void Init()
    {
      if (!debug)
      {
        Ext_Type.SetStaticFieldsDefault(typeof(DebugProperties));
        return;
      }

      DebugHelper.Local.VehicleDef = DefDatabase<VehicleDef>.GetNamedSilentFail(regionDebugging.defName);
      if (DebugHelper.Local.VehicleDef != null)
      {
        DebugHelper.Local.DebugType = regionDebugging.regionType;
      }
    }
  }
}
