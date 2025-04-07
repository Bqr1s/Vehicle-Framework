using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using DevTools;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.UnitTesting;
using UnityEngine;
using Verse;

namespace Vehicles
{
  /// <summary>
  /// WorldGrid for vehicles
  /// </summary>
  public class WorldVehiclePathGrid : WorldComponent
  {
    public const float ImpassableMovementDifficulty = 1000f;

    private static readonly Func<Hilliness, float> HillinessMovementDifficultyOffset;

    public event Action<VehicleDef> onPathGridRecalculated;

    /// <summary>
    /// Store entire pathGrid for each <see cref="VehicleDef"/>
    /// </summary>
    public PathGrid[] movementDifficulty;

    public readonly WorldVehicleReachability reachability;

    private readonly float[] winter;

    private int allPathCostsRecalculatedDayOfYear = -1;

    static WorldVehiclePathGrid()
    {
      // Remove singleton reference, we shouldn't rely on reference being overwritten on
      // subsequent playthroughs.
      GameEvent.onWorldRemoved += () => Instance = null;

      MethodInfo hillinessMethod =
        AccessTools.Method(typeof(WorldPathGrid), "HillinessMovementDifficultyOffset");
      HillinessMovementDifficultyOffset =
        (Func<Hilliness, float>)Delegate.CreateDelegate(typeof(Func<Hilliness, float>),
          hillinessMethod);
    }

    public WorldVehiclePathGrid(World world) : base(world)
    {
      this.world = world;
      movementDifficulty = new PathGrid[DefDatabase<VehicleDef>.DefCount];
      winter = new float[Find.WorldGrid.TilesCount];
      ResetPathGrid();
      Initialized = false;
      Instance = this;
      reachability = new WorldVehicleReachability(this);
    }

    /// <summary>
    /// Singleton getter
    /// </summary>
    public static WorldVehiclePathGrid Instance { get; private set; }

    public bool Recalculating { get; private set; }

    public bool Initialized { get; private set; }

    /// <summary>
    /// Day of year at 0 longitude for recalculating pathGrids
    /// </summary>
    private static int DayOfYearAt0Long => GenDate.DayOfYear(GenTicks.TicksAbs, 0f);

    private void ResetPathGrid()
    {
      // TODO - implement piggybacking for path grids
      foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
      {
        movementDifficulty[vehicleDef.DefIndex] =
          new PathGrid(vehicleDef, Find.WorldGrid.TilesCount);
      }
    }

    /// <summary>
    /// Recalculate all perceived path costs at <see cref="DayOfYearAt0Long"/>
    /// </summary>
    public override void WorldComponentTick()
    {
      if (!Recalculating && allPathCostsRecalculatedDayOfYear != DayOfYearAt0Long)
      {
        if (!UnitTestManager.RunningUnitTests)
          RunTaskRecalculateAllPathCosts();
        else
          RecalculateAllPerceivedPathCosts();
      }

      if (Prefs.DevMode)
        FlashWorldGrid();
    }

