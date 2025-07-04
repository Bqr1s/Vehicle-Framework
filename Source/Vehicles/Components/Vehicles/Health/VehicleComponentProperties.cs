﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using RimWorld;
using SmashTools;
using Verse;

namespace Vehicles;

[PublicAPI]
public class VehicleComponentProperties
{
  [NoTranslate]
  public string key;

  public string label;

  public Type compClass;

  public int health;
  public VehicleComponent.VehiclePartDepth depth;
  public float efficiencyWeight = 1;
  public float hitWeight = 1;
  public List<StatModifier> armor;
  public VehicleEMPSeverity empSeverity = VehicleEMPSeverity.None;
  public bool priorityStatEfficiency;

  public ComponentHitbox hitbox = new();
  public List<VehicleStatDef> categories;
  public LinearCurve efficiency;

  [TweakField]
  public List<Reactor> reactors;

  public List<string> tags;

  public virtual T GetReactor<T>() where T : Reactor
  {
    return reactors?.FirstOrDefault(reactor => reactor is T) as T;
  }

  public virtual bool HasReactor<T>() where T : Reactor
  {
    return GetReactor<T>() != null;
  }

  public virtual void ResolveReferences(VehicleDef def)
  {
    efficiency ??=
    [
      new CurvePoint(0, 0),
      new CurvePoint(0.25f, 0f),
      new CurvePoint(0.4f, 0.4f),
      new CurvePoint(0.7f, 0.7f),
      new CurvePoint(0.85f, 1),
      new CurvePoint(1, 1)
    ];
    categories ??= new List<VehicleStatDef>();
    compClass ??= typeof(VehicleComponent);
    hitbox.Initialize(def);
  }

  public virtual IEnumerable<string> ConfigErrors()
  {
    if (key.NullOrEmpty())
    {
      yield return $"{key}: <field>key</field> field must be implemented.".ConvertRichText();
    }
    if (health <= 0)
    {
      yield return $"{key}: <field>health</field> must be greater than 0.".ConvertRichText();
    }
    if (efficiency is { PointsCount: < 5 })
    {
      yield return
        $"{key}: <field>efficiency</field> must include at least 5 points for proper color gradient construction."
         .ConvertRichText();
    }
    if (hitbox is null)
    {
      yield return $"{key}: <field>hitbox</field> must be specified even if it occupies no cells."
       .ConvertRichText();
    }
    if (efficiencyWeight == 0)
    {
      yield return
        $"{key}: <field>efficiencyWeight</field> cannot = 0. If average weight = 0, resulting damage will be NaN, causing an instant-kill on the vehicle.";
    }
  }
}