using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ClaudeCodePanel.Windows.Services;

/// <summary>
/// Windows Credential Manager wrapper — equivalent of macOS KeychainService.
/// Uses P/Invoke to advapi32.dll (CredWriteW, CredReadW, CredDeleteW, CredFree)
/// to store and retrieve generic credentials.
/// </summary>
public sealed class CredentialService : ICredentialService
{
    public static CredentialService Instance { get; } = new();

    private const string ServiceName = "com.claudecodepanel.app";

    private CredentialService() { }

    // --- Win32 constants ----------------------------------------------------

    private const int CRED_TYPE_GENERIC = 1;
    private const int CRED_PERSIST_LOCAL_MACHINE = 2;

    // --- P/Invoke declarations ----------------------------------------------

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWriteW([In] ref CREDENTIAL credential, [In] uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredReadW(
        [In] string targetName,
        [In] uint type,
        [In] uint flags,
        [Out] out IntPtr credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDeleteW(
        [In] string targetName,
        [In] uint type,
        [In] uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree([In] IntPtr buffer);

    // --- CREDENTIAL struct --------------------------------------------------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string UserName;
    }

    // --- Private helpers ----------------------------------------------------

    private static string MakeTargetName(string key)
    {
        return $"{ServiceName}/{key}";
    }

    // --- Public API ---------------------------------------------------------

    /// <summary>
    /// Saves a credential value for the given key.
    /// Deletes any existing credential with the same key first, then writes the new value.
    /// </summary>
    public void Save(string key, string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        string targetName = MakeTargetName(key);

        // Remove any existing credential for this key (ignore not-found).
        CredDeleteW(targetName, CRED_TYPE_GENERIC, 0);

        byte[] blob = Encoding.UTF8.GetBytes(value);
        IntPtr blobPtr = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);

            var credential = new CREDENTIAL
            {
                Flags = 0,
                Type = CRED_TYPE_GENERIC,
                TargetName = targetName,
                Comment = null!,
                LastWritten = default,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobPtr,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                TargetAlias = null!,
                UserName = null!,
            };

            if (!CredWriteW(ref credential, 0))
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    $"CredWriteW failed for target '{targetName}'. Win32 error: {error}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    /// <summary>
    /// <summary>
    /// Reads the credential value for the given key.
    /// Returns the stored value, or throws if not found / on error.
    /// </summary>
    public string Read(string key)
    {
        if (TryRead(key, out var value))
            return value;

        throw new InvalidOperationException(
            $"Credential not found for key '{key}'.");
    }

    /// <summary>
    /// Attempts to read the credential value for the given key.
    /// Returns true and sets <paramref name="value"/> if found;
    /// returns false if the credential does not exist or on error.
    /// </summary>
    public bool TryRead(string key, out string value)
    {
        ArgumentNullException.ThrowIfNull(key);
        value = "";

        string targetName = MakeTargetName(key);

        if (!CredReadW(targetName, CRED_TYPE_GENERIC, 0, out IntPtr credPtr))
        {
            int error = Marshal.GetLastWin32Error();
            // ERROR_NOT_FOUND (1168) is a normal "not found" case
            if (error == 1168)
                return false;

            throw new InvalidOperationException(
                $"CredReadW failed for target '{targetName}'. Win32 error: {error}");
        }

        try
        {
            var credential = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
                return false;

            byte[] blob = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, blob, 0, (int)credential.CredentialBlobSize);
            value = Encoding.UTF8.GetString(blob);
            return true;
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    /// <summary>
    /// Deletes the credential for the given key.
    /// No-op if the credential does not exist.
    /// </summary>
    public void Delete(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        string targetName = MakeTargetName(key);

        if (!CredDeleteW(targetName, CRED_TYPE_GENERIC, 0))
        {
            int error = Marshal.GetLastWin32Error();
            // ERROR_NOT_FOUND (1168) — the item doesn't exist, which is a successful no-op.
            if (error == 1168) // ERROR_NOT_FOUND
            {
                return;
            }

            throw new InvalidOperationException(
                $"CredDeleteW failed for target '{targetName}'. Win32 error: {error}");
        }
    }

    /// <summary>
    /// Returns true if a credential exists for the given key.
    /// Uses TryRead to avoid exception-as-control-flow.
    /// </summary>
    public bool Exists(string key)
    {
        return TryRead(key, out _);
    }
}