    private void FlashWorldGrid()
    {
      if (DebugHelper.World.VehicleDef != null && Find.WorldSelector.selectedTile >= 0 &&
        Find.TickManager.TicksGame % 30 == 0) //Twice per second at 60fps
      {
        if (DebugHelper.World.DebugType == WorldPathingDebugType.PathCosts)
        {
          int tile = Find.WorldSelector.selectedTile;
          List<int> neighbors = [];
          Find.WorldGrid.GetTileNeighbors(tile, neighbors);

          float cost = movementDifficulty[DebugHelper.World.VehicleDef.DefIndex][tile];
          Find.World.debugDrawer.FlashTile(tile, colorPct: cost * 10 / ImpassableMovementDifficulty,
            text: cost.ToString(), duration: 15);
          foreach (int neighborTile in neighbors)
          {
            Find.World.debugDrawer.FlashTile(neighborTile,
              text: movementDifficulty[DebugHelper.World.VehicleDef.DefIndex][neighborTile]
               .ToString(), duration: 30);
          }
        }
        else if (DebugHelper.World.DebugType == WorldPathingDebugType.Reachability)
        {
          int tile = Find.WorldSelector.selectedTile;
          List<int> neighbors = [];
          Ext_World.BFS(tile, neighbors, radius: 10);

          Find.World.debugDrawer.FlashTile(tile, colorPct: 0.8f, text: IdStringAt(tile), 15);
          foreach (int neighbor in neighbors)
          {
            bool canReach =
              Instance.reachability.CanReach(vehicleDef: DebugHelper.World.VehicleDef,
                tile, neighbor);
            float colorPct = canReach ? 0.65f : 0f;
            Find.World.debugDrawer.FlashTile(neighbor, colorPct: colorPct,
              text: IdStringAt(neighbor),
              duration: 30);
          }

          static string IdStringAt(int t) => Instance.reachability.GetRegionId(
            DebugHelper.World.VehicleDef,
            t).ToString();
        }
        else if (DebugHelper.World.DebugType == WorldPathingDebugType.WinterPct)
        {
          int tile = Find.WorldSelector.selectedTile;
          List<int> neighbors = [];
          Ext_World.BFS(tile, neighbors, radius: 10);

          float winterPct = WinterPercentAt(tile);
          Find.World.debugDrawer.FlashTile(tile, colorPct: winterPct,
            text: winterPct.ToString("#.00"), duration: 15);
          foreach (int neighbor in neighbors)
          {
            winterPct = WinterPercentAt(neighbor);
            Find.World.debugDrawer.FlashTile(neighbor, colorPct: winterPct,
              text: winterPct.ToString("#.00"), duration: 30);
          }
        }
      }
    }

    /// <summary>
    /// <paramref name="tile"/> is passable for <paramref name="vehicleDef"/>
    /// </summary>
    /// <param name="tile"></param>
    /// <param name="vehicleDef"></param>
    public bool Passable(int tile, VehicleDef vehicleDef)
    {
      return Find.WorldGrid.InBounds(tile) && movementDifficulty[vehicleDef.DefIndex][tile] <
        ImpassableMovementDifficulty;
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
    private void RecalculatePerceivedMovementDifficultyAt(int tile, VehicleDef vehicleDef,
      int? ticksAbs = null)
    {
      if (!Find.WorldGrid.InBounds(tile))
      {
        return;
      }
      movementDifficulty[vehicleDef.DefIndex][tile] =
        CalculatedMovementDifficultyAt(tile, vehicleDef, ticksAbs);
    }

    /// <summary>
    /// Recalculate all path costs for all VehicleDefs
    /// </summary>
    private void RunTaskRecalculateAllPathCosts()
    {
      if (Recalculating)
      {
        Trace.Fail(
          "Attempting to regenerate world path grid for all vehicles but it is already running.");
        return;
      }
      allPathCostsRecalculatedDayOfYear = DayOfYearAt0Long;
      TaskManager.RunAsync(RecalculateAllAsync);
    }

    // Shorthand method for async task on method with 1 optional parameter
    private void RecalculateAllAsync()
    {
      RecalculateAllPerceivedPathCosts(ticksAbs: null);
    }

    /// <summary>
    /// Recalculate all path costs for all VehicleDefs
    /// </summary>
    /// <param name="ticksAbs"></param>
    internal void RecalculateAllPerceivedPathCosts(int? ticksAbs = null)
    {
      allPathCostsRecalculatedDayOfYear = DayOfYearAt0Long;

      using GridInitializerState gis = new(this);
      foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
      {
        for (int i = 0; i < Find.WorldGrid.TilesCount; i++)
        {
          RecalculatePerceivedMovementDifficultyAt(i, vehicleDef, ticksAbs);
        }
        onPathGridRecalculated?.Invoke(vehicleDef);
      }

      // Only needs to be done once and not for every grid owner
      for (int i = 0; i < Find.WorldGrid.TilesCount; i++)
      {
        RecalculateWinterPercentAt(i, ticksAbs);
      }
    }

    private void RecalculateWinterPercentAt(int tile, int? ticksAbs = null)
    {
      winter[tile] = WinterPathingHelper.GetWinterPercent(tile, ticksAbs: ticksAbs);
    }

    /// <summary>
    /// Calculate path cost for <paramref name="vehicleDef"/> at <paramref name="tile"/>
    /// </summary>
    public static float CalculatedMovementDifficultyAt(int tile, VehicleDef vehicleDef,
      int? ticksAbs = null, StringBuilder explanation = null, bool coastalTravel = true)
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
        if (riverLink.river != null &&
          vehicleDef.properties.customRiverCosts.TryGetValue(riverLink.river,
            out float riverCost) && riverCost != ImpassableMovementDifficulty)
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
        defaultBiomeCost = biomeDef.impassable ?
          ImpassableMovementDifficulty :
          biomeDef.movementDifficulty;
      }

      if (coastalTravel && vehicleDef.CoastalTravel(tile))
      {
        defaultBiomeCost = Mathf.Min(defaultBiomeCost,
          vehicleDef.properties.customBiomeCosts[BiomeDefOf.Ocean]);
      }

      float biomeCost =
        vehicleDef.properties.customBiomeCosts.TryGetValue(worldTile.biome, defaultBiomeCost);
      float hillinessCost =
        vehicleDef.properties.customHillinessCosts.TryGetValue(worldTile.hilliness,
          HillinessMovementDifficultyOffset(worldTile.hilliness));

      if (!VehicleMod.settings.main.vehiclePathingBiomesCostOnRoads)
      {
        if (!worldTile.Roads.NullOrEmpty())
        {
          biomeCost = 1;
          hillinessCost = 0;
        }
      }

      if (biomeCost >= ImpassableMovementDifficulty ||
        hillinessCost >= ImpassableMovementDifficulty)
      {
        explanation?.Append("Impassable".Translate());
        return ImpassableMovementDifficulty;
      }

      explanation?.Append(worldTile.biome.LabelCap + ": " + biomeCost.ToStringWithSign("0.#"));

      float totalCost = biomeCost + hillinessCost;
      if (explanation != null && hillinessCost != 0f)
      {
        explanation.AppendLine();
        explanation.Append(worldTile.hilliness.GetLabelCap() + ": " +
          hillinessCost.ToStringWithSign("0.#"));
      }

      // + GetCurrentWinterMovementDifficultyOffset(tile, vehicleDef, new int?(ticksAbs ?? GenTicks.TicksAbs), explanation);
      return totalCost;
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
      return Mathf.Max(CalculatedMovementDifficultyAt(tile, vehicleDef, null, null, false),
        CalculatedMovementDifficultyAt(neighbor, vehicleDef, null, null, false));
    }

    [DebugAction(VehicleHarmony.VehiclesLabel, name = "Regen WorldGrid",
      allowedGameStates = AllowedGameStates.PlayingOnWorld)]
    private static void RecalculatePathGrid()
    {
      Instance.RunTaskRecalculateAllPathCosts();
    }

    public class PathGrid
    {
      public readonly VehicleDef owner;
      private readonly float[] costs;

      public float this[int index]
      {
        get => costs[index];
        set => costs[index] = value;
      }

      public PathGrid(VehicleDef owner, int size)
      {
        this.owner = owner;
        costs = new float[size];
      }
    }

    private readonly struct GridInitializerState : IDisposable
    {
      private readonly WorldVehiclePathGrid pathGrid;

      public GridInitializerState(WorldVehiclePathGrid pathGrid)
      {
        this.pathGrid = pathGrid;
        this.pathGrid.Initialized = false;
        this.pathGrid.Recalculating = true;
      }

      void IDisposable.Dispose()
      {
        pathGrid.Recalculating = false;
        pathGrid.Initialized = true;
      }
    }
  }
}