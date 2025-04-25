﻿using RimWorld;
using UnityEngine;
using Verse;

namespace Vehicles;

public class Designator_AreaRoadExpand : Designator_AreaRoad
{
  public Designator_AreaRoadExpand() : base(DesignateMode.Add)
  {
    defaultLabel = "VF_RoadZoneExpand".Translate();
    defaultDesc = "VF_RoadZoneExpandDesc".Translate();
    icon = ContentFinder<Texture2D>.Get("UI/Designators/RoadAreaOn");
    soundDragSustain = SoundDefOf.Designate_DragAreaAdd;
    soundDragChanged = SoundDefOf.Designate_DragZone_Changed;
    soundSucceeded = SoundDefOf.Designate_ZoneAdd_AllowedArea;
  }
}