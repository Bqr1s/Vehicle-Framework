﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using SmashTools;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Vehicles
{
	[HeaderTitle(Label = nameof(CompFueledTravel))]
	public class CompFueledTravel : VehicleComp, IRefundable
	{
		public const float FuelPerLeak = 1;
		public const float TicksPerLeakCheck = 120;
		public const float MaxTicksPerLeak = 400;

		public const float EfficiencyTickMultiplier = 1f / GenDate.TicksPerDay;
		public const float EfficiencyIdleMultiplier = 0.5f;
		public const float CellOffsetIntVec3ToVector3 = 0.5f;
		public const float TicksToCharge = 120;

		private static MethodInfo powerNetMethod;

		[Unsaved]	
		private bool leaking;

		private float fuel;
		private float targetFuelLevel; //TODO 1.6 - remove
		private float targetFuelPercent = -1;

		private bool terminateMotes = false;
		private Vector3 motePosition;
		private float offsetX;
		private float offsetZ;

		private CompPower connectedPower;
		private bool postLoadReconnect;

		public List<(VehicleComponent component, Reactor_FuelLeak fuelLeak)> FuelComponents { get; private set; }

		public CompProperties_FueledTravel Props => props as CompProperties_FueledTravel;

		public override bool TickByRequest => true;

		//Fuel Tank
		public float Fuel => fuel;

		public float FuelPercent => Fuel / FuelCapacity;

		public bool EmptyTank => Fuel <= 0f;

		public bool FullTank => Mathf.Approximately(fuel, TargetFuelLevel);

		public int FuelCountToFull => Mathf.CeilToInt(TargetFuelLevel - Fuel);

		public float TargetFuelPercent
		{
			get
			{
				return targetFuelPercent;
			}
			set
			{
				if (targetFuelPercent != value)
				{
					targetFuelPercent = value;
				}
			}
		}

		public float TargetFuelLevel => targetFuelPercent * FuelCapacity;

		public float FuelPercentOfTarget
		{
			get
			{
				if (TargetFuelLevel == 0)
				{
					return 0;
				}
				return fuel / TargetFuelLevel;
			}
		}

		public bool FuelLeaking => leaking;

		//Fuel Consumption
		public float ConsumptionRatePerTick => FuelEfficiency * EfficiencyTickMultiplier;
		public FuelConsumptionCondition FuelCondition => Props.fuelConsumptionCondition;
		public bool ShouldAutoRefuelNow => FuelPercentOfTarget <= Props.autoRefuelPercent && !FullTank && TargetFuelLevel > 0f && !Vehicle.Drafted && ShouldAutoRefuelNowIgnoringFuelPct;
		public bool ShouldAutoRefuelNowIgnoringFuelPct => !Vehicle.IsBurning() && parent.Map.designationManager.DesignationOn(Vehicle, DesignationDefOf_Vehicles.DisassembleVehicle) == null;

		//Electric
		private bool Charging => connectedPower != null && !FullTank && connectedPower.PowerNet.CurrentStoredEnergy() > Props.chargeRate;

		public IEnumerable<(ThingDef thingDef, float count)> Refunds
		{
			get
			{
				if (!Props.electricPowered)
				{
					yield return (Props.fuelType, Fuel);
				}
			}
		}

		public static MethodInfo PowerNetMethod
		{
			get
			{
				if (powerNetMethod is null)
				{
					powerNetMethod = AccessTools.Method(typeof(PowerNet), "ChangeStoredEnergy");
				}
				return powerNetMethod;
			}
		}

		public virtual float ChargeRate
		{
			get
			{
				float chargeRate = Props.chargeRate;
				chargeRate = Vehicle.statHandler.GetStatOffset(VehicleStatUpgradeCategoryDefOf.ChargeRate, chargeRate);
				return chargeRate;
			}
		}

		public virtual float DischargeRate
		{
			get
			{
				float dischargeRate = Props.dischargeRate;// SettingsCache.TryGetValue(Vehicle.VehicleDef, typeof(CompProperties_FueledTravel), nameof(CompProperties_FueledTravel.dischargeRate), Props.dischargeRate);
				dischargeRate = Vehicle.statHandler.GetStatOffset(VehicleStatUpgradeCategoryDefOf.DischargeRate, dischargeRate);
				return dischargeRate;
			}
		}

		public virtual float FuelEfficiency
		{
			get
			{
				float consumptionRate = SettingsCache.TryGetValue(Vehicle.VehicleDef, typeof(CompProperties_FueledTravel), nameof(CompProperties_FueledTravel.fuelConsumptionRate), Props.fuelConsumptionRate);
				consumptionRate = Vehicle.statHandler.GetStatOffset(VehicleStatUpgradeCategoryDefOf.FuelConsumptionRate, consumptionRate);
				return consumptionRate;
			}
		}

		public virtual float FuelCapacity
		{
			get
			{
				float fuelCapacity = SettingsCache.TryGetValue(Vehicle.VehicleDef, typeof(CompProperties_FueledTravel), nameof(CompProperties_FueledTravel.fuelCapacity), Props.fuelCapacity);
				fuelCapacity = Vehicle.statHandler.GetStatOffset(VehicleStatUpgradeCategoryDefOf.FuelCapacity, fuelCapacity);
				return fuelCapacity;
			}
		}

		///Flying fuel consumption is handled in <see cref="AerialVehicleInFlight.SpendFuel"/>
		public bool ShouldConsumeNow => (!EmptyTank && Vehicle.Spawned) && ConsumeWhenDrafted || ConsumeWhenMoving || ConsumeAlways;

		public bool ConsumeAlways => FuelCondition.HasFlag(FuelConsumptionCondition.Always);

		public bool ConsumeWhenDrafted => Vehicle.Spawned && FuelCondition.HasFlag(FuelConsumptionCondition.Drafted) && Vehicle.Drafted;

		public bool ConsumeWhenMoving
		{
			get
			{
				if (FuelCondition.HasFlag(FuelConsumptionCondition.Moving))
				{
					if (Vehicle.Spawned && Vehicle.vehiclePather.Moving)
					{
						return true;
					}
					if (Vehicle.GetVehicleCaravan() is VehicleCaravan caravan && caravan.vehiclePather.MovingNow)
					{
						return true;
					}
				}
				return false;
			}
		}

		public virtual Thing ClosestFuelAvailable(Pawn pawn)
		{
			if (Props.electricPowered)
			{
				return null;
			}
			Predicate<Thing> validator = (Thing x) => !x.IsForbidden(pawn) && pawn.CanReserve(x, 1, -1, null, false) && x.def == Props.fuelType;
			return GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(Props.fuelType), PathEndMode.ClosestTouch, TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn,
				false), 9999f, validator, null, 0, -1, false, RegionType.Set_Passable, false);
		}

		public override bool CanDraft(out string failReason, out bool allowDevMode)
		{
			allowDevMode = false;
			if (EmptyTank)
			{
				failReason = "VF_OutOfFuel".Translate(Vehicle);
				return false;
			}
			return base.CanDraft(out failReason, out allowDevMode);
		}

		public virtual void Refuel(List<Thing> fuelThings)
		{
			int countToFull = FuelCountToFull;
			while (countToFull > 0 && fuelThings.Count > 0)
			{
				Thing thing = fuelThings.Pop();
				int count = Mathf.Min(countToFull, thing.stackCount);
				Refuel(count);
				thing.SplitOff(count).Destroy(DestroyMode.Vanish);
				countToFull -= count;
			}
		}

		public virtual void Refuel(float amount)
		{
			if (fuel >= FuelCapacity)
			{
				return;
			}
			fuel += amount;
			Vehicle.EventRegistry?[VehicleEventDefOf.Refueled].ExecuteEvents();
			if (fuel >= FuelCapacity)
			{
				fuel = FuelCapacity;
			}
		}

		/// <summary>
		/// Only for Incident spawning / AI spawning. Will randomize fuel levels later (REDO)
		/// </summary>
		private void RefuelHalfway()
		{
			ConsumeFuel(float.MaxValue);
			Refuel(FuelCapacity / 2f);
		}

		public virtual void ConsumeFuel(float amount)
		{
			if (fuel <= 0f)
			{
				return;
			}
			fuel -= amount;
			if (fuel <= 0f)
			{
				fuel = 0f;
				Vehicle.EventRegistry[VehicleEventDefOf.OutOfFuel].ExecuteEvents();
			}
		}

		public virtual void ConsumeFuelWorld()
		{
			if (fuel <= 0f) return;

			float fuelToConsume = ConsumptionRatePerTick * Props.fuelConsumptionWorldMultiplier;
			VehicleCaravan caravan = Vehicle.GetVehicleCaravan();
			if (!caravan.vehiclePather.Moving) fuelToConsume *= EfficiencyIdleMultiplier;

			fuel -= fuelToConsume;
			if (fuel <= 0f)
			{
				fuel = 0f;
				Vehicle.EventRegistry[VehicleEventDefOf.OutOfFuel].ExecuteEvents();
			}
		}

		public override void PostDraw()
		{
			base.PostDraw();
			if (EmptyTank)
			{
				if (Props.electricPowered)
				{
					parent.Map.overlayDrawer.DrawOverlay(parent, OverlayTypes.NeedsPower);
				}
				else
				{
					parent.Map.overlayDrawer.DrawOverlay(parent, OverlayTypes.OutOfFuel);
				}
			}
		}

		public override IEnumerable<Gizmo> CompGetGizmosExtra()
		{
			foreach (Gizmo gizmo in base.CompGetGizmosExtra())
			{
				yield return gizmo;
			}

			if (Find.Selector.SingleSelectedThing == parent)
			{
				yield return new Gizmo_RefuelableFuelTravel(this, false);
			}

			if (Props.electricPowered)
			{
				yield return new Command_Toggle
				{
					hotKey = KeyBindingDefOf.Command_TogglePower,
					icon = VehicleTex.FlickerIcon,
					defaultLabel = "VF_ElectricFlick".Translate(),
					defaultDesc = "VF_ElectricFlickDesc".Translate(),
					isActive = (() => Charging),
					toggleAction = delegate()
					{
						if(!Charging)
						{
							TryConnectPower();
						}
						else
						{
							DisconnectPower();
						}
					}
				};
			}

			foreach (Gizmo gizmo in DevModeGizmos())
			{
				yield return gizmo;
			}
		}

		public override IEnumerable<Gizmo> CompCaravanGizmos()
		{
			yield return new Gizmo_RefuelableFuelTravel(this, true);

			if (DebugSettings.ShowDevGizmos)
			{
				yield return new Command_Action
				{
					defaultLabel = $"Vehicle Dev: [{Vehicle.Label}] Set fuel to 0.",
					action = delegate ()
					{
						ConsumeFuel(float.MaxValue);
					}
				};
				yield return new Command_Action
				{
					defaultLabel = $"Vehicle Dev: [{Vehicle.Label}] Set fuel to max.",
					action = delegate ()
					{
						Refuel(FuelCapacity);
					}
				};
			}
		}

		public virtual IEnumerable<Gizmo> DevModeGizmos()
		{
			if (DebugSettings.ShowDevGizmos)
			{
				yield return new Command_Action
				{
					defaultLabel = "Debug: Set fuel to 0",
					action = delegate ()
					{
						ConsumeFuel(float.MaxValue);
					}
				};
				yield return new Command_Action
				{
					defaultLabel = "Debug: Set fuel to half",
					action = delegate ()
					{
						RefuelHalfway();
					}
				};
				yield return new Command_Action
				{
					defaultLabel = "Debug: Set fuel to max",
					action = delegate ()
					{
						Refuel(FuelCapacity);
					}
				};
				yield return new Command_Action
				{
					defaultLabel = "Debug: Set fuel to 99.99%",
					action = delegate ()
					{
						ConsumeFuel(float.MaxValue);
						Refuel(FuelCapacity * 0.999999f);
					}
				};
			}
		}

		public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
		{
			yield return new FloatMenuOption("Refuel".Translate().ToString(),
				delegate ()
				{
					Job job = new Job(JobDefOf_Vehicles.RefuelVehicle, parent, ClosestFuelAvailable(selPawn));
					selPawn.jobs.TryTakeOrderedJob(job, JobTag.DraftedOrder);
				}, MenuOptionPriority.Default, null, null, 0f, null, null);
		}

		public override void CompCaravanInspectString(StringBuilder stringBuilder)
		{
			if (EmptyTank)
			{
				stringBuilder.AppendLine("VF_OutOfFuel".Translate(Vehicle));
			}
		}

		private void RevalidateConsumptionStatus()
		{
			if (ShouldConsumeNow)
			{
				StartTicking();
			}
			else
			{
				StopTicking();
			}
		}

		public override void CompTick()
		{
			float fuelToConsume = ConsumptionRatePerTick;
			if (!Vehicle.vehiclePather.Moving) fuelToConsume *= EfficiencyIdleMultiplier;
			ConsumeFuel(fuelToConsume);

			if (!terminateMotes && !Props.motesGenerated.NullOrEmpty())
			{
				if (Find.TickManager.TicksGame % Props.ticksToSpawnMote == 0)
				{
					DrawMotes();
				}
			}
			if (EmptyTank && !VehicleMod.settings.debug.debugDraftAnyVehicle)
			{
				Vehicle.ignition.Drafted = false;
			}

			if (Props.electricPowered)
			{
				if (!Charging)
				{
					ConsumeFuel(Mathf.Min(DischargeRate * EfficiencyTickMultiplier, Fuel));
				}
				else if(Find.TickManager.TicksGame % TicksToCharge == 0)
				{
					PowerNetMethod.Invoke(connectedPower.PowerNet, new object[] { -ChargeRate });
					Refuel(ChargeRate);
				}
			}
		}

		public void LeakTick()
		{
			//Validate leak every so often
			if (Find.TickManager.TicksGame % TicksPerLeakCheck == 0 && !FuelComponents.NullOrEmpty())
			{
				leaking = false;
				foreach ((VehicleComponent component, Reactor_FuelLeak fuelLeak) in FuelComponents)
				{
					leaking |= component.HealthPercent <= fuelLeak.maxHealth;
				}
			}

			//If leaking, then loop through and spawn filth
			if (leaking)
			{
				foreach ((VehicleComponent component, Reactor_FuelLeak fuelLeak) in FuelComponents)
				{
					float t = (fuelLeak.maxHealth - component.HealthPercent) * (1 / fuelLeak.maxHealth);
					float rate = Mathf.Lerp(fuelLeak.rate.min, fuelLeak.rate.max, t);
					if (rate == 0)
					{
						continue;
					}
					int ticksPerLeak = Mathf.CeilToInt(60 / rate);
					if (Find.TickManager.TicksGame % ticksPerLeak == 0)
					{
						ConsumeFuel(FuelPerLeak);
						if (Vehicle.Spawned && Props.leakDef != null && !EmptyTank)
						{
							IntVec2 offset = component.props.hitbox.cells.RandomElementWithFallback(fallback: IntVec2.Zero);
							IntVec3 leakCell = new IntVec3(Vehicle.Position.x + offset.x, 0, Vehicle.Position.z + offset.z);
							FilthMaker.TryMakeFilth(leakCell, Vehicle.Map, Props.leakDef);
						}
					}
				}
			}
		}

		public override void CompTickRare()
		{
			base.CompTickRare();
			
			RevalidateConsumptionStatus(); //Intermittent checks to ensure no missed cases cause vehicle to drain
			
			if (Vehicle.Spawned)
			{
				if (!FullTank)
				{
					Vehicle.Map.GetCachedMapComponent<VehicleReservationManager>().RegisterLister(Vehicle, ReservationType.Refuel);
				}
				else
				{
					Vehicle.Map.GetCachedMapComponent<VehicleReservationManager>().RemoveLister(Vehicle, ReservationType.Refuel);
				}
			}

			if (Vehicle.vehiclePather.Moving)
			{
				DisconnectPower();
			}
		}

		public virtual bool TryConnectPower()
		{
			if (Props.electricPowered)
			{
				foreach (IntVec3 cell in Vehicle.InhabitedCells(1))
				{
					Thing building = Vehicle.Map.thingGrid.ThingAt(cell, ThingCategory.Building);
					if(building != null)
					{
						CompPower powerSource = building.TryGetComp<CompPower>();
						if(powerSource != null && powerSource.TransmitsPowerNow)
						{
							connectedPower = powerSource;
							break;
						}
					}
				}
			}
			return connectedPower is null;
		}

		public virtual void DisconnectPower()
		{
			connectedPower = null;
		}

		//TODO - Refactor wind direction
		public virtual void DrawMotes()
		{
			foreach (OffsetMote offset in Props.motesGenerated)
			{
				for(int i = 0; i < offset.NumTimesSpawned; i++)
				{
					try
					{
						Vector2 moteOffset = VehicleGraphics.VehicleDrawOffset(Vehicle.FullRotation, offset.xOffset, offset.zOffset);
						offsetX = moteOffset.x;
						offsetZ = moteOffset.y;

						motePosition = new Vector3(parent.Position.x + offsetX + CellOffsetIntVec3ToVector3, parent.Position.y, parent.Position.z + offsetZ + CellOffsetIntVec3ToVector3);
					
						MoteThrown mote = (MoteThrown)ThingMaker.MakeThing(Props.MoteDisplayed, null);
						mote.exactPosition = motePosition;
						mote.Scale = 1f;
						mote.rotationRate = 15f;
						float moteAngle = offset.predeterminedAngleVector is null ? (Vehicle.Map.components.First(x => x is WindDirectional) as WindDirectional).WindDirection : (float)offset.predeterminedAngleVector;
						float moteSpeed = offset.windAffected ? Rand.Range(0.5f, 3.5f) * Vehicle.Map.windManager.WindSpeed : offset.moteThrownSpeed;
						mote.SetVelocity(moteAngle, moteSpeed);
						RenderHelper.ThrowMoteEnhanced(motePosition, parent.Map, mote);
					}
					catch (Exception ex)
					{
						Log.Error($"Exception thrown while trying to display {Props.MoteDisplayed.defName} Terminating MoteDraw Method from {parent.LabelShort} Exception={ex}");
						terminateMotes = true;
						return;
					}
				}
			} 
		}

		public override void EventRegistration()
		{
			FuelComponents = new List<(VehicleComponent component, Reactor_FuelLeak fuelLeak)>();
			foreach (VehicleComponent component in Vehicle.statHandler.components.Where(component => component.props.HasReactor<Reactor_FuelLeak>()))
			{
				if (component.props.HasReactor<Reactor_FuelLeak>())
				{
					FuelComponents.Add((component, component.props.GetReactor<Reactor_FuelLeak>()));
				}
			}

			Vehicle.AddEvent(VehicleEventDefOf.MoveStart, RevalidateConsumptionStatus);
			Vehicle.AddEvent(VehicleEventDefOf.MoveStop, RevalidateConsumptionStatus);
			Vehicle.AddEvent(VehicleEventDefOf.OutOfFuel, RevalidateConsumptionStatus);
			Vehicle.AddEvent(VehicleEventDefOf.Refueled, RevalidateConsumptionStatus);
			Vehicle.AddEvent(VehicleEventDefOf.IgnitionOn, RevalidateConsumptionStatus);
			Vehicle.AddEvent(VehicleEventDefOf.IgnitionOff, RevalidateConsumptionStatus);
			Vehicle.AddEvent(VehicleEventDefOf.DamageTaken, RevalidateConsumptionStatus);
			Vehicle.AddEvent(VehicleEventDefOf.Repaired, RevalidateConsumptionStatus);
		}

		public override void PostGeneration()
		{
			base.PostGeneration();
			targetFuelLevel = FuelCapacity;
			targetFuelPercent = 1;
			if (Vehicle.Faction != Faction.OfPlayer)
			{
				Refuel(FuelCapacity * Rand.Range(0.45f, 0.85f));
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);

			RevalidateConsumptionStatus();

			if (postLoadReconnect)
			{
				TryConnectPower();
			}
		}

		public override void PostExposeData()
		{
			base.PostExposeData();

			//CurValues
			Scribe_Values.Look(ref fuel, nameof(fuel));

			if (Scribe.mode == LoadSaveMode.Saving && targetFuelPercent < 0)
			{
				targetFuelPercent = targetFuelLevel / FuelCapacity;
			}

			Scribe_Values.Look(ref targetFuelPercent, nameof(targetFuelPercent), defaultValue: 1);

			if (Scribe.mode == LoadSaveMode.LoadingVars)
			{
				Scribe_Values.Look(ref targetFuelLevel, nameof(targetFuelLevel), defaultValue: Props.fuelCapacity);
			}

			if (Scribe.mode == LoadSaveMode.Saving)
			{
				postLoadReconnect = Charging;
			}
			Scribe_Values.Look(ref postLoadReconnect, nameof(postLoadReconnect), defaultValue: false);
		}
	}
}
