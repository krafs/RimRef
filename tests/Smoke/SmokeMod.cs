using UnityEngine;
using Verse;

namespace Smoke;

public class SmokeMod(ModContentPack content) : Mod(content)
{
    public override void DoSettingsWindowContents(Rect inRect)
    {
        Widgets.Label(inRect, "Smoke");
#if WAYLITHCLR
        Widgets.Label(inRect, WaylithClr.MethodBodyUpdater.IsSupported.ToString());
#pragma warning disable WAYLITHCLR_SPECIFIC
        var counter = System.Runtime.Intrinsics.X86.X86Base.ReadTimeStampCounter();
#pragma warning restore WAYLITHCLR_SPECIFIC
        Widgets.Label(inRect, counter.CounterLower.ToString());
#endif
    }
}
