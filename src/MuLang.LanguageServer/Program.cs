namespace MuLang.LanguageServer;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            bool useVisualStudioClassifications = false;

            foreach (string argument in args)
            {
                if (argument == "--visual-studio")
                {
                    useVisualStudioClassifications = true;
                    continue;
                }

                throw new ArgumentException(
                    $"Unsupported command-line argument '{argument}'.",
                    nameof(args)
                );
            }

            LanguageServer server = new (
                Console.OpenStandardInput(),
                Console.OpenStandardOutput(),
                useVisualStudioClassifications
            );

            return await server.RunAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            await Console.Error.WriteLineAsync(exception.ToString());

            return 1;
        }
    }
}
