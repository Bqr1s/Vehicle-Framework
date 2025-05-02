using Verse;

namespace Vehicles;

public static class Debug
{
  public static void Message(string text)
  {
    if (VehicleMod.settings.debug.debugLogging)
      Log.Message(text);
  }

  public static void Warning(string text)
  {
    if (VehicleMod.settings.debug.debugLogging)
      Log.Warning(text);
  }

  public static void Error(string text)
  {
    if (VehicleMod.settings.debug.debugLogging)
      Log.Error(text);
  }
}