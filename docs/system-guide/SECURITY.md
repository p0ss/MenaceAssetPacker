# Security Policy

## Reporting Vulnerabilities

If you discover a security vulnerability in MenaceAssetPacker, please report it responsibly:

1. **Do not** create a public GitHub issue for security vulnerabilities
2. Message us on discord with
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Any suggested fixes (optional)

We will respond within 72 hours and work with you to address the issue.

## Security Model

### Trust Levels

MenaceAssetPacker operates with different trust levels for different content:

| Content Type | Trust Level | Notes |
|--------------|-------------|-------|
| Vanilla game assets | Fully trusted | Read from game installation |
| Modpack data files (JSON) | Semi-trusted | User downloads and installs; validated on load |
| Modpack assets (images, etc.) | Semi-trusted | User downloads and installs; loaded by game |
| Modpack DLLs | Untrusted | Requires explicit user approval to load |
| REPL code | Sandboxed | Dangerous operations blocked |

### Modpack Security

Modpacks are semi-trusted because:
- Users explicitly choose to download and install them
- They can modify game behavior significantly
- They may contain code (DLLs, scripts)

**DLL Loading:**
- DLLs in modpacks are marked as "UNVERIFIED" by default
- Unverified DLLs are **not loaded** unless the user explicitly enables them
- Source-verified modpacks (where source can be audited) are loaded normally

### REPL Security

The built-in REPL console has security restrictions:

**Blocked Namespaces:**
- `System.Reflection.Emit` (code generation)
- `System.Runtime.InteropServices` (native interop)
- `System.Diagnostics.Process` (process execution)
- `System.IO.File`, `System.IO.Directory` (file system access)
- `System.Net.*` (network access)

**Blocked Operations:**
- `Process.Start`
- `Assembly.Load*`
- `File.WriteAllText`, `File.Delete`
- `Environment.Exit`

**Disabled Features:**
- Unsafe code (`unsafe` keyword)
- Pointer operations (`fixed`, `stackalloc`)

### Path Traversal Protection

All file paths derived from user input are validated:
- Archive extraction validates each entry path (Zip Slip protection)
- Modpack asset paths are validated to stay within modpack directory
- Document navigation validates paths stay within docs directory

### Archive Extraction

When extracting archives (modpacks, tools):
- Each entry path is validated before extraction
- Entries attempting to escape the destination directory are rejected
- Both relative paths (`../`) and absolute paths are blocked

## Secure Development Practices

### For Contributors

1. **Validate all paths** derived from user input using `PathValidator`
2. **Never use string interpolation** for command-line arguments; use `ProcessStartInfo.ArgumentList`
3. **Avoid empty catch blocks**; log exceptions appropriately
4. **Don't store secrets** in source code or configuration files

### For Modpack Creators

1. **Don't include unnecessary DLLs** in modpacks
2. **Document what your code does** if you include DLLs
3. **Provide source code** when possible to enable verification
4. **Test modpacks** before distribution

## Version Support

| Version | Supported |
|---------|-----------|
| Latest  | Yes       |
| Older   | Best effort |

## Acknowledgments

We thank all security researchers who responsibly disclose vulnerabilities.
