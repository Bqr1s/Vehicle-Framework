using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace Vehicles
{
  // TODO 1.6 - rename to fit naming style
  public static class PawnKindDefGenerator_Vehicles
  {
    public static bool GenerateImpliedPawnKindDef(VehicleDef vehicleDef, out PawnKindDef kindDef,
      bool hotReload)
    {
      kindDef = vehicleDef.kindDef;
      if (kindDef == null)
      {
        string defName = vehicleDef.defName + "_PawnKind";
        kindDef = !hotReload ?
                    new PawnKindDef() :
                    DefDatabase<PawnKindDef>.GetNamed(defName, false) ?? new PawnKindDef();
        kindDef.defName = defName;
        kindDef.label = vehicleDef.label;
        kindDef.description = vehicleDef.description;
        kindDef.combatPower = vehicleDef.combatPower;
        kindDef.race = vehicleDef;
        kindDef.ignoresPainShock = true;
        kindDef.lifeStages = [new PawnKindLifeStage() { bodyGraphicData = vehicleDef.graphicData }];
        vehicleDef.kindDef = kindDef;
        return true;
      }

      return false;
    }
  }
}