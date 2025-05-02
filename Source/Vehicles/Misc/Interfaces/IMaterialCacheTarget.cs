using UnityEngine;

namespace Vehicles;

public interface IMaterialCacheTarget
{
  int MaterialCount { get; }

  PatternDef PatternDef { get; }

  string Name { get; }

  MaterialPropertyBlock PropertyBlock { get; }
}