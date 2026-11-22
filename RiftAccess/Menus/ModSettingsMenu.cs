using System.Collections.Generic;
using RiftAccess.Core;
using RiftAccess.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RiftAccess.Menus
{
    /// <summary>
    /// The Rift Access settings menu (F10, from anywhere). Built from ModConfig.All plus a few actions.
    /// Keys: Up/Down item, Left/Right change value, Enter toggle or run, Tab/Shift+Tab next/previous section,
    /// Home/End first/last, Ctrl+Up/Ctrl+Down read the item's description, Escape or F10 close.
    /// Changes are saved immediately.
    /// </summary>
    internal static class ModSettingsMenu
    {
        public static bool IsOpen { get; private set; }
        private static readonly List<Setting> Items = new List<Setting>();
        private static int _index;
        private static float _resetArmedUntil;

        private sealed class ActionItem : Setting
        {
            private readonly System.Action _run;
            public ActionItem(string section, string label, System.Action run) : base(section, label) { _run = run; }
            public override string ValueText => "";
            public override string Role => Loc.T("role.button");
            public override bool Step(int dir) => false;
            public override void Activate() => _run();
        }

        public static void Open()
        {
            Items.Clear();
            Items.AddRange(ModConfig.All);
            Items.Add(new ActionItem("section.tools", "tool.test", CueDemo.Play));
            Items.Add(new ActionItem("section.tools", "tool.reset", ResetDefaults));
            IsOpen = true;
            _index = 0;
            InputBlocker.Block();
            Speech.Say(Loc.T("settings.title") + ". " + Items[0].Section + ". " + ItemText(Items[0]), true);
        }

        public static void Close()
        {
            IsOpen = false;
            InputBlocker.Release();
            Plugin.Instance.Config.Save();
            Speech.Say(Loc.T("settings.closed"), true);
        }

        private static string ItemText(Setting s)
        {
            if (s is ActionItem) return s.Label + ", " + s.Role;
            return s.Label + ", " + s.Role + ", " + s.ValueText;
        }

        private static void Move(int to)
        {
            if (to < 0) { Speech.Say(Loc.T("settings.top") + " " + ItemText(Items[_index]), true); return; }
            if (to >= Items.Count) { Speech.Say(Loc.T("settings.end") + " " + ItemText(Items[_index]), true); return; }
            bool newSection = Items[to].SectionKey != Items[_index].SectionKey;
            _index = to;
            Speech.Say((newSection ? Items[_index].Section + ". " : "") + ItemText(Items[_index]), true);
        }

        private static void JumpSection(int dir)
        {
            string current = Items[_index].SectionKey;
            int i = _index;
            if (dir > 0)
            {
                while (i < Items.Count && Items[i].SectionKey == current) i++;
                if (i >= Items.Count) i = 0;
            }
            else
            {
                while (i > 0 && Items[i - 1].SectionKey == current) i--; // start of this section
                i = i == 0 ? Items.Count - 1 : i - 1;                     // into the previous one
                string prev = Items[i].SectionKey;
                while (i > 0 && Items[i - 1].SectionKey == prev) i--;
            }
            _index = i;
            Speech.Say(Items[_index].Section + ". " + ItemText(Items[_index]), true);
        }

        public static void Update()
        {
            var item = Items[_index];

            if (KeyInput.WasPressed(Key.Escape) || KeyInput.WasPressed(Key.F10)) { Close(); return; }
            if (KeyInput.Ctrl && (KeyInput.WasPressed(Key.UpArrow) || KeyInput.WasPressed(Key.DownArrow)))
            {
                Speech.Say(item.Help, true);
                return;
            }
            if (KeyInput.WasPressed(Key.DownArrow)) Move(_index + 1);
            else if (KeyInput.WasPressed(Key.UpArrow)) Move(_index - 1);
            else if (KeyInput.WasPressed(Key.Home)) Move(0);
            else if (KeyInput.WasPressed(Key.End)) Move(Items.Count - 1);
            else if (KeyInput.WasPressed(Key.Tab)) JumpSection(KeyInput.Shift ? -1 : 1);
            else if (KeyInput.WasPressed(Key.RightArrow) || KeyInput.WasPressed(Key.LeftArrow))
            {
                int dir = KeyInput.WasPressed(Key.RightArrow) ? 1 : -1;
                if (item is ActionItem) { Speech.Say(ItemText(item), true); return; }
                bool changed = item.Step(dir);
                Speech.Say(changed ? item.ValueText : Loc.T(dir > 0 ? "settings.max" : "settings.min") + ", " + item.ValueText, true);
            }
            else if (KeyInput.WasPressed(Key.Enter) || KeyInput.WasPressed(Key.NumpadEnter) || KeyInput.WasPressed(Key.Space))
            {
                item.Activate();
                if (item is BoolSetting) Speech.Say(item.ValueText, true);
                else if (!(item is ActionItem)) Speech.Say(ItemText(item) + ". " + Loc.T("settings.adjust"), true);
            }
        }

        /// <summary>Asks for a second Enter within a few seconds, so a stray key cannot wipe the settings.</summary>
        private static void ResetDefaults()
        {
            if (Time.unscaledTime > _resetArmedUntil)
            {
                _resetArmedUntil = Time.unscaledTime + 4f;
                Speech.Say(Loc.T("settings.reset_confirm"), true);
                return;
            }
            _resetArmedUntil = 0f;
            foreach (var kv in (IEnumerable<KeyValuePair<BepInEx.Configuration.ConfigDefinition, BepInEx.Configuration.ConfigEntryBase>>)Plugin.Instance.Config)
                kv.Value.BoxedValue = kv.Value.DefaultValue;
            Plugin.Instance.Config.Save();
            Speech.Say(Loc.T("settings.reset_done"), true);
        }
    }
}
