using UnityEngine;

namespace Vehicles;

public class VehicleStatWorker_MoveSpeed : VehicleStatWorker
{
  public override bool ShouldShowFor(VehicleDef vehicleDef)
  {
    if (Mathf.Approximately(vehicleDef.GetStatValueAbstract(VehicleStatDefOf.MoveSpeed), 0))
      return false;
    return base.ShouldShowFor(vehicleDef);
  }
}