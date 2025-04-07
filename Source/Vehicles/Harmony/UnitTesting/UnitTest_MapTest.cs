using System;
using System.Collections.Generic;
using DevTools;
using RimWorld;
using SmashTools;
using SmashTools.UnitTesting;
using UnityEngine;
using Verse;

namespace Vehicles.Testing;

internal abstract class UnitTest_MapTest : UnitTest_VehicleTest
{
  protected virtual Map TestMap => Find.CurrentMap;

  protected virtual Faction Faction => Faction.OfPlayer;

  public override TestType ExecuteOn => TestType.Playing;

  protected virtual bool RefreshGrids => true;

  protected virtual bool ShouldTest(VehicleDef vehicleDef)
  {
    return true;
  }

  protected virtual CellRect TestArea(VehicleDef vehicleDef, IntVec3 root)
  {
    int maxSize = Mathf.Max(vehicleDef.Size.x, vehicleDef.Size.z);
    return CellRect.CenteredOn(root, maxSize).ExpandedBy(5);
  }

  public override IEnumerable<UTResult> Execute()
  {
    CameraJumper.TryHideWorld();
    Assert.IsNotNull(TestMap);
    Assert.IsTrue(DefDatabase<VehicleDef>.AllDefsListForReading.Count > 0,
      "No vehicles to test with");

    // All map-based tests should be run synchronously, otherwise 
    // we would have race conditions when validating grids.
    using ThreadDisabler td = new();

    VehicleMapping mapping = TestMap.GetCachedMapComponent<VehicleMapping>();

    foreach (VehicleDef vehicleDef in VehicleHarmony.AllMoveableVehicleDefs)
    {
      if (!ShouldTest(vehicleDef)) continue;

      if (RefreshGrids)
        mapping.RequestGridsFor(vehicleDef, DeferredGridGeneration.Urgency.Urgent);

      // Path and region grids should all be initialized before starting any map-based test.
      Assert.IsTrue(!mapping[vehicleDef].Suspended);
      Assert.IsTrue(mapping[vehicleDef].VehiclePathGrid.Enabled);
      if (!mapping.GridOwners.IsOwner(vehicleDef))
        Assert.IsTrue(mapping[mapping.GridOwners.GetOwner(vehicleDef)].VehiclePathGrid.Enabled);

      VehiclePawn vehicle = VehicleSpawner.GenerateVehicle(vehicleDef, Faction);
      TerrainDef terrainDef = DefDatabase<TerrainDef>.AllDefsListForReading
       .FirstOrDefault(def => VehiclePathGrid.PassableTerrainCost(vehicleDef, def, out _) &&
          def.affordances.Contains(vehicleDef.buildDef.terrainAffordanceNeeded));

      IntVec3 root = TestMap.Center;
      DebugHelper.DestroyArea(TestArea(vehicleDef, root), TestMap, terrainDef);

      CameraJumper.TryJump(root, TestMap, mode: CameraJumper.MovementMode.Cut);
      yield return TestVehicle(vehicle, root);

      // Ensure vehicles are completely cleared from caches to not interfere with other tests.
      if (!vehicle.Destroyed)
        vehicle.DestroyVehicleAndPawns();

      Find.WorldPawns.RemoveAndDiscardPawnViaGC(vehicle);
      Assert.IsFalse(Find.WorldPawns.Contains(vehicle));

      // Grids from owners shouldn't interfere with their piggies. If we don't clear, we don't
      // be able to validate incorrect grid updating between owner and piggy.
      if (RefreshGrids)
        mapping.deferredGridGeneration.DoPass();
    }
  }

  protected abstract UTResult TestVehicle(VehiclePawn vehicle, IntVec3 root);

  /// <summary>
  /// Test class for validating cells within a vehicle's hitbox.
  /// </summary>
  protected class HitboxTester<T>
  {
    private readonly VehiclePawn vehicle;
    private readonly Func<IntVec3, T> valueGetter;
    private readonly Func<T, bool> validator;
    private readonly Action<IntVec3> reset;

    private readonly CellRect rect;

    public HitboxTester(VehiclePawn vehicle, IntVec3 root, Func<IntVec3, T> valueGetter,
      Func<T, bool> validator, Action<IntVec3> reset = null)
    {
      this.vehicle = vehicle;
      this.valueGetter = valueGetter;
      this.validator = validator;
      this.reset = reset;

      int radius = Mathf.Max(vehicle.VehicleDef.Size.x, vehicle.VehicleDef.Size.z);
      rect = CellRect.CenteredOn(root, radius);
    }

    public void Start()
    {
      Reset();
    }

    public void Reset()
    {
      if (reset != null)
      {
        foreach (IntVec3 cell in rect)
        {
          reset.Invoke(cell);
        }
      }
    }

    public bool All(bool value)
    {
      return IsTrue(_ => value);
    }

    public bool Hitbox(bool value)
    {
      return IsTrue(cell => value ^ !vehicle.OccupiedRect().Contains(cell));
    }

    public bool IsTrue(Func<IntVec3, bool> expected)
    {
      foreach (IntVec3 cell in rect)
      {
        if (!Valid(cell, expected(cell)))
        {
          Valid(cell, expected(cell));
          return false;
        }
      }

      return true;
    }

    private bool Valid(IntVec3 cell, bool expected)
    {
      T current = valueGetter(cell);
      bool value = validator(current);
      bool result = value == expected;
      return result;
    }
  }
}