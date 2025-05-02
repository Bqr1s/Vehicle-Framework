using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Vehicles
{
  public partial class VehiclePawn
  {
    [Unsaved]
    public VehicleSustainers sustainers;

    private List<TimedExplosion> explosives = [];

    public override bool Suspended => false; //Vehicles are not suspendable

    public int AttachedExplosives => explosives.Count;

    public TimedExplosion AddTimedExplosion(TimedExplosion.Data explosionData,
      DrawOffsets drawOffsets = null)
    {
      TimedExplosion timedExplosion = new(this, explosionData, drawOffsets: drawOffsets);
      explosives.Add(timedExplosion);
      DrawTracker.AddRenderer(timedExplosion);
      return timedExplosion;
    }

    // TODO 1.6 - Refactor
    public void Tick_TEMP()
    {
      Tick();
    }

    protected override void Tick()
    {
      BaseTickOptimized();
      TickAllComps();
      if (Faction != Faction.OfPlayer)
      {
        vehicleAI?.AITick();
      }

      if (AllPawnsAboard.Count > 0)
      {
        TrySatisfyPawnNeeds();
      }
    }

    public bool RequestTickStart<T>(T comp) where T : ThingComp
    {
      if (!compTickers.Contains(comp))
      {
        compTickers.Add(comp);
        return true;
      }
      return false;
    }

    public bool RequestTickStop<T>(T comp) where T : ThingComp
    {
      if (!VehicleMod.settings.main.opportunisticTicking)
      {
        // If opportunistic ticking is off, disallow removal from ticker list.
        // VehicleComp should then always tick.
        return false;
      }
      return compTickers.Remove(comp);
    }

    private void TickExplosives()
    {
      for (int i = explosives.Count - 1; i >= 0; i--)
      {
        TimedExplosion timedExplosion = explosives[i];
        if (!timedExplosion.Tick())
        {
          explosives.Remove(timedExplosion);
          DrawTracker.RemoveRenderer(timedExplosion);
        }
      }
    }

    protected virtual void TickAllComps()
    {
      for (int i = compTickers.Count - 1; i >= 0; i--)
      {
        // Must run back to front in case CompTick methods trigger their own removal
        compTickers[i].CompTick();
      }
      // TODO - should check leaking when vehicle takes damage
      // Leak tick is separate from tick by request so the fuel can continue to leak even if
      // the comp itself does not need to be ticking.
      CompFueledTravel?.LeakTick();
    }

    public override void TickRare()
    {
      base.TickRare();
      statHandler.MarkAllDirty();
    }

    protected override void TickInterval(int delta)
    {
      ageTracker.AgeTickInterval(delta);
      records.RecordsTickInterval(delta);
    }

    protected virtual void BaseTickOptimized()
    {
      if (Find.TickManager.TicksGame % 250 == 0)
      {
        TickRare();
      }
      //if (Suspended) return; // Vehicles can't be suspended, unsure if I'll implement such a feature.

      sustainers.Tick();
      if (Spawned)
      {
        animator?.AnimationTick();
        vehiclePather.PatherTick();
        stances.StanceTrackerTick();
        if (Drafted || CompVehicleTurrets is { Deploying: true })
        {
          jobs.JobTrackerTick();
        }

        TickHandlers();
        TickExplosives();
        if (currentlyFishing && Find.TickManager.TicksGame % 240 == 0)
        {
          if (AllPawnsAboard.Count == 0)
          {
            currentlyFishing = false;
          }
          else
          {
            IntVec3 cell = this.OccupiedRect().ExpandedBy(1).EdgeCells.RandomElement();
            MoteMaker.MakeStaticMote(cell, Map, ThingDefOf_VehicleMotes.Mote_FishingNet);
          }
        }
      }
      //equipment?.EquipmentTrackerTick();

      //caller?.CallTrackerTick();
      //skills?.SkillsTick();
      //abilities?.AbilitiesTick();
      inventory?.InventoryTrackerTick();
      //relations?.RelationsTrackerTick();

      if (ModsConfig.RoyaltyActive)
      {
        //royalty?.RoyaltyTrackerTick();
      }
    }
  }
}