using System;
using System.Collections.Generic;
using System.Threading;
using DevTools;
using RimWorld;
using SmashTools;
using SmashTools.Performance;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Vehicles;

public class VehiclePathFinder : VehicleGridManager
{
  private const float RoadCostMultiplier = 0.5f;
  private const float RoadAvoidalCost = 250;
  private const float RoadHeuristicWeight = 0.15f;

  private const int NodesToOpenBeforeRegionBasedPathing = 100000;
  public const int DefaultMoveTicksCardinal = 13;
  public const int DefaultMoveTicksDiagonal = 18;
  private const int SearchLimit = 160000;
  private const int TurnCostTicks = 3;
  private const float SecondsBetweenDebugDrawing = 1 / 1000f;

  private const float RootPosWeight = 0.75f;

  private readonly Dictionary<IntVec3, float> postCalculatedCells = [];

  private readonly FastPriorityQueue<CostNode> openList;
  private readonly VehiclePathFinderNodeFast[] calcGrid;

  private ushort statusOpenValue = 1;
  private ushort statusClosedValue = 2;

  private readonly int mapSizeX;
  private readonly int mapSizeZ;

  private VehiclePathGrid vehiclePathGrid;
  private readonly VehicleRegionCostCalculatorWrapper regionCostCalculator;

  private Area_Road roadGrid;
  private Area_RoadAvoidal roadAvoidalGrid;
  private readonly EdificeGrid edificeGrid;
  private readonly BlueprintGrid blueprintGrid;

  private readonly CellIndices cellIndices;
  private readonly List<int> disallowedCornerIndices = new(4);

  /// <summary>
  /// 8 directional x,y adjacent offsets
  /// </summary>
  internal static readonly int[] neighborOffsets =
  [
    //x coord
    0, //North
    1, //East
    0, //South
    -1, //West
    1, //NorthEast
    1, //SouthEast
    -1, //SouthWest
    -1, //NorthWest
    //y coord
    -1, //North
    0, //East
    1, //South
    0, //West
    -1, //NorthEast
    1, //SouthEast
    1, //SouthWest
    -1 //NorthWest
  ];

  private static readonly SimpleCurve nonRegionBasedHeuristicCurve =
  [
    new CurvePoint(50f, 1f),
    new CurvePoint(120f, 2f)
  ];

  private static readonly SimpleCurve heuristicWeightByNodesOpened =
  [
    new CurvePoint(0, 0),
    new CurvePoint(25, 0),
    new CurvePoint(50, 0.5f),
    new CurvePoint(150, 1f),
  ];

  private static readonly SimpleCurve regionHeuristicWeightByNodesOpened =
  [
    new CurvePoint(0f, 0),
    new CurvePoint(250, 0),
    new CurvePoint(3500f, 1f),
    new CurvePoint(4500f, 5f),
    new CurvePoint(30000f, 50f),
    new CurvePoint(100000f, 500f),
  ];

  public VehiclePathFinder(VehicleMapping mapping, VehicleDef vehicleDef) : base(mapping,
    vehicleDef)
  {
    roadGrid = mapping.map.areaManager.Get<Area_Road>();
    roadAvoidalGrid = mapping.map.areaManager.Get<Area_RoadAvoidal>();
    edificeGrid = mapping.map.edificeGrid;
    blueprintGrid = mapping.map.blueprintGrid;
    cellIndices = mapping.map.cellIndices;

    mapSizeX = mapping.map.Size.x;
    mapSizeZ = mapping.map.Size.z;
    calcGrid = new VehiclePathFinderNodeFast[mapSizeX * mapSizeZ];
    openList = new FastPriorityQueue<CostNode>(new CostNodeComparer());
    regionCostCalculator = new VehicleRegionCostCalculatorWrapper(mapping, vehicleDef);
  }

  public override void PostInit()
  {
    vehiclePathGrid = mapping[createdFor].VehiclePathGrid;
  }

  /// <summary>
  /// Find path from <paramref name="start"/> to <paramref name="start"/>
  /// </summary>
  /// <param name="start"></param>
  /// <param name="dest"></param>
  /// <param name="vehicle"></param>
  /// <param name="token"></param>
  /// <param name="peMode"></param>
  public VehiclePath FindPath(IntVec3 start, LocalTargetInfo dest, VehiclePawn vehicle,
    CancellationToken token, PathEndMode peMode = PathEndMode.OnCell)
  {
    if (!vehicle.FitsOnCell(dest.Cell))
    {
      Messages.Message("VF_CannotFit".Translate(), MessageTypeDefOf.RejectInput);
      return VehiclePath.NotFound;
    }
    return FindPath(start, dest, TraverseParms.For(vehicle), token, peMode);
  }

  /// <summary>
  /// Find path from <paramref name="start"/> to <paramref name="start"/> internal algorithm call
  /// </summary>
  /// <param name="start"></param>
  /// <param name="dest"></param>
  /// <param name="traverseParms"></param>
  /// <param name="token"></param>
  /// <param name="peMode"></param>
  public VehiclePath FindPath(IntVec3 start, LocalTargetInfo dest,
    TraverseParms traverseParms, CancellationToken token, PathEndMode peMode = PathEndMode.OnCell)
  {
    postCalculatedCells.Clear();
    if (DebugSettings.pathThroughWalls)
    {
      traverseParms.mode = TraverseMode.PassAllDestroyableThings;
    }

    VehiclePawn vehicle = traverseParms.pawn as VehiclePawn;
    Assert.IsNotNull(vehicle);

    if (!ValidatePathRequest(start, dest, traverseParms, peMode: peMode))
      return VehiclePath.NotFound;

    int x = dest.Cell.x;
    int z = dest.Cell.z;
    int vehicleSize = createdFor.Size.x * createdFor.Size.z;
    int startIndex = cellIndices.CellToIndex(start);
    int destIndex = cellIndices.CellToIndex(dest.Cell);
    vehicle.TryGetAvoidGrid(out AvoidGrid avoidGrid);

    roadGrid ??= mapping.map.areaManager.Get<Area_Road>();
    roadAvoidalGrid ??= mapping.map.areaManager.Get<Area_RoadAvoidal>();

    bool passAllDestroyableThings = traverseParms.mode == TraverseMode.PassAllDestroyableThings ||
      traverseParms.mode == TraverseMode.PassAllDestroyableThingsNotWater;
    bool freeTraversal = traverseParms.mode != TraverseMode.NoPassClosedDoorsOrWater &&
      traverseParms.mode != TraverseMode.PassAllDestroyableThingsNotWater;
    CellRect cellRect = CalculateDestinationRect(dest, peMode);
    bool singleRect = cellRect is { Width: 1, Height: 1 };
    int[] pathGrid = vehiclePathGrid.innerArray;
    TerrainDef[] topGrid = mapping.map.terrainGrid.topGrid;
    int searchCount = 0;
    int nodesOpened = 0;
    bool drawPaths = VehicleMod.settings.debug.debugDrawPathfinderSearch;
    bool allowedRegionTraversal = !passAllDestroyableThings &&
      VehicleRegionAndRoomQuery.RegionAt(start, mapping, createdFor) !=
      null && freeTraversal;
    bool weightedHeuristics = false;
    bool drafted = vehicle.Drafted;

    float heuristicStrength = DetermineHeuristicStrength(vehicle, start, dest);
    float ticksCardinal = vehicle.TicksPerMoveCardinal;
    float ticksDiagonal = vehicle.TicksPerMoveDiagonal;

    int minSize = VehicleMod.settings.main.fullVehiclePathing ?
      Mathf.Min(createdFor.Size.x, createdFor.Size.z) :
      1;

    ChunkSet chunks = null;
#if HIERARCHAL_PATHFINDING
    if (VehicleMod.settings.debug.hierarchalPathfinding)
    {
      try
      {
        chunks = mapping[createdFor].VehicleReachability.FindChunks(start, dest,
          PathEndMode.OnCell, traverseParms, debugDrawSearch: drawPaths,
          SecondsBetweenDebugDrawing);
      }
      catch (Exception ex)
      {
        Log.Error(
          $"Exception thrown while attempting to fetch chunks for HPA* search. Exception = {ex}");
        return VehiclePath.NotFound;
      }
    }

    bool useHPA = VehicleMod.settings.debug.hierarchalPathfinding && chunks != null &&
      !chunks.NullOrEmpty();
#else
    const bool useHPA = false;
#endif

    InitStatusesAndPushStartNode(ref startIndex, start);
    while (openList.Count > 0)
    {
      if (token.IsCancellationRequested)
      {
        Debug.Message("Path request canceled. Exiting...");
        return VehiclePath.NotFound;
      }

      CostNode costNode = openList.Pop();
      startIndex = costNode.index;

      if (!Mathf.Approximately(costNode.cost, calcGrid[startIndex].costNodeCost) ||
        calcGrid[startIndex].status == statusClosedValue)
      {
        continue;
      }

      IntVec3 prevCell = cellIndices.IndexToCell(startIndex);
      int x2 = prevCell.x;
      int z2 = prevCell.z;

      if (drawPaths)
      {
        float colorWeight = Mathf.Lerp(5000, 15000, vehicleSize / 15f);
        DebugFlash(prevCell, calcGrid[startIndex].knownCost / colorWeight,
          calcGrid[startIndex].knownCost.ToString("0"));
      }

      if (singleRect && startIndex == destIndex) //Single cell vehicles
      {
        return FinalizedPath(startIndex, weightedHeuristics);
      }
      else if (!singleRect && cellRect.Contains(prevCell) &&
        !disallowedCornerIndices.Contains(startIndex)) //Multi-cell vehicles
      {
        return FinalizedPath(startIndex, weightedHeuristics);
      }

      if (searchCount > SearchLimit)
      {
        Log.Warning(
          $"Vehicle {vehicle} pathing from {start} to {dest} hit search limit of {SearchLimit}.");
        DebugDrawRichData();
        return VehiclePath.NotFound;
      }

      for (int i = 0; i < 8; i++)
      {
        int cellIntX = x2 + neighborOffsets[i];
        int cellIntZ = z2 + neighborOffsets[i + 8];

        if (cellIntX < 0 || cellIntX >= mapSizeX || cellIntZ < 0 || cellIntZ >= mapSizeZ)
        {
          goto SkipNode; //skip out of bounds
        }

        int cellIndex = cellIndices.CellToIndex(cellIntX, cellIntZ);
        IntVec3 cellToCheck = new(cellIntX, 0, cellIntZ);

        Rot8 pathDir = Rot8.DirectionFromCells(prevCell, cellToCheck);
        if (useHPA && !chunks.Cells.Contains(cellToCheck))
        {
          goto SkipNode; //Node not included in hierarchal path, ignore
        }

        if (calcGrid[cellIndex].status != statusClosedValue || weightedHeuristics)
        {
          int initialCost = 0;
          if (!vehicle.DrivableFast(cellIndex))
          {
            if (!passAllDestroyableThings)
            {
              if (drawPaths)
              {
                DebugFlash(cellToCheck, 0.22f, "impass");
              }

              goto SkipNode;
            }

            initialCost += 70;
            Building building = edificeGrid[cellIndex];
            if (building is null)
            {
              if (drawPaths)
              {
                DebugFlash(cellToCheck, 0.22f, "impass");
              }

              goto SkipNode;
            }

            if (!IsDestroyable(building))
            {
              if (drawPaths)
              {
                DebugFlash(cellToCheck, 0.22f, "impass");
              }

              goto SkipNode;
            }

            initialCost += (int)(building.HitPoints * 0.2f);
          }

          float tickCost = ((i <= 3) ? ticksCardinal : ticksDiagonal) + initialCost;
          if (VehicleMod.settings.main.smoothVehiclePaths &&
            (vehicle.VehicleDef.size.x != 1 ||
              vehicle.VehicleDef.size.z != 1)) //Don't add turn cost for 1x1 vehicles
          {
            if (pathDir != costNode.direction)
            {
              int turnCost = costNode.direction.Difference(pathDir) * TurnCostTicks;
              tickCost += turnCost;
            }
          }

          float totalAreaCost = 0;
          float rootCost = 0;
          CellRect
            cellToCheckRect =
              vehicle.VehicleRect(cellToCheck,
                pathDir); // CellRect.CenteredOn(cellToCheck, Mathf.FloorToInt(minSize / 2f));
          foreach (IntVec3 cellInRect in cellToCheckRect)
          {
            if (!vehicle.Drivable(cellInRect))
            {
              if (drawPaths)
              {
                DebugFlash(cellInRect, 0.22f, "impass");
              }

              goto SkipNode; //hitbox has invalid node, ignore in neighbor search
            }

            int cellToCheckIndex = cellIndices.CellToIndex(cellInRect);

            //Give priority to roads if faction is non-hostile to player
            float roadMultiplier = 1;
            float roadExtraCost = 0;
            if (!vehicle.Faction.HostileTo(Faction.OfPlayer))
            {
              if (roadGrid[cellToCheckIndex])
              {
                roadMultiplier = RoadCostMultiplier;
              }
              else if (roadAvoidalGrid[cellToCheckIndex])
              {
                roadExtraCost = RoadAvoidalCost;
              }
            }

            float cellCost = pathGrid[cellToCheckIndex] * roadMultiplier + roadExtraCost;
            if (cellInRect == cellToCheck)
            {
              rootCost = cellCost * RootPosWeight;
            }
            else
            {
              totalAreaCost += cellCost * (1 - RootPosWeight);
            }
          }

          if (vehicleSize > 1)
          {
            tickCost +=
              Mathf.RoundToInt(totalAreaCost /
                (vehicleSize - 1)); //size - 1 to account for average of all cells except root
          }

          tickCost += Mathf.RoundToInt(rootCost);
          tickCost += drafted ?
            topGrid[cellIndex].extraDraftedPerceivedPathCost :
            topGrid[cellIndex].extraNonDraftedPerceivedPathCost;
          if (avoidGrid != null)
          {
            tickCost += avoidGrid.Grid[cellIndex] * 8;
          }

          if (!blueprintGrid.InnerArray[cellIndex].NullOrEmpty())
          {
            tickCost += 1000;
          }

          float calculatedCost = tickCost + calcGrid[startIndex].knownCost;
          ushort status = calcGrid[cellIndex].status;

          //For debug path drawing
          if (VehicleMod.settings.debug.debugDrawVehiclePathCosts)
          {
            postCalculatedCells[cellToCheck] = calculatedCost;
          }

          if (status == statusClosedValue || status == statusOpenValue)
          {
            float closedValueCost = 0;
            if (status == statusClosedValue)
            {
              closedValueCost = ticksCardinal;
            }

            if (calcGrid[cellIndex].knownCost <= calculatedCost + closedValueCost)
            {
              goto SkipNode;
            }
          }

          if (weightedHeuristics)
          {
            int pathCostFromDestToRegion =
              Mathf.RoundToInt(regionCostCalculator.GetPathCostFromDestToRegion(cellIndex));
            float heuristicWeight = regionHeuristicWeightByNodesOpened.Evaluate(nodesOpened);
            calcGrid[cellIndex].heuristicCost = pathCostFromDestToRegion * heuristicWeight;
            if (calcGrid[cellIndex].heuristicCost < 0)
            {
              Log.ErrorOnce(
                $"Heuristic cost overflow for vehicle {vehicle} pathing from {start} to {dest}.",
                vehicle.GetHashCode() ^ "FVPHeuristicCostOverflow".GetHashCode());
              calcGrid[cellIndex].heuristicCost = 0;
            }
          }
          else if (status != statusClosedValue && status != statusOpenValue)
          {
            int dx = Math.Abs(cellIntX - x);
            int dz = Math.Abs(cellIntZ - z);
            int octileDist = GenMath.OctileDistance(dx, dz, Mathf.RoundToInt(ticksCardinal),
              Mathf.RoundToInt(ticksDiagonal));
            float heuristicWeight = heuristicWeightByNodesOpened.Evaluate(nodesOpened);
            float roadHeuristicMultiplier = 1;
            if (!vehicle.Faction.HostileTo(Faction.OfPlayer) && roadGrid[cellIndex])
            {
              roadHeuristicMultiplier *= RoadHeuristicWeight;
            }

            calcGrid[cellIndex].heuristicCost =
              Mathf.RoundToInt(octileDist * heuristicStrength * heuristicWeight) *
              roadHeuristicMultiplier;
          }

          float costWithHeuristic = calculatedCost + calcGrid[cellIndex].heuristicCost;
          if (costWithHeuristic < 0)
          {
            Log.ErrorOnce(
              $"Node cost overflow for vehicle {vehicle} pathing from {start} to {dest}.",
              vehicle.GetHashCode() ^ "FVPNodeCostOverflow".GetHashCode());
            costWithHeuristic = 0;
          }

          calcGrid[cellIndex].parentIndex = startIndex;
          calcGrid[cellIndex].knownCost = calculatedCost;
          calcGrid[cellIndex].status = statusOpenValue;
          calcGrid[cellIndex].costNodeCost = costWithHeuristic;
          nodesOpened++;
          openList.Push(new CostNode(cellIndex, costWithHeuristic, pathDir));
        }

        SkipNode: ;
      }

      searchCount++;
      calcGrid[startIndex].status = statusClosedValue;
      if (nodesOpened >= NodesToOpenBeforeRegionBasedPathing && allowedRegionTraversal &&
        !weightedHeuristics)
      {
        weightedHeuristics = true;
        regionCostCalculator.Init(cellRect, traverseParms, ticksCardinal, ticksDiagonal,
          avoidGrid,
          drafted, disallowedCornerIndices);
        InitStatusesAndPushStartNode(ref startIndex, start);
        nodesOpened = 0;
        searchCount = 0;
      }
    }

    string curJob = vehicle.CurJob?.ToString() ?? "NULL";
    string curFaction = vehicle.Faction?.ToString() ?? "NULL";
    Log.Warning(
      $"Vehicle {vehicle} pathing from {start} to {dest} ran out of cells to process. Job={curJob} Faction={curFaction}");
    DebugDrawRichData();
    return VehiclePath.NotFound;
  }

  private bool ValidatePathRequest(IntVec3 start, LocalTargetInfo dest,
    TraverseParms traverseParms, PathEndMode peMode = PathEndMode.OnCell)
  {
    VehiclePawn vehicle = traverseParms.pawn as VehiclePawn;
    if (vehicle is null)
    {
      Log.Error("Tried to find Vehicle path for null vehicle.");
      return false;
    }
    else if (vehicle.Map != mapping.map)
    {
      Log.Error(
        $"Tried to FindVehiclePath for vehicle which is spawned in another map. Their map PathFinder should  have been used, not this one. vehicle={vehicle} vehicle's map={vehicle.Map} map={mapping.map}");
      return false;
    }

    if (!start.IsValid)
    {
      Log.Error($"Tried to FindVehiclePath with invalid start {start}. vehicle={vehicle}");
      return false;
    }

    if (!dest.IsValid)
    {
      Log.Error($"Tried to FindVehiclePath with invalid destination {dest}. vehicle={vehicle}");
      return false;
    }

    //Will almost always be ByPawn
    if (traverseParms.mode == TraverseMode.ByPawn &&
      !vehicle.CanReachVehicle(dest, peMode, Danger.Deadly, traverseParms.mode))
    {
      Log.Error(
        "Trying to path to region not reachable, this should be blocked by reachability checks.");
      return false;
    }

    return true;
  }

  /// <summary>
  /// Can path through <paramref name="thing"/> by destroying
  /// </summary>
  /// <param name="thing"></param>
  /// <returns></returns>
  public static bool IsDestroyable(Thing thing)
  {
    return thing.def.useHitPoints && thing.def.destroyable;
  }

  /// <summary>
  /// Diagonal movement is blocked
  /// </summary>
  public static bool BlocksDiagonalMovement(Map map, VehicleDef vehicleDef, int x, int z)
  {
    return BlocksDiagonalMovement(map, vehicleDef, map.cellIndices.CellToIndex(x, z));
  }

  /// <summary>
  /// Diagonal movement is blocked
  /// </summary>
  public static bool BlocksDiagonalMovement(Map map, VehicleDef vehicleDef, int index)
  {
    return map.GetCachedMapComponent<VehicleMapping>()[vehicleDef].VehiclePathGrid
     .WalkableFast(index) || map.edificeGrid[index] is Building_Door;
  }

  /// <summary>
  /// Diagonal movement is blocked
  /// </summary>
  public static bool BlocksDiagonalMovement(VehiclePawn vehicle, int x, int z)
  {
    return BlocksDiagonalMovement(vehicle, vehicle.Map.cellIndices.CellToIndex(x, z));
  }

  /// <summary>
  /// Diagonal movement is blocked
  /// </summary>
  /// <param name="vehicle"></param>
  /// <param name="index"></param>
  public static bool BlocksDiagonalMovement(VehiclePawn vehicle, int index)
  {
    return !vehicle.DrivableFast(index) || vehicle.Map.edificeGrid[index] is Building_Door;
  }

  /// <summary>
  /// Flash cell on map
  /// </summary>
  private void DebugFlash(IntVec3 cell, float colorPct, string label)
  {
    if (cell.InBounds(mapping.map))
    {
      DebugFlash(cell, mapping.map, colorPct, label);
    }
  }

  /// <summary>
  /// Flash cell on <paramref name="map"/> with duration
  /// </summary>
  private static void DebugFlash(IntVec3 cell, Map map, float colorPct, string label,
    int duration = 50)
  {
    //map.debugDrawer.FlashCell(cell, colorPct, text: label, duration: duration);
    CoroutineManager.QueueOrInvoke(
      () => map.debugDrawer.FlashCell(cell, colorPct, label, duration), SecondsBetweenDebugDrawing);
  }

  /// <summary>
  /// Finalize path results from internal algorithm call
  /// </summary>
  /// <param name="finalIndex"></param>
  /// <param name="usedRegionHeuristics"></param>
  private VehiclePath FinalizedPath(int finalIndex, bool usedRegionHeuristics)
  {
    DebugDrawPathCost();

    VehiclePath newPath = AsyncPool<VehiclePath>.Get();
    int index = finalIndex;
    while (true)
    {
      int parentIndex = calcGrid[index].parentIndex;
      IntVec3 cell = mapping.map.cellIndices.IndexToCell(index);
      newPath.AddNode(cell);
      if (index == parentIndex)
        break;
      index = parentIndex;
    }
    newPath.Init(usedRegionHeuristics);
    return newPath;
  }

  /// <summary>
  /// Push <paramref name="start"/> onto node list and reset associated <paramref name="curIndex"/> costs
  /// </summary>
  /// <param name="curIndex"></param>
  /// <param name="start"></param>
  private void InitStatusesAndPushStartNode(ref int curIndex, IntVec3 start)
  {
    statusOpenValue += 2;
    statusClosedValue += 2;
    if (statusClosedValue >= 65435)
    {
      ResetStatuses();
    }

    curIndex = cellIndices.CellToIndex(start);
    calcGrid[curIndex].knownCost = 0;
    calcGrid[curIndex].heuristicCost = 0;
    calcGrid[curIndex].costNodeCost = 0;
    calcGrid[curIndex].parentIndex = curIndex;
    calcGrid[curIndex].status = statusOpenValue;
    openList.Clear();
    openList.Push(new CostNode(curIndex, 0, Rot8.Invalid));
  }

  /// <summary>
  /// Reset all node statuses
  /// </summary>
  private void ResetStatuses()
  {
    for (int i = 0; i < calcGrid.Length; i++)
    {
      calcGrid[i].status = 0;
    }

    statusOpenValue = 1;
    statusClosedValue = 2;
  }

  /// <summary>
  /// Draw all open cells
  /// </summary>
  private void DebugDrawRichData()
  {
    if (VehicleMod.settings.debug.debugDrawVehiclePathCosts)
    {
      while (openList.Count > 0)
      {
        int index = openList.Pop().index;
        IntVec3 cell = new IntVec3(index % mapSizeX, 0, index / mapSizeX);
        DebugFlash(cell, 0, "open");
      }
    }
  }

  /// <summary>
  /// Draw all calculated path costs
  /// </summary>
  /// <param name="colorPct"></param>
  /// <param name="duration"></param>
  private void DebugDrawPathCost(float colorPct = 0f, int duration = 50)
  {
    if (VehicleMod.settings.debug.debugDrawVehiclePathCosts)
    {
      foreach ((IntVec3 cell, float cost) in postCalculatedCells)
      {
        DebugFlash(cell, mapping.map, colorPct, cost.ToString(), duration: duration);
      }
    }
  }

  //REDO - Allow player to modify weighted heuristic or spin into seperate thread for long distance traversal with accurate pathing
  /// <summary>
  /// Heuristic strength to use for A* algorithm
  /// </summary>
  /// <param name="vehicle"></param>
  /// <param name="start"></param>
  /// <param name="dest"></param>
  private float DetermineHeuristicStrength(VehiclePawn vehicle, IntVec3 start,
    LocalTargetInfo dest)
  {
    float lengthHorizontal = (start - dest.Cell).LengthHorizontal;
    return Mathf.RoundToInt(nonRegionBasedHeuristicCurve.Evaluate(lengthHorizontal));
  }

  /// <summary>
  /// Calculate rect on <paramref name="dest"/> target
  /// </summary>
  /// <param name="dest"></param>
  /// <param name="peMode"></param>
  private CellRect CalculateDestinationRect(LocalTargetInfo dest, PathEndMode peMode)
  {
    CellRect result;
    result = (!dest.HasThing || peMode == PathEndMode.OnCell) ?
      CellRect.SingleCell(dest.Cell) :
      dest.Thing.OccupiedRect();
    result = (peMode == PathEndMode.Touch) ? result.ExpandedBy(1) : result;
    return result;
  }

  /// <summary>
  /// Node data
  /// </summary>
  internal struct CostNode
  {
    public int index;
    public float cost;
    public Rot8 direction;

    public CostNode(int index, float cost, Rot8 direction)
    {
      this.index = index;
      this.cost = cost;
      this.direction = direction;
    }
  }

  /// <summary>
  /// Node data pre-calculation
  /// </summary>
  private struct VehiclePathFinderNodeFast
  {
    public float knownCost;
    public float heuristicCost;
    public int parentIndex;
    public float costNodeCost;
    public ushort status;
  }

  /// <summary>
  /// Node cost comparer for path determination
  /// </summary>
  internal class CostNodeComparer : IComparer<CostNode>
  {
    public int Compare(CostNode a, CostNode b)
    {
      return a.cost.CompareTo(b.cost);
    }
  }
}