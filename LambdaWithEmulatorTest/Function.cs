using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.SystemTextJson;
using System.Text;

namespace LambdaWithEmulatorTest;

public class Function
{
    private static async Task Main(string[] args)
    {
        //using var listener = new ConsoleListener();
        Func<Stream, ILambdaContext, Stream> handler = FunctionHandler;
        await LambdaBootstrapBuilder.Create(handler)
            .Build()
            .RunAsync();
    }

    public static Stream FunctionHandler(Stream stream, ILambdaContext context)
    {
        context.Logger.LogInformation("IsServerGC: {serverGC}", System.Runtime.GCSettings.IsServerGC);
        context.Logger.LogInformation("Function invoked with stream of length: " + stream.Length);
        var responseStream = new MemoryStream(UTF8Encoding.UTF8.GetBytes("Hello World"));
        return responseStream;
    }
}