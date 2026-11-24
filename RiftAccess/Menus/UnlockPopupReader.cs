using System;
using System.Collections.Generic;
using HarmonyLib;
using RiftAccess.Core;
using Shared.PlayerData.Profile;
using Shared.PlayerData.Profile.Menu;
using Shared.Utilities;
using TicToc.Localization;
using UnityEngine;

namespace RiftAccess.Menus
{
    /// <summary>
    /// The "new Rift profile item unlocked" popup (ProfileUnlockPopup): its title, the items (pictures on profile
    /// cards, so their names are read from the item data) and the call to action. Only its two buttons are menu
    /// options, so without this only "Accept" and "Equip" were heard. F2 repeats it while it is open.
    /// </summary>
    internal static class UnlockPopupReader
    {
        private static ProfileUnlockPopup _popup;
        private static string _text;
        private static bool _wasOpen;
        private static int _openedFrame = -1;

        public static bool IsOpen => _popup != null && _popup.gameObject.activeInHierarchy && _text != null;
        public static string Text => _text;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                harmony.Patch(AccessTools.Method(typeof(ProfileUnlockPopup), nameof(ProfileUnlockPopup.SetUnlockedItems)),
                    prefix: new HarmonyMethod(typeof(UnlockPopupReader), nameof(SetItemsPrefix)));
                Plugin.LogInfo("Patched ProfileUnlockPopup.SetUnlockedItems (unlocked items popup)");
            }
            catch (Exception ex) { Plugin.LogError("Unlock popup reader: " + ex); }
        }

        private static void SetItemsPrefix(ProfileUnlockPopup __instance, List<ItemInstance> items)
        {
            try
            {
                _popup = __instance;
                var expr = new ExpressionString();
                var names = new List<string>();
                foreach (var item in items)
                {
                    if (item?.Definition == null) continue;
                    string name = expr.Evaluate(item.Definition.Name);
                    if (string.IsNullOrWhiteSpace(name)) name = item.Definition.Id;
                    string category = item.Category != null ? expr.Evaluate(item.Category.Name) : null;
                    names.Add(string.IsNullOrWhiteSpace(category) ? name : category + ": " + name);
                }
                string title = items.Count == 1 ? Localizer.GetText("Profile_UnlockedItems_Singular")
                             : Localizer.GetFormattedText("Profile_UnlockedItems_MainText", items.Count.ToString());
                _text = Speech.Clean(title) + " " + string.Join(". ", names) + ". " + Speech.Clean(Localizer.GetText("Profile_UnlockedItems_CallToAction"));
                Plugin.LogDebug("Unlock popup: " + _text);
            }
            catch (Exception ex) { Plugin.LogError("Unlock popup items: " + ex.Message); }
        }

        public static void Update()
        {
            bool open = IsOpen;
            if (open && !_wasOpen) _openedFrame = Time.frameCount;
            _wasOpen = open;
            // A couple of frames after it appears, so the focused button has been announced: the popup first, then
            // the button again.
            if (open && _openedFrame >= 0 && Time.frameCount >= _openedFrame + 3)
            {
                _openedFrame = -1;
                Speech.Say(_text, true);
                string focus = MenuReader.FocusText();
                if (!string.IsNullOrEmpty(focus)) Speech.Say(focus);
            }
        }
    }
}
