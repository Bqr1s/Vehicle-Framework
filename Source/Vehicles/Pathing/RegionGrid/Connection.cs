namespace Vehicles;

public readonly struct Connection
{
  public readonly int from;
  public readonly int to;
  public readonly float cost;

  public Connection(int from, int to, float cost)
  {
    this.from = from;
    this.to = to;
    this.cost = cost;
  }
}