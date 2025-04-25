﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SmashTools.Performance;
using UnityEngine;
using Verse;

namespace Vehicles;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class VehiclePath : IDisposable
{
  private const int InitialPathSize = 1 << 7;

  private int current;
  private readonly List<IntVec3> nodes = new(InitialPathSize);

  public bool Found { get; private set; }

  public bool UsedHeuristics { get; private set; }

  public IntVec3 LastNode => nodes[0];

  public int NodesLeft => current + 1;

  public bool Finished => NodesLeft <= 0;

  public int NodesConsumedCount => nodes.Count - NodesLeft;

  public IReadOnlyList<IntVec3> Nodes => nodes;

  public static VehiclePath NotFound => new();

  public void Init(bool usedHeuristics)
  {
    UsedHeuristics = usedHeuristics;
    current = nodes.Count - 1;
    Found = true;
  }

  public void AddNode(IntVec3 cell)
  {
    nodes.Add(cell);
  }

  public IntVec3 ConsumeNextNode()
  {
    IntVec3 cell = Peek(1);
    current--;
    return cell;
  }

  public IntVec3 Peek(int nodesAhead)
  {
    return nodes[current - nodesAhead];
  }

  public void DrawPath(VehiclePawn vehicle)
  {
    if (!Found || Finished)
      return;

    float drawOffset = AltitudeLayer.Item.AltitudeFor();

    for (int i = 0; i < NodesLeft - 1; i++)
    {
      Vector3 from = Peek(i).ToVector3Shifted();
      from.y = drawOffset;
      Vector3 to = Peek(i + 1).ToVector3Shifted();
      to.y = drawOffset;
      GenDraw.DrawLineBetween(from, to);
    }
    if (vehicle is not null)
    {
      Vector3 curFrom = vehicle.DrawPos;
      curFrom.y = drawOffset;
      Vector3 curTo = Peek(0).ToVector3Shifted();
      curTo.y = drawOffset;
      if ((curFrom - curTo).sqrMagnitude > 0.01f)
      {
        GenDraw.DrawLineBetween(curFrom, curTo);
      }
    }
  }


  public void Dispose()
  {
    current = -1;
    UsedHeuristics = false;
    Found = false;
    nodes.Clear();
    AsyncPool<VehiclePath>.Return(this);
  }
}