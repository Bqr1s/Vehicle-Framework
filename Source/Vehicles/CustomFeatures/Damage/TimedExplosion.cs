using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.Sound;
using RimWorld;
using SmashTools;
using SmashTools.Performance;
using Verse.AI;

namespace Vehicles
{
  [StaticConstructorOnStartup]
  public class TimedExplosion : IExposable
  {
    private const float WickerFlickerTicks = 3;
    private const int ExplosionNotificationInterval = 60;

    private static readonly int PawnNotifyCellCount = GenRadial.NumCellsInRadius(4.5f);

    private static readonly Material WickMaterialA =
      MaterialPool.MatFrom("Things/Special/BurningWickA", ShaderDatabase.MetaOverlay);

    private static readonly Material WickMaterialB =
      MaterialPool.MatFrom("Things/Special/BurningWickB", ShaderDatabase.MetaOverlay);

    private static readonly float WickAltitude = AltitudeLayer.MetaOverlays.AltitudeFor();

    // internal void Pawn_MindState::Notify_DangerousExploderAboutToExplode(Thing exploder)
    private static readonly MethodInfo Notify_DangerousExploderAboutToExplode = AccessTools.Method(
      typeof(Pawn_MindState),
      "Notify_DangerousExploderAboutToExplode");

    private int ticksLeft;

    private VehiclePawn vehicle;

    private Data data;
    private DrawOffsets drawOffsets;

    private Sustainer wickSoundSustainer;

    private List<Pawn> pawnsNotifiedOfExplosion = [];

    public bool Active { get; private set; }

    public TimedExplosion(VehiclePawn vehicle, Data data,
      DrawOffsets drawOffsets = null)
    {
      this.vehicle = vehicle;
      this.drawOffsets = drawOffsets;
      this.data = data;
      ticksLeft = data.wickTicks;

      Start();
    }

    private IntVec3 AdjustedCell
    {
      get
      {
        IntVec2 adjustedLoc =
          data.cell.RotatedBy(vehicle.Rotation, vehicle.VehicleDef.Size, reverseRotate: true);
        IntVec3 adjustedCell = new(adjustedLoc.x + vehicle.Position.x, 0,
          adjustedLoc.z + vehicle.Position.z);
        return adjustedCell;
      }
    }

    public void DrawAt(Vector3 drawLoc, Rot8 rot)
    {
      if (Active && vehicle.Spawned)
      {
        // Alternate sprite every 3 ticks
        Material material =
          (vehicle.thingIDNumber + Find.TickManager.TicksGame) % (WickerFlickerTicks * 2) <
          WickerFlickerTicks ?
            WickMaterialA :
            WickMaterialB;

        drawLoc.y = WickAltitude;
        if (drawOffsets != null)
        {
          Vector3 offset = drawOffsets.OffsetFor(rot);
          drawLoc += offset;
        }
        else
        {
          IntVec2 rotatedHitCell =
            data.cell.RotatedBy(rot, vehicle.VehicleDef.Size, reverseRotate: true);
          Vector3 position = rotatedHitCell.ToIntVec3.ToVector3();
          Vector3 drawPos = vehicle.DrawPos;
          drawLoc = new Vector3(drawPos.x + position.x, drawLoc.y, drawPos.z + position.z);
        }

        Matrix4x4 matrix = Matrix4x4.TRS(drawLoc, Quaternion.identity, Vector3.one);
        Graphics.DrawMesh(MeshPool.plane20, matrix, material, 0);
      }
    }

    private void Start()
    {
      if (!Active)
      {
        Active = true;
        StartWickSustainer();
        NotifyPawnsOfExplosion();

        if (ticksLeft <= 0)
        {
          Explode();
        }
      }
    }

    private void End()
    {
      Active = false;
      wickSoundSustainer?.End();

      foreach (Pawn pawn in pawnsNotifiedOfExplosion)
      {
        if (pawn.mindState != null && pawn.mindState.knownExploder == vehicle)
        {
          pawn.mindState.knownExploder = null;
        }
      }
    }

