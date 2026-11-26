using System;
using System.Collections.Generic;
using HarmonyLib;
using RiftAccess.Core;
using Shared.MenuOptions;
using Shared.Title;

namespace RiftAccess.Menus
{
    /// <summary>
    /// The main menu's promotion carousel (top right): slides with a picture and a caption ("DLC Out Now!") that turn
    /// by themselves; Enter opens the slide's link (a store page, a web page or an in-game announcement). The game's
    /// option only shows the caption, so the focus line says what it is, which slide, and what Enter does, and when the slide
    /// turns while focused MenuReader's change detection reads what changed.
    /// </summary>
    internal static class CarouselReader
    {
        private static AccessTools.FieldRef<MainMenuManager, TextButtonOption> _option;
        private static AccessTools.FieldRef<MainMenuManager, CarouselDisplay> _display;
        private static System.Reflection.MethodInfo _label, _url;
        private static System.Reflection.FieldInfo _index, _pages;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                _option = AccessTools.FieldRefAccess<MainMenuManager, TextButtonOption>("_carouselOption");
                _display = AccessTools.FieldRefAccess<MainMenuManager, CarouselDisplay>("_carouselDisplay");
                _label = AccessTools.Method(typeof(MainMenuManager), "GetCarouselCurrentLabel");
                _url = AccessTools.Method(typeof(MainMenuManager), "GetCarouselCurrentUrl");
                _index = AccessTools.Field(typeof(CarouselDisplay), "currentIndex");
                _pages = AccessTools.Field(typeof(CarouselDisplay), "totalPages");
            }
            catch (Exception ex) { Plugin.LogError("Carousel reader: " + ex); }
        }

        /// <summary>The carousel's focus line, or null if this option is not the carousel.</summary>
        public static List<string> Describe(UnityEngine.Component option)
        {
            if (!(option is TextButtonOption) || _option == null) return null;
            var menu = option.GetComponentInParent<MainMenuManager>() ?? UnityEngine.Object.FindObjectOfType<MainMenuManager>();
            if (menu == null || _option(menu) != option) return null;
            try
            {
                var parts = new List<string>();
                string label = _label?.Invoke(menu, null) as string;
                parts.Add(Loc.F("carousel.label", string.IsNullOrEmpty(label) ? "" : Speech.Clean(label)));
                var d = _display(menu);
                if (d != null && _index != null && _pages != null)
                {
                    int pages = (int)_pages.GetValue(d);
                    if (pages > 1) parts.Add(Loc.F("carousel.slide", (int)_index.GetValue(d) + 1, pages));
                }
                string url = _url?.Invoke(menu, null) as string;
                if (!string.IsNullOrEmpty(url))
                    parts.Add(Loc.T(url.StartsWith("announcement://") ? "carousel.enter_news"
                        : url.IndexOf("steampowered.com", StringComparison.OrdinalIgnoreCase) >= 0 || url.StartsWith("steam://") ? "carousel.enter_store" : "carousel.enter_web"));
                return parts;
            }
            catch (Exception ex) { Plugin.LogDebug("Carousel: " + ex.Message); return null; }
        }
    }
}
