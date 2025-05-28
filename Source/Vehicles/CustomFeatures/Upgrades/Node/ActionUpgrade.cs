﻿using System.Collections.Generic;
using JetBrains.Annotations;
using SmashTools;
using Verse;

namespace Vehicles;

[PublicAPI]
public class ActionUpgrade : Upgrade
{
  private List<DynamicDelegate<VehiclePawn>> unlockMethods;

  private List<DynamicDelegate<VehiclePawn>> refundMethods;

  private bool unlockOnLoad;

  public override bool UnlockOnLoad => unlockOnLoad;

  public override void Unlock(VehiclePawn vehicle, bool unlockingPostLoad)
  {
    if (!unlockMethods.NullOrEmpty())
    {
      foreach (DynamicDelegate<VehiclePawn> method in unlockMethods)
      {
        method.Invoke(null, vehicle);
      }
    }
  }

  public override void Refund(VehiclePawn vehicle)
  {
    if (!refundMethods.NullOrEmpty())
    {
      foreach (DynamicDelegate<VehiclePawn> method in refundMethods)
      {
        method.Invoke(null, vehicle);
      }
    }
  }
}