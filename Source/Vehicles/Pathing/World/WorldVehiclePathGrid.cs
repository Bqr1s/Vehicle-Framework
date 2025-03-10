﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;
using SmashTools;

namespace Vehicles
{
	/// <summary>
	/// WorldGrid for vehicles
	/// </summary>
	public class WorldVehiclePathGrid : WorldComponent
	{
		public const float ImpassableMovementDifficulty = 1000f;
		
		/// <summary>
		/// Store entire pathGrid for each <see cref="VehicleDef"/>
		/// </summary>
		public PathGrid[] movementDifficulty;
		private float[] winter;

		private readonly List<VehicleDef> owners = new List<VehicleDef>();

		private int allPathCostsRecalculatedDayOfYear = -1;

		private static readonly MethodInfo hillinessMethod = AccessTools.Method(typeof(WorldPathGrid), "HillinessMovementDifficultyOffset");

		public WorldVehiclePathGrid(World world) : base(world)
		{
			this.world = world;
			movementDifficulty = new PathGrid[DefDatabase<VehicleDef>.DefCount];
			winter = new float[Find.WorldGrid.TilesCount];
			ResetPathGrid();
			Instance = this;
			Initialized = false;
		}

		/// <summary>
		/// Singleton getter
		/// </summary>
		public static WorldVehiclePathGrid Instance { get; private set; }

		public static bool Recalculating { get; private set; }

		public static bool Initialized { get; private set; }

		/// <summary>
		/// Day of year at 0 longitude for recalculating pathGrids
		/// </summary>
		private int DayOfYearAt0Long => GenDate.DayOfYear(GenTicks.TicksAbs, 0f);

		/// <summary>
		/// <paramref name="cost"/> is &gt; <see cref="ImpassableMovementDifficulty"/> or &lt; 0
		/// </summary>
		/// <param name="cost"></param>
		/// <returns><paramref name="cost"/> is impassable</returns>
		public static bool ImpassableCost(float cost) => cost >= ImpassableMovementDifficulty;

		/// <summary>
		/// Reset all cached pathGrids for VehicleDefs
		/// </summary>
		public void Request_ResetPathGrid()
		{
			if (Recalculating)
			{
				CoroutineManager.StartCoroutine(ResetWhenAvailable);
			}
			else
			{
				ResetPathGrid();
			}
		}

		private IEnumerator ResetWhenAvailable()
		{
			while (Recalculating) yield return null; //Delay until recalculation is finished
			ResetPathGrid();
		}

		private void ResetPathGrid()
		{
			foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
			{
				bool owner = true;
				foreach (VehicleDef ownerDef in owners)
				{
					if (MatchingPathCosts(vehicleDef, ownerDef))
					{
						owner = false;
						movementDifficulty[vehicleDef.DefIndex] = movementDifficulty[ownerDef.DefIndex]; //Piggy back off same configuration of already registered vehicle
						break;
					}
				}
				if (owner)
				{
					owners.Add(vehicleDef);
					movementDifficulty[vehicleDef.DefIndex] = new PathGrid(vehicleDef, Find.WorldGrid.TilesCount); //Register as owner with new path grid
				}
			}
		}

		public bool MatchingPathCosts(VehicleDef vehicleDef, VehicleDef otherVehicleDef)
		{
			if (vehicleDef.properties.defaultBiomesImpassable != otherVehicleDef.properties.defaultBiomesImpassable)
			{
				return false; //Quick filter to avoid comparing all defs when it is most likely not going to match costs due to default impassable costs
			}

			/* -- Must check both vehicles' configurations to avoid missed cases resulting in malformed pathing due to unequal ownership -- */

			//Biome costs
			foreach ((BiomeDef biomeDef, float cost) in vehicleDef.properties.customBiomeCosts)
			{
				if (!otherVehicleDef.properties.customBiomeCosts.TryGetValue(biomeDef, out float matchingCost) || matchingCost != cost)
				{
					return false;
				}
			}

			//Biome costs
			foreach ((BiomeDef biomeDef, float cost) in otherVehicleDef.properties.customBiomeCosts)
			{
				if (!vehicleDef.properties.customBiomeCosts.TryGetValue(biomeDef, out float matchingCost) || matchingCost != cost)
				{
					return false;
				}
			}

			//Hilliness costs
			foreach ((Hilliness hilliness, float cost) in vehicleDef.properties.customHillinessCosts)
			{
				if (!otherVehicleDef.properties.customHillinessCosts.TryGetValue(hilliness, out float matchingCost) || matchingCost != cost)
				{
					return false;
				}
			}

			//Hilliness costs
			foreach ((Hilliness hilliness, float cost) in otherVehicleDef.properties.customHillinessCosts)
			{
				if (!vehicleDef.properties.customHillinessCosts.TryGetValue(hilliness, out float matchingCost) || matchingCost != cost)
				{
					return false;
				}
			}

			//River costs
			foreach ((RiverDef riverDef, float cost) in vehicleDef.properties.customRiverCosts)
			{
				if (!otherVehicleDef.properties.customRiverCosts.TryGetValue(riverDef, out float matchingCost) || matchingCost != cost)
				{
					return false;
				}
			}

			//River costs
			foreach ((RiverDef riverDef, float cost) in otherVehicleDef.properties.customRiverCosts)
			{
				if (!vehicleDef.properties.customRiverCosts.TryGetValue(riverDef, out float matchingCost) || matchingCost != cost)
				{
					return false;
				}
			}

			return true;
		}

		public bool MatchesReachability(VehicleDef vehicleDef, VehicleDef otherVehicleDef)
		{
			//Biomes
			foreach (BiomeDef biomeDef in DefDatabase<BiomeDef>.AllDefsListForReading)
			{
				float pathCost = vehicleDef.properties.customBiomeCosts.TryGetValue(biomeDef, biomeDef.movementDifficulty);
				float otherCost = otherVehicleDef.properties.customBiomeCosts.TryGetValue(biomeDef, biomeDef.movementDifficulty);
				if ((pathCost == ImpassableMovementDifficulty || otherCost == ImpassableMovementDifficulty) && pathCost != otherCost)
				{
					return false;
				}
			}

			//Hills
			foreach (Hilliness hilliness in Enum.GetValues(typeof(Hilliness)))
			{
				float pathCost = vehicleDef.properties.customHillinessCosts.TryGetValue(hilliness, HillinessMovementDifficultyOffset(hilliness));
				float otherCost = otherVehicleDef.properties.customHillinessCosts.TryGetValue(hilliness, HillinessMovementDifficultyOffset(hilliness));
				if ((pathCost == ImpassableMovementDifficulty || otherCost == ImpassableMovementDifficulty) && pathCost != otherCost)
				{
					return false;
				}
			}

			//if (!vehicleDef.properties.customRiverCosts.NullOrEmpty() && !otherVehicleDef.properties.customRiverCosts.NullOrEmpty())
			//{
			//	foreach (RiverDef riverDef in DefDatabase<RiverDef>.AllDefsListForReading)
			//	{
			//		float riverCost = vehicleDef.properties.customRiverCosts.TryGetValue(riverDef, 0);
			//		float otherCost = otherVehicleDef.properties.customRiverCosts.TryGetValue(riverDef, 0);
			//		if ((riverCost == ImpassableMovementDifficulty || otherCost == ImpassableMovementDifficulty) && riverCost != otherCost)
			//		{
			//			return false;
			//		}
			//	}
			//	if (vehicleDef.properties.riverTravel != otherVehicleDef.properties.riverTravel)
			//	{
			//		return false;
			//	}
			//}
			return true;
		}

		/// <summary>
		/// Recalculate all perceived path costs at <see cref="DayOfYearAt0Long"/>
		/// </summary>
		public override void WorldComponentTick()
		{
			base.WorldComponentTick();
			if (!Recalculating && allPathCostsRecalculatedDayOfYear != DayOfYearAt0Long)
			{
				RecalculateAllPerceivedPathCosts();
			}
			if (DebugHelper.World.VehicleDef != null && Find.WorldSelector.selectedTile >= 0 && Find.TickManager.TicksGame % 30 == 0) //Twice per second at 60fps
			{
				if (DebugHelper.World.DebugType == WorldPathingDebugType.PathCosts)
				{
					int tile = Find.WorldSelector.selectedTile;
					List<int> neighbors = new List<int>();
					Find.WorldGrid.GetTileNeighbors(tile, neighbors);

					float cost = movementDifficulty[DebugHelper.World.VehicleDef.DefIndex][tile];
					Find.World.debugDrawer.FlashTile(tile, colorPct: cost * 10 / ImpassableMovementDifficulty, text: cost.ToString(), duration: 15);
					foreach (int neighborTile in neighbors)
					{
						Find.World.debugDrawer.FlashTile(neighborTile, text: movementDifficulty[DebugHelper.World.VehicleDef.DefIndex][neighborTile].ToString(), duration: 30);
					}
				}
				else if (DebugHelper.World.DebugType == WorldPathingDebugType.Reachability)
				{
					int tile = Find.WorldSelector.selectedTile;
					List<int> neighbors = new List<int>();
					Ext_World.BFS(tile, neighbors, radius: 10);

					Find.World.debugDrawer.FlashTile(tile, colorPct: 0.8f, duration: 15);
					foreach (int neighbor in neighbors)
					{
						bool canReach = Find.World.GetComponent<WorldVehicleReachability>().CanReach(vehicleDef: DebugHelper.World.VehicleDef, tile, neighbor);
						float colorPct = canReach ? 0.55f : 0f;
						Find.World.debugDrawer.FlashTile(neighbor, colorPct: colorPct, duration: 30);
					}
				}
				else if (DebugHelper.World.DebugType == WorldPathingDebugType.WinterPct)
				{
					int tile = Find.WorldSelector.selectedTile;
					List<int> neighbors = new List<int>();
					Ext_World.BFS(tile, neighbors, radius: 10);

					float winterPct = WinterPercentAt(tile);
					Find.World.debugDrawer.FlashTile(tile, colorPct: winterPct, text: winterPct.ToString("#.00"), duration: 15);
					foreach (int neighbor in neighbors)
					{
						winterPct = WinterPercentAt(neighbor);
						Find.World.debugDrawer.FlashTile(neighbor, colorPct: winterPct, text: winterPct.ToString("#.00"), duration: 30);
					}
				}
			}
		}

		/// <summary>
		/// Flash all path costs for <paramref name="vehicleDef"/> on the world grid
		/// </summary>
		/// <param name="vehicleDef"></param>
		public static void PushVehicleToDraw(VehicleDef vehicleDef)
		{
			for (int i = 0; i < Find.WorldGrid.TilesCount; i++)
			{
				float pathCost = Instance.PerceivedMovementDifficultyAt(i, vehicleDef).RoundTo(0.1f);
				Find.World.debugDrawer.FlashTile(i, 0.01f, pathCost.RoundTo(0.1f).ToString(), 600);
			}
		}

		/// <summary>
		/// <paramref name="tile"/> is passable for <paramref name="vehicleDef"/>
		/// </summary>
		/// <param name="tile"></param>
		/// <param name="vehicleDef"></param>
		public bool Passable(int tile, VehicleDef vehicleDef)
		{
			return Find.WorldGrid.InBounds(tile) && movementDifficulty[vehicleDef.DefIndex][tile] < ImpassableMovementDifficulty;
		}

		/// <summary>
		/// <paramref name="tile"/> is passable for <paramref name="vehicleDef"/> (no bounds check)
		/// </summary>
		/// <param name="tile"></param>
		/// <param name="vehicleDef"></param>
		public bool PassableFast(int tile, VehicleDef vehicleDef)
		{
			return movementDifficulty[vehicleDef.DefIndex][tile] < ImpassableMovementDifficulty;
		}

		/// <summary>
		/// pathCost for <paramref name="vehicleDef"/> at <paramref name="tile"/>
		/// </summary>
		/// <param name="tile"></param>
		/// <param name="vehicleDef"></param>
		public float PerceivedMovementDifficultyAt(int tile, VehicleDef vehicleDef)
		{
			return movementDifficulty[vehicleDef.DefIndex][tile];
		}

		public float WinterPercentAt(int tile)
		{
			return winter[tile];
		}

		/// <summary>
		/// Recalculate pathCost at <paramref name="tile"/> for <paramref name="vehicleDef"/>
		/// </summary>
		/// <param name="tile"></param>
		/// <param name="vehicleDef"></param>
		/// <param name="ticksAbs"></param>
		public void RecalculatePerceivedMovementDifficultyAt(int tile, VehicleDef vehicleDef, int? ticksAbs = null)
		{
			if (!Find.WorldGrid.InBounds(tile))
			{
				return;
			}
			bool flag = PassableFast(tile, vehicleDef);
			movementDifficulty[vehicleDef.DefIndex][tile] = CalculatedMovementDifficultyAt(tile, vehicleDef, ticksAbs, null);
			if (flag != PassableFast(tile, vehicleDef))
			{
				WorldVehicleReachability.Instance.ClearCache();
			}
		}

		/// <summary>
		/// Recalculate all path costs for all VehicleDefs
		/// </summary>
		public void RecalculateAllPerceivedPathCosts()
		{
			TaskManager.RunAsync(RecalculateAllPerceivedPathCosts_Async);
		}

		/// <summary>
		/// Only triggers on World.FinalizedInit since it is during a long event and PostLoad events will need this done synchronously
		/// </summary>
		internal void RecalculateAllPerceivedPathCostsSynchronous()
		{
			RecalculateAllPerceivedPathCosts(null);
		}

		private void RecalculateAllPerceivedPathCosts_Async()
		{
			RecalculateAllPerceivedPathCosts(null);
		}

		/// <summary>
		/// Recalculate all path costs for all VehicleDefs
		/// </summary>
		/// <param name="ticksAbs"></param>
		private void RecalculateAllPerceivedPathCosts(int? ticksAbs)
		{
			Initialized = false;
			Recalculating = true;
			try
			{
				foreach (VehicleDef vehicleDef in owners)
				{
					for (int i = 0; i < Find.WorldGrid.TilesCount; i++)
					{
						RecalculatePerceivedMovementDifficultyAt(i, vehicleDef, ticksAbs);
					}
				}
				for (int i = 0; i < Find.WorldGrid.TilesCount; i++)
				{
					RecalculateWinterPercentAt(i, ticksAbs);
				}
				allPathCostsRecalculatedDayOfYear = DayOfYearAt0Long;
			}
			finally
			{
				Recalculating = false;
				Initialized = true;
			}
		}

		private void RecalculateWinterPercentAt(int tile, int? ticksAbs = null)
		{
			winter[tile] = WinterPathingHelper.GetWinterPercent(tile, ticksAbs: ticksAbs);
		}

		public override void FinalizeInit()
		{
			base.FinalizeInit();
			RecalculateAllPerceivedPathCostsSynchronous();
		}

		/// <summary>
		/// Calculate path cost for <paramref name="vehicleDef"/> at <paramref name="tile"/>
		/// </summary>
		/// <param name="tile"></param>
		/// <param name="vehicleDef"></param>
		/// <param name="ticksAbs"></param>
		/// <param name="explanation"></param>
		public static float CalculatedMovementDifficultyAt(int tile, VehicleDef vehicleDef, int? ticksAbs = null, StringBuilder explanation = null, bool coastalTravel = true)
		{
			Tile worldTile = Find.WorldGrid[tile];
			if (worldTile == null)
			{
				Log.Error($"Attempting to calculate difficulty at null tile.");
				return ImpassableMovementDifficulty;
			}

			if (explanation != null && explanation.Length > 0)
			{
				explanation.AppendLine();
			}

			List<Tile.RiverLink> rivers = worldTile.Rivers;
			if (!rivers.NullOrEmpty())
			{
				Tile.RiverLink riverLink = WorldHelper.BiggestRiverOnTile(rivers);
				if (riverLink.river != null && vehicleDef.properties.customRiverCosts.TryGetValue(riverLink.river, out float riverCost) && riverCost != ImpassableMovementDifficulty)
				{
					explanation?.Append($"{riverLink.river.LabelCap}: {riverCost.ToStringWithSign("0.#")}");
					return riverCost;
				}
			}

			float defaultBiomeCost = 1;
			if (vehicleDef.properties.defaultBiomesImpassable)
			{
				defaultBiomeCost = ImpassableMovementDifficulty;
			}
			else
			{
				BiomeDef biomeDef = worldTile.biome;
				defaultBiomeCost = biomeDef.impassable ? ImpassableMovementDifficulty : biomeDef.movementDifficulty;
			}

			if (coastalTravel && vehicleDef.CoastalTravel(tile))
			{
				defaultBiomeCost = Mathf.Min(defaultBiomeCost, vehicleDef.properties.customBiomeCosts[BiomeDefOf.Ocean]);
			}
			float biomeCost = vehicleDef.properties.customBiomeCosts.TryGetValue(worldTile.biome, defaultBiomeCost);
			float hillinessCost = vehicleDef.properties.customHillinessCosts.TryGetValue(worldTile.hilliness, HillinessMovementDifficultyOffset(worldTile.hilliness));

			if (!VehicleMod.settings.main.vehiclePathingBiomesCostOnRoads)
			{
				if (!worldTile.Roads.NullOrEmpty())
				{
					biomeCost = 1;
					hillinessCost = 0;
				}
			}
			if (ImpassableCost(biomeCost) || ImpassableCost(hillinessCost))
			{
				if (explanation != null)
				{
					explanation.Append("Impassable".Translate());
				}
				return ImpassableMovementDifficulty;
			}
			if (explanation != null)
			{
				explanation.Append(worldTile.biome.LabelCap + ": " + biomeCost.ToStringWithSign("0.#"));
			}

			float totalCost = biomeCost + hillinessCost;
			if (explanation != null && hillinessCost != 0f)
			{
				explanation.AppendLine();
				explanation.Append(worldTile.hilliness.GetLabelCap() + ": " + hillinessCost.ToStringWithSign("0.#"));
			}
			return totalCost;// + GetCurrentWinterMovementDifficultyOffset(tile, vehicleDef, new int?(ticksAbs ?? GenTicks.TicksAbs), explanation);
		}

		/// <summary>
		/// Max cost on <paramref name="tile"/> given neighbor tile <paramref name="neighbor"/> for <paramref name="vehicleDef"/>
		/// </summary>
		/// <remarks>
		/// <paramref name="tile"/> must have coast
		/// </remarks>
		/// <param name="tile"></param>
		/// <param name="neighbor"></param>
		/// <param name="vehicleDef"></param>
		public static float ConsistentDirectionCost(int tile, int neighbor, VehicleDef vehicleDef)
		{
			return Mathf.Max(CalculatedMovementDifficultyAt(tile, vehicleDef, null, null, false), CalculatedMovementDifficultyAt(neighbor, vehicleDef, null, null, false));
		}

		/// <summary>
		/// Default hilliness path costs
		/// </summary>
		/// <param name="hilliness"></param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float HillinessMovementDifficultyOffset(Hilliness hilliness) => (float)hillinessMethod.Invoke(null, new object[] { hilliness });


		public class PathGrid
		{
			public readonly VehicleDef owner;
			private readonly float[] costs;

			public float this[int index] { get => costs[index]; set => costs[index] = value; }

			public PathGrid(VehicleDef owner, int size)
			{
				this.owner = owner;
				costs = new float[size];
			}
		}
	}
}
