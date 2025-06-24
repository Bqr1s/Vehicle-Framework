using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using SmashTools;

namespace Vehicles
{
  public class DrawOffsets
  {
    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public Vector3 defaultOffset;

    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public Vector3? north;

    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public Vector3? east;

    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public Vector3? south;

    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public Vector3? west;

    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public Vector3? northEast;

    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public Vector3? southEast;

    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public Vector3? southWest;

    [TweakField(SettingsType = UISettingsType.FloatBox)]
    public Vector3? northWest;

    public Vector3 OffsetFor(Rot8 rot)
    {
      switch (rot.AsInt)
      {
        case 0:
          return north ?? defaultOffset;
        case 1:
          if (east == null && west != null)
          {
            return new Vector3(-west.Value.x, west.Value.y, west.Value.z);
          }
          return east ?? defaultOffset.RotatedBy(rot.AsAngle);
        case 2:
          if (south == null && north != null)
          {
            return new Vector3(north.Value.x, north.Value.y, -north.Value.z);
          }
          return south ?? defaultOffset.RotatedBy(rot.AsAngle);
        case 3:
          if (west == null && east != null)
          {
            return new Vector3(-east.Value.x, east.Value.y, east.Value.z);
          }
          return west ?? defaultOffset.RotatedBy(rot.AsAngle);
        case 4:
          return northEast ?? northWest?.MirrorHorizontal() ??
            north?.RotatedBy(45) ?? defaultOffset.RotatedBy(rot.AsAngle);
        case 5:
          return southEast ?? southWest?.MirrorHorizontal() ??
            south?.RotatedBy(-45) ?? defaultOffset.RotatedBy(rot.AsAngle);
        case 6:
          return southWest ?? southEast?.MirrorHorizontal() ??
            south?.RotatedBy(45) ?? defaultOffset.RotatedBy(rot.AsAngle);
        case 7:
          return northWest ?? northEast?.MirrorHorizontal() ??
            north?.RotatedBy(-45) ?? defaultOffset.RotatedBy(rot.AsAngle);
        default:
          throw new NotImplementedException(nameof(Rot8));
      }
    }
  }
}