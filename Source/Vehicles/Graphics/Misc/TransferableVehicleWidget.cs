using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using UnityEngine.Assertions;
using Verse;
using TransferableOneWay = RimWorld.TransferableOneWay;

namespace Vehicles.Rendering;

[StaticConstructorOnStartup]
public sealed class TransferableVehicleWidget
{
  private const int ColumnCount = 4;
  private const float CardHeight = 300;
  private const float LabelHeight = 30;
  private const float CardIconSize = 150;
  private const float CardSpacing = 5;
  private const float CardContentPadding = 5;

  private const float FirstTransferableY = 6f;
  private const float ExtraSpaceAfterSectionTitle = 5f;

  public const float TopAreaWidth = 515f;

  public const float CountColumnWidth = 75f;
  public const float AdjustColumnWidth = 240f;
  public const float MassColumnWidth = 100f;

  // HostilityResponseModeUtility::FleeIcon
  private static readonly Texture2D pawnIcon =
    ContentFinder<Texture2D>.Get("UI/Icons/HostilityResponse/Flee");

  private static readonly Color cardColor = new(1f, 1f, 1f, 0.04f);

  private static readonly StringBuilder stringBuilder = new();

  private readonly List<Section> sections = [];
  private string sourceLabel;
  private string destinationLabel;
  private string sourceCountDesc;
  private bool drawMass;
  private IgnorePawnsInventoryMode ignorePawnInventoryMass = IgnorePawnsInventoryMode.DontIgnore;
  private bool includePawnsMassInMassUsage;
  private Func<float> availableMassGetter;
  private float extraHeaderSpace;
  private bool ignoreSpawnedCorpseGearAndInventoryMass;
  private PlanetTile tile;
  private bool drawMarketValue;
  private bool drawFishPerDay;
  private bool transferablesCached;

  private readonly TransferableSorter sortByCategory;
  private readonly TransferableSorter sortByMarketValue;

  private readonly HashSet<VehicleDef> impassableOnTile = [];

  private Vector2 scrollPosition;

  private float Height { get; set; } = -1;

  private bool AnyTransferable
  {
    get
    {
      if (!transferablesCached)
      {
        CacheTransferables();
      }
      foreach (Section section in sections)
      {
        if (section.SortedTransferables.Any())
          return true;
      }
      return false;
    }
  }

  public TransferableVehicleWidget(IEnumerable<TransferableOneWay> transferables,
    string sourceLabel, string destinationLabel, string sourceCountDesc, bool drawMass = false,
    IgnorePawnsInventoryMode ignorePawnInventoryMass = IgnorePawnsInventoryMode.DontIgnore,
    bool includePawnsMassInMassUsage = false, Func<float> availableMassGetter = null,
    float extraHeaderSpace = 0f, bool ignoreSpawnedCorpseGearAndInventoryMass = false,
    int tile = -1, bool drawMarketValue = false, bool drawFishPerDay = false)
  {
    if (transferables != null)
    {
      AddSection(null, transferables);
    }
    this.sourceLabel = sourceLabel;
    this.destinationLabel = destinationLabel;
    this.sourceCountDesc = sourceCountDesc;
    this.drawMass = drawMass;
    this.ignorePawnInventoryMass = ignorePawnInventoryMass;
    this.includePawnsMassInMassUsage = includePawnsMassInMassUsage;
    this.availableMassGetter = availableMassGetter;
    this.extraHeaderSpace = extraHeaderSpace;
    this.ignoreSpawnedCorpseGearAndInventoryMass = ignoreSpawnedCorpseGearAndInventoryMass;
    this.tile = tile;
    this.drawMarketValue = drawMarketValue;
    this.drawFishPerDay = drawFishPerDay;

    sortByCategory = new TransferableSorter(this, TransferableSorterDefOf.Category);
    sortByMarketValue = new TransferableSorter(this, TransferableSorterDefOf.MarketValue);
  }

  public void AddSection(string title, IEnumerable<TransferableOneWay> transferables)
  {
    Section section = new()
    {
      title = title,
    };
    section.transferables.AddRange(transferables);
    sections.Add(section);
    transferablesCached = false;

    if (!section.transferables.NullOrEmpty())
    {
      WorldVehiclePathGrid worldVehiclePathGrid = Find.World.GetComponent<WorldVehiclePathGrid>();
      foreach (TransferableOneWay transferable in section.transferables)
      {
        VehicleDef vehicleDef = transferable.ThingDef as VehicleDef;
        Assert.IsNotNull(vehicleDef, "Non-vehicle transferable in vehicles section.");
        if (!worldVehiclePathGrid.Passable(tile, vehicleDef))
          impassableOnTile.Add(vehicleDef);
      }
    }
  }

  private void CacheTransferables()
  {
    transferablesCached = true;
    foreach (Section section in sections)
    {
      section.SortedTransferables.Clear();
      section.SortedTransferables.AddRange(section.transferables
       .OrderBy(transferableOneWay => CanCaravan(transferableOneWay, out _))
       .ThenBy(transferOneWay => transferOneWay, sortByCategory.sorterDef.Comparer)
       .ThenBy(transferOneWay => transferOneWay, sortByMarketValue.sorterDef.Comparer)
       .ThenBy(TransferableUIUtility.DefaultListOrderPriority));
    }
    RecalculateHeight();
  }

  private void RecalculateHeight()
  {
    float height = FirstTransferableY;
    foreach (Section section in sections)
    {
      height += Mathf.CeilToInt(section.SortedTransferables.Count / (float)ColumnCount) *
        CardHeight;
      if (section.title != null)
        height += LabelHeight + ExtraSpaceAfterSectionTitle;
    }
    Height = height;
  }

  public void OnGUI(Rect inRect)
  {
    if (!transferablesCached)
      CacheTransferables();

    TransferableUIUtility.DoTransferableSorters(sortByCategory.sorterDef,
      sortByMarketValue.sorterDef, sortByCategory.Sort, sortByMarketValue.Sort);

    if (!sourceLabel.NullOrEmpty() || !destinationLabel.NullOrEmpty())
    {
      // TODO - split caravans UI
      float num = inRect.width - 515f;
      Rect position = new Rect(inRect.x + num, inRect.y, inRect.width - num, 37f);
      Widgets.BeginGroup(position);
      Text.Font = GameFont.Medium;
      if (!sourceLabel.NullOrEmpty())
      {
        Rect rect = new Rect(0f, 0f, position.width / 2f, position.height);
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(rect, sourceLabel);
      }
      if (!destinationLabel.NullOrEmpty())
      {
        Rect rect2 = new Rect(position.width / 2f, 0f, position.width / 2f, position.height);
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(rect2, destinationLabel);
      }
      Text.Font = GameFont.Small;
      Text.Anchor = TextAnchor.UpperLeft;
      Widgets.EndGroup();
    }
    Rect mainRect = new Rect(inRect.x, inRect.y + 37f + extraHeaderSpace, inRect.width,
      inRect.height - 37f - extraHeaderSpace);
    FillMainRect(mainRect);
  }

  private bool CanCaravan(TransferableOneWay transferable, out string disableReason)
  {
    VehicleDef vehicleDef = transferable.ThingDef as VehicleDef;
    Assert.IsNotNull(vehicleDef);
    if (impassableOnTile.Contains(vehicleDef))
    {
      disableReason = "VF_ImpassableBiome";
      return false;
    }
    if (!vehicleDef.canCaravan)
    {
      disableReason = "VF_CaravanDisabled";
      return false;
    }
    if (transferable.AnyThing is VehiclePawn vehicle)
    {
      if (!vehicle.CanMove)
      {
        disableReason = "VF_CaravanCantMove";
        return false;
      }
    }
    disableReason = null;
    return true;
  }

  private void FillMainRect(Rect mainRect)
  {
    if (!AnyTransferable)
    {
      using TextBlock colorBlock = new(TextAnchor.UpperCenter, Color.gray);
      Widgets.Label(mainRect, "NoneBrackets".Translate());
      return;
    }

    using TextBlock fontBlock = new(GameFont.Small);
    float curY = FirstTransferableY;
    float availableMass = availableMassGetter?.Invoke() ?? float.MaxValue;
    float bottomLimit = scrollPosition.y - CardHeight;
    float topLimit = scrollPosition.y + mainRect.height;

    Rect viewRect = new(0f, 0f, mainRect.width - 16f, Height);
    Widgets.BeginScrollView(mainRect, ref scrollPosition, viewRect);
    float cardWidth = viewRect.width / ColumnCount;
    foreach (Section section in sections)
    {
      List<TransferableOneWay> cachedTransferables = section.SortedTransferables;
      if (cachedTransferables.NullOrEmpty())
        continue;

      if (section.title != null)
      {
        Widgets.ListSeparator(ref curY, viewRect.width, section.title);
        curY += ExtraSpaceAfterSectionTitle;
      }
      for (int i = 0; i < section.transferables.Count; i++)
      {
        TransferableOneWay transferable = section.transferables[i];
        if (curY > bottomLimit && curY < topLimit)
        {
          int column = i % ColumnCount;
          Rect rect = new(column * cardWidth, curY, cardWidth, CardHeight);

          Widgets.BeginGroup(rect);
          rect = rect.AtZero().ContractedBy(CardSpacing / 2f);
          Widgets.DrawBoxSolidWithOutline(rect, cardColor, Widgets.SeparatorLineColor);
          DrawCard(rect.ContractedBy(CardContentPadding), section, transferable,
            availableMass);
          Widgets.EndGroup();
        }
        if ((i + 1) % ColumnCount == 0)
          curY += CardHeight;
      }
      section.texturesDirty = false;
    }
    Widgets.EndScrollView();
  }

  private void DrawCard(Rect rect, Section section, TransferableOneWay transferable,
    float availableMass)
  {
    const float Margin = 15;
    const float LinePadding = 2;
    const float CheckboxSize = 24;
    const float SpecialPropIconSize = 24;

    VehiclePawn vehicle = transferable.AnyThing as VehiclePawn;
    VehicleDef vehicleDef = transferable.ThingDef as VehicleDef;
    Assert.IsNotNull(vehicleDef);
    bool canCaravan = CanCaravan(transferable, out string disableReason);

    Rect iconBar = rect with { height = CardIconSize };
    Rect iconRect = iconBar.ToSquare();

    bool checkOn = transferable.CountToTransfer > 0;
    Rect checkboxRect = new(iconBar.xMax - CheckboxSize, iconBar.y, CheckboxSize, CheckboxSize);
    Widgets.Checkbox(checkboxRect.position, ref checkOn, disabled: !canCaravan, size: CheckboxSize);
    if (!canCaravan)
    {
      TooltipHandler.TipRegionByKey(checkboxRect, disableReason);
    }
    if (checkOn != transferable.CountToTransfer > 0)
      transferable.AdjustTo(checkOn ? 1 : 0);

    Rect specialPropsRect = iconBar with
    {
      y = iconBar.yMax - SpecialPropIconSize, height = SpecialPropIconSize
    };
    //DrawSpecialProperties(specialPropsRect, vehicleDef, vehicle);

    RenderTextureBuffer buffer = section.buffers.TryGetValue(transferable);

    Widgets.BeginGroup(iconRect);
    Rect textureRect = iconRect.AtZero();
    if (section.texturesDirty)
    {
      BlitRequest blitRequest = vehicle != null ?
        BlitRequest.For(vehicle) :
        BlitRequest.For(vehicleDef);
      buffer ??= section.buffers[transferable] =
        VehicleGui.CreateRenderTextureBuffer(textureRect, in blitRequest);
      VehicleGui.Blit(buffer.GetWrite(), textureRect, in blitRequest);
    }
    GUI.DrawTexture(textureRect, buffer.Read);
    Widgets.EndGroup();

    Widgets.DrawLineHorizontal(rect.x, iconRect.yMax, rect.width, Widgets.SeparatorLineColor);

    Rect infoRect = (rect with { yMin = iconRect.yMax }).ContractedBy(Margin, 0);
    infoRect.yMin += 10;

    Rect lineRect = infoRect with { height = Text.LineHeight };
    DrawMoveSpeed(lineRect, transferable);
    lineRect.y += lineRect.height + LinePadding;

    //DrawMass(rect, transferable, availableMass);
    //lineRect.y += lineRect.height * LinePadding;

    DrawCargoCapacity(lineRect, transferable);
    lineRect.y += lineRect.height + LinePadding;

    if (vehicleDef.tradeability is Tradeability.Sellable or Tradeability.All)
    {
      DrawMarketValue(lineRect, transferable);
      lineRect.y += lineRect.height + LinePadding;
    }
    if (vehicle != null)
    {
      foreach (ThingComp comp in vehicle.AllComps)
      {
        if (comp is VehicleComp vehicleComp)
        {
          float heightUsed = vehicleComp.CompStatCard(lineRect);
          if (heightUsed > 0)
            lineRect.y += heightUsed;
        }
      }
    }
  }

  private static void DrawSpecialProperties(Rect rect, VehicleDef vehicleDef, VehiclePawn vehicle)
  {
    if (vehicle != null)
    {
      DrawIcon(ref rect, VehicleTex.DraftVehicle, vehicle.PawnCountToOperate.ToString());
      DrawIcon(ref rect, pawnIcon, vehicle.PawnsByHandlingType[HandlingType.None].Count.ToString());
      //DrawIcon(ref rect, VehicleTex.DraftVehicle, vehicle.PawnCountToOperate.ToString());
    }
    else
    {
      int movementSlots = vehicleDef.properties.RoleSeats(HandlingType.Movement);
      int nonMovementSlots = vehicleDef.properties.TotalSeats - movementSlots;
      DrawIcon(ref rect, VehicleTex.DraftVehicle, movementSlots.ToString());
      DrawIcon(ref rect, pawnIcon, nonMovementSlots.ToString());
    }
    return;

    static void DrawIcon(ref Rect rect, Texture2D icon, string label)
    {
      Rect iconRect = new(rect.x, rect.y, rect.height, rect.height);
      rect.xMin += iconRect.width;
      GUI.DrawTexture(iconRect, icon);
      Widgets.Label(rect, label);
      rect.xMin += Text.CalcSize(label).x;
    }
  }

  private static void DrawMoveSpeed(Rect rect, TransferableOneWay trad)
  {
    Widgets.DrawHighlightIfMouseover(rect);
    rect.SplitVertically(rect.width / 2, out Rect labelRect, out Rect valueRect);
    using TextBlock fontBlock = new(GameFont.Small);
    TooltipHandler.TipRegionByKey(rect, "MarketValueTip");

    float moveSpeed;
    if (trad.AnyThing is VehiclePawn vehicle)
    {
      moveSpeed = vehicle.statHandler.GetStatValue(VehicleStatDefOf.MoveSpeed) *
        vehicle.WorldSpeedMultiplier;
    }
    else
    {
      VehicleDef vehicleDef = trad.ThingDef as VehicleDef;
      Assert.IsNotNull(vehicleDef);
      moveSpeed = vehicleDef.GetStatValueAbstract(VehicleStatDefOf.MoveSpeed) *
        vehicleDef.properties.worldSpeedMultiplier;
    }
    float tilesPerDay = 0;
    if (moveSpeed > 0)
    {
      // Conversion for tiles per day
      moveSpeed /= 60;
      int ticksPerTile = VehicleCaravanTicksPerMoveUtility.TicksFromMoveSpeed(moveSpeed);
      tilesPerDay = GenDate.TicksPerDay / (float)ticksPerTile;
    }
    using (new TextBlock(TextAnchor.MiddleLeft))
    {
      Widgets.Label(labelRect, VehicleStatDefOf.MoveSpeed.LabelCap);
    }
    using (new TextBlock(TextAnchor.MiddleRight))
    {
      Widgets.Label(valueRect, $"{tilesPerDay:0.#} {"TilesPerDay".Translate()}");
    }
  }

  //private static void DrawMass(Rect rect, TransferableOneWay trad, float massCapacity)
  //{
  //  Widgets.DrawHighlightIfMouseover(rect);
  //  rect.SplitVertically(rect.width / 2, out Rect labelRect, out Rect valueRect);
  //  TooltipHandler.TipRegion(rect, "ItemWeightTip".Translate());
  //  using TextBlock fontBlock = new(GameFont.Small, TextAnchor.MiddleRight);

  //  using (new TextBlock(TextAnchor.MiddleLeft))
  //  {
  //    Widgets.Label(labelRect, VehicleStatDefOf.Mass.LabelCap);
  //  }
  //  float mass = trad.AnyThing is VehiclePawn vehicle ?
  //    vehicle.statHandler.GetStatValue(VehicleStatDefOf.Mass) :
  //    (trad.ThingDef as VehicleDef).GetStatValueAbstract(VehicleStatDefOf.Mass);

  //  using (new TextBlock(TextAnchor.MiddleRight,
  //    mass > massCapacity ? TransferableOneWayWidget.ItemMassColor : ColorLibrary.RedReadable))
  //  {
  //    Widgets.Label(valueRect, mass.ToStringMass());
  //  }
  //}

  private static void DrawCargoCapacity(Rect rect, TransferableOneWay trad)
  {
    Widgets.DrawHighlightIfMouseover(rect);
    rect.SplitVertically(rect.width / 2, out Rect labelRect, out Rect valueRect);
    using TextBlock fontBlock = new(GameFont.Small, TextAnchor.MiddleRight);

    using (new TextBlock(TextAnchor.MiddleLeft))
    {
      Widgets.Label(labelRect, VehicleStatDefOf.CargoCapacity.LabelCap);
    }
    float cargoCapacity = trad.AnyThing is VehiclePawn vehicle ?
      vehicle.statHandler.GetStatValue(VehicleStatDefOf.CargoCapacity) :
      (trad.ThingDef as VehicleDef).GetStatValueAbstract(VehicleStatDefOf.CargoCapacity);

    using (new TextBlock(TextAnchor.MiddleRight, cargoCapacity > 0 ? Color.green : Color.gray))
    {
      Widgets.Label(valueRect, cargoCapacity.ToStringMassOffset());
    }
  }

  private static void DrawMarketValue(Rect rect, TransferableOneWay trad)
  {
    Widgets.DrawHighlightIfMouseover(rect);
    rect.SplitVertically(rect.width / 2, out Rect labelRect, out Rect valueRect);
    using TextBlock fontBlock = new(GameFont.Small, TextAnchor.MiddleRight);

    using (new TextBlock(TextAnchor.MiddleLeft))
    {
      Widgets.Label(labelRect, StatDefOf.MarketValue.LabelCap);
    }
    using (new TextBlock(TextAnchor.MiddleRight))
    {
      Widgets.Label(valueRect, trad.AnyThing.MarketValue.ToStringMoney());
    }
  }

  private void DrawMass(Rect rect, TransferableOneWay trad, float availableMass)
  {
    VehiclePawn vehicle = trad.AnyThing as VehiclePawn;

    using TextBlock colorBlock = new(Color.white);

    Widgets.DrawHighlightIfMouseover(rect);
    if (vehicle == null)
    {
      VehicleDef vehicleDef = trad.ThingDef as VehicleDef;
      Assert.IsNotNull(vehicleDef);
      float defMass = vehicleDef.GetStatValueAbstract(VehicleStatDefOf.Mass);
      GUI.color = defMass <= availableMass ?
        TransferableOneWayWidget.ItemMassColor :
        ColorLibrary.RedReadable;
      Widgets.Label(rect, defMass.ToStringMass());
      return;
    }
    float mass = vehicle.statHandler.GetStatValue(VehicleStatDefOf.Mass);
    float invMass =
      InventoryCalculatorsUtility.ShouldIgnoreInventoryOf(vehicle, ignorePawnInventoryMass) ?
        0f :
        MassUtility.InventoryMass(vehicle);
    float totalMass = mass + invMass;
    GUI.color = totalMass < availableMass ? Color.green : TransferableOneWayWidget.ItemMassColor;

    Widgets.Label(rect, totalMass.ToStringMass());
    if (Mouse.IsOver(rect))
    {
      TooltipHandler.TipRegion(rect, $"{"Mass".Translate()}: {totalMass.ToStringMass()}");
    }
  }

  private class TransferableSorter(
    TransferableVehicleWidget widget,
    TransferableSorterDef sorterDef)
  {
    public TransferableSorterDef sorterDef = sorterDef;

    public void Sort(TransferableSorterDef def)
    {
      sorterDef = def;
      widget.CacheTransferables();
    }
  }

  private class Section : IDisposable
  {
    public string title;
    public readonly List<TransferableOneWay> transferables = [];
    public readonly Dictionary<TransferableOneWay, RenderTextureBuffer> buffers = [];
    public bool texturesDirty = true;

    public List<TransferableOneWay> SortedTransferables { get; } = [];

    void IDisposable.Dispose()
    {
      if (!buffers.NullOrEmpty())
      {
        foreach (RenderTextureBuffer buffer in buffers.Values)
        {
          buffer.Dispose();
        }
        buffers.Clear();
      }
      GC.SuppressFinalize(this);
    }
  }
}