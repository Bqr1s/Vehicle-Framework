﻿using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace Vehicles;

[VehicleSettingsClass]
public abstract class VehicleCompProperties : CompProperties
{
  public virtual IEnumerable<VehicleStatDef> StatCategoryDefs()
  {
    yield break;
  }

  public virtual void PostDefDatabase()
  {
  }
}