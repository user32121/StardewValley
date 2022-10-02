using HarmonyLib;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace User32121Lib
{
    public class ModPatches
    {
        private static List<Keys> keysDown = new List<Keys>();
        private static List<Keys> keysQuickPress = new List<Keys>();
        private static readonly List<Keys> suppressUntilReleased = new List<Keys>();

        public static void SetKeyDown(Keys key)
        {
            if (!keysDown.Contains(key))
                keysDown.Add(key);
        }
        public static void SetKeyUp(Keys key)
        {
            keysDown.Remove(key);
        }
        public static void QuickPressKey(Keys key)
        {
            keysQuickPress.Add(key);
        }
        public static void SuppressKey(Keys key)
        {
            suppressUntilReleased.Add(key);
        }

        public static void ClearKeys(bool pressedKeys = true, bool quickKeys = true)
        {
            if (pressedKeys)
                keysDown.Clear();
            if (quickKeys)
                keysQuickPress.Clear();
        }

        static bool tempDisablePatch;
        public static void UpdateSuppressed()
        {
            if (suppressUntilReleased.Count > 0)
            {
                tempDisablePatch = true;
                KeyboardState ks = Keyboard.GetState();
                for (int i = 0; i < suppressUntilReleased.Count; i++)
                    if (ks.IsKeyUp(suppressUntilReleased[i]))
                    {
                        suppressUntilReleased.RemoveAt(i);
                        i--;
                    }
                tempDisablePatch = false;
            }
        }

        public static void KeyboardState_InternalGetKey_Postfix(ref bool __result, Keys key)
        {
            if (tempDisablePatch)
                return;

            if (suppressUntilReleased.Contains(key))
                if (__result)
                    __result = false;

            __result = __result || keysDown.Contains(key);
            if (keysQuickPress.Contains(key))
            {
                keysQuickPress.Remove(key);
                __result = true;
            }
        }

        public static void PatchInput(Mod mod)
        {
            //register harmony
            Harmony harmony = new Harmony(mod.ModManifest.UniqueID);

            harmony.Patch(original: mod.Helper.Reflection.GetMethod(new KeyboardState(), "InternalGetKey").MethodInfo,
                postfix: new HarmonyMethod(typeof(ModPatches), nameof(ModPatches.KeyboardState_InternalGetKey_Postfix)));
        }
    }
}
