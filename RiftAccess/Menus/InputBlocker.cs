using System;
using System.Collections.Generic;
using HarmonyLib;
using Shared.RiftInput;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RiftAccess.Menus
{
    /// <summary>
    /// While a mod menu is open the game must not also act on arrows, Enter and Escape. The game reads input in
    /// two ways, both through its one RiftInputActions (InputAccessor.Instance.RiftInput):
    ///   - polling (WasPerformedThisFrame, IsPressed, WasPressedThisFrame, WasReleasedThisFrame): gated by
    ///     Harmony prefixes that answer false for the game's actions while blocking;
    ///   - performed/started callbacks: stopped by disabling its action maps.
    /// Both are lifted one frame after the menu closes, so the closing key press never reaches the game.
    /// </summary>
    internal static class InputBlocker
    {
        private static readonly List<InputActionMap> Disabled = new List<InputActionMap>();
        private static bool _blocking;
        private static int _releaseFrame = -1;
        private static InputActionAsset _asset;

        public static bool IsBlocking => _blocking || _releaseFrame >= 0;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var prefix = new HarmonyMethod(typeof(InputBlocker), nameof(PollPrefix));
                foreach (var name in new[] { "WasPerformedThisFrame", "IsPressed", "WasPressedThisFrame", "WasReleasedThisFrame" })
                {
                    var m = AccessTools.Method(typeof(InputAction), name, Type.EmptyTypes);
                    if (m != null) harmony.Patch(m, prefix: prefix);
                    else Plugin.LogWarning("InputAction." + name + " not found");
                }
                Plugin.LogInfo("Patched InputAction polling (input blocking for mod menus)");
            }
            catch (Exception ex) { Plugin.LogError("Input blocker: " + ex); }
        }

        private static bool PollPrefix(InputAction __instance, ref bool __result)
        {
            if (!IsBlocking) return true;
            try
            {
                if (_asset == null) _asset = InputAccessor.Instance?.RiftInput?.asset;
                if (_asset != null && __instance.actionMap != null && __instance.actionMap.asset == _asset)
                {
                    __result = false;
                    return false;
                }
            }
            catch { }
            return true;
        }

        public static void Block()
        {
            _blocking = true;
            _releaseFrame = -1;
            Enforce();
        }

        public static void Release()
        {
            _blocking = false;
            _releaseFrame = Time.frameCount + 2;
        }

        /// <summary>Call every frame.</summary>
        public static void Update()
        {
            if (_blocking) Enforce();
            else if (_releaseFrame >= 0 && Time.frameCount >= _releaseFrame)
            {
                foreach (var m in Disabled)
                {
                    try { if (!m.enabled) m.Enable(); } catch { }
                }
                Disabled.Clear();
                _releaseFrame = -1;
            }
        }

        private static void Enforce()
        {
            try
            {
                var asset = InputAccessor.Instance?.RiftInput?.asset;
                if (asset == null) return;
                _asset = asset;
                foreach (var map in asset.actionMaps)
                {
                    if (!map.enabled) continue;
                    if (!Disabled.Contains(map)) Disabled.Add(map);
                    map.Disable();
                }
            }
            catch (Exception ex) { Plugin.LogDebug("Input blocker: " + ex.Message); }
        }
    }
}
