using System;
using UnityEngine;
using Verse;

namespace Vehicles;

public class Dialog_ColorWheel : Window
{
  private const int ButtonWidth = 90;
  private const float ButtonHeight = 30f;

  private Color color;
  private readonly Action<Color> onComplete;

  private float hue;
  private float saturation;
  private float value;

  private readonly ColorPicker colorPicker = new();

  public Dialog_ColorWheel(Color color, Action<Color> onComplete)
  {
    this.color = color;
    this.onComplete = onComplete;
    doCloseX = true;
    closeOnClickedOutside = true;
  }

  public override Vector2 InitialSize => new(375, 350 + ButtonHeight);

  public override void DoWindowContents(Rect inRect)
  {
    Rect colorContainerRect = inRect with { height = inRect.width - 25 };
    colorPicker.Draw(colorContainerRect, ref hue, ref saturation, ref value, SetColor);

    Rect buttonRect = new(0f, inRect.height - ButtonHeight, ButtonWidth, ButtonHeight);
    DoBottomButtons(buttonRect);
  }

  private void DoBottomButtons(Rect rect)
  {
    if (Widgets.ButtonText(rect, "VF_ApplyButton".Translate()))
    {
      onComplete(color);
      Close();
    }
    rect.x += ButtonWidth;
    if (Widgets.ButtonText(rect, "CancelButton".Translate()))
    {
      Close();
    }
  }

  private void SetColor(float h, float s, float b)
  {
    color = new ColorInt(Color.HSVToRGB(h, s, b)).ToColor;
  }
}