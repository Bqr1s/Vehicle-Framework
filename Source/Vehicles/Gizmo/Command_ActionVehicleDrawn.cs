﻿using RimWorld;
using UnityEngine;
using Vehicles.Rendering;
using Verse;

namespace Vehicles;

public class Command_ActionVehicleDrawn : Command_Action
{
  public VehicleBuildDef buildDef;

  public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
  {
    GizmoResult result = VehicleGui.GizmoOnGUIWithMaterial(this,
      new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f), parms, buildDef);
    if (buildDef.MadeFromStuff)
    {
      Designator_Dropdown.DrawExtraOptionsIcon(topLeft, GetWidth(maxWidth));
    }
    return result;
  }
}