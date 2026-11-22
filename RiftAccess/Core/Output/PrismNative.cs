using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RiftAccess.Core.Output
{
    /// <summary>
    /// P/Invoke layer over Prism v0.18.2 (vendor\prism, prism-windows-x64; header in vendor\prism\include).
    /// Facts from prism.h that matter: cdecl; UTF-8 null-terminated strings owned by the caller;
    /// C bool is one byte; PrismError 0 = PRISM_OK; strings returned by the library are library-owned.
    /// The game is x64, so the DLL loads in process.
    /// Nothing outside <see cref="PrismSpeech"/> may call into this class.
    /// </summary>
    internal static class PrismNative
    {
        private const string Dll = "prism";

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibraryW(string path);

        /// <summary>PrismConfig from prism.h. Natural C alignment; the default marshaller matches it on x64.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct PrismConfig
        {
            public byte version;
            public IntPtr registry;
            public IntPtr availability_callback;
            public IntPtr availability_userdata;
            public uint availability_poll_interval_ms;
            public uint availability_debounce_samples;
            public uint availability_backoff_max_ms;
            [MarshalAs(UnmanagedType.I1)] public bool availability_auto_power_manage;
            public IntPtr availability_baseline_callback; // added in 0.18.3; prism_config_init returns the full struct by value
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void AvailabilityCallback(IntPtr userdata, ulong backend, IntPtr name,
            [MarshalAs(UnmanagedType.I1)] bool available);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PrismConfig prism_config_init();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_init(ref PrismConfig cfg);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_init(IntPtr cfg);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void prism_shutdown(IntPtr ctx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_registry_create_best(IntPtr ctx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UIntPtr prism_registry_count(IntPtr ctx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong prism_registry_id(IntPtr ctx, byte[] name);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong prism_registry_id_at(IntPtr ctx, UIntPtr index);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_registry_name(IntPtr ctx, ulong id);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_registry_create(IntPtr ctx, ulong id);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void prism_backend_free(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_backend_name(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong prism_backend_get_features(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_speak(IntPtr backend, byte[] utf8,
            [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_output(IntPtr backend, byte[] utf8,
            [MarshalAs(UnmanagedType.I1)] bool interrupt);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_braille(IntPtr backend, byte[] utf8);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int prism_backend_stop(IntPtr backend);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr prism_error_string(int error);

        // Feature bits (PrismBackendFeature in prism.h).
        internal const ulong SupportsSpeak = 1UL << 2;
        internal const ulong SupportsBraille = 1UL << 4;
        internal const ulong SupportsOutput = 1UL << 5;
        internal const ulong SupportsStop = 1UL << 7;

        /// <summary>
        /// Old Unity Mono runtimes do not reliably support UnmanagedType.LPUTF8Str, so encode by hand.
        /// The returned array's last byte is already zero.
        /// </summary>
        internal static byte[] Utf8Z(string s)
        {
            if (s == null) s = "";
            var bytes = new byte[Encoding.UTF8.GetByteCount(s) + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
            return bytes;
        }

        /// <summary>Reads a library-owned UTF-8 string. Never freed by us.</summary>
        internal static string ReadUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return "";
            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0) len++;
            if (len == 0) return "";
            var buf = new byte[len];
            Marshal.Copy(ptr, buf, 0, len);
            return Encoding.UTF8.GetString(buf);
        }

        internal static string ErrorText(int error)
        {
            try { return ReadUtf8(prism_error_string(error)); }
            catch { return "error " + error; }
        }
    }
}
