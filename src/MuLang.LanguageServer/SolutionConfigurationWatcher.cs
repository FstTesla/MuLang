using System.Threading.Channels;

namespace MuLang.LanguageServer;

internal sealed class SolutionConfigurationWatcher : IAsyncDisposable
{
    private readonly Channel<string> changes = Channel.CreateUnbounded<string>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        }
    );

    private readonly CancellationTokenSource cancellation = new ();
    private readonly Func<
        IReadOnlyCollection<string>,
        CancellationToken,
        Task
    > processChanges;

    private readonly Task processingTask;
    private readonly FileSystemWatcher watcher;

    public SolutionConfigurationWatcher(
        string root,
        Func<IReadOnlyCollection<string>, CancellationToken, Task> processChanges
    )
    {
        this.processChanges = processChanges;
        watcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter =
                NotifyFilters.CreationTime |
                NotifyFilters.FileName |
                NotifyFilters.LastWrite |
                NotifyFilters.Size,
        };
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Deleted += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.EnableRaisingEvents = true;
        processingTask = ProcessChangesAsync(cancellation.Token);
    }

    public async ValueTask DisposeAsync()
    {
        watcher.EnableRaisingEvents = false;
        watcher.Changed -= OnChanged;
        watcher.Created -= OnChanged;
        watcher.Deleted -= OnChanged;
        watcher.Renamed -= OnRenamed;
        watcher.Dispose();
        cancellation.Cancel();
        changes.Writer.TryComplete();

        try
        {
            await processingTask;
        }
        catch (OperationCanceledException) when (
            cancellation.IsCancellationRequested
        )
        {
        }

        cancellation.Dispose();
    }

    private void OnChanged(object sender, FileSystemEventArgs eventArguments)
    {
        Queue(eventArguments.FullPath);
    }

    private void OnRenamed(object sender, RenamedEventArgs eventArguments)
    {
        Queue(eventArguments.OldFullPath);
        Queue(eventArguments.FullPath);
    }

    private void Queue(string file)
    {
        if (
            string.Equals(
                Path.GetFileName(file),
                WorkspaceConfigurationFile.FileName,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal
            )
        )
        {
            changes.Writer.TryWrite(file);
        }
    }

    private async Task ProcessChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (
                await changes.Reader.WaitToReadAsync(cancellationToken)
            )
            {
                ISet<string> files = new HashSet<string>(
                    OperatingSystem.IsWindows()
                        ? StringComparer.OrdinalIgnoreCase
                        : StringComparer.Ordinal
                );

                ReadPendingFiles(files);
                await Task.Delay(150, cancellationToken);
                ReadPendingFiles(files);
                await processChanges([ .. files ], cancellationToken);
            }
        }
        catch (OperationCanceledException) when (
            cancellationToken.IsCancellationRequested
        )
        {
        }
    }

    private void ReadPendingFiles(ISet<string> files)
    {
        while (changes.Reader.TryRead(out string? file))
        {
            files.Add(file);
        }
    }
}
