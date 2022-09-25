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
    internal class ModPatches
    {
        private static List<Keys> keysDown = new List<Keys>();
        private static List<Keys> keysQuickPress = new List<Keys>();
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
        public static void ClearKeys(bool pressedKeys = true, bool quickKeys = true)
        {
            if (pressedKeys)
                keysDown.Clear();
            if (quickKeys)
                keysQuickPress.Clear();
        }

        public static void KeyboardState_InternalGetKey_Postfix(ref bool __result, Keys key)
        {
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
