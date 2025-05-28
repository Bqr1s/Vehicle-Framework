using RimWorld;
using Verse;

namespace Vehicles;

[DefOf]
public static class KeyBindingDefOf_Vehicles
{
  public static KeyBindingDef VF_Command_ReverseVehicle;

  public static KeyBindingDef VF_QuickStartMenu;
  public static KeyBindingDef VF_RestartGame;
  public static KeyBindingDef VF_DebugSettings;

  static KeyBindingDefOf_Vehicles()
  {
    DefOfHelper.EnsureInitializedInCtor(typeof(KeyBindingDefOf));
  }
}