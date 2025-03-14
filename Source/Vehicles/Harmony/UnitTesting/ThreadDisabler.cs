﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmashTools;
using Verse;

namespace Vehicles
{
  /// <summary>
  /// RAII pattern for suspending dedicated thread activity on VehicleMapping components.
  /// This does not stop or abort the threads, it only flags the thread as being unavailable
  /// so that further actions are executed synchronously rather than getting enqueued to
  /// the dedicated thread.
  /// </summary>
  public readonly struct ThreadDisabler : IDisposable
  {
    private readonly Dictionary<Map, bool> activateThread = [];

    public ThreadDisabler()
    {
      // Need to disable from main thread, Find.Maps is not thread safe
      Assert.IsTrue(UnityData.IsInMainThread);

      foreach (Map map in Find.Maps)
      {
        VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
        if (mapping.ThreadAlive)
        {
          activateThread[map] = !mapping.dedicatedThread.Suspended;
          mapping.dedicatedThread.Suspended = true;
        }
      }
    }

    void IDisposable.Dispose()
    {
      foreach (Map map in Find.Maps)
      {
        VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
        if (mapping.ThreadAlive && activateThread.TryGetValue(map, out bool activate) && activate)
        {
          mapping.dedicatedThread.Suspended = false;
        }
      }
    }
  }
}
