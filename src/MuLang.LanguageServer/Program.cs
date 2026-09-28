namespace MuLang.LanguageServer;

internal static class Program
{
    public static async Task<int> Main()
    {
        try
        {
            LanguageServer server = new (
                Console.OpenStandardInput(),
                Console.OpenStandardOutput()
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
