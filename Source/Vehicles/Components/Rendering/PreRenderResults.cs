using UnityEngine;

namespace Vehicles.Rendering;

public struct PreRenderResults
{
  public bool valid;
  public bool draw;
  public Mesh mesh;
  public Material material;
  public Vector3 position;
  public Quaternion quaternion;
}