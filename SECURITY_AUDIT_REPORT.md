# Security and Code Quality Audit Report
## Q2 Server Browser Application

**Date:** $(date)  
**Scope:** Critical and High Severity Issues  
**Application:** Q2Connect/Q2Browser Server Browser

---

## Executive Summary

This audit identified **3 Critical** and **4 High** severity issues that require immediate attention. The most significant concerns involve command injection vulnerabilities, insecure JSON deserialization, and insufficient input validation.

---

## CRITICAL SEVERITY ISSUES

### 1. Command Injection Vulnerability - Missing Address Validation in Edit Window
**Severity:** CRITICAL  
**File:** `Q2Connect.Wpf/Views/EditAddressBookEntryWindow.xaml.cs`  
**Lines:** 21-35

**Issue:**
The `EditAddressBookEntryWindow` only validates that the address field is not empty, but does not validate the format or safety of the address before allowing it to be saved. While `LauncherService.SanitizeAddress()` provides some protection, the validation should occur at the point of input entry.

**Vulnerability:**
An attacker could potentially inject malicious characters into the address field, which could bypass sanitization if the regex pattern has edge cases, or if the address is used in other contexts.

**Current Code:**
```csharp
private void OkButton_Click(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrWhiteSpace(Entry.Address))
    {
        MessageBox.Show(...);
        return;
    }
    DialogResult = true;  // No format validation!
    Close();
}
```

**Recommendation:**
1. Add address format validation identical to `AddressBookViewModel.IsValidAddress()`
2. Use the same validation logic before accepting edited addresses
3. Validate both IPv4 and IPv6 address formats
4. Validate port range (1-65535)

**Fix:**
```csharp
private void OkButton_Click(object sender, RoutedEventArgs e)
{
    if (string.IsNullOrWhiteSpace(Entry.Address))
    {
        MessageBox.Show(
            "Address cannot be empty.",
            "Validation Error",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return;
    }
    
    // ADD: Format validation
    if (!IsValidAddress(Entry.Address.Trim()))
    {
        MessageBox.Show(
            "Invalid address format. Expected format: IP:Port (e.g., 192.168.1.1:27910)",
            "Invalid Address",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return;
    }
    
    DialogResult = true;
    Close();
}

private bool IsValidAddress(string address)
{
    if (string.IsNullOrWhiteSpace(address))
        return false;
    
    var parts = address.Split(':');
    if (parts.Length != 2)
        return false;
    
    if (!IPAddress.TryParse(parts[0], out _))
        return false;
    
    if (!int.TryParse(parts[1], out var port) || port < 1 || port > 65535)
        return false;
    
    return true;
}
```

---

### 2. Insecure JSON Deserialization - No Size Limits or Validation
**Severity:** CRITICAL  
**Files:** 
- `Q2Connect.Core/Services/FavoritesService.cs` (lines 86, 151, 258)
- `Q2Browser.Core/Services/FavoritesService.cs` (lines 72, 137, 230)

**Issue:**
JSON deserialization uses `JsonSerializer.Deserialize` without:
- File size limits (DoS vulnerability)
- Maximum depth limits (stack overflow)
- Type validation before deserialization
- Bounded array/list sizes

**Vulnerability:**
A maliciously crafted JSON file could:
- Cause denial of service through memory exhaustion
- Cause stack overflow with deeply nested structures
- Potentially trigger deserialization vulnerabilities

**Current Code:**
```csharp
var json = await File.ReadAllTextAsync(_favoritesPath).ConfigureAwait(false);
var favorites = JsonSerializer.Deserialize<List<string>>(json, _jsonOptions);
```

**Recommendation:**
1. Implement file size limits (e.g., 10MB max for settings, 5MB for favorites)
2. Configure JsonSerializerOptions with MaxDepth limit
3. Add try-catch with specific exception handling
4. Consider streaming deserialization for large files

**Fix:**
```csharp
private const int MAX_SETTINGS_FILE_SIZE = 10 * 1024 * 1024; // 10MB
private const int MAX_FAVORITES_FILE_SIZE = 5 * 1024 * 1024; // 5MB
private const int MAX_ADDRESSBOOK_FILE_SIZE = 10 * 1024 * 1024; // 10MB
private const int MAX_JSON_DEPTH = 64;

public async Task<List<string>> LoadFavoritesAsync()
{
    if (!File.Exists(_favoritesPath))
        return new List<string>();
    
    try
    {
        var fileInfo = new FileInfo(_favoritesPath);
        if (fileInfo.Length > MAX_FAVORITES_FILE_SIZE)
        {
            _logger?.LogError($"Favorites file too large: {fileInfo.Length} bytes");
            return new List<string>();
        }
        
        var json = await File.ReadAllTextAsync(_favoritesPath).ConfigureAwait(false);
        
        var options = new JsonSerializerOptions(_jsonOptions)
        {
            MaxDepth = MAX_JSON_DEPTH,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        
        var favorites = JsonSerializer.Deserialize<List<string>>(json, options);
        return favorites ?? new List<string>();
    }
    catch (JsonException ex)
    {
        _logger?.LogError($"Invalid JSON in favorites file: {ex.Message}", ex.StackTrace);
        return new List<string>();
    }
    catch (Exception ex)
    {
        _logger?.LogError($"Failed to load favorites: {ex.Message}", ex.StackTrace);
        return new List<string>();
    }
}
```

---

### 3. Unbounded Binary Data Parsing - HTTP Master Server Response
**Severity:** CRITICAL  
**File:** `Q2Connect.Core/Networking/HttpMasterServerClient.cs`  
**Lines:** 53, 127-170

**Issue:**
The HTTP master server client reads the entire response into memory without size limits, and then parses it without bounds checking on the number of servers that could be returned.

**Vulnerability:**
A malicious or compromised master server could:
- Send an extremely large response causing memory exhaustion (DoS)
- Return millions of server entries causing the application to consume excessive memory
- Cause performance degradation or crashes

**Current Code:**
```csharp
var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
// ... no size check ...
servers.AddRange(ParseBinaryFormat(data, 6));
```

**Recommendation:**
1. Limit maximum response size (e.g., 50MB)
2. Limit maximum number of servers parsed (e.g., 10,000)
3. Add early termination if limits are exceeded
4. Consider streaming/chunked processing

**Fix:**
```csharp
private const int MAX_HTTP_RESPONSE_SIZE = 50 * 1024 * 1024; // 50MB
private const int MAX_SERVERS_PER_RESPONSE = 10000;

public async Task<List<IPEndPoint>> QueryServersAsync(CancellationToken cancellationToken = default)
{
    // ... existing code ...
    
    var data = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    
    // ADD: Size check
    if (data.Length > MAX_HTTP_RESPONSE_SIZE)
    {
        _logger?.LogError($"HTTP master server response too large: {data.Length} bytes");
        return servers;
    }
    
    // ... existing parsing code ...
    
    var parsed = ParseBinaryFormat(data, 6);
    
    // ADD: Limit number of servers
    if (parsed.Count > MAX_SERVERS_PER_RESPONSE)
    {
        _logger?.LogWarning($"HTTP master server returned {parsed.Count} servers, limiting to {MAX_SERVERS_PER_RESPONSE}");
        servers.AddRange(parsed.Take(MAX_SERVERS_PER_RESPONSE));
    }
    else
    {
        servers.AddRange(parsed);
    }
    
    return servers;
}
```

---

## HIGH SEVERITY ISSUES

### 4. HttpClient Resource Leak - Not Using Static/Reused Instance
**Severity:** HIGH  
**File:** `Q2Connect.Core/Networking/HttpMasterServerClient.cs`  
**Lines:** 16-24

**Issue:**
Each `HttpMasterServerClient` instance creates a new `HttpClient`, which is disposed when the client is disposed. However, creating multiple instances (e.g., during settings reload) can lead to socket exhaustion.

**Vulnerability:**
- Socket exhaustion under high load
- DNS cache issues (each HttpClient maintains its own DNS cache)
- Performance degradation

**Current Code:**
```csharp
_httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(10)
};
```

**Recommendation:**
Use `IHttpClientFactory` or a static/shared HttpClient instance with proper lifetime management.

**Fix:**
```csharp
private static readonly HttpClient SharedHttpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(10)
};

public HttpMasterServerClient(Settings settings, ILogger? logger = null)
{
    _settings = settings;
    _logger = logger;
    // Use shared instance instead of creating new
}

public void Dispose()
{
    // Don't dispose shared client - it's static
    _disposed = true;
}
```

**Alternative (Better):**
Use dependency injection with `IHttpClientFactory`:
```csharp
public HttpMasterServerClient(Settings settings, IHttpClientFactory httpClientFactory, ILogger? logger = null)
{
    _settings = settings;
    _logger = logger;
    _httpClient = httpClientFactory.CreateClient();
    _httpClient.Timeout = TimeSpan.FromSeconds(10);
}
```

---

### 5. Missing Validation in AddressBookViewModel Edit Flow
**Severity:** HIGH  
**File:** `Q2Connect.Wpf/ViewModels/AddressBookViewModel.cs`  
**Lines:** 249-307

**Issue:**
The `EditSelectedEntryAsync` method updates the entry address from `EditAddressBookEntryWindow` without validating the edited address format. While it checks for duplicates, it doesn't validate the format.

**Vulnerability:**
An edited address could contain invalid format that passes through to `LauncherService`, potentially causing issues.

**Current Code:**
```csharp
var trimmedAddress = editedEntry.Address.Trim();
// Only checks for duplicates, not format
```

**Recommendation:**
Add the same `IsValidAddress()` validation check before updating the entry.

**Fix:**
```csharp
var trimmedAddress = editedEntry.Address.Trim();

// ADD: Format validation
if (!IsValidAddress(trimmedAddress))
{
    MessageBox.Show(
        "Invalid address format. Expected format: IP:Port (e.g., 192.168.1.1:27910)",
        "Invalid Address",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
    return;
}

// Check for duplicates (excluding the current entry)
// ... rest of code
```

---

### 6. Race Condition in Disposal Pattern
**Severity:** HIGH  
**File:** `Q2Connect.Wpf/ViewModels/MainViewModel.cs`  
**Lines:** 857-895

**Issue:**
The disposal pattern has potential race conditions between checking `_disposed` and accessing resources, especially with async operations that may continue after disposal begins.

**Vulnerability:**
- Access to disposed objects
- Null reference exceptions
- Resource leaks if disposal is not properly awaited

**Current Code:**
```csharp
public void Dispose()
{
    if (_disposed)
        return;
    
    _disposed = true;
    // ... disposal code that accesses fields
}
```

**Recommendation:**
1. Use thread-safe disposal pattern
2. Cancel async operations before disposal
3. Wait for async operations to complete
4. Use lock or Interlocked for disposal flag

**Fix:**
```csharp
private readonly object _disposalLock = new object();
private volatile bool _disposed;

public void Dispose()
{
    if (_disposed)
        return;
    
    lock (_disposalLock)
    {
        if (_disposed)
            return;
        _disposed = true;
    }
    
    // Cancel all async operations
    _disposalCancellation?.Cancel();
    _refreshCancellation?.Cancel();
    
    // Wait for operations to complete (with timeout)
    try
    {
        Task.WaitAll(
            _refreshTask ?? Task.CompletedTask,
            Task.Delay(1000)  // Timeout
        );
    }
    catch { }
    
    // Dispose resources
    _httpMasterServerClient?.Dispose();
    // ... rest of disposal
}
```

---

### 7. Missing Input Size Limits on Network Data
**Severity:** HIGH  
**File:** `Q2Connect.Core/Networking/GameServerProbe.cs`  
**Lines:** 96-108

**Issue:**
The server status response is parsed without size limits on the response string or parsed data structures (CVARs, players).

**Vulnerability:**
- Memory exhaustion from extremely large responses
- Performance degradation from parsing large amounts of data
- Potential DoS from malicious servers

**Recommendation:**
1. Limit maximum response size (e.g., 64KB for status response)
2. Limit maximum CVAR count
3. Limit maximum player count
4. Truncate or reject oversized responses

**Fix:**
```csharp
private const int MAX_STATUS_RESPONSE_SIZE = 64 * 1024; // 64KB
private const int MAX_CVARS = 256;
private const int MAX_PLAYERS = 128;

var payload = PacketHeader.RemoveOobHeader(data);

// ADD: Size check
if (payload.Length > MAX_STATUS_RESPONSE_SIZE)
{
    _logger?.LogWarning($"Server {endPoint} response too large: {payload.Length} bytes, truncating");
    payload = payload.Take(MAX_STATUS_RESPONSE_SIZE).ToArray();
}

var response = Encoding.ASCII.GetString(payload);

// In ParseStatusResponse, add limits:
private void ParseStatusResponse(string response, ServerEntry serverEntry)
{
    // ... existing code ...
    
    // ADD: Limit CVAR count
    foreach (Match match in cvarMatches.Take(MAX_CVARS))
    {
        // ... existing parsing ...
    }
    
    // ADD: Limit player count
    foreach (var line in playerLines.Take(MAX_PLAYERS))
    {
        // ... existing parsing ...
    }
}
```

---

## ADDITIONAL RECOMMENDATIONS

### Medium Priority Issues

1. **Path Validation:** Consider validating `Q2ExecutablePath` in Settings to prevent path traversal, though this is less critical since it comes from settings, not direct user input.

2. **Error Message Information Disclosure:** Ensure error messages don't leak sensitive information (file paths, internal state) to users in production builds.

3. **Logging Security:** Review log files to ensure sensitive data (passwords, tokens) are not logged.

4. **Async/Await Best Practices:** Ensure all async operations properly handle cancellation tokens and don't have fire-and-forget tasks without proper error handling.

---

## Testing Recommendations

1. **Fuzzing:** Test JSON deserialization with malformed input
2. **Load Testing:** Test with extremely large master server responses
3. **Penetration Testing:** Attempt command injection through address fields
4. **Memory Profiling:** Monitor for memory leaks during extended operation
5. **Concurrent Access Testing:** Test disposal and resource access under concurrent operations

---

## Priority Actions

**Immediate (Critical):**
1. Fix address validation in EditAddressBookEntryWindow (#1)
2. Add JSON deserialization limits (#2)
3. Add HTTP response size limits (#3)

**Short-term (High):**
4. Fix HttpClient resource management (#4)
5. Add validation in edit flow (#5)
6. Fix disposal race conditions (#6)
7. Add network input size limits (#7)

---

## Conclusion

The application has several critical security and stability issues that should be addressed promptly. The most urgent concerns are around input validation and resource limits, which could lead to security vulnerabilities or denial of service conditions.

