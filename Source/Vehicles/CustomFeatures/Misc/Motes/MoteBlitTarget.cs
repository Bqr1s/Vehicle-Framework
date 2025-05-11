using System;
using System.Collections.Generic;
using SmashTools.Performance;
using SmashTools.Rendering;
using UnityEngine;
using Verse;
using Object = UnityEngine.Object;

namespace Vehicles;

public class MoteBlitTarget : MoteThrown, IDisposable
{
  private RenderTexture renderTexture;

  private List<IParallelRenderer> renderers;

  public void Init(List<IParallelRenderer> renderers)
  {
    this.renderers = [.. renderers];
  }

  private void Blit()
  {
    foreach (IParallelRenderer renderer in renderers)
    {
    }
  }

  public override void SpawnSetup(Map map, bool respawningAfterLoad)
  {
    base.SpawnSetup(map, respawningAfterLoad);
    UnityThread.ExecuteOnMainThread(Blit);
  }

  void IDisposable.Dispose()
  {
    renderTexture.Release();
    Object.Destroy(renderTexture);
  }
}