using System;

namespace Vehicles;

[Flags]
public enum DebugRegionType
{
  None = 0,
  Regions = 1 << 0,
  Rooms = 1 << 1,
  Links = 1 << 2,
  Weights = 1 << 3,
  PathCosts = 1 << 4,
  References = 1 << 5,
}