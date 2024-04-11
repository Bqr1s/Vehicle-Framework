﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using UnityEngine;

namespace Vehicles
{
	public partial class VehiclePawn
	{
		public float PawnCollisionMultiplier
		{
			get
			{
				float multiplier = SettingsCache.TryGetValue(VehicleDef, typeof(VehicleProperties), nameof(VehicleProperties.pawnCollisionMultiplier), VehicleDef.properties.pawnCollisionMultiplier);
				float offset = statHandler.GetStatOffset(VehicleStatUpgradeCategoryDefOf.PawnCollisionMultiplier);
				return multiplier + offset;
			}
		}
		
		public float PawnCollisionRecoilMultiplier
		{
			get
			{
				float multiplier = SettingsCache.TryGetValue(VehicleDef, typeof(VehicleProperties), nameof(VehicleProperties.pawnCollisionRecoilMultiplier), VehicleDef.properties.pawnCollisionRecoilMultiplier); ;
				float offset = statHandler.GetStatOffset(VehicleStatUpgradeCategoryDefOf.PawnCollisionRecoilMultiplier);
				return multiplier + offset;
			}
		}
			
		public virtual bool CanApplyStun(Thing instigator)
		{
			return false;
		}

		public virtual bool CanApplyStagger()
		{
			return false;
		}

		public float FriendlyFireChance(Pawn pawn)
		{
			float multiplier = 1;
			if (pawn.Faction == Faction)
			{
				multiplier = 0.5f;
			}
			return multiplier * VehicleMod.settings.main.friendlyFire switch
			{
				VehicleTracksFriendlyFire.None => 0,
				VehicleTracksFriendlyFire.Vanilla => Find.Storyteller.difficulty.friendlyFireChanceFactor,
				VehicleTracksFriendlyFire.Custom => VehicleMod.settings.main.friendlyFireChance,
				_ => throw new NotImplementedException(nameof(VehicleTracksFriendlyFire)),
			};
		}

		public void CheckForCollisions(float moveSpeed)
		{
			CellRect occupiedRect = this.OccupiedRect();
			foreach (IntVec3 cell in occupiedRect)
			{
				if (Map.thingGrid.ThingAt(cell, ThingCategory.Pawn) is Pawn pawn && !(pawn is VehiclePawn))
				{
					if (pawn.Faction.HostileTo(Faction) || Rand.Chance(FriendlyFireChance(pawn)))
					{
						(float pawnDamage, float vehicleDamage) = CalculateImpactDamage(pawn, this, moveSpeed);
						Pawn culprit = GetPriorityHandlers(HandlingTypeFlags.Movement)?.FirstOrDefault(handler => handler.handlers.Any)?.handlers.InnerListForReading.FirstOrDefault();
						IntVec3 position = pawn.Position;
						DamageWorker.DamageResult result = pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, pawnDamage, instigator: culprit));
						TryTakeDamage(new DamageInfo(DamageDefOf.Blunt, vehicleDamage, instigator: pawn, instigatorGuilty: false), position, out _);
					}
				}
			}
		}

		public static (float pawnDamage, float vehicleDamage) CalculateImpactDamage(Pawn pawn, VehiclePawn vehicle, float velocity)
		{
			float mass = vehicle.GetStatValue(VehicleStatDefOf.Mass);
			float bodySize = pawn.RaceProps.baseBodySize;
			float ke = 0.5f * mass * (velocity * velocity); //   (1/2)mv^2
			float pawnDamage = vehicle.PawnCollisionMultiplier * ke / 100 * bodySize; //   0.7k / 100b
			float baseBluntArmor = vehicle.GetStatValue(StatDefOf.ArmorRating_Blunt);
			float vehicleDamage = vehicle.PawnCollisionRecoilMultiplier * (2- baseBluntArmor) * ke * bodySize / 200; //   p(2-a)kb / 200
			return (pawnDamage, vehicleDamage);
		}
	}
}
