using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using Verse;

namespace Vehicles
{
  /// <summary>
  /// Handles instance behavior of a vehicle's role.
  /// </summary>
  [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
  public class VehicleRoleHandler : IExposable, ILoadReferenceable, IThingHolderPawnOverlayer,
                                    IParallelRenderer
  {
    /// <summary>
    /// innerContainer for role instance
    /// </summary>
    public ThingOwner<Pawn> thingOwner;

    private /* readonly */ string roleKey;
    public VehicleRole role;

    public int uniqueID = -1;
    public VehiclePawn vehicle;

    public VehicleRoleHandler()
    {
      thingOwner = new ThingOwner<Pawn>(this, false);
    }

    public VehicleRoleHandler(VehiclePawn vehicle) : this()
    {
      uniqueID = VehicleIdManager.Instance.GetNextHandlerId();
      this.vehicle = vehicle;
    }

    public VehicleRoleHandler(VehiclePawn vehicle, VehicleRole role) : this(vehicle)
    {
      this.role = new VehicleRole(role); //Role must be instance based for upgrades to modify data
      roleKey = role.key;
    }

    public IThingHolder ParentHolder => vehicle;

    Rot4 IThingHolderPawnOverlayer.PawnRotation =>
      role.PawnRenderer?.RotFor(vehicle.FullRotation) ?? Rot4.South;

    float IThingHolderWithDrawnPawn.HeldPawnDrawPos_Y =>
      vehicle.DrawPos.y + role.PawnRenderer.LayerFor(vehicle.FullRotation);

    float IThingHolderWithDrawnPawn.HeldPawnBodyAngle =>
      role.PawnRenderer.AngleFor(vehicle.FullRotation);

    PawnPosture IThingHolderWithDrawnPawn.HeldPawnPosture => PawnPosture.LayingInBedFaceUp;

    bool IThingHolderPawnOverlayer.ShowBody => role.PawnRenderer.showBody;

    public bool RequiredForMovement => role.HandlingTypes.HasFlag(HandlingType.Movement);

    public bool RoleFulfilled
    {
      get
      {
        bool minRequirement = role != null && thingOwner.Count >= role.SlotsToOperate;
        if (!minRequirement)
        {
          return false;
        }
        int operationalCount = 0;
        foreach (Pawn pawn in thingOwner)
        {
          if (CanOperateRole(pawn))
          {
            operationalCount++;
          }
        }
        return operationalCount >= role.SlotsToOperate;
      }
    }

    public bool AreSlotsAvailable => role != null && thingOwner.Count < role.Slots;

    public bool AreSlotsAvailableAndReservable
    {
      get
      {
        if (vehicle.Map == null)
          return AreSlotsAvailable;

        return AreSlotsAvailable && vehicle.Map.GetCachedMapComponent<VehicleReservationManager>()
         .CanReserve<VehicleRoleHandler, VehicleHandlerReservation>(vehicle, null, this);
      }
    }

    public void DoTick()
    {
      thingOwner.DoTick();
    }

    public void DynamicDrawPhaseAt(DrawPhase phase, in TransformData transformData)
    {
      foreach (Pawn pawn in thingOwner)
      {
        Rot4 rotOverride = role.PawnRenderer.RotFor(transformData.orientation);
        Vector3 offset = role.PawnRenderer.DrawOffsetFor(transformData.orientation);
        pawn.Drawer.renderer.DynamicDrawPhaseAt(phase, transformData.position + offset,
          rotOverride: rotOverride,
          neverAimWeapon: true);
      }
    }

    //public void Draw()
    //{
    //  foreach (Pawn pawn in handlers)
    //  {
    //    pawn.Drawer.renderer.RenderPawnAt(
    //      transformData.position + role.PawnRenderer.DrawOffsetFor(transformData.orientation),
    //      rotOverride: role.PawnRenderer.RotFor(transformData.orientation));
    //  }
    //}

    public static bool operator ==(VehicleRoleHandler lhs, VehicleRoleHandler rhs)
    {
      if (lhs is null)
      {
        return rhs is null;
      }
      return lhs.Equals(rhs);
    }

    public static bool operator !=(VehicleRoleHandler lhs, VehicleRoleHandler rhs)
    {
      return !(lhs == rhs);
    }

    public static bool operator ==(VehicleRoleHandler lhs, IThingHolder rhs)
    {
      if (rhs is not VehicleRoleHandler handler)
        return false;
      return lhs == handler;
    }

    public static bool operator !=(VehicleRoleHandler lhs, IThingHolder rhs)
    {
      return !(lhs == rhs);
    }

    public bool CanOperateRole(Pawn pawn)
    {
      if (role.HandlingTypes > HandlingType.None)
      {
        bool manipulation = pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
        bool downed = pawn.Downed;
        bool dead = pawn.Dead;
        bool isCrazy = pawn.InMentalState;
        bool prisoner = pawn.IsPrisoner;
        return manipulation && !downed && !dead && !isCrazy && !prisoner;
      }
      return true;
    }

    public override bool Equals(object obj)
    {
      return obj is VehicleRoleHandler handler && Equals(handler);
    }

    public bool Equals(VehicleRoleHandler obj2)
    {
      return obj2?.roleKey == roleKey;
    }

    public override string ToString()
    {
      return roleKey;
    }

    public override int GetHashCode()
    {
      // ReSharper disable once NonReadonlyMemberInGetHashCode
      return roleKey.GetHashCode();
    }

    public string GetUniqueLoadID()
    {
      return $"VehicleHandler_{uniqueID}";
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
      ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
    }

    public ThingOwner GetDirectlyHeldThings()
    {
      return thingOwner;
    }

    public void ExposeData()
    {
      Scribe_Values.Look(ref uniqueID, nameof(uniqueID), -1);
      Scribe_References.Look(ref vehicle, nameof(vehicle), true);
      Scribe_Values.Look(ref roleKey, nameof(role), forceSave: true);

      if (Scribe.mode == LoadSaveMode.Saving)
      {
        //Deep save if inner pawns are not world pawns, as they will not be saved in the WorldPawns list
        thingOwner.contentsLookMode =
          (thingOwner.InnerListForReading.FirstOrDefault()?.IsWorldPawn() ?? false) ?
            LookMode.Reference :
            LookMode.Deep;
      }
      Scribe_Deep.Look(ref thingOwner, nameof(thingOwner), this);

      if (Scribe.mode == LoadSaveMode.PostLoadInit)
      {
        role = vehicle.VehicleDef.CreateRole(roleKey);
        if (role is null)
        {
          Log.Error(
            $"Unable to load role={roleKey}. Creating empty role to avoid game-breaking issues.");
          role ??= new VehicleRole()
          {
            key = $"{roleKey}_INVALID",
            label = $"{roleKey} (INVALID)",
          };
        }
      }
    }
  }
}