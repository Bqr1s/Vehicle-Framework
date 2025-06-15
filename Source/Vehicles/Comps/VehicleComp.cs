﻿using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using SmashTools;
using SmashTools.Rendering;
using Verse;
using Verse.AI;

namespace Vehicles;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class VehicleComp : ThingComp
{
  public VehiclePawn Vehicle => parent as VehiclePawn;

  /// <summary>
  /// If true, must request to start / stop ticking caller
  /// </summary>
  public virtual bool TickByRequest => false;

  public virtual IEnumerable<AnimationDriver> Animations { get; }

  public virtual IEnumerable<Gizmo> CompCaravanGizmos()
  {
    yield break;
  }

  public virtual IEnumerable<FloatMenuOption> CompFloatMenuOptions()
  {
    yield break;
  }

  public virtual void CompCaravanInspectString(StringBuilder stringBuilder)
  {
  }

  public virtual void PostLoad()
  {
  }

  public virtual void OnDestroy()
  {
  }

  public virtual void PostDrawUnspawned(ref readonly TransformData transform)
  {
  }

  /// <summary>
  /// Called when newly generated, unlike PostSpawnSetup called every time it is spawned in-map
  /// </summary>
  public virtual void PostGeneration()
  {
  }

  public virtual void EventRegistration()
  {
  }

  public virtual void SpawnedInGodMode()
  {
  }

  public override void Notify_ColorChanged()
  {
  }

  public virtual bool CanDraft(out string failReason, out bool allowDevMode)
  {
    failReason = string.Empty;
    allowDevMode = true;
    return true;
  }

  public virtual bool IsThreat(IAttackTargetSearcher searcher)
  {
    return false;
  }

  public virtual void StartTicking()
  {
    if (TickByRequest)
    {
      Vehicle.RequestTickStart(this);
    }
  }

  public virtual void StopTicking()
  {
    if (TickByRequest)
    {
      Vehicle.RequestTickStop(this);
    }
  }
}