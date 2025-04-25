using UnityEngine;
using Verse;

namespace Vehicles
{
  public class MoteThrownExpand : MoteThrown
  {
    public float growthRate;

    protected override void Tick()
    {
      base.Tick();
      linearScale += new Vector3(growthRate, 0, growthRate);
    }
  }
}