using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Security;
using System.Security.Permissions;
using System.Text.RegularExpressions;

#pragma warning disable CS0618

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace SafeExit;

[BepInPlugin(PLUGIN_GUID, "SafeExit", "1.0")]
public class SafeExit : BaseUnityPlugin
{
    public const string PLUGIN_GUID = "heretichazel.safeexit";
    public static Configurable<int> safeExitSeconds;
    private bool _isInit = false;

    private void OnEnable()
    {
        On.RainWorld.OnModsInit += RainWorld_OnModsInit;
    }
    
    private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
    {
        orig(self);

        if (_isInit) return;
        _isInit = true;
        
        var OI = MachineConnector.GetRegisteredOI(PLUGIN_GUID);
        var config = OI.config;

        safeExitSeconds = config.Bind("Seconds", 0, new ConfigurableInfo("Seconds until exiting counts as a death, when 0 allows safely exiting at any time", new ConfigAcceptableRange<int>(0, 800), autoTab: "Main"));

        try {
            On.Menu.PauseMenu.Singal += Menu_PauseMenu_Singal_OnHook;
            On.Menu.Menu.Translate_string += Menu_Translate_OnHook;
            IL.RainWorldGame.ExitGame += RainWorldGame_ExitGame_ILHook;
        }
        catch (Exception e)
        { Logger.LogError(e); }

    }

    private void Menu_PauseMenu_Singal_OnHook(On.Menu.PauseMenu.orig_Singal orig, Menu.PauseMenu self, Menu.MenuObject sender, string message)
    {
        RainWorldGame rainWorldGame = self.manager.currentMainLoop as RainWorldGame;

        self.pauseWarningActive = safeExitSeconds.Value != 0 && rainWorldGame.clock > (safeExitSeconds.Value * 40);

        orig(self, sender, message);
    }

    private string Menu_Translate_OnHook(On.Menu.Menu.orig_Translate_string orig, Menu.Menu self, string s)
    {
        if (s == "Really exit? Note that quitting after 30 seconds into a cycle counts as a loss.") {
            return Regex.Replace(orig(self, s), @"\d\d", safeExitSeconds.Value.ToString());
        }
        return orig(self, s);
    }

    private static bool ClockEval(int clock) =>
        safeExitSeconds.Value != 0 && clock > (safeExitSeconds.Value * 40);

    private void RainWorldGame_ExitGame_ILHook(ILContext il) {
        try {
            ILCursor c = new(il);
            var skip = il.DefineLabel();
            c.GotoNext(
                MoveType.Before,
                x => x.MatchLdcI4(1200),
                x => x.MatchCgt()
            );

            c.EmitDelegate<Func<int,bool>>(ClockEval);
            c.Emit(OpCodes.Br, skip);

            c.GotoNext(
                MoveType.After,
                x => x.MatchLdcI4(1200),
                x => x.MatchCgt()
            );

            c.MarkLabel(skip);

            //Logger.LogDebug(il);
        }
        catch (Exception e)
        { Logger.LogError(e); }
    }
}