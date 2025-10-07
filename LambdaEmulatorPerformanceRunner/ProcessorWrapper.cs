using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

public class ProcessorWrapper
{
    const string TEST_CONTAINER_NAME = "lambda-coldstart-issue";

    public static void BuildLambdaImage(string targetFramework, string imageName)
    {
        var dockerfilePath = LambdaWithEmulatorTestDirectory();
        
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"build --build-arg TARGET_FRAMEWORK={targetFramework} -t {imageName} {dockerfilePath}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        string output = process!.StandardOutput.ReadToEnd();
        string error = process!.StandardError.ReadToEnd();
        process?.WaitForExit();
        
        if (process?.ExitCode != 0)
            throw new Exception($"Docker build failed with exit code {process!.ExitCode}:\n{output}\n{error}");
    }

    static int _hostPort = 9000;
    public static double RunDockerImageUntilBilledDuration(string imageName)
    {
        KillContainer();

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"run --name {TEST_CONTAINER_NAME} --cpus=0.1 --rm -p {_hostPort}:8080 {imageName}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (process == null)
            throw new Exception("Failed to start docker process");

        var outputBuilder = new StringBuilder();
        double? duration = null;

        var callback = new DataReceivedEventHandler((sender, args) =>
        {
            outputBuilder.AppendLine(args.Data);
            Console.WriteLine(args.Data);
            if (args.Data?.Contains("Billed Duration") == true)
            {
                var tokens = args.Data.Split('\t');
                duration = double.Parse(Regex.Replace(tokens[3], @"[^0-9.]", ""));
                process.Kill();
                KillContainer();
            }
        });
        process.ErrorDataReceived += callback;
        process.OutputDataReceived += callback;
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(1));
        try
        {
            _ = InvokeLambdaFunction(cancellationTokenSource.Token);
            process.WaitForExit(TimeSpan.FromMinutes(1));
        }
        finally
        {
            cancellationTokenSource.Cancel();
        }

        if (duration == null)
        {
            _hostPort++;
            throw new Exception("Failed to find Billed Duration in output\n" + outputBuilder.ToString());
        }

        return duration.Value;
    }

    static async Task InvokeLambdaFunction(CancellationToken cancellationToken)
    {
        int attempt = 1;
        while(!cancellationToken.IsCancellationRequested)
        {
            var url = $"http://localhost:{_hostPort}/2015-03-31/functions/function/invocations";
            using var client = new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent("{}");
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            try
            {
                Console.WriteLine($"**** Invoking Lambda function (Attempt {attempt})****");
                using var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("**** Successfully invoked Lambda function ****");
                    return;
                }
            }
            catch (Exception)
            {
                attempt++;
            }
        }
    }

    public static void KillContainer()
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"kill {TEST_CONTAINER_NAME}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        Thread.Sleep(1000);
    }

    static string LambdaWithEmulatorTestDirectory()
    {
        var baseDirectory = new DirectoryInfo(Environment.CurrentDirectory);

        while (baseDirectory.GetDirectories("LambdaWithEmulatorTest").Length == 0)
        {
            if (baseDirectory.Parent == null)
            {
                throw new Exception("Could not find LambdaWithEmulator directory");
            }
            baseDirectory = baseDirectory.Parent;
        }

        return Path.Combine(baseDirectory.FullName, "LambdaWithEmulatorTest");
    }
}