using UnityEngine;
using Verse;

namespace Smoke;

public class SmokeMod : Mod
{
    public SmokeMod(ModContentPack content) : base(content)
    {
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Widgets.Label(inRect, "Smoke");
    }
}
