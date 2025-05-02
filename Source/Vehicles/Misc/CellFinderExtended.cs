﻿using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SmashTools;
using SmashTools.Performance;
using UnityEngine.Assertions;
using Verse;
using Verse.AI;

namespace Vehicles
{
  public static class CellFinderExtended
  {
    private static List<IntVec3> mapEdgeCells;
    private static IntVec3 mapEdgeCellsSize;
    private static readonly List<VehicleRegion> workingRegions = [];

    public static bool TryFindRandomEdgeCell(Rot4 dir, Map map, Predicate<IntVec3> validator,
      int offset, out IntVec3 result)
    {
      List<IntVec3> cellsToCheck = dir.IsValid ?
        CellRect.WholeMap(map).ContractedBy(offset).GetEdgeCells(dir).ToList() :
        CellRect.WholeMap(map).ContractedBy(offset).EdgeCells.ToList();
      for (;;)
      {
        IntVec3 rCell = cellsToCheck.PopRandom();
        if (validator(rCell))
        {
          result = rCell;
          return true;
        }

        if (cellsToCheck.Count <= 0)
        {
          Log.Warning("Failed to find edge cell at " + dir.AsInt);
          break;
        }
      }

      result = CellFinder.RandomEdgeCell(map);
      return false;
    }

    public static bool TryFindRandomCenterCell(Map map, Predicate<IntVec3> validator,
      out IntVec3 result, bool allowRoofed = false)
    {
      Faction hostFaction = map.ParentFaction ?? Faction.OfPlayer;
      List<Thing> thingsOnMap = map.mapPawns.FreeHumanlikesSpawnedOfFaction(hostFaction)
       .Cast<Thing>().ToList();
      if (hostFaction == Faction.OfPlayer)
      {
        thingsOnMap.AddRange(map.listerBuildings.allBuildingsColonist);
      }
      else
      {
        thingsOnMap.AddRange(map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial)
         .Where(thing => thing.Faction == hostFaction));
      }

      float num2 = 65f;
      for (int i = 0; i < 300; i++)
      {
        CellFinder.TryFindRandomCellNear(map.Center, map, 30, validator, out IntVec3 intVec);
        if (validator(intVec) && !intVec.Fogged(map))
        {
          if (allowRoofed || !Ext_Vehicles.IsRoofed(intVec, map))
          {
            num2 -= 0.2f;
            bool flag = false;
            foreach (Thing thing in thingsOnMap)
            {
              if ((intVec - thing.Position).LengthHorizontalSquared < num2 * num2)
              {
                flag = true;
                break;
              }
            }

            if (!flag && map.reachability.CanReachFactionBase(intVec, hostFaction))
            {
              result = intVec;
              return true;
            }
          }
        }
      }

      result = IntVec3.Invalid;
      return false;
    }

    public static IntVec3 MiddleEdgeCell(Rot4 dir, Map map, Pawn pawn, Predicate<IntVec3> validator)
    {
      List<IntVec3> cellsToCheck = CellRect.WholeMap(map).GetEdgeCells(dir).ToList();
      bool riverSpawn = Find.World.CoastDirectionAt(map.Tile) != dir &&
        !Find.WorldGrid[map.Tile.tileId].Rivers.NullOrEmpty();
      int padding = (pawn.def.size.z / 2) > 4 ? (pawn.def.size.z / 2 + 1) : 4;
      int startIndex = cellsToCheck.Count / 2;

      bool riverSpawnValidator(IntVec3 x) =>
        map.terrainGrid.TerrainAt(x) == TerrainDefOf.WaterMovingChestDeep ||
        map.terrainGrid.TerrainAt(x) == TerrainDefOf.WaterMovingShallow;

      for (int j = 0; j < 10000; j++)
      {
        IntVec3 c = pawn.ClampToMap(CellFinder.RandomEdgeCell(dir, map), map, padding);

        foreach (IntVec3 cAll in pawn.PawnOccupiedCells(c, dir.Opposite))
        {
          if (!validator(cAll) || (riverSpawn && !riverSpawnValidator(cAll)))
          {
            goto Block_Skip;
          }
        }

        Debug.Message("Found: " + c);
        return c;
        Block_Skip: ;
      }

      Log.Warning("Running secondary spawn cell check for boats");
      int i = 0;
      while (cellsToCheck.Count > 0 && i < cellsToCheck.Count / 2)
      {
        if (i > cellsToCheck.Count)
        {
          Log.Warning(
            "List of Cells almost went out of bounds. Report to Boats mod author - Smash Phil");
          break;
        }

        IntVec3 rCell = pawn.ClampToMap(cellsToCheck[startIndex + i], map, padding);
        Debug.Message("Checking right: " + rCell + " | " + validator(rCell));
        foreach (IntVec3 c in pawn.PawnOccupiedCells(rCell, dir.Opposite))
        {
          if (!validator(c))
            goto Block_0;
        }

        return rCell;

        Block_0: ;
        IntVec3 lCell = pawn.ClampToMap(cellsToCheck[startIndex - i], map, padding);
        Debug.Message("Checking left: " + lCell + " | " + validator(lCell));
        foreach (IntVec3 c in pawn.PawnOccupiedCells(lCell, dir.Opposite))
        {
          if (!validator(c))
            goto Block_1;
        }

        return lCell;

        Block_1: ;
        i++;
        Debug.Message("==============");
      }

      Log.Error(
        "Could not find valid edge cell to spawn boats on. This could be due to the Boat being too large to spawn on the coast of a Mountainous Map.");
      return pawn.ClampToMap(CellFinder.RandomEdgeCell(dir, map), map, padding);
    }

    public static bool TryFindRandomReachableCellNear(IntVec3 root, Map map, VehicleDef vehicleDef,
      float radius, TraverseParms traverseParms,
      Predicate<IntVec3> extraValidator, Predicate<VehicleRegion> regionValidator,
      out IntVec3 result, int maxRegions = 999999)
    {
      if (map is null)
      {
        Log.Error("Tried to find reachable cell using a null map");
        result = IntVec3.Invalid;
        return false;
      }
      //return TryFindRandomEdgeCell(Rot4.Invalid, map, (IntVec3 c) => GenGridVehicles.Standable(c, vehicleDef, map) && !c.Fogged(map), 0, out result);

      VehicleRegion region =
        VehicleRegionAndRoomQuery.RegionAt(root, map, vehicleDef, RegionType.Set_Passable);
      if (region == null)
      {
        result = IntVec3.Invalid;
        return false;
      }

      workingRegions.Clear();
      float radSquared = radius * radius;
      VehicleRegionTraverser.BreadthFirstTraverse(root, map, vehicleDef,
        (VehicleRegion from, VehicleRegion to) => to.Allows(traverseParms, true)
          && (radius > 1000f || to.extentsClose.ClosestDistSquaredTo(root) <= radSquared)
          && (regionValidator == null || regionValidator(to)),
        delegate(VehicleRegion region)
        {
          workingRegions.Add(region);
          return false;
        }, maxRegions, RegionType.Set_Passable);

      while (workingRegions.Count > 0)
      {
        VehicleRegion currentRegion =
          workingRegions.RandomElementByWeight((VehicleRegion region) => region.CellCount);
        if (currentRegion.TryFindRandomCellInRegion(Validator, out result))
        {
          workingRegions.Clear();
          return true;
        }

        workingRegions.Remove(currentRegion);
      }

      result = IntVec3.Invalid;
      workingRegions.Clear();
      return false;

      bool Validator(IntVec3 cell)
      {
        return (cell - root).LengthHorizontalSquared <= radSquared &&
          (extraValidator == null || extraValidator(cell));
      }
    }

    public static bool TryFindRandomCellInRegion(this VehicleRegion region,
      Predicate<IntVec3> validator, out IntVec3 result)
    {
      for (int i = 0; i < 10; i++)
      {
        result = region.RandomCell;
        if (validator == null || validator(result))
        {
          return true;
        }
      }

      List<IntVec3> cells = AsyncPool<List<IntVec3>>.Get();
      cells.AddRange(region.Cells);
      cells.Shuffle();
      for (int j = 0; j < cells.Count; j++)
      {
        result = cells[j];
        if (validator == null || validator(result))
        {
          return true;
        }
      }

      result = region.RandomCell;
      cells.Clear();
      AsyncPool<List<IntVec3>>.Return(cells);
      return false;
    }

    public static IntVec3 RandomClosewalkCellNear(IntVec3 root, Map map, VehicleDef vehicleDef,
      int radius, Predicate<IntVec3> validator = null)
    {
      if (TryRandomClosewalkCellNear(root, map, vehicleDef, radius, out IntVec3 result, validator))
      {
        return result;
      }

      return root;
    }

    public static bool TryRandomClosewalkCellNear(IntVec3 root, Map map, VehicleDef vehicleDef,
      int radius, out IntVec3 result, Predicate<IntVec3> validator = null)
    {
      return TryFindRandomReachableCellNear(root, map, vehicleDef, radius,
        TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Deadly, false), validator, null,
        out result);
    }

    public static IntVec3 RandomSpawnCellForPawnNear(IntVec3 root, Map map, Pawn pawn,
      Predicate<IntVec3> validator, bool waterEntry = false, int firstTryWithRadius = 4)
    {
      IntVec3 result;
      if (pawn is VehiclePawn vehicle)
      {
        if (validator(root) && root.GetFirstPawn(map) is null &&
          vehicle.CellRectStandable(map, root))
        {
          return root;
        }

        int num = firstTryWithRadius;
        for (int i = 0; i < 3; i++)
        {
          if (waterEntry)
          {
            if (TryFindRandomReachableCellNear(root, map, vehicle.VehicleDef, num,
              TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Deadly, false),
              (IntVec3 c) => validator(c) && vehicle.CellRectStandable(map, c) &&
                (root.Fogged(map) || !c.Fogged(map)) && c.GetFirstPawn(map) is null, null,
              out result))
            {
              return result;
            }
          }
          else
          {
            if (CellFinder.TryFindRandomReachableNearbyCell(root, map, num,
              TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Deadly, false),
              (IntVec3 c) => validator(c) && vehicle.CellRectStandable(map, c) &&
                (root.Fogged(map) || !c.Fogged(map)) && c.GetFirstPawn(map) is null, null,
              out result))
            {
              return result;
            }
          }

          num *= 2;
        }

        num = firstTryWithRadius + 1;

        while (!TryRandomClosewalkCellNear(root, map, vehicle.VehicleDef, num, out result, null))
        {
          if (num > map.Size.x / 2 && num > map.Size.z / 2)
          {
            return root;
          }

          num *= 2;
        }

        return result;
      }

      if (CellFinder.TryFindRandomSpawnCellForPawnNear(root, map, out result, firstTryWithRadius,
        validator))
      {
        return result;
      }

      return root;
    }

    public static bool TryFindRandomEdgeCellWith(Predicate<IntVec3> validator, Map map,
      Rot4 exitDir, VehicleDef largestVehicleDef, float roadChance, out IntVec3 result)
    {
      result = IntVec3.Invalid;

      if (Rand.Chance(roadChance))
      {
        CacheAndShuffleMapEdgeCells(map);

        Area_Road roadArea = map.areaManager.Get<Area_Road>();
        foreach (IntVec3 cell in mapEdgeCells)
        {
          IntVec3 paddedCell = cell.PadForHitbox(map, largestVehicleDef);
          if (roadArea[paddedCell] && validator(paddedCell))
          {
            result = paddedCell;
            return true;
          }
        }

        foreach (IntVec3 cell in map.roadInfo.roadEdgeTiles)
        {
          IntVec3 paddedCell = cell.PadForHitbox(map, largestVehicleDef);
          if (validator(paddedCell))
          {
            result = paddedCell;
            return true;
          }
        }
      }

      //Try to find random edge cell quickly
      for (int i = 0; i < 100; i++)
      {
        result = CellFinder.RandomEdgeCell(map).PadForHitbox(map, largestVehicleDef);
        if (validator(result))
        {
          return true;
        }
      }

      CacheAndShuffleMapEdgeCells(map);

      foreach (IntVec3 cell in mapEdgeCells)
      {
        try
        {
          if (validator(cell))
          {
            result = cell;
            return true;
          }
        }
        catch (Exception ex)
        {
          Log.Error(
            $"CellFinderExtended.TryFindRandomEdgeCellWith threw exception while validating {cell}. Exception={ex}");
        }
      }

      result = IntVec3.Invalid;
      return false;
    }

    public static bool TryFindRandomExitSpot(VehiclePawn vehicle, out IntVec3 dest,
      TraverseMode mode = TraverseMode.ByPawn)
    {
      Assert.IsTrue(vehicle.Spawned, "Trying to find exit spot for despawned vehicle.");

      Map map = vehicle.Map;
      VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
      VehicleMapping.VehiclePathData pathData = mapping[vehicle.VehicleDef];

      Danger maxDanger = Danger.Some;
      IntVec3 cell;
      for (int i = 0; i < 40; i++)
      {
        // Increase danger tolerance in hopes of finding any spot to exit
        if (i > 15)
        {
          maxDanger = Danger.Deadly;
        }

        // NOTE - It's faster to just pull a random cell + random direction and then adjust to the edge,
        // than it is to fetch a random cell from the list of edge cells. Needing padding won't change that
        cell = CellFinder.RandomCell(map);
        int dir = Rand.RangeInclusive(0,
          3); // Just use int instead of casting back and forth from Rot4.Random
        switch (dir)
        {
          case 0:
            cell.x = 0;
            break;
          case 1:
            cell.x = map.Size.x - 1;
            break;
          case 2:
            cell.z = 0;
            break;
          case 3:
            cell.z = map.Size.z - 1;
            break;
        }

        cell = cell.PadForHitbox(map, vehicle.VehicleDef);
        if (Validator(cell))
        {
          dest = cell;
          return true;
        }
      }

      dest = vehicle.Position;
      return false;

      bool Validator(IntVec3 cell)
      {
        IntVec3 paddedCell = cell.PadForHitbox(map, vehicle);
        return pathData.VehicleReachability.CanReachVehicle(vehicle.Position, paddedCell,
          PathEndMode.OnCell, mode, maxDanger);
      }
    }

    public static bool TryFindBestExitSpot(VehiclePawn vehicle, out IntVec3 cell,
      TraverseMode mode = TraverseMode.ByPawn)
    {
      Assert.IsTrue(vehicle.Spawned, "Trying to find exit spot for despawned vehicle.");

      cell = IntVec3.Invalid;
      Map map = vehicle.Map;
      VehicleMapping mapping = map.GetCachedMapComponent<VehicleMapping>();
      VehicleMapping.VehiclePathData pathData = mapping[vehicle.VehicleDef];
      if (!pathData.VehicleReachability.CanReachMapEdge(vehicle.Position,
        TraverseParms.For(vehicle)))
      {
        return false;
      }

      // More attempts allowed than vanilla since failing to find an exit location for vehicles would mean
      // ditching. This may have a non-negligeable impact on game balance for maps with narrow map edges.
      int sqrRadius = 0;
      for (int i = 0; i < 100; i++)
      {
        sqrRadius += 4;
        if (!CellFinder.TryFindRandomCellNear(vehicle.Position, map, sqrRadius, null,
          out IntVec3 searchCell))
        {
          continue;
        }

        int x = searchCell.x;
        cell = new IntVec3(0, 0, searchCell.z).PadForHitbox(map, vehicle.VehicleDef);
        if (vehicle.Map.Size.z - searchCell.z < x)
        {
          x = map.Size.z - searchCell.z;
          cell = new IntVec3(searchCell.x, 0, map.Size.z - 1).PadForHitbox(map, vehicle.VehicleDef);
        }

        if (map.Size.x - searchCell.x < x)
        {
          x = map.Size.x - searchCell.x;
          cell = new IntVec3(map.Size.x - 1, 0, searchCell.z).PadForHitbox(map, vehicle.VehicleDef);
        }

        if (searchCell.z < x)
        {
          cell = new IntVec3(searchCell.x, 0, 0).PadForHitbox(map, vehicle.VehicleDef);
        }

        if (cell.Standable(vehicle, map) &&
          pathData.VehicleReachability.CanReachVehicle(vehicle.Position, cell, PathEndMode.OnCell,
            mode, Danger.Deadly))
        {
          return true;
        }
      }

      // Last attempt to find any exit location before giving up and ditching vehicle
      for (int i = 0; i < Rot4.RotationCount; i++)
      {
        if (CellFinderExtended.TryFindRandomEdgeCellWith(Validator, map, new Rot4(i),
          vehicle.VehicleDef, 0, out cell))
        {
          return true;
        }
      }

      cell = IntVec3.Invalid;
      return false;

      bool Validator(IntVec3 cell)
      {
        IntVec3 paddedCell = cell.PadForHitbox(map, vehicle);
        return pathData.VehicleReachability.CanReachVehicle(vehicle.Position, paddedCell,
          PathEndMode.OnCell, TraverseParms.For(vehicle));
      }
    }

    public static bool TryRadialSearchForCell(IntVec3 cell, Map map, float radius,
      Predicate<IntVec3> validator, out IntVec3 result)
    {
      result = IntVec3.Invalid;
      int num = GenRadial.NumCellsInRadius(radius);
      for (int i = 0; i < num; i++)
      {
        IntVec3 radialCell = GenRadial.RadialPattern[i] + cell;
        if (radialCell.InBounds(map) && validator(radialCell))
        {
          result = radialCell;
          return true;
        }
      }

      return false;
    }

    private static void CacheAndShuffleMapEdgeCells(Map map)
    {
      if (mapEdgeCells.NullOrEmpty() || map.Size != mapEdgeCellsSize)
      {
        mapEdgeCellsSize = map.Size;
        mapEdgeCells = CellRect.WholeMap(map).EdgeCells.ToList();
      }

      mapEdgeCells.Shuffle();
    }
  }
}