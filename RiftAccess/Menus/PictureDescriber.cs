using System.Collections.Generic;
using RiftAccess.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RiftAccess.Menus
{
    /// <summary>
    /// Describes the pictures the game shows, written by the mod from the game's own art (Lang art.&lt;sprite&gt;), as
    /// detail lines of the review buffer (Alt+Up). In a dialogue scene: the place and who is there; in a boss battle the
    /// boss; in a song its background. Elsewhere: the art of the focused item (album covers, monsters, profile
    /// items...), or with nothing focused the described art on screen.
    /// </summary>
    internal static class PictureDescriber
    {
        private static readonly System.Reflection.FieldInfo _albumArt =
            HarmonyLib.AccessTools.Field(typeof(Shared.TrackSelection.BaseTrackSelectionOption), "_albumArtImage");

        // Profile cards preview the highlighted item on the card itself (icon, portrait, frame), not in the list item:
        // the card part whose sprite changed last is the item being looked at.
        private static readonly Dictionary<string, string> CardSprites = new Dictionary<string, string>();
        private static string _cardChanged;
        private static float _cardChangedAt = -100f, _nextCardPoll;

        /// <summary>Called every frame; polls the profile card parts a few times a second.</summary>
        public static void PollProfileCard()
        {
            if (Time.unscaledTime < _nextCardPoll) return;
            _nextCardPoll = Time.unscaledTime + 0.25f;
            var targets = Object.FindObjectsOfType<Shared.PlayerData.Profile.ProfileCustomizationTarget>();
            if (targets.Length == 0) { CardSprites.Clear(); _cardChanged = null; return; }
            var seen = new HashSet<string>();
            foreach (var t in targets)
            {
                if (!t.isActiveAndEnabled || !t.TryGetComponent<Image>(out var img) || img.sprite == null) continue;
                // Keyed by card part: the game rebuilds the card for each previewed item.
                string id = t.TargetName;
                string n = img.sprite.name;
                if (!seen.Add(id)) continue; // several cards on screen (leaderboards): the first of each part
                if (CardSprites.TryGetValue(id, out var old) && old != n) { _cardChanged = n; _cardChangedAt = Time.unscaledTime; Plugin.LogDebug("profile card " + t.TargetName + " -> " + n); }
                CardSprites[id] = n;
            }
        }

        public static List<string> DetailLines()
        {
            var parts = new List<string>();
            string scene = SceneDescriber.CurrentSceneText();
            if (scene != null) parts.AddRange(scene.Split(new[] { ". " }, System.StringSplitOptions.RemoveEmptyEntries));
            if (parts.Count == 0) { string video = TutorialVideos.Current(); if (video != null) parts.Add(video); }
            if (parts.Count == 0) { string battle = BattleText(); if (battle != null) { parts.Add(battle); parts.AddRange(StageArt.MinionLines()); } }
            if (parts.Count == 0 && Gameplay.StageWatcher.InStage && !Gameplay.StageWatcher.InBattle)
            {
                // Paused in a Rhythm Rift song (the buffers are off while it plays): the portraits, then the backdrop.
                parts.AddRange(StageArt.PortraitLines());
                parts.Add(Loc.T("art.song_background"));
            }
            if (parts.Count == 0)
            {
                // Track lists: the selected row's album art.
                foreach (var g in Object.FindObjectsOfType<Shared.TrackSelection.BaseTrackSelectionOptionGroup>())
                {
                    if (!g.isActiveAndEnabled) continue;
                    var opt = g.GetSelectedOption();
                    var img = opt != null && _albumArt != null ? _albumArt.GetValue(opt) as Image : null;
                    if (img != null && img.sprite != null) { Add(img.sprite.name, parts); break; }
                }
            }
            if (parts.Count == 0)
            {
                var focus = MenuReader.Focused;
                if (focus != null && focus.isActiveAndEnabled) Collect(focus.transform, parts);
            }
            if (parts.Count == 0 && _cardChanged != null && Time.unscaledTime - _cardChangedAt < 30f) Add(_cardChanged, parts);
            if (scene == null && !Gameplay.StageWatcher.InStage) Backdrop(parts);
            if (parts.Count == 0 && ModConfig.DebugLogging.Value)
            {
                // Which pictures are on screen, so descriptions can be written for them.
                var seen = new List<string>();
                var focus = MenuReader.Focused;
                if (focus != null) foreach (var img in focus.GetComponentsInChildren<Image>(false)) if (img.sprite != null) seen.Add("focus:" + img.sprite.name);
                foreach (var img in Object.FindObjectsOfType<Image>()) if (img.isActiveAndEnabled && img.sprite != null && img.rectTransform.rect.width > 120) seen.Add(img.sprite.name);
                Plugin.LogInfo("Undescribed pictures: " + string.Join(" | ", seen));
            }
            return parts;
        }

        /// <summary>
        /// The screen's large described pictures (the main menu's Cadence and city, the menu backdrop...), after the
        /// focused item's own details. Only pictures really visible: menus keep hidden screens loaded.
        /// </summary>
        private static void Backdrop(List<string> parts)
        {
            var people = new List<string>();
            var other = new List<string>();
            foreach (var img in Object.FindObjectsOfType<Image>())
            {
                if (!img.isActiveAndEnabled || img.sprite == null || img.color.a < 0.05f) continue;
                var r = img.rectTransform.rect;
                if (r.width < 300 && r.height < 300) continue;
                if (img.canvasRenderer.GetInheritedAlpha() < 0.05f || img.canvasRenderer.cull || !OnScreen(img)) continue;
                if (img.GetComponentInParent<Shared.MenuOptions.SelectableOption>() != null) continue; // row content, not backdrop
                bool person = Portraits.Map.ContainsKey(img.sprite.name.ToLowerInvariant().Replace("(clone)", "").Trim());
                if (!Add(img.sprite.name, person ? people : other) && ModConfig.DebugLogging.Value && LoggedMissing.Add(img.sprite.name))
                    Plugin.LogInfo("Undescribed visible picture: " + img.sprite.name + " (" + img.transform.name + ")");
            }
            // Characters first, then specific pictures, the screen-wide backdrops last.
            var generic = new HashSet<string>();
            foreach (var k in GenericBackdrops) generic.Add(Loc.T(k));
            other.Sort((x, y) => (generic.Contains(x) ? 1 : 0).CompareTo(generic.Contains(y) ? 1 : 0));
            int added = 0;
            foreach (var s in people) if (added < 4 && !parts.Contains(s)) { parts.Add(s); added++; }
            foreach (var s in other) if (added < 4 && !parts.Contains(s)) { parts.Add(s); added++; }
        }

        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly HashSet<string> LoggedMissing = new HashSet<string>();
        private static readonly string[] GenericBackdrops =
        {
            "art.rift_ui_bg_colorlines", "art.tsbg_easy", "art.tsbg_medium", "art.tsbg_hard", "art.tsbg_expert", "art.splotch",
            "art.storybg", "art.ui_playerprofile_expandedprofilescreen_background", "art.mainmenu_bg_temp",
        };

        /// <summary>The image's canvas is drawn and at least a third of the image lies inside the screen.</summary>
        private static bool OnScreen(Image img)
        {
            var canvas = img.canvas;
            if (canvas == null || !canvas.isActiveAndEnabled) return false;
            for (var cg = img.GetComponentInParent<CanvasGroup>(); cg != null; cg = cg.transform.parent != null ? cg.transform.parent.GetComponentInParent<CanvasGroup>() : null)
            {
                if (cg.alpha < 0.05f) return false;
                if (cg.ignoreParentGroups) break;
            }
            var root = canvas.rootCanvas;
            Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            img.rectTransform.GetWorldCorners(Corners);
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var c in Corners)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, c);
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            float area = (maxX - minX) * (maxY - minY);
            if (area <= 1f) return false;
            float ix = Mathf.Max(0f, Mathf.Min(maxX, Screen.width) - Mathf.Max(minX, 0f));
            float iy = Mathf.Max(0f, Mathf.Min(maxY, Screen.height) - Mathf.Max(minY, 0f));
            return ix * iy >= area / 3f;
        }

        /// <summary>In a boss battle: the boss's look, from the boss data's name.</summary>
        /// <summary>The current boss battle's boss id (harmonie, banshee, queen, matron, reaper, nd_boss), or null.</summary>
        public static string BossId()
        {
            try
            {
                var boss = Object.FindObjectOfType<BossBattles.BossController>();
                if (boss == null || !boss.isActiveAndEnabled) return null;
                var data = HarmonyLib.AccessTools.Field(typeof(BossBattles.BossController), "_bossData").GetValue(boss) as BossBattles.BossData;
                string n = ((data != null ? data.BossName + " " + data.name : "") ?? "").ToLowerInvariant();
                string ch = n.Contains("harmonie") ? "harmonie" : n.Contains("banshee") ? "banshee" : n.Contains("deep") || n.Contains("queen") ? "queen"
                          : n.Contains("matron") ? "matron" : n.Contains("reaper") ? "reaper" : n.Contains("necro") ? "nd_boss" : null;
                if (ch == null) Plugin.LogDebug("Unknown boss: " + n);
                return ch;
            }
            catch { return null; }
        }

        private static string BattleText()
        {
            string id = BossId();
            return id != null ? Loc.F("art.boss", Loc.T("char." + id)) : null;
        }

        private static void Collect(Transform root, List<string> parts)
        {
            // The focused item's own pictures (up to 3), not those of rows nested inside it (a list group holds its rows).
            var self = root.GetComponent<Shared.MenuOptions.SelectableOption>();
            foreach (var img in root.GetComponentsInChildren<Image>(false))
            {
                if (parts.Count >= 3) return;
                if (img.sprite == null) continue;
                if (self != null && img.GetComponentInParent<Shared.MenuOptions.SelectableOption>() != self) continue;
                Add(img.sprite.name, parts);
            }
        }

        /// <summary>Adds the sprite's description unless already there; false when the sprite has none.</summary>
        private static bool Add(string spriteName, List<string> parts)
        {
            if (string.IsNullOrEmpty(spriteName)) return false;
            if (Portraits.Map.TryGetValue(spriteName.ToLowerInvariant().Replace("(clone)", "").Trim(), out var pk))
            {
                string p = Loc.T(pk[0]) + (pk[1] != null ? ", " + Loc.T(pk[1]) : "");
                if (!parts.Contains(p)) parts.Add(p);
                return true;
            }
            string k = Portraits.Alias.TryGetValue(spriteName.ToLowerInvariant().Trim(), out var alias) ? alias : Key(spriteName);
            if (k == null || !Loc.Has(k)) return false;
            string s = Loc.T(k);
            if (!parts.Contains(s)) parts.Add(s);
            return true;
        }

        /// <summary>Lang key for a sprite: art.&lt;name&gt; in lower case, small and large album art sharing one.</summary>
        public static string Key(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
            string n = spriteName.ToLowerInvariant().Replace("(clone)", "").Trim();
            n = n.Replace("album_small_", "album_large_").Replace("partamello", "portamello").Replace("_downloadicon", "");
            return "art." + n;
        }
    }
}
