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
using SmashTools.Animations;
using System.Reflection;

namespace Vehicles
{
	public partial class VehiclePawn : Pawn, IInspectable, IAnimationTarget, IAnimator, IEventManager<VehicleEventDef>, IMaterialCacheTarget
	{
		public bool Initialized { get; private set; }

		public EventManager<VehicleEventDef> EventRegistry { get; set; }

		public VehicleDef VehicleDef => def as VehicleDef;

		public VehiclePawn()
		{
		}

		public Pawn FindPawnWithBestStat(StatDef stat, Predicate<Pawn> pawnValidator = null)
		{
			Pawn pawn = null;
			float num = -1f;
			List<Pawn> pawnsListForReading = AllPawnsAboard;
			for (int i = 0; i < pawnsListForReading.Count; i++)
			{
				Pawn pawn2 = pawnsListForReading[i];
				if (!pawn2.Dead && !pawn2.Downed && !pawn2.InMentalState && CaravanUtility.IsOwner(pawn2, Faction) && !stat.Worker.IsDisabledFor(pawn2) && (pawnValidator is null || pawnValidator(pawn2)))
				{
					float statValue = pawn2.GetStatValue(stat, true);
					if (pawn == null || statValue > num)
					{
						pawn = pawn2;
						num = statValue;
					}
				}
			}
			return pawn;
		}

		public int AverageSkillOfCapablePawns(SkillDef skill)
		{
			if (AllCapablePawns.Count == 0)
			{
				return 0;
			}
			int value = 0;
			foreach (Pawn p in AllCapablePawns)
			{
				value += p.skills.GetSkill(skill).Level;
			}
			value /= AllCapablePawns.Count;
			return value;
		}

		private void InitializeVehicle()
		{
			if (handlers != null && handlers.Count > 0)
			{
				return;
			}
			if (cargoToLoad is null)
			{
				cargoToLoad = new List<TransferableOneWay>();
			}
			if (bills is null)
			{
				bills = new List<Bill_BoardVehicle>();
			}

			//navigationCategory = VehicleDef.defaultNavigation;

			if (!VehicleDef.properties.roles.NullOrEmpty())
			{
				foreach (VehicleRole role in VehicleDef.properties.roles)
				{
					handlers.Add(new VehicleHandler(this, role));
				}
			}

			RecacheComponents();
		}

		public override void PostMapInit()
		{
			vehiclePather.TryResumePathingAfterLoading();
		}

		public virtual void PostGenerationSetup()
		{
			InitializeVehicle();
			ageTracker.AgeBiologicalTicks = 0;
			ageTracker.AgeChronologicalTicks = 0;
			ageTracker.BirthAbsTicks = 0;
			health.Reset();
			statHandler.InitializeComponents();
			if (Faction != Faction.OfPlayer && VehicleDef.npcProperties != null)
			{
				GenerateInventory();
			}
		}

		public override void PostMake()
		{
			base.PostMake();
			this.EnsureUncachedCompList();
		}

		private void GenerateInventory()
		{
			if (VehicleDef.npcProperties?.raidParams?.inventory != null)
			{
				foreach (PawnInventoryOption inventoryOption in VehicleDef.npcProperties.raidParams.inventory)
				{
					foreach (Thing thing in inventoryOption.GenerateThings())
					{
						inventory.innerContainer.TryAdd(thing);
					}
				}
			}
		}

		/// <summary>
		/// Executes after vehicle has been loaded into the game
		/// </summary>
		/// <remarks>Called regardless if vehicle is spawned or unspawned. Responsible for important variables being set that may be called even for unspawned vehicles</remarks>
		protected virtual void PostLoad()
		{
			this.RegisterEvents(); //Events must be registered before comp post loads, SpawnSetup won't trigger register in this case
			RegenerateUnsavedComponents();
			RecacheComponents();
			RecachePawnCount();
			foreach (VehicleComp comp in AllComps.Where(t => t is VehicleComp))
			{
				comp.PostLoad();
			}
		}

		protected virtual void RegenerateUnsavedComponents()
		{
			vehicleAI = new VehicleAI(this);
			vDrawer = new Vehicle_DrawTracker(this);
			graphicOverlay = new VehicleGraphicOverlay(this);
			sustainers ??= new VehicleSustainers(this);
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			this.RegisterEvents(); //Must register before comps call SpawnSetup to allow comps to access Registry
			base.SpawnSetup(map, respawningAfterLoad);
			
			if (!UnityData.IsInMainThread)
			{
				LongEventHandler.ExecuteWhenFinished(graphicOverlay.Init);
			}
			else
			{
				graphicOverlay.Init();
			}
			if (VehicleDef.drawProperties.controller != null)
			{
				animator = new AnimationManager(this, VehicleDef.drawProperties.controller);
			}
			ReleaseSustainerTarget(); //Ensure SustainerTarget and sustainer manager is given a clean slate to work with
			EventRegistry[VehicleEventDefOf.Spawned].ExecuteEvents();
			if (Drafted)
			{
				EventRegistry[VehicleEventDefOf.IgnitionOn].ExecuteEvents(); //Retrigger draft event if spawned with draft status = on (important for sustainers, tick requests, etc.)
			}

			sharedJob ??= new SharedJob();
			if (!respawningAfterLoad)
			{
				vehiclePather.ResetToCurrentPosition();
			}

			if (Faction != Faction.OfPlayer)
			{
				ignition.Drafted = true;
				CompVehicleTurrets turretComp = CompVehicleTurrets;
				if (turretComp != null)
				{
					foreach (VehicleTurret turret in turretComp.turrets)
					{
						turret.autoTargeting = true;
						turret.AutoTarget = true;
					}
				}
			}

			RecachePawnCount();

			foreach (Pawn pawn in AllPawnsAboard)
			{
				if (pawn.IsWorldPawn())
				{
					Find.WorldPawns.RemovePawn(pawn); //Remove internal pawns from WorldPawns
				}
			}
			foreach (Thing thing in inventory.innerContainer)
			{
				if (thing is Pawn pawn)
				{
					Find.WorldPawns.RemovePawn(pawn); //Remove inventory pawns in case some were transfered here (like animals)
				}
			}

			UpdateRotationAndAngle();

			Drawer.Notify_Spawned();
			InitializeHitbox();
			Map.GetCachedMapComponent<VehicleMapping>().VehicleSpawned(this);
			Map.GetCachedMapComponent<VehiclePositionManager>().ClaimPosition(this);
			//Map.GetCachedMapComponent<VehicleRegionUpdateCatalog>().Notify_VehicleSpawned(this);
			Map.GetCachedMapComponent<ListerVehiclesRepairable>().Notify_VehicleSpawned(this);
			ResetRenderStatus();

			Initialized = true;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			
			Scribe_Collections.Look(ref activatableComps, nameof(activatableComps), lookMode: LookMode.Deep);
			activatableComps ??= new List<ActivatableThingComp>();
			if (Scribe.mode == LoadSaveMode.LoadingVars)
			{
				SyncActivatableComps();
			}

			if (!deactivatedComps.NullOrEmpty())
			{
				foreach (ThingComp comp in deactivatedComps)
				{
					comp.PostExposeData();
				}
			}

			Scribe_Deep.Look(ref vehiclePather, nameof(vehiclePather), [this]);
			Scribe_Deep.Look(ref ignition, nameof(ignition), [this]);
			Scribe_Deep.Look(ref statHandler, nameof(statHandler), [this]);
			Scribe_Deep.Look(ref sharedJob, nameof(sharedJob));
			Scribe_Deep.Look(ref animator, nameof(animator));

			Scribe_Values.Look(ref angle, nameof(angle));
			Scribe_Values.Look(ref reverse, nameof(reverse));
			Scribe_Values.Look(ref crashLanded, nameof(crashLanded));

			Scribe_Deep.Look(ref patternData, nameof(patternData));
			Scribe_Defs.Look(ref retextureDef, nameof(retextureDef));
			Scribe_Deep.Look(ref patternToPaint, nameof(patternToPaint));

			if (!VehicleMod.settings.main.useCustomShaders)
			{
				patternData = new PatternData(VehicleDef.graphicData.color, 
											  VehicleDef.graphicData.colorTwo, 
											  VehicleDef.graphicData.colorThree,
											  PatternDefOf.Default, Vector2.zero, 0);
				retextureDef = null;
				patternToPaint = null;
			}

			Scribe_Values.Look(ref movementStatus, nameof(movementStatus), VehicleMovementStatus.Online);
			//Scribe_Values.Look(ref navigationCategory, nameof(navigationCategory), NavigationCategory.Opportunistic);
			Scribe_Values.Look(ref currentlyFishing, nameof(currentlyFishing), false);
			Scribe_Values.Look(ref showAllItemsOnMap, nameof(showAllItemsOnMap));

			Scribe_Collections.Look(ref cargoToLoad, nameof(cargoToLoad), lookMode: LookMode.Deep);

			Scribe_Collections.Look(ref handlers, nameof(handlers), LookMode.Deep);
			Scribe_Collections.Look(ref bills, nameof(bills), LookMode.Deep);

			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				animator?.Init(this, VehicleDef.drawProperties?.controller);

				this.EnsureUncachedCompList();
				PostLoad();
			}
		}
	}
}
