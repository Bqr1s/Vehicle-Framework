using System;
using SmashTools;
using SmashTools.Performance;
using UnityEngine;
using Verse;

namespace Vehicles
{
  /// <summary>
  /// Link between regions for reachability determination
  /// </summary>
  public class VehicleRegionLink : IPoolable
  {
    private const float WeightColorCeiling = 30;

    public VehicleRegion regionA;
    public VehicleRegion regionB;

    public EdgeSpan span;
    public IntVec3 anchor;

    // RegionLink color weights
    internal static readonly LinearPool<SimpleColor> colorWeights = new LinearPool<SimpleColor>
    {
      range = new FloatRange(0, WeightColorCeiling),
      items =
      [
        SimpleColor.White,
        SimpleColor.Green,
        SimpleColor.Yellow,
        SimpleColor.Orange,
        SimpleColor.Red,
        SimpleColor.Magenta
      ]
    };

    public VehicleRegionLink()
    {
      ObjectCounter.Increment<VehicleRegionLink>();
    }

    public bool InPool { get; set; }

    // If only 1 is null, the span should be registered in VehicleRegionLinkDatabase
    // as a half link, and not registered in a region as a full link. It will still be
    // invalid for reachability checks.
    public bool IsValid => regionA != null || regionB != null;

    public void SetNew(EdgeSpan span)
    {
      Reset();
      this.span = span;
      this.anchor = VehicleRegionCostCalculator.RegionLinkCenter(this);
    }

    public void Reset()
    {
      regionA = null;
      regionB = null;
    }

    public void Register(VehicleRegion region)
    {
      // RegionLinks can double register if the same region is used
      // and the links remained the same
      if (regionA == region || regionB == region) return;

      if (regionA is null || !regionA.valid)
      {
        regionA = region;
      }
      else if (regionB is null || !regionB.valid)
      {
        regionB = region;
      }
    }

    /// <returns>Link is invalid and can be removed from cache</returns>
    public void Deregister(VehicleRegion region)
    {
      if (regionA == region)
      {
        regionA = null;
      }
      else if (regionB == region)
      {
        regionB = null;
      }
    }

    public bool LinksRegions(VehicleRegion regionA, VehicleRegion regionB)
    {
      return (this.regionA == regionA && this.regionB == regionB) ||
        (this.regionA == regionB && this.regionB == regionA);
    }

    /// <summary>
    /// Draws <paramref name="weight"/> on map from this link to <paramref name="regionLink"/>
    /// </summary>
    public void DrawWeight(Map map, in VehicleRegionLink regionLink, float weight,
      int duration = 50)
    {
      Vector3 from = anchor.ToVector3();
      from.y += AltitudeLayer.MapDataOverlay.AltitudeFor();
      Vector3 to = regionLink.anchor.ToVector3();
      to.y += AltitudeLayer.MapDataOverlay.AltitudeFor();
      map.DrawLine_ThreadSafe(from, to, color: WeightColor(weight), duration: duration);
    }

    /// <summary>
    /// Get opposite region linking to <paramref name="region"/>
    /// </summary>
    public VehicleRegion GetOtherRegion(VehicleRegion region)
    {
      return (region != regionA) ? regionA : regionB;
    }

    public VehicleRegion GetInFacingRegion(VehicleRegionLink regionLink)
    {
      if (regionA == regionLink.regionA || regionA == regionLink.regionB) return regionA;
      if (regionB == regionLink.regionB || regionB == regionLink.regionA) return regionB;
      Log.Warning($"Attempting to fetch region between links {anchor} and {regionLink.anchor}, " +
        $"but they do not share a region.\n--- Regions ---\n{regionA}\n{regionB}\n" +
        $"{regionLink.regionA}\n{regionLink.regionB}\n");
      return null;
    }

    /// <summary>
    /// Hashcode for cache data
    /// </summary>
    public ulong UniqueHashCode()
    {
      return span.UniqueHashCode();
    }

    /// <summary>
    /// String output with data
    /// </summary>
    public override string ToString()
    {
      return $"({regionA.ID},{regionB.ID}, regions=[spawn={span}, hash={UniqueHashCode()}])";
    }

    public static SimpleColor WeightColor(float weight)
    {
      return colorWeights.Evaluate(weight);
    }
  }
}