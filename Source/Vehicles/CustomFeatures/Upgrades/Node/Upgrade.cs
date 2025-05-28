﻿using System.Collections.Generic;
using JetBrains.Annotations;

namespace Vehicles;

[PublicAPI]
public abstract class Upgrade
{
  protected UpgradeNode node;

  public abstract bool UnlockOnLoad { get; }

  public virtual bool HasGraphics => false;

  public virtual IEnumerable<string> ConfigErrors
  {
    get { yield break; }
  }

  public virtual IEnumerable<UpgradeTextEntry> UpgradeDescription(VehiclePawn vehicle)
  {
    yield break;
  }

  /// <summary>
  /// Called when node has upgraded fully, after upgrade build ticks hits 0 or triggered by god mode
  /// </summary>
  public abstract void Unlock(VehiclePawn vehicle, bool unlockingPostLoad);

  /// <summary>
  /// Undo Upgrade action. Should be polar opposite of Upgrade functionality to revert changes
  /// </summary>
  public abstract void Refund(VehiclePawn vehicle);

  public virtual void PostLoad()
  {
  }

  public virtual void Init(UpgradeNode node)
  {
    this.node = node;
  }
}