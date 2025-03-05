﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Sound;
using Verse.AI;
using Verse.AI.Group;
using SmashTools;
using SmashTools.Performance;

namespace Vehicles
{
	public partial class VehiclePawn
	{
		[Unsaved]
		public VehicleSustainers sustainers;

		private List<TimedExplosion> explosives = new List<TimedExplosion>();

		public override bool Suspended => false; //Vehicles are not suspendable

		public TimedExplosion AddTimedExplosion(IntVec2 cell, int ticks, int radius, DamageDef damageDef, int damageAmount = -1, float armorPenetration = -1, DrawOffsets drawOffsets = null)
		{
			if (damageAmount < 0)
			{
				damageAmount = damageDef.defaultDamage;
			}
			if (armorPenetration < 0)
			{
				armorPenetration = damageDef.defaultArmorPenetration;
			}

			TimedExplosion timedExplosion = new TimedExplosion(this, cell, ticks, radius, damageDef, damageAmount, armorPenetration: armorPenetration, drawOffsets: drawOffsets);
			explosives.Add(timedExplosion);
			return timedExplosion;
		}

		public override void Tick()
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
				return false; //If opportunistic ticking is off, disallow removal from ticker list. VehicleComp should then always tick
			}
			return compTickers.Remove(comp);
		}

		private void TickExplosives()
		{
			if (explosives.Count > 0)
			{
				for (int i = explosives.Count - 1; i >= 0; i--)
				{
					TimedExplosion timedExplosion = explosives[i];
					if (!timedExplosion.Tick())
					{
						explosives.Remove(timedExplosion);
					}
				}
			}
		}

		protected virtual void TickAllComps()
		{
			for (int i = compTickers.Count - 1; i >= 0; i--)
			{
				compTickers[i].CompTick(); //Must run back to front in case CompTick methods trigger their own removal
			}

			if (CompFueledTravel != null)
			{
				CompFueledTravel.LeakTick(); //Tick manually for leak checks that is separate from tick by request.
			}
		}

		public override void TickRare()
		{
			base.TickRare();
			statHandler.MarkAllDirty();
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
        if (Drafted || Deploying)
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
      ageTracker.AgeTick();
      records.RecordsTick();
    }
	}
}
