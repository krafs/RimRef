using UnityEngine;
using Verse;

namespace Smoke;

public class SmokeMod(ModContentPack content) : Mod(content)
{
    public override void DoSettingsWindowContents(Rect inRect)
    {
        Widgets.Label(inRect, "Smoke");
    }
}
