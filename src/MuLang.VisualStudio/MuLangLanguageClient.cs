using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Reflection;

namespace MuLang.VisualStudio;

[ContentType(MuLangContentType.Name)]
[Export(typeof(ILanguageClient))]
internal sealed class MuLangLanguageClient : ILanguageClient
{
    public string Name => "MuLang Language Server";

    public IEnumerable<string> ConfigurationSections => [ ];

    public object InitializationOptions => new ();

    public IEnumerable<string> FilesToWatch => [ ];

    public bool ShowNotificationOnInitializeFailed => true;

    public event AsyncEventHandler<EventArgs>? StartAsync;

    public event AsyncEventHandler<EventArgs>? StopAsync
    {
        add { }
        remove { }
    }

    public Task<Connection?> ActivateAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
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

        ProcessStartInfo startInfo = new ()
        {
            FileName = serverPath,
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

        return Task.FromResult<Connection?>(
            new Connection(
                process.StandardOutput.BaseStream,
                process.StandardInput.BaseStream
            )
        );
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
