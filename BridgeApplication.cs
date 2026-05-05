using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RevitCodexBridge;

public sealed class BridgeApplication : IExternalApplication
{
    private static BridgeServer? _server;
    private static bool _assemblyResolverRegistered;

    public Result OnStartup(UIControlledApplication application)
    {
        RegisterAssemblyResolver();
        var handler = new BridgeExternalEventHandler();
        var externalEvent = ExternalEvent.Create(handler);
        _server = new BridgeServer(handler, externalEvent);
        _server.Start();
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        _server?.Dispose();
        _server = null;
        return Result.Succeeded;
    }

    private static void RegisterAssemblyResolver()
    {
        if (_assemblyResolverRegistered)
        {
            return;
        }

        var assemblyDirectory = Path.GetDirectoryName(typeof(BridgeApplication).Assembly.Location);
        if (string.IsNullOrWhiteSpace(assemblyDirectory))
        {
            return;
        }

        var loadedAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        Assembly? ResolveFromAddInDirectory(string? requestedName)
        {
            var assemblyName = string.IsNullOrWhiteSpace(requestedName)
                ? null
                : new AssemblyName(requestedName).Name;
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                return null;
            }

            var candidatePath = Path.Combine(assemblyDirectory, assemblyName + ".dll");
            if (!File.Exists(candidatePath))
            {
                return null;
            }

            if (loadedAssemblies.TryGetValue(candidatePath, out var loadedAssembly))
            {
                return loadedAssembly;
            }

            var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
            if (alreadyLoaded is not null)
            {
                loadedAssemblies[candidatePath] = alreadyLoaded;
                return alreadyLoaded;
            }

            loadedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(candidatePath);
            loadedAssemblies[candidatePath] = loadedAssembly;
            return loadedAssembly;
        }

        AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
        {
            return ResolveFromAddInDirectory(args.Name);
        };

        AssemblyLoadContext.Default.Resolving += (_, assemblyName) =>
        {
            return ResolveFromAddInDirectory(assemblyName.FullName);
        };

        ResolveFromAddInDirectory("System.Collections.Immutable");
        ResolveFromAddInDirectory("System.Reflection.Metadata");
        ResolveFromAddInDirectory("Microsoft.CodeAnalysis");
        ResolveFromAddInDirectory("Microsoft.CodeAnalysis.CSharp");

        _assemblyResolverRegistered = true;
    }
}

internal sealed class BridgeServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    private readonly CancellationTokenSource _cts = new();
    private readonly BridgeExternalEventHandler _handler;
    private readonly TcpListener _listener = new(IPAddress.Loopback, 17825);
    private readonly ExternalEvent _externalEvent;
    private Task? _listenTask;

    public BridgeServer(BridgeExternalEventHandler handler, ExternalEvent externalEvent)
    {
        _handler = handler;
        _externalEvent = externalEvent;
    }

    public void Start()
    {
        _listener.Start();
        _listenTask = Task.Run(ListenAsync);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        _cts.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client), _cts.Token);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        await using var stream = client.GetStream();
        using var _ = client;

        try
        {
            var request = await ReadHttpRequestAsync(stream, _cts.Token).ConfigureAwait(false);

            if (request.Method == "OPTIONS")
            {
                await WriteJsonAsync(stream, new { ok = true }).ConfigureAwait(false);
                return;
            }

            if (request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
            {
                await WriteJsonAsync(stream, new
                {
                    ok = true,
                    bridge = "RevitCodexBridge",
                    api = new[] { "/health", "/command" }
                }).ConfigureAwait(false);
                return;
            }

            if (!request.Path.Equals("/command", StringComparison.OrdinalIgnoreCase) ||
                request.Method != "POST")
            {
                await WriteJsonAsync(stream, new { ok = false, error = "Use GET /health or POST /command." }, 404)
                    .ConfigureAwait(false);
                return;
            }

            var bridgeRequest = JsonSerializer.Deserialize<BridgeRequest>(request.Body, JsonOptions) ?? new BridgeRequest();
            var workItem = new BridgeWorkItem(bridgeRequest);
            _handler.Enqueue(workItem);

            var raiseResult = _externalEvent.Raise();
            if (raiseResult != ExternalEventRequest.Accepted)
            {
                await WriteJsonAsync(stream, new { ok = false, error = $"ExternalEvent was not accepted: {raiseResult}" }, 503)
                    .ConfigureAwait(false);
                return;
            }

            var response = await workItem.Task.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            await WriteJsonAsync(stream, response, response.Ok ? 200 : 400).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            await WriteJsonAsync(stream, new { ok = false, error = "Timed out waiting for Revit API context." }, 504)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await WriteJsonAsync(stream, new { ok = false, error = ex.Message }, 500).ConfigureAwait(false);
        }
    }

    private static async Task<HttpRequest> ReadHttpRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        var count = 0;
        var headerEnd = -1;

        while (headerEnd < 0 && count < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(count, buffer.Length - count), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            count += read;
            headerEnd = IndexOfHeaderEnd(buffer, count);
        }

        if (headerEnd < 0)
        {
            throw new InvalidOperationException("Invalid HTTP request.");
        }

        var headerText = Encoding.ASCII.GetString(buffer, 0, headerEnd);
        var lines = headerText.Split("\r\n", StringSplitOptions.None);
        var requestLine = lines[0].Split(' ', 3);
        if (requestLine.Length < 2)
        {
            throw new InvalidOperationException("Invalid HTTP request line.");
        }

        var contentLength = 0;
        foreach (var line in lines.Skip(1))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            if (line[..separator].Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            {
                _ = int.TryParse(line[(separator + 1)..].Trim(), out contentLength);
            }
        }

        var bodyStart = headerEnd + 4;
        var receivedBodyBytes = count - bodyStart;
        var body = new byte[contentLength];
        if (receivedBodyBytes > 0)
        {
            Buffer.BlockCopy(buffer, bodyStart, body, 0, Math.Min(receivedBodyBytes, contentLength));
        }

        while (receivedBodyBytes < contentLength)
        {
            var read = await stream.ReadAsync(body.AsMemory(receivedBodyBytes, contentLength - receivedBodyBytes), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            receivedBodyBytes += read;
        }

        return new HttpRequest(requestLine[0], requestLine[1].Split('?', 2)[0], Encoding.UTF8.GetString(body));
    }

    private static int IndexOfHeaderEnd(byte[] buffer, int count)
    {
        for (var i = 3; i < count; i++)
        {
            if (buffer[i - 3] == '\r' && buffer[i - 2] == '\n' && buffer[i - 1] == '\r' && buffer[i] == '\n')
            {
                return i - 3;
            }
        }

        return -1;
    }

    private static async Task WriteJsonAsync(NetworkStream stream, object value, int statusCode = 200)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions));
        var reason = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            404 => "Not Found",
            500 => "Internal Server Error",
            503 => "Service Unavailable",
            504 => "Gateway Timeout",
            _ => "OK"
        };
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {statusCode} {reason}\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Access-Control-Allow-Origin: http://127.0.0.1\r\n" +
            "Access-Control-Allow-Methods: GET,POST,OPTIONS\r\n" +
            "Access-Control-Allow-Headers: Content-Type\r\n" +
            "Connection: close\r\n\r\n");

        await stream.WriteAsync(header).ConfigureAwait(false);
        await stream.WriteAsync(body).ConfigureAwait(false);
    }
}

internal sealed record HttpRequest(string Method, string Path, string Body);

internal static class CSharpScriptRunner
{
    private static int _scriptCounter;

    public static object? Run(UIApplication app, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Request field 'code' is required.");
        }

        var assemblyName = $"RevitCodexBridge.Script.{Interlocked.Increment(ref _scriptCounter)}";
        var syntaxTree = CSharpSyntaxTree.ParseText(WrapSource(code), new CSharpParseOptions(LanguageVersion.Latest));
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { syntaxTree },
            GetReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

        using var assemblyStream = new MemoryStream();
        var emitResult = compilation.Emit(assemblyStream);
        if (!emitResult.Success)
        {
            var diagnostics = emitResult.Diagnostics
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => diagnostic.ToString())
                .ToArray();
            throw new InvalidOperationException(string.Join(Environment.NewLine, diagnostics));
        }

        assemblyStream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(assemblyStream);
        var type = assembly.GetType("RevitCodexBridge.DynamicScript.ScriptEntry", throwOnError: true)!;
        var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException("ScriptEntry.Run was not generated.");

        try
        {
            return method.Invoke(null, new object[] { app });
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static string WrapSource(string body)
    {
        return $$"""
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitCodexBridge.DynamicScript;

public static class ScriptEntry
{
    public static object? Run(UIApplication app)
    {
{{body}}
    }
}
""";
    }

    private static MetadataReference[] GetReferences()
    {
        var references = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            AddReference(references, assembly);
        }

        AddReference(references, typeof(object).Assembly);
        AddReference(references, typeof(Enumerable).Assembly);
        AddReference(references, typeof(UIApplication).Assembly);
        AddReference(references, typeof(Element).Assembly);
        AddReference(references, typeof(Dictionary<,>).Assembly);

        return references.Values.ToArray();
    }

    private static void AddReference(Dictionary<string, MetadataReference> references, Assembly assembly)
    {
        if (assembly.IsDynamic || string.IsNullOrWhiteSpace(assembly.Location) || references.ContainsKey(assembly.Location))
        {
            return;
        }

        references[assembly.Location] = MetadataReference.CreateFromFile(assembly.Location);
    }
}

internal sealed class BridgeExternalEventHandler : IExternalEventHandler
{
    private readonly ConcurrentQueue<BridgeWorkItem> _queue = new();

    public string GetName() => "Codex Revit Bridge";

    public void Enqueue(BridgeWorkItem workItem) => _queue.Enqueue(workItem);

    public void Execute(UIApplication app)
    {
        while (_queue.TryDequeue(out var item))
        {
            try
            {
                item.SetResult(ExecuteRequest(app, item.Request));
            }
            catch (Exception ex)
            {
                item.SetResult(BridgeResponse.Fail(ex.Message));
            }
        }
    }

    private static BridgeResponse ExecuteRequest(UIApplication app, BridgeRequest request)
    {
        return request.Command?.Trim().ToLowerInvariant() switch
        {
            "app-info" => BridgeResponse.Success(GetAppInfo(app)),
            "doc-info" => BridgeResponse.Success(GetDocumentInfo(app)),
            "selection" => BridgeResponse.Success(GetSelection(app)),
            "collect" => BridgeResponse.Success(CollectElements(app, request.Category, request.Limit)),
            "delete-line-patterns" => BridgeResponse.Success(DeleteLinePatterns(app, request.Prefix ?? "IMPORT", request.DryRun ?? true)),
            "run-csharp" => BridgeResponse.Success(CSharpScriptRunner.Run(app, request.Code)),
            _ => BridgeResponse.Fail("Unknown command. Supported commands: app-info, doc-info, selection, collect, delete-line-patterns, run-csharp.")
        };
    }

    private static object GetAppInfo(UIApplication app)
    {
        var application = app.Application;
        return new
        {
            application.VersionName,
            application.VersionNumber,
            application.VersionBuild,
            activeAddInId = app.ActiveAddInId?.GetGUID().ToString()
        };
    }

    private static object GetDocumentInfo(UIApplication app)
    {
        var document = app.ActiveUIDocument?.Document;
        if (document is null)
        {
            return new { hasDocument = false };
        }

        return new
        {
            hasDocument = true,
            title = document.Title,
            pathName = document.PathName,
            isFamilyDocument = document.IsFamilyDocument,
            isModified = document.IsModified,
            elementCount = new FilteredElementCollector(document).WhereElementIsNotElementType().GetElementCount()
        };
    }

    private static object GetSelection(UIApplication app)
    {
        var uiDocument = app.ActiveUIDocument;
        var document = uiDocument?.Document;
        if (uiDocument is null || document is null)
        {
            return new { hasDocument = false, elements = Array.Empty<object>() };
        }

        var elements = uiDocument.Selection.GetElementIds()
            .Select(id => ToElementDto(document.GetElement(id)))
            .Where(dto => dto is not null)
            .ToArray();

        return new { hasDocument = true, count = elements.Length, elements };
    }

    private static object CollectElements(UIApplication app, string? categoryName, int? requestedLimit)
    {
        var document = app.ActiveUIDocument?.Document;
        if (document is null)
        {
            return new { hasDocument = false, elements = Array.Empty<object>() };
        }

        var limit = Math.Clamp(requestedLimit ?? 25, 1, 200);
        IEnumerable<Element> elements = new FilteredElementCollector(document).WhereElementIsNotElementType();

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            elements = elements.Where(element =>
                element.Category?.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase) == true);
        }

        var result = elements
            .Take(limit)
            .Select(ToElementDto)
            .Where(dto => dto is not null)
            .ToArray();

        return new { hasDocument = true, count = result.Length, elements = result };
    }

    private static object DeleteLinePatterns(UIApplication app, string prefix, bool dryRun)
    {
        var document = app.ActiveUIDocument?.Document;
        if (document is null)
        {
            return new { hasDocument = false, deletedCount = 0, patterns = Array.Empty<object>() };
        }

        var linePatterns = new FilteredElementCollector(document)
            .OfClass(typeof(LinePatternElement))
            .Cast<LinePatternElement>()
            .Where(pattern => pattern.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pattern => pattern.Name)
            .ToArray();

        var patternDtos = linePatterns
            .Select(pattern => new
            {
                id = pattern.Id.Value,
                name = pattern.Name
            })
            .ToArray();

        if (dryRun || linePatterns.Length == 0)
        {
            return new
            {
                hasDocument = true,
                dryRun = true,
                prefix,
                matchCount = patternDtos.Length,
                deletedCount = 0,
                patterns = patternDtos
            };
        }

        var ids = linePatterns.Select(pattern => pattern.Id).ToList();
        int deletedCount;
        using (var transaction = new Transaction(document, $"Delete line patterns starting with {prefix}"))
        {
            transaction.Start();
            deletedCount = document.Delete(ids).Count;
            transaction.Commit();
        }

        return new
        {
            hasDocument = true,
            dryRun = false,
            prefix,
            matchCount = patternDtos.Length,
            deletedCount,
            patterns = patternDtos
        };
    }

    private static object? ToElementDto(Element? element)
    {
        if (element is null)
        {
            return null;
        }

        return new
        {
            id = element.Id.Value,
            uniqueId = element.UniqueId,
            name = element.Name,
            category = element.Category?.Name,
            type = element.GetType().FullName
        };
    }
}

internal sealed class BridgeWorkItem
{
    private readonly TaskCompletionSource<BridgeResponse> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BridgeWorkItem(BridgeRequest request)
    {
        Request = request;
    }

    public BridgeRequest Request { get; }

    public Task<BridgeResponse> Task => _completion.Task;

    public void SetResult(BridgeResponse response) => _completion.TrySetResult(response);
}

internal sealed class BridgeRequest
{
    public string? Command { get; set; }

    public string? Category { get; set; }

    public int? Limit { get; set; }

    public string? Prefix { get; set; }

    public bool? DryRun { get; set; }

    public string? Code { get; set; }
}

internal sealed class BridgeResponse
{
    public bool Ok { get; init; }

    public object? Data { get; init; }

    public string? Error { get; init; }

    public static BridgeResponse Success(object data) => new() { Ok = true, Data = data };

    public static BridgeResponse Fail(string error) => new() { Ok = false, Error = error };
}
