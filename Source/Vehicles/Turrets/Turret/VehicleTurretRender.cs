using JetBrains.Annotations;
using SmashTools;
using UnityEngine;
using Verse;

namespace Vehicles;

[PublicAPI]
public class VehicleTurretRender : ITweakFields
{
  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public Vector2? north;

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public Vector2? east;

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public Vector2? south;

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public Vector2? west;

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public Vector2? northEast;

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public Vector2? southEast;

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public Vector2? southWest;

  [TweakField(SettingsType = UISettingsType.FloatBox)]
  public Vector2? northWest;

  string ITweakFields.Label => "Render Properties";

  string ITweakFields.Category => string.Empty;

  /// <summary>
  /// Init from CompProperties
  /// </summary>
  public VehicleTurretRender()
  {
  }

  public VehicleTurretRender(VehicleTurretRender reference)
  {
    if (reference != null)
    {
      north = reference.north;
      east = reference.east;
      south = reference.south;
      west = reference.west;
      northEast = reference.northEast;
      southEast = reference.southEast;
      southWest = reference.southWest;
      northWest = reference.northWest;
    }
    PostLoad();
  }

  public void OnFieldChanged()
  {
    RecacheOffsets();
  }

  /// <summary>
  /// Reflection call from vanilla
  /// </summary>
  public void PostLoad()
  {
    RecacheOffsets();
  }

  public void RecacheOffsets()
  {
    north ??= south.HasValue ? Rotate(south.Value, 180) : Vector2.zero;
    south ??= Rotate(north.Value, 180);
    east ??= west.HasValue ? Flip(west.Value, true, false) : Rotate(north.Value, -90);
    west ??= east.HasValue ? Flip(east.Value, true, false) : Rotate(north.Value, 90);
    northEast ??= Rotate(north.Value, -45);
    northWest ??= Rotate(north.Value, 45);
    southEast ??= Rotate(south.Value, 45);
    southWest ??= Rotate(south.Value, -45);
  }

  // NOTE - Verse extension rotates CCW, angle must be negative for CW rotation
  private static Vector2 Rotate(Vector2 offset, float angle)
  {
    if (angle % 45 != 0)
    {
      Log.Error(
        "Cannot rotate VehicleTurretRender.offset with an angle non-multiple of 45.");
      return offset;
    }
    return offset.RotatedBy(angle);
  }

  private static Vector2 Flip(Vector2 offset, bool flipX, bool flipY)
  {
    Vector2 newOffset = offset;
    if (flipX)
    {
      newOffset.x *= -1;
    }
    if (flipY)
    {
      newOffset.y *= -1;
    }
    return newOffset;
  }

  public Vector2 OffsetFor(Rot8 rot)
  {
    return rot.AsInt switch
    {
      0 => north ?? Vector2.zero,
      1 => east ?? Vector2.zero,
      2 => south ?? Vector2.zero,
      3 => west ?? Vector2.zero,
      4 => northEast ?? Vector2.zero,
      5 => southEast ?? Vector2.zero,
      6 => southWest ?? Vector2.zero,
      7 => northWest ?? Vector2.zero,
      _ => Vector2.zero,
    };
  }

  public override string ToString()
  {
    return
      $"north: {north} east: {east} south: {south} west: {west} NE: {northEast} SE: {southEast} SW: {southWest} NW: {northWest}";
  }
}