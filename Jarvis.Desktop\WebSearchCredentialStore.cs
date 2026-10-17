using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Jarvis.Desktop;

internal static class WebSearchCredentialStore
{
    private const string CredentialFileName = "ollama-web-search-key.dpapi";

    public static bool HasKey(string dataPath) => File.Exists(Path.Combine(dataPath, CredentialFileName));

    public static void Save(string dataPath, string apiKey)
    {
        string key = apiKey.Trim();
        if (key.Length is < 16 or > 512 || key.Contains('\n') || key.Contains('\r'))
            throw new ArgumentException("La clave parece vacía o tiene un formato no válido.");

        byte[] clear = Encoding.UTF8.GetBytes(key);
        byte[] encrypted = Protect(clear);
        CryptographicOperations.ZeroMemory(clear);
        string path = Path.Combine(dataPath, CredentialFileName);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, encrypted);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encrypted);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string Load(string dataPath)
    {
        string path = Path.Combine(dataPath, CredentialFileName);
        if (!File.Exists(path)) throw new InvalidOperationException("Primero configure la clave de búsqueda web.");
        byte[] encrypted = File.ReadAllBytes(path);
        byte[] clear = Unprotect(encrypted);
        CryptographicOperations.ZeroMemory(encrypted);
        try { return Encoding.UTF8.GetString(clear); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    public static void Remove(string dataPath)
    {
        string path = Path.Combine(dataPath, CredentialFileName);
        if (File.Exists(path)) File.Delete(path);
    }

    private static byte[] Protect(byte[] data) => Transform(data, protect: true);
    private static byte[] Unprotect(byte[] data) => Transform(data, protect: false);

    private static byte[] Transform(byte[] data, bool protect)
    {
        var input = new DataBlob { Length = data.Length, Data = Marshal.AllocHGlobal(data.Length) };
        DataBlob output = default;
        IntPtr description = IntPtr.Zero;
        try
        {
            Marshal.Copy(data, 0, input.Data, data.Length);
            bool succeeded = protect
                ? CryptProtectData(ref input, "J.A.R.V.I.S. Ollama web search key", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output)
                : CryptUnprotectData(ref input, out description, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, out output);
            if (!succeeded) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows no pudo proteger la clave de búsqueda.");
            byte[] result = new byte[output.Length];
            Marshal.Copy(output.Data, result, 0, output.Length);
            return result;
        }
        finally
        {
            if (input.Data != IntPtr.Zero)
            {
                ZeroMemory(input.Data, input.Length);
                Marshal.FreeHGlobal(input.Data);
            }
            if (output.Data != IntPtr.Zero) LocalFree(output.Data);
            if (description != IntPtr.Zero) LocalFree(description);
        }
    }

    private static void ZeroMemory(IntPtr pointer, int length)
    {
        for (int i = 0; i < length; i++) Marshal.WriteByte(pointer, i, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Length;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob dataIn, string description, IntPtr optionalEntropy,
        IntPtr reserved, IntPtr prompt, uint flags, out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob dataIn, out IntPtr description, IntPtr optionalEntropy,
        IntPtr reserved, IntPtr prompt, uint flags, out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
