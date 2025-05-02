﻿using System;
using System.Collections.Generic;
using System.Linq;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using Verse;

namespace Vehicles;

public partial class VehiclePawn
{
  [Unsaved]
  private bool fetchedCompVehicleTurrets;

  [Unsaved]
  private bool fetchedCompFuel;

  [Unsaved]
  private bool fetchedCompUpgradeTree;

  [Unsaved]
  private bool fetchedCompVehicleLauncher;

  [Unsaved]
  private CompVehicleTurrets compVehicleTurrets;

  [Unsaved]
  private CompFueledTravel compFuel;

  [Unsaved]
  private CompUpgradeTree compUpgradeTree;

  [Unsaved]
  private CompVehicleLauncher compVehicleLauncher;

  [Unsaved]
  private SelfOrderingList<ThingComp> cachedComps = [];

  [Unsaved]
  private List<ThingComp> deactivatedComps = [];

  [Unsaved]
  private List<ThingComp> compTickers = [];

  private List<ActivatableThingComp> activatableComps = [];
  private List<Type> deactivatedCompTypes = [];

  public CompVehicleTurrets CompVehicleTurrets
  {
    get
    {
      if (!fetchedCompVehicleTurrets)
      {
        compVehicleTurrets = GetCachedComp<CompVehicleTurrets>();
        fetchedCompVehicleTurrets = true;
      }
      return compVehicleTurrets;
    }
  }

  public CompFueledTravel CompFueledTravel
  {
    get
    {
      if (!fetchedCompFuel)
      {
        compFuel = GetCachedComp<CompFueledTravel>();
        fetchedCompFuel = true;
      }
      return compFuel;
    }
  }

  public CompUpgradeTree CompUpgradeTree
  {
    get
    {
      if (!fetchedCompUpgradeTree)
      {
        compUpgradeTree = GetCachedComp<CompUpgradeTree>();
        fetchedCompUpgradeTree = true;
      }
      return compUpgradeTree;
    }
  }

  public CompVehicleLauncher CompVehicleLauncher
  {
    get
    {
      if (!fetchedCompVehicleLauncher)
      {
        compVehicleLauncher = GetCachedComp<CompVehicleLauncher>();
        fetchedCompVehicleLauncher = true;
      }
      return compVehicleLauncher;
    }
  }

  private void CacheCompRenderers()
  {
    foreach (ThingComp thingComp in AllComps)
    {
      if (thingComp is IParallelRenderer parallelRenderer)
        DrawTracker.AddRenderer(parallelRenderer);
    }
  }

  public void AddComp(ThingComp thingComp)
  {
    AllComps.Add(thingComp);
    if (thingComp is IParallelRenderer parallelRenderer)
      DrawTracker.AddRenderer(parallelRenderer);
    RecacheComponents();
  }

  public bool RemoveComp(ThingComp thingComp)
  {
    bool result = AllComps.Remove(thingComp);
    if (result)
    {
      if (thingComp is IParallelRenderer parallelRenderer)
        DrawTracker.RemoveRenderer(parallelRenderer);
      RecacheComponents();
    }
    return result;
  }

  public void ActivateComp(ThingComp comp)
  {
    ActivatableThingComp activatableComp =
      activatableComps.FirstOrDefault(activatableComp => activatableComp.Type == comp.GetType());
    if (activatableComp == null)
    {
      activatableComp = new ActivatableThingComp(this);
      activatableComp.Init(comp);
      activatableComps.Add(activatableComp);
    }
    activatableComp.Owners++;
  }

  public void DeactivateComp(ThingComp comp)
  {
    foreach (ActivatableThingComp activatableComp in activatableComps)
    {
      if (activatableComp.Type == comp.GetType())
      {
        activatableComp.Owners--;
        return;
      }
    }
  }

  public T GetCachedComp<T>() where T : ThingComp
  {
    for (int i = 0; i < cachedComps.Count; i++)
    {
      if (cachedComps[i] is T t)
      {
        cachedComps.CountIndex(i);
        return t;
      }
    }
    return null;
  }

  public ThingComp GetComp(Type type)
  {
    // AllComps should always be initialized to new instance list, and never be null
    foreach (ThingComp thingComp in AllComps)
    {
      if (thingComp.GetType().SameOrSubclass(type))
        return thingComp;
    }
    return null;
  }

  public ThingComp GetDeactivatedComp(Type type)
  {
    // AllComps should always be initialized to new instance list, and never be null
    foreach (ThingComp thingComp in deactivatedComps)
    {
      if (thingComp.GetType().SameOrSubclass(type))
        return thingComp;
    }
    return null;
  }

  protected virtual void RecacheComponents()
  {
    fetchedCompVehicleTurrets = false;
    fetchedCompFuel = false;
    fetchedCompUpgradeTree = false;
    fetchedCompVehicleLauncher = false;

    cachedComps.Clear();
    if (!AllComps.NullOrEmpty())
    {
      cachedComps.AddRange(AllComps);
    }
    RecacheCompTickers();
  }

  private void RecacheCompTickers()
  {
    compTickers.Clear();
    foreach (ThingComp thingComp in AllComps)
    {
      if (!(thingComp is VehicleComp vehicleComp) || !vehicleComp.TickByRequest)
      {
        compTickers.Add(thingComp);
      }
    }
  }

  private void SyncActivatableComps()
  {
    foreach (ActivatableThingComp activatableComp in activatableComps)
    {
      ThingComp matchingComp =
        AllComps.FirstOrDefault(thingComp => thingComp.GetType() == activatableComp.Type);
      if (matchingComp == null)
      {
        Log.Error($"Unable to sync {activatableComp.Type}. No matching comp in comp list.");
        continue;
      }
      activatableComp.Init(matchingComp);
      activatableComp.RevalidateCompStatus();
    }
  }

  private class ActivatableThingComp : IExposable
  {
    [Unsaved]
    private readonly VehiclePawn vehicle;

    [Unsaved]
    private ThingComp comp;

    private int owners;
    private Type type;

    public ActivatableThingComp(VehiclePawn vehicle)
    {
      this.vehicle = vehicle;
    }

    private bool Deactivated => owners == 0;

    public Type Type => type;

    public int Owners
    {
      get { return owners; }
      set
      {
        if (owners != value)
        {
          owners = Mathf.Clamp(value, 0, int.MaxValue);
          RevalidateCompStatus();
        }
      }
    }

    public void RevalidateCompStatus()
    {
      if (Deactivated)
      {
        if (vehicle.RemoveComp(comp))
        {
          vehicle.deactivatedComps.Add(comp);
          vehicle.deactivatedCompTypes.Add(comp.GetType());
          vehicle.activatableComps.Remove(this);
        }
      }
      else if (!vehicle.AllComps.Contains(comp))
      {
        vehicle.AddComp(comp);
      }
    }

    public void Init(ThingComp comp)
    {
      this.comp = comp;
      type = comp.GetType();
    }

    public void ExposeData()
    {
      Scribe_Values.Look(ref owners, nameof(owners));
      Scribe_Values.Look(ref type, nameof(type));
    }
  }
}