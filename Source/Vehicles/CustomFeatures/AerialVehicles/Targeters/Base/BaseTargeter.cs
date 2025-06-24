using System;
using UnityEngine;
using Verse;

namespace Vehicles;

public abstract class BaseTargeter
{
  protected VehiclePawn vehicle;
  protected Action actionWhenFinished;
  protected Texture2D mouseAttachment;

  public abstract bool IsTargeting { get; }

  public abstract void StopTargeting();

  public abstract void ProcessInputEvents();

  public abstract void TargeterOnGUI();

  public abstract void TargeterUpdate();

  protected virtual void OnStart()
  {
    Targeters.PushTargeter(this);
  }

  protected virtual LocalTargetInfo CurrentTargetUnderMouse()
  {
    if (!IsTargeting)
    {
      return LocalTargetInfo.Invalid;
    }
    LocalTargetInfo target = Verse.UI.MouseCell();
    return target;
  }

  public virtual void PostInit()
  {
  }
}