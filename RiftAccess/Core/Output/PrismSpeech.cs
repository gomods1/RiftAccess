using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RiftAccess.Core.Output
{
    /// <summary>
    /// Owns the Prism context and backend. The only class that touches <see cref="PrismNative"/>.
    /// A backend instance is not thread safe, so every call here must come from Unity's main thread;
    /// <see cref="Speech"/> enforces that by queueing anything that arrives from elsewhere.
    /// </summary>
    internal sealed class PrismSpeech
    {
        private IntPtr _ctx = IntPtr.Zero;
        private IntPtr _backend = IntPtr.Zero;
        private ulong _features;
        private float _lastRecreate;
        private int _consecutiveErrors;

        // Kept in a field so the GC cannot collect the delegate Prism holds a pointer to.
        private PrismNative.AvailabilityCallback _availabilityCallback;

        /// <summary>Set from Prism's polling thread; acted on by the main thread in <see cref="Poll"/>.</summary>
        private volatile bool _availabilityChanged;

        public string BackendName { get; private set; } = "";
        public bool Ready => _backend != IntPtr.Zero;

        /// <summary>The backend the player asked for in the config: "Auto" or a Prism backend name.</summary>
        private string _preferred = "Auto";

        public bool Initialize(string pluginDir, string preferredBackend)
        {
            _preferred = string.IsNullOrEmpty(preferredBackend) ? "Auto" : preferredBackend.Trim();
            try
            {
                // The plugins folder is not on the DLL search path: load by full path first.
                string dllPath = Path.Combine(pluginDir, "prism.dll");
                if (File.Exists(dllPath))
                {
                    if (PrismNative.LoadLibraryW(dllPath) == IntPtr.Zero)
                        Plugin.LogWarning("LoadLibrary(prism.dll) failed; relying on the default search path.");
                }
                else
                {
                    Plugin.LogWarning("prism.dll not found next to the plugin (" + dllPath + ").");
                }

                var cfg = PrismNative.prism_config_init();
                _availabilityCallback = OnAvailabilityChanged;
                cfg.availability_callback = Marshal.GetFunctionPointerForDelegate(_availabilityCallback);
                _ctx = PrismNative.prism_init(ref cfg);
                if (_ctx == IntPtr.Zero)
                {
                    Plugin.LogError("prism_init returned null; speech is unavailable.");
                    return false;
                }

                CreateBackend();
                return Ready;
            }
            catch (DllNotFoundException)
            {
                Plugin.LogError("prism.dll could not be loaded. Copy it next to RiftAccess.dll " +
                                "(BepInEx\\plugins\\RiftAccess\\). Rift of the NecroDancer is 64-bit, so it needs the x64 build.");
            }
            catch (BadImageFormatException)
            {
                Plugin.LogError("prism.dll is the wrong architecture. Rift of the NecroDancer is 64-bit; use the prism-windows-x64 build.");
            }
            catch (Exception ex)
            {
                Plugin.LogError("Prism initialisation failed: " + ex);
            }
            return false;
        }

        /// <summary>Called on Prism's polling thread. Only sets a flag: no Prism call may happen here.</summary>
        private void OnAvailabilityChanged(IntPtr userdata, ulong backend, IntPtr name, bool available)
        {
            _availabilityChanged = true;
        }

        /// <summary>Main thread. Rebuilds the backend when a screen reader started or stopped.</summary>
        public void Poll()
        {
            if (!_availabilityChanged) return;
            _availabilityChanged = false;
            if (_ctx == IntPtr.Zero) return;
            Plugin.LogInfo("Prism reports a change in screen reader availability; re-selecting the backend.");
            RecreateBackend();
        }

        private void CreateBackend()
        {
            if (_ctx == IntPtr.Zero) return;
            try
            {
                if (!string.Equals(_preferred, "Auto", StringComparison.OrdinalIgnoreCase))
                {
                    ulong id = PrismNative.prism_registry_id(_ctx, PrismNative.Utf8Z(_preferred));
                    if (id != 0)
                    {
                        IntPtr named = PrismNative.prism_registry_create(_ctx, id);
                        if (named != IntPtr.Zero)
                        {
                            Adopt(named);
                            return;
                        }
                    }
                    Plugin.LogWarning("Speech backend \"" + _preferred + "\" is not available; falling back to the best one. " +
                                      "Available: " + string.Join(", ", RegistryNames()));
                }

                // create_best returns the highest-priority backend that initialises, already initialised.
                IntPtr best = PrismNative.prism_registry_create_best(_ctx);
                if (best == IntPtr.Zero)
                {
                    Plugin.LogError("No speech backend is available. Start a screen reader, or install Windows speech.");
                    return;
                }
                Adopt(best);
            }
            catch (Exception ex)
            {
                Plugin.LogError("Creating a speech backend failed: " + ex.Message);
            }
        }

        private void Adopt(IntPtr backend)
        {
            _backend = backend;
            _features = PrismNative.prism_backend_get_features(backend);
            BackendName = PrismNative.ReadUtf8(PrismNative.prism_backend_name(backend));
            _consecutiveErrors = 0;
            Plugin.LogInfo("Speech backend: " + BackendName +
                           " (output " + Supports(PrismNative.SupportsOutput) +
                           ", speak " + Supports(PrismNative.SupportsSpeak) +
                           ", braille " + Supports(PrismNative.SupportsBraille) +
                           ", stop " + Supports(PrismNative.SupportsStop) + ")");
        }

        private string Supports(ulong bit) => (_features & bit) != 0 ? "yes" : "no";

        public string[] RegistryNames()
        {
            try
            {
                int n = (int)PrismNative.prism_registry_count(_ctx).ToUInt32();
                var names = new string[n];
                for (int i = 0; i < n; i++)
                {
                    ulong id = PrismNative.prism_registry_id_at(_ctx, (UIntPtr)(uint)i);
                    names[i] = PrismNative.ReadUtf8(PrismNative.prism_registry_name(_ctx, id));
                }
                return names;
            }
            catch { return new string[0]; }
        }

        private void RecreateBackend()
        {
            FreeBackend();
            CreateBackend();
        }

        private void FreeBackend()
        {
            if (_backend == IntPtr.Zero) return;
            try { PrismNative.prism_backend_free(_backend); } catch { }
            _backend = IntPtr.Zero;
            _features = 0;
            BackendName = "";
        }

        public void Speak(string text, bool interrupt, bool braille)
        {
            if (_backend == IntPtr.Zero || string.IsNullOrEmpty(text)) return;
            byte[] utf8 = PrismNative.Utf8Z(text);
            int result;
            try
            {
                if ((_features & PrismNative.SupportsOutput) != 0)
                {
                    // Output is speech plus braille wherever the backend does both.
                    result = PrismNative.prism_backend_output(_backend, utf8, interrupt);
                }
                else
                {
                    result = PrismNative.prism_backend_speak(_backend, utf8, interrupt);
                    if (braille && (_features & PrismNative.SupportsBraille) != 0)
                    {
                        try { PrismNative.prism_backend_braille(_backend, utf8); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LogError("Speech call threw: " + ex.Message);
                TryRecover();
                return;
            }
            if (result != 0) OnError(result);
            else _consecutiveErrors = 0;
        }

        public void Stop()
        {
            if (_backend == IntPtr.Zero) return;
            if ((_features & PrismNative.SupportsStop) == 0) return;
            try { PrismNative.prism_backend_stop(_backend); } catch { }
        }

        private void OnError(int result)
        {
            _consecutiveErrors++;
            if (_consecutiveErrors <= 3 || _consecutiveErrors % 50 == 0)
                Plugin.LogWarning("Speech failed: " + PrismNative.ErrorText(result));
            // A screen reader that was closed leaves a dead backend behind; rebuild, but not every frame.
            if (_consecutiveErrors >= 3) TryRecover();
        }

        private void TryRecover()
        {
            float now = Time.unscaledTime;
            if (now - _lastRecreate < 5f) return;
            _lastRecreate = now;
            Plugin.LogInfo("Rebuilding the speech backend after repeated failures.");
            RecreateBackend();
        }

        public void Shutdown()
        {
            try { Stop(); } catch { }
            FreeBackend();
            if (_ctx != IntPtr.Zero)
            {
                try { PrismNative.prism_shutdown(_ctx); } catch { }
                _ctx = IntPtr.Zero;
            }
            _availabilityCallback = null;
        }
    }
}
