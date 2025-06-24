using System.Collections.Generic;
using SmashTools.Rendering;
using UnityEngine;
using Verse;

namespace Vehicles.Rendering;

public interface IBlitTarget
{
  (int width, int height) TextureSize(in BlitRequest request);
  IEnumerable<RenderData> GetRenderData(Rect rect, BlitRequest request);
}