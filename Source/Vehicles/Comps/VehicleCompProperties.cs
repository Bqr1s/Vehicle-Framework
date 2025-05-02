using System.Collections.Generic;
using Verse;

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