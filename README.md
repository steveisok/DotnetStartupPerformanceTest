This repo is for captures metrics and reproducing cold start regressions with .NET 10.

When testing .NET 10 in AWS Lambda I'm seeing a 5 to 10 percent regression compared with .NET 8 for a simple hello world Lambda function. The test I have done is deploying Lambda function as a self contained console application for both .NET 10 and 8. That gives a closer apples to apples comparison since there is not currently a .NET 10 managed runtime and it avoids the caching layers used on Lambda which makes it harder to predict timing.

## Metrics

In the Lambda function I have added the following `EventListener` to capture events coming out of the runtime. In the Lambda environment I don't have the ability to attach profilers to the process.

```csharp
public class ConsoleListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // Attach to interesting sources
        if (eventSource.Name == "Microsoft-Windows-DotNETRuntime")
        {
            EnableEvents(eventSource, EventLevel.Informational,
                (EventKeywords)(-1)); // All keywords
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        string value = string.Empty;
        if (e.Payload != null)
        {
            value = string.Join(", ", e.Payload);
        }
        Console.WriteLine($"{e.EventName}: {value}");
    }
}
```

For reference this is the Lambda function console application
```csharp
public class Function
{
    private static async Task Main(string[] args)
    {
        using var listener = new ConsoleListener();

        Func<Stream, ILambdaContext, Stream> handler = FunctionHandler;
        await LambdaBootstrapBuilder.Create(handler)
            .Build()
            .RunAsync();
    }

    public static Stream FunctionHandler(Stream stream, ILambdaContext context)
    {
        context.Logger.LogInformation("Function invoked with stream of length: " + stream.Length);
        var responseStream = new MemoryStream(UTF8Encoding.UTF8.GetBytes("Hello World"));
        return responseStream;
    }
}
```

The file [LambdaCaptures/dotnet10-metrics.txt](./LambdaCaptures/dotnet10-metrics.txt) is a run for .NET 10 and [LambdaCaptures/dotnet8-metrics.txt](./LambdaCaptures/dotnet8-metrics.txt).

## Attempt to reproduce the issue in a non-Lambda environment

In this repo I have the [DotnetStartupPerformanceTest](./DotnetStartupPerformanceTest) solution. It is a simple console application that opens a socket then a client to connect to the socket and send a simple message and then shutdown. I just want some startup code to run but still have the program be sure. I'm not sure the following results are correlated with what I'm seeing in Lambda but it might be helpful for testing outside of Lambda.

Then to test build and run the container. Run the following commands in the project directory to build the containers.
```
docker build -f Dockerfile.net8 -t startup:net8 .
docker build -f Dockerfile.net10 -t startup:net10 .
```

When you run the images it executes the .NET project that was build during docker build a 100 times and returns back the average. What I have found so far is that if I run the containers with no cpu restrictions .NET 10 is faster. But if I run the images highly restricting the cpu amount like in a Lambda environment the average shows .NET 10 generally being 5% slower then .NET 8.

Command to run image with restricted cpu:

NET 10: `docker run --rm -it --cpus=0.1 startup:net10`

NET  8: `docker run --rm -it --cpus=0.1 startup:net8`