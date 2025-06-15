﻿using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System.Linq;

namespace Vehicles
{
  public static class DebugHelper
  {
    public static readonly PathDebugData<DebugRegionType> Local =
      new PathDebugData<DebugRegionType>();

    public static readonly PathDebugData<WorldPathingDebugType> World =
      new PathDebugData<WorldPathingDebugType>();

    internal static List<WorldPath> debugLines = new List<WorldPath>();

    internal static List<Pair<int, int>>
      tiles = new List<Pair<int, int>>(); // Pair -> TileID : Cycle

    public static bool AnyDebugSettings => Local.DebugType != DebugRegionType.None ||
      World.DebugType != WorldPathingDebugType.None;

    /// <summary>
    /// Indiscriminately destroys all entities and roofs from area.
    /// </summary>
    /// <remarks>If non-destroyables should not be destroyed, use <see cref="GenDebug.ClearArea(CellRect, Map)"/> instead.</remarks>
    public static void DestroyArea(CellRect rect, Map map, TerrainDef replaceTerrain = null)
    {
      Thing.allowDestroyNonDestroyable = true;
      rect.ClipInsideMap(map);
      foreach (IntVec3 cell in rect)
      {
        map.roofGrid.SetRoof(cell, null);
      }

      foreach (IntVec3 cell in rect)
      {
        foreach (Thing thing in cell.GetThingList(map).ToList())
        {
          thing.Destroy();
        }
      }

      if (replaceTerrain != null)
      {
        foreach (IntVec3 cell in rect)
        {
          map.terrainGrid.SetTerrain(cell, replaceTerrain);
        }
      }

      Thing.allowDestroyNonDestroyable = false;
    }

    /// <summary>
    /// Draw settlement debug lines that show original locations before settlement was pushed to the coastline
    /// </summary>
    /// <param name="from"></param>
    /// <param name="to"></param>
    public static void DebugDrawSettlement(int from, int to)
    {
      PeaceTalks o =
        (PeaceTalks)WorldObjectMaker.MakeWorldObject(WorldObjectDefOfVehicles.DebugSettlement);
      o.Tile = from;
      o.SetFaction(Faction.OfMechanoids);
      Find.WorldObjects.Add(o);
      if (DebugProperties.drawPaths)
      {
        debugLines.Add(Find.WorldPathFinder.FindPath(from, to, null, null));
      }
    }

    public static List<Toggle> DebugToggles<T>(VehicleDef vehicleDef, PathDebugData<T> debugData)
      where T : Enum
    {
      List<Toggle> toggles = new List<Toggle>();
      if (Enum.GetUnderlyingType(typeof(T)) != typeof(int))
      {
        Log.Error(
          $"Cannot generate DebugToggles for enum type {typeof(T)}. Must be 32bit int to avoid overflow.");
        return toggles;
      }

      foreach (T @enum in Enum.GetValues(typeof(T)))
      {
        bool flags = typeof(T).IsDefined(typeof(FlagsAttribute), false);
        Toggle toggle = new Toggle(@enum.ToString(), stateGetter: delegate()
        {
          bool matchingDef = debugData.VehicleDef == vehicleDef;
          return matchingDef && ((flags && debugData.DebugType.HasFlag(@enum)) ||
            debugData.DebugType.Equals(@enum));
        }, stateSetter: delegate(bool value)
        {
          debugData.VehicleDef = vehicleDef;
          if (flags)
          {
            if (value)
            {
              debugData.DebugType = (T)Enum.ToObject(typeof(T),
                Convert.ToInt32(debugData.DebugType) | Convert.ToInt32(@enum));
            }
            else
            {
              debugData.DebugType = (T)Enum.ToObject(typeof(T),
                Convert.ToInt32(debugData.DebugType) & ~Convert.ToInt32(@enum));
            }
          }
          else if (value)
          {
            debugData.DebugType = @enum;
          }
        });

        toggles.Add(toggle);
      }

      return toggles;
    }

    /// <summary>
    /// Draw water regions to show if they are valid and initialized
    /// </summary>
    /// <param name="map"></param>
    public static void DebugDrawVehicleRegion(Map map)
    {
      if (Local.VehicleDef != null)
      {
        map.GetCachedMapComponent<VehicleMapping>()[Local.VehicleDef].VehicleRegionGrid
         .DebugDraw(Local.DebugType);
      }
    }

    /// <summary>
    /// Draw path costs overlay on GUI
    /// </summary>
    /// <param name="map"></param>
    public static void DebugDrawVehiclePathCostsOverlay(Map map)
    {
      if (Local.VehicleDef != null)
      {
        map.GetCachedMapComponent<VehicleMapping>()[Local.VehicleDef].VehicleRegionGrid
         .DebugOnGUI(Local.DebugType);
      }
    }

    public class PathDebugData<T> where T : Enum
    {
      private VehicleDef vehicleDef;
      private T debugType;

      public VehicleDef VehicleDef
      {
        get => vehicleDef;
        set => vehicleDef = value;
      }

      public T DebugType
      {
        get => debugType;
        set => debugType = value;
      }
    }
  }
}