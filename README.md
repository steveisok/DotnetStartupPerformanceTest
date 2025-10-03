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


## Simulate locally with Lambda emulator

The [LambdaEmulatorPerformanceRunner](./LambdaEmulatorPerformanceRunner) project attempts to simulate the issue using the Lambda Runtime Interface Emulator (RIE) 
running as a container. It builds the [LambdaWithEmulatorTest](./LambdaWithEmulatorTest) Lambda project as a container using the `public.ecr.aws/lambda/provided:al2023`
base image which has the RIE installed in it. A .NET 8 image will be built with tag `lambda-coldstart-issue:net8.0` and the .NET 10 version tag will 
`lambda-coldstart-issue:net10.0`. 

It will run for 50 iterations for each container doing the following steps.
* Start the container which will start up the RIE mapping the container port 8080 to host port 9000.
  * The container is started with the `--cpus=0.1` to simulate Lambda environment running with limited CPU.
* Send an HTTP request to port 9000 as the initial event.
  * Due to timing waiting for the container to startup this part will loop till it gets a HTTP success status code.
* Read the stdout and stderr coming from the process that started the container.
* Look for the output line that says `Billed Duration`. 
  * The Billed Duration captures how much time was spent in just the .NET Lambda part and not container and RIE startup.
* Once the Billed Duration is found kill the container because we only want to capture first invokes.
* Add the duration to the collection of results.
* After the 50 iterations are done compute the P80.
  * I went with P80 figuring there would be a lot more noise running locally then in the Lambda environment.

I'm my machine I'm seeing the following results showing .NET 10 is slower on cold start.
```
P80 .NET 10: 1188.6000000000001
P80 .NET  8: 1002.8
```


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