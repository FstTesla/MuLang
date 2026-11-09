using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace MuLang.VisualStudio;

internal sealed class MuLangLanguageClientMiddleLayer
    : ILanguageClientMiddleLayer2<JToken>
{
    private readonly IServiceProvider serviceProvider;

    public MuLangLanguageClientMiddleLayer(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    public bool CanHandle(string methodName)
    {
        return methodName == "textDocument/references";
    }

    public Task HandleNotificationAsync(
        string methodName,
        JToken methodParam,
        Func<JToken, Task> sendNotification
    )
    {
        return sendNotification(methodParam);
    }

    [SuppressMessage(
        "Usage",
        "VSTHRD103",
        Justification = "The middle-layer contract requires synchronous invocation of sendRequest."
    )]
    public Task<JToken?> HandleRequestAsync(
        string methodName,
        JToken methodParam,
        Func<JToken, Task<JToken?>> sendRequest
    )
    {
        if (CanHandle(methodName))
        {
            if (
                Uri.TryCreate(
                    methodParam["textDocument"]?["uri"]?.Value<string>(),
                    UriKind.Absolute,
                    out Uri? documentUri
                ) &&
                documentUri.AbsolutePath.EndsWith(
                    ".muir",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return sendRequest(methodParam);
            }

            IReadOnlyCollection<string>? documentUris =
                ThreadHelper.JoinableTaskFactory.Run(
                    async () =>
                    {
                        await ThreadHelper.JoinableTaskFactory
                            .SwitchToMainThreadAsync();

                        return MuLangSolutionSourceService.GetDocumentUris(
                            serviceProvider
                        );
                    }
                );

            if (documentUris is not null)
            {
                JObject parameters = (JObject)methodParam.DeepClone();
                parameters["_muLang_solutionDocumentUris"] =
                    new JArray(documentUris);
                methodParam = parameters;
            }
        }

        return sendRequest(methodParam);
    }
}
