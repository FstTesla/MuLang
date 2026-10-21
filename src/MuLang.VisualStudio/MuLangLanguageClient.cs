using Microsoft.VisualStudio;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using StreamJsonRpc;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace MuLang.VisualStudio;

[ContentType(MuLangContentType.Name)]
[Export(typeof(ILanguageClient))]
[Export(typeof(IMuLangQuickInfoService))]
[PartCreationPolicy(CreationPolicy.Shared)]
internal sealed class MuLangLanguageClient
    : ILanguageClient,
        ILanguageClientCustomMessage2,
        IMuLangQuickInfoService
{
    private JsonRpc? customMessageRpc;

    [Import]
    internal SVsServiceProvider ServiceProvider { get; set; } = null!;

    public string Name => "MuLang Language Server";

    public IEnumerable<string> ConfigurationSections => [ ];

    public object InitializationOptions => new ();

    public IEnumerable<string> FilesToWatch =>
        [ "**/mulang.config.json" ];

    public bool ShowNotificationOnInitializeFailed => true;

    public object? CustomMessageTarget => null;

    public object? MiddleLayer =>
        new MuLangLanguageClientMiddleLayer(ServiceProvider);

    public event AsyncEventHandler<EventArgs>? StartAsync;

    public event AsyncEventHandler<EventArgs>? StopAsync
    {
        add { }
        remove { }
    }

    public async Task<Connection?> ActivateAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Volatile.Write(ref customMessageRpc, null);
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(token);
        string extensionDirectory = Path.GetDirectoryName(
            Assembly.GetExecutingAssembly().Location
        )!;
        string serverPath = Path.Combine(
            extensionDirectory,
            "LanguageServer",
            "MuLang.LanguageServer.exe"
        );

        if (!File.Exists(serverPath))
        {
            throw new FileNotFoundException(
                "The bundled MuLang language server was not found.",
                serverPath
            );
        }

        string? solutionRoot = GetSolutionRoot();
        ProcessStartInfo startInfo = new ()
        {
            FileName = serverPath,
            Arguments = solutionRoot is null
                ? "--visual-studio"
                : $"--visual-studio --workspace-root={solutionRoot}",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        Process process = new ()
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException(
                "The MuLang language server could not be started."
            );
        }

        return new Connection(
            process.StandardOutput.BaseStream,
            process.StandardInput.BaseStream
        );
    }

    public Task AttachForCustomMessageAsync(JsonRpc rpc)
    {
        if (rpc is null)
        {
            throw new ArgumentNullException(nameof(rpc));
        }

        Volatile.Write(ref customMessageRpc, rpc);

        return Task.CompletedTask;
    }

    public Task<MuLangQuickInfoResponse?> GetQuickInfoAsync(
        string uri,
        int line,
        int character,
        CancellationToken cancellationToken
    )
    {
        JsonRpc? rpc = Volatile.Read(ref customMessageRpc);

        if (rpc is null)
        {
            return Task.FromResult<MuLangQuickInfoResponse?>(null);
        }

        return rpc.InvokeWithParameterObjectAsync<MuLangQuickInfoResponse?>(
            "mulang/quickInfo",
            new
            {
                textDocument = new { uri },
                position = new { line, character },
            },
            cancellationToken
        );
    }

    private string? GetSolutionRoot()
    {
        ThreadHelper.ThrowIfNotOnUIThread();

        if (
            ServiceProvider.GetService(typeof(SVsSolution)) is
            not IVsSolution solution
        )
        {
            return null;
        }

        int result = solution.GetSolutionInfo(
            out string solutionDirectory,
            out _,
            out _
        );

        if (
            ErrorHandler.Failed(result) ||
            string.IsNullOrWhiteSpace(solutionDirectory)
        )
        {
            return null;
        }

        string root = Path.GetFullPath(solutionDirectory);

        if (
            !root.EndsWith(
                Path.DirectorySeparatorChar.ToString(),
                StringComparison.Ordinal
            ) &&
            !root.EndsWith(
                Path.AltDirectorySeparatorChar.ToString(),
                StringComparison.Ordinal
            )
        )
        {
            root = $"{root}{Path.DirectorySeparatorChar}";
        }

        return new Uri(root).AbsoluteUri;
    }

    public async Task OnLoadedAsync()
    {
        AsyncEventHandler<EventArgs>? startAsync = StartAsync;

        if (startAsync is not null)
        {
            await startAsync.InvokeAsync(this, EventArgs.Empty);
        }
    }

    public Task<InitializationFailureContext?> OnServerInitializeFailedAsync(
        ILanguageClientInitializationInfo initializationInfo
    )
    {
        return Task.FromResult<InitializationFailureContext?>(null);
    }

    public Task OnServerInitializedAsync()
    {
        return Task.CompletedTask;
    }
}
