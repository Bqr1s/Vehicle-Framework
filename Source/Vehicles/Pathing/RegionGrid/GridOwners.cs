using System.Collections.Generic;
using System.Linq;
using SmashTools;
using UnityEngine;
using Verse;

namespace Vehicles
{
  public static class GridOwners
  {
    private static int[] piggyToOwner;
    private static List<VehicleDef> owners;
    private static List<VehicleDef> piggies;

    private static PathConfig[] configs;

    private static object gridOwnerLock = new();

    public static List<VehicleDef> AllOwners => owners;

    public static List<VehicleDef> AllPiggies => piggies;

    internal static void Init()
    {
      // Lock needs to remain in place until piggyToOwner map is completely populated
      // with valid indices. Fetching invalid indices (-1) for known owners could result
      // in IOR exceptions for pathgrid / region updating.
      lock (gridOwnerLock)
      {
        piggyToOwner = new int[DefDatabase<VehicleDef>.DefCount].Populate(-1);

        List<VehicleDef> owners = [];
        List<VehicleDef> piggies = [];
        GenerateConfigs();
        SeparateIntoGroups(owners, piggies);

        // Reference assignment is atomic, but the lists need to be populated
        // separately since many callers will be accessing these through getters
        // and locking every single time they're accessed is an unnecessary performance
        // drain if they are never going to be changed outside of initialization.
        GridOwners.owners = owners;
        GridOwners.piggies = piggies;
      }
    }

    // Accessed from Init, already locked for the duration of owner generation
    private static void GenerateConfigs()
    {
      configs = new PathConfig[DefDatabase<VehicleDef>.DefCount];
      foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
      {
        configs[vehicleDef.DefIndex] = new PathConfig(vehicleDef);
      }
    }

    // Accessed from Init, already locked for the duration of owner generation
    private static void SeparateIntoGroups(List<VehicleDef> owners, List<VehicleDef> piggies, 
      bool compress = true)
    {
      foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
      {
        if (TryGetOwner(owners, vehicleDef, out int ownerId) && compress)
        {
          //Piggyback off vehicles with similar width + impassability
          piggyToOwner[vehicleDef.DefIndex] = ownerId;
          piggies.Add(vehicleDef);
        }
        else
        {
          piggyToOwner[vehicleDef.DefIndex] = vehicleDef.DefIndex;
          owners.Add(vehicleDef);
        }
      }
    }

    // Accessed from Init, already locked for the duration of owner generation
    private static bool TryGetOwner(List<VehicleDef> owners, VehicleDef vehicleDef, out int ownerId)
    {
      PathConfig config = configs[vehicleDef.DefIndex];
      foreach (VehicleDef checkingOwner in owners)
      {
        ownerId = checkingOwner.DefIndex;
        if (config.MatchesReachability(configs[ownerId]))
        {
          return true;
        }
      }
      ownerId = -1;
      return false;
    }

    public static bool IsOwner(VehicleDef vehicleDef)
    {
      lock (gridOwnerLock)
      {
        return piggyToOwner[vehicleDef.DefIndex] == vehicleDef.DefIndex;
      }
    }

    public static VehicleDef GetOwner(VehicleDef vehicleDef)
    {
      lock (gridOwnerLock)
      {
        int id = piggyToOwner[vehicleDef.DefIndex];
        return GetOwner(id);
      }
    }

    public static VehicleDef GetOwner(int ownerId)
    {
      lock (gridOwnerLock)
      {
        return owners.FirstOrDefault(vehicleDef => vehicleDef.DefIndex == ownerId);
      }
    }

    private readonly struct PathConfig
    {
      private readonly VehicleDef vehicleDef;

      private readonly HashSet<ThingDef> impassableThingDefs;
      private readonly HashSet<TerrainDef> impassableTerrain;
      private readonly int size;
      private readonly bool defaultTerrainImpassable;

      internal PathConfig(VehicleDef vehicleDef)
      {
        this.vehicleDef = vehicleDef;

        size = Mathf.Min(vehicleDef.Size.x, vehicleDef.Size.z);
        defaultTerrainImpassable = vehicleDef.properties.defaultTerrainImpassable;
        impassableThingDefs = vehicleDef.properties.customThingCosts.Where(kvp => kvp.Value >= VehiclePathGrid.ImpassableCost).Select(kvp => kvp.Key).ToHashSet();
        impassableTerrain = vehicleDef.properties.customTerrainCosts.Where(kvp => kvp.Value >= VehiclePathGrid.ImpassableCost).Select(kvp => kvp.Key).ToHashSet();
      }

      public bool UsesRegions => vehicleDef.vehicleMovementPermissions > VehiclePermissions.NotAllowed;

      public bool MatchesReachability(PathConfig other)
      {
        return UsesRegions == other.UsesRegions && size == other.size && defaultTerrainImpassable == other.defaultTerrainImpassable &&
          impassableThingDefs.SetEquals(other.impassableThingDefs) && impassableTerrain.SetEquals(other.impassableTerrain);
      }
    }
  }
}
