using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmashTools;
using Verse;

namespace Vehicles
{
  public readonly struct ThreadDisabler : IDisposable
  {
    public ThreadDisabler()
    {
      // Need to disable from main thread, map list is not thread safe
      Assert.IsTrue(UnityData.IsInMainThread);
      PauseAllThreads();
    }

    private void PauseAllThreads()
    {
      foreach (Map map in Find.Maps)
      {
        VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
        if (mapping.ThreadAlive)
        {
          mapping.dedicatedThread.Suspended = true;
        }
      }
    }

    void IDisposable.Dispose()
    {
      foreach (Map map in Find.Maps)
      {
        VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
        if (mapping.ThreadAlive)
        {
          mapping.dedicatedThread.Suspended = false;
        }
      }
    }
  }
}
