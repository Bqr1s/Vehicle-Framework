using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmashTools.Performance;
using Verse;

namespace Vehicles;

public class AsyncConnectionAction : AsyncAction
{
  private VehicleRegionConnector connector;
  private VehicleRegion region;

  public override bool IsValid => region is
    { InPool: false, valid: true, Map.Disposed: false };

  public void Set(VehicleRegionConnector connector, VehicleRegion region)
  {
    this.connector = connector;
    this.region = region;
  }

  public override void Invoke()
  {
    connector.RecalculateWeights(region);
  }

  public override void ReturnToPool()
  {
    connector = null;
    region = null;
    AsyncPool<AsyncConnectionAction>.Return(this);
  }
}