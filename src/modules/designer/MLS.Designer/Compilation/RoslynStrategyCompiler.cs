using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Options;
using MLS.Designer.Configuration;

namespace MLS.Designer.Compilation;

/// <summary>
/// Roslyn-based strategy compiler that compiles user-authored C# code to an in-memory
/// assembly and optionally publishes the result to IPFS via the Kubo HTTP API.
/// </summary>
/// <remarks>
/// <para>
/// Compilation is fast (&lt; 2 s) because it runs entirely in-process via the
/// <c>Microsoft.CodeAnalysis.CSharp</c> NuGet package — no external toolchain needed.
/// </para>
/// <para>
/// Security policy enforcement happens in two stages:
/// <list type="number">
///   <item>
///     Source-level scan: forbidden namespace identifiers are detected via Roslyn
///     <see cref="SyntaxTree"/> walking before any IL emission begins.
///   </item>
///   <item>
///     Reference restriction: only a minimal, curated set of BCL + MLS.Core assemblies
///     is provided to the compiler — MLS.Designer internals are intentionally excluded.
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class RoslynStrategyCompiler(
    IHttpClientFactory httpClientFactory,
    IOptions<DesignerOptions> options,
    ILogger<RoslynStrategyCompiler> logger) : IStrategyCompiler
{
    // ── Constants ─────────────────────────────────────────────────────────────────

    private const string AssemblyName   = "MLS.UserBlock";
    private const string IpfsHttpClient = "ipfs";

    /// <summary>
    /// Namespace prefixes that the user is NOT allowed to reference.
    /// Matched against the fully-qualified identifier text in the syntax tree.
    /// </summary>
    private static readonly string[] ForbiddenNamespaces =
    [
        "System.IO",
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.WebClient",
        "System.Reflection",
        "System.Runtime.InteropServices",
        "System.Diagnostics.Process",
        "Microsoft.Win32",
        "System.Security.Cryptography",
    ];

    // ── References built lazily (thread-safe) ─────────────────────────────────────

    private static readonly Lazy<IReadOnlyList<MetadataReference>> _refs = new(BuildReferences);

    // ── IStrategyCompiler ─────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<CompilationResult> CompileAsync(string csharpSource, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        var (securityOk, securityErrors) = CheckForbiddenNamespaces(csharpSource);
        if (!securityOk)
        {
            sw.Stop();
            logger.LogWarning("Compilation rejected: forbidden API usage detected ({Count} violations)", securityErrors.Count);
            return new CompilationResult(false, null, securityErrors, null, sw.Elapsed);
        }

        var (bytes, diagnostics) = await Task.Run(() => CompileCore(csharpSource), ct).ConfigureAwait(false);
        sw.Stop();

        var success = bytes is not null;
        if (success)
            logger.LogInformation("Compilation succeeded in {ElapsedMs:F0} ms, assembly {Size} bytes",
                sw.Elapsed.TotalMilliseconds, bytes!.Length);
        else
            logger.LogWarning("Compilation failed in {ElapsedMs:F0} ms ({Count} errors)",
                sw.Elapsed.TotalMilliseconds, diagnostics.Count);

        return new CompilationResult(success, bytes, diagnostics, null, sw.Elapsed);
    }

    /// <inheritdoc/>
    public async Task<CompilationResult> CompileAndUploadAsync(string csharpSource, CancellationToken ct)
    {
        var result = await CompileAsync(csharpSource, ct).ConfigureAwait(false);
        if (!result.Success || result.AssemblyBytes is null)
            return result;

        string? cid = null;
        try
        {
            cid = await UploadAssemblyAsync(result.AssemblyBytes, ct).ConfigureAwait(false);
            logger.LogInformation("Assembly uploaded to IPFS, CID={Cid}", cid);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "IPFS upload failed; returning result without CID");
        }

        return result with { IpfsCid = cid };
    }

    /// <inheritdoc/>
    public async Task<string> UploadAssemblyAsync(byte[] assemblyBytes, CancellationToken ct)
    {
        var ipfsApiUrl = options.Value.IpfsApiUrl;
        if (string.IsNullOrWhiteSpace(ipfsApiUrl))
            throw new InvalidOperationException("Designer:IpfsApiUrl is not configured.");

        using var client  = httpClientFactory.CreateClient(IpfsHttpClient);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(assemblyBytes), "file", "userblock.dll");

        var uri      = new Uri(new Uri(ipfsApiUrl), "/api/v0/add?pin=true&cid-version=1");
        var response = await client.PostAsync(uri, content, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("Hash").GetString()
               ?? throw new InvalidOperationException("IPFS /api/v0/add response missing 'Hash' field.");
    }

    // ── Security scan ─────────────────────────────────────────────────────────────

    private static (bool ok, IReadOnlyList<string> errors) CheckForbiddenNamespaces(string source)
    {
        var tree  = CSharpSyntaxTree.ParseText(source);
        var root  = tree.GetRoot();
        var errors = new List<string>();

        // Walk every IdentifierNameSyntax and check if it matches a forbidden prefix
        foreach (var node in root.DescendantNodes())
        {
            var text = node switch
            {
                Microsoft.CodeAnalysis.CSharp.Syntax.QualifiedNameSyntax qn  => qn.ToString(),
                Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax ma => ma.ToString(),
                _ => null,
            };

            if (text is null) continue;

            foreach (var forbidden in ForbiddenNamespaces)
            {
                // Use exact-prefix matching with a separator guard to avoid false positives:
                // "System.IOExtensions" must NOT be rejected by the "System.IO" rule.
                if (text.StartsWith(forbidden, StringComparison.Ordinal) &&
                    (text.Length == forbidden.Length || text[forbidden.Length] == '.'))
                {
                    var location = node.GetLocation().GetLineSpan();
                    errors.Add(
                        $"[MLS-SEC] Forbidden API '{text}' at line {location.StartLinePosition.Line + 1}: " +
                        $"access to '{forbidden}' is not permitted in user blocks.");
                    break;
                }
            }
        }

        return (errors.Count == 0, errors);
    }

    // ── Roslyn compilation core ───────────────────────────────────────────────────

    private static (byte[]? bytes, IReadOnlyList<string> diagnostics) CompileCore(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var tree         = CSharpSyntaxTree.ParseText(SourceText.From(source), parseOptions);

        var compilation = CSharpCompilation.Create(
            assemblyName: AssemblyName,
            syntaxTrees:  [tree],
            references:   _refs.Value,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel:  OptimizationLevel.Release,
                allowUnsafe:        false,
                nullableContextOptions: NullableContextOptions.Enable));

        using var ms = new MemoryStream();
        var result   = compilation.Emit(ms);

        var messages = result.Diagnostics
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(d => d.ToString())
            .ToList();

        if (!result.Success)
            return (null, messages);

        var bytes = ms.ToArray();

        // Validate that the assembly contains at least one public class with a known base type
        // (BlockBase or IBlockElement) — checked via the semantic model for accuracy.
        var hasBlockType = false;
        foreach (var syntaxTree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var classNodes    = syntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>();

            foreach (var cls in classNodes)
            {
                if (!cls.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))) continue;

                var symbol = semanticModel.GetDeclaredSymbol(cls);
                if (symbol is null) continue;

                // Walk the base type chain to check for BlockBase or IBlockElement
                var baseType = symbol.BaseType;
                while (baseType is not null)
                {
                    if (baseType.Name is "BlockBase" or "IBlockElement")
                    {
                        hasBlockType = true;
                        break;
                    }
                    baseType = baseType.BaseType;
                }

                if (!hasBlockType)
                {
                    // Also check implemented interfaces
                    hasBlockType = symbol.AllInterfaces.Any(i => i.Name is "IBlockElement");
                }

                if (hasBlockType) break;
            }
            if (hasBlockType) break;
        }

        if (!hasBlockType)
        {
            messages.Add("[MLS-VAL] Compiled assembly must contain at least one public class " +
                         "extending BlockBase (or implementing IBlockElement).");
            return (null, messages);
        }

        return (bytes, messages);
    }

    // ── Reference assembly list ────────────────────────────────────────────────────

    /// <summary>
    /// Builds the minimal set of metadata references exposed to user code.
    /// Uses the .NET runtime directory (alongside System.Private.CoreLib) plus MLS assemblies.
    /// </summary>
    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        // .NET runtime directory: where System.Runtime.dll, System.Threading.dll etc. live
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        // Explicit BCL assemblies required by user block code
        var runtimeAssemblies = new[]
        {
            "System.Private.CoreLib.dll",
            "System.Runtime.dll",
            "System.Collections.dll",
            "System.Linq.dll",
            "System.Text.Json.dll",
            "System.Memory.dll",
            "System.Threading.dll",
            "System.Threading.Tasks.dll",
            "System.Runtime.Extensions.dll",
            "System.ObjectModel.dll",
        };

        var refs = new List<MetadataReference>();

        foreach (var name in runtimeAssemblies)
        {
            var path = Path.Combine(runtimeDir, name);
            if (File.Exists(path))
                refs.Add(MetadataReference.CreateFromFile(path));
        }

        // MLS.Core — IBlockElement, BlockSignal, BlockSocketType, BlockParameter
        refs.Add(MetadataReference.CreateFromFile(typeof(MLS.Core.Designer.IBlockElement).Assembly.Location));

        // MLS.Designer — BlockBase + BlockSocket factory methods
        refs.Add(MetadataReference.CreateFromFile(typeof(MLS.Designer.Blocks.BlockBase).Assembly.Location));

        return refs.AsReadOnly();
    }
}