    public bool Tick()
    {
      if (!Active || !vehicle.Spawned)
      {
        return false;
      }

      if (ticksLeft % ExplosionNotificationInterval == 0)
      {
        NotifyPawnsOfExplosion();
      }

      UpdateWick();
      if (ticksLeft <= 0)
      {
        Explode();
      }

      ticksLeft--;
      return true;
    }

    private void NotifyPawnsOfExplosion()
    {
      if (!data.notifyNearbyPawns) return;

      for (int index1 = 0; index1 < PawnNotifyCellCount; ++index1)
      {
        IntVec3 notifyAtCell = AdjustedCell + GenRadial.RadialPattern[index1];
        if (notifyAtCell.InBounds(vehicle.MapHeld))
        {
          //using ListSnapshot<Thing> thingList = new(notifyAtCell.GetThingList(vehicle.MapHeld));
          foreach (Thing thing in notifyAtCell.GetThingList(vehicle.MapHeld))
          {
            if (thing is Pawn pawn && CanNotifyPawn(pawn))
            {
              Room room = pawn.GetRoom();
              if (room == null || room.CellCount == 1 || room == vehicle.GetRoom() &&
                GenSight.LineOfSightToThing(pawn.Position, vehicle, vehicle.MapHeld, true))
              {
                pawnsNotifiedOfExplosion.Add(pawn);
                Notify_DangerousExploderAboutToExplode.Invoke(pawn.mindState, [vehicle]);
              }
            }
          }
        }
      }

      return;

      bool CanNotifyPawn(Pawn pawn)
      {
        return pawn.RaceProps.intelligence >= Intelligence.Humanlike &&
          data.damageDef.ExternalViolenceFor(pawn);
      }
    }

    private void UpdateWick()
    {
      if (wickSoundSustainer == null)
      {
        StartWickSustainer();
      }
      else
      {
        wickSoundSustainer.Maintain();
      }
    }

    private void StartWickSustainer()
    {
      SoundInfo info = SoundInfo.InMap(vehicle, MaintenanceType.PerTick);
      wickSoundSustainer = SoundDefOf.HissSmall.TrySpawnSustainer(info);
    }

    private void Explode()
    {
      End();
      GenExplosion.DoExplosion(AdjustedCell, vehicle.Map, data.radius, data.damageDef, vehicle,
        data.damageAmount,
        data.armorPenetration);
    }

    public void ExposeData()
    {
      Scribe_References.Look(ref vehicle, nameof(vehicle));
      Scribe_Deep.Look(ref data, nameof(data));
      Scribe_Values.Look(ref ticksLeft, nameof(ticksLeft));
    }

    public class Data : IExposable
    {
      public IntVec2 cell;
      public int wickTicks;
      public int radius;
      public DamageDef damageDef;
      public int damageAmount;
      public float armorPenetration = -1;
      public bool notifyNearbyPawns;

      public Data(IntVec2 cell, int wickTicks, int radius,
        DamageDef damageDef, int damageAmount, float armorPenetration = -1,
        bool notifyNearbyPawns = true)
      {
        this.cell = cell;
        this.wickTicks = wickTicks;
        this.radius = radius;
        this.damageDef = damageDef;
        this.damageAmount = damageAmount;
        this.armorPenetration = armorPenetration;
        this.notifyNearbyPawns = notifyNearbyPawns;

        if (this.armorPenetration < 0)
        {
          this.armorPenetration = damageDef.defaultArmorPenetration;
        }
      }

      public void ExposeData()
      {
        Scribe_Values.Look(ref cell, nameof(cell));
        Scribe_Values.Look(ref wickTicks, nameof(wickTicks));
        Scribe_Values.Look(ref radius, nameof(radius));
        Scribe_Defs.Look(ref damageDef, nameof(damageDef));
        Scribe_Values.Look(ref damageAmount, nameof(damageAmount));
        Scribe_Values.Look(ref armorPenetration, nameof(armorPenetration), defaultValue: -1);
        Scribe_Values.Look(ref notifyNearbyPawns, nameof(notifyNearbyPawns));
      }
    }
  }
}