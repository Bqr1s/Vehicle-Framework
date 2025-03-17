﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vehicles
{
  public abstract class VehicleGridManager
  {
    protected readonly VehicleMapping mapping;
    protected readonly VehicleDef createdFor;

    public VehicleGridManager(VehicleMapping mapping, VehicleDef createdFor)
    {
      this.mapping = mapping;
      this.createdFor = createdFor;
    }

    public virtual void PostInit()
    {
    }
  }
}