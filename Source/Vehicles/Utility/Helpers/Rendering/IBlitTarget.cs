using System.Collections.Generic;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;

namespace Vehicles.Rendering;

public interface IBlitTarget
{
  IEnumerable<RenderData> GetRenderData(Rect rect, BlitRequest request);
}