
string net8ImageName = "lambda-coldstart-issue:net8.0";
Console.WriteLine("Building image for .NET 8.0");
ProcessorWrapper.BuildLambdaImage("net8.0", net8ImageName);
string net10ImageName = "lambda-coldstart-issue:net10.0";
Console.WriteLine("Building image for .NET 10.0");
ProcessorWrapper.BuildLambdaImage("net10.0", net10ImageName);

Console.WriteLine("Running .NET 10 for P90:");
var p90NET10 = ComputeP80(net10ImageName);

Console.WriteLine("Running .NET 8 for P90:");
var p90NET8 = ComputeP80(net8ImageName);

Console.WriteLine("P80 .NET 10: " + p90NET10);
Console.WriteLine("P80 .NET  8: " + p90NET8);

static double ComputeP80(string imageName)
{
    var durations = new List<double>();
    for(var i = 0; i < 50; i++)
    {
        Console.WriteLine("\tIteration: " + i);
        try
        {
            var duration = ProcessorWrapper.RunDockerImageUntilBilledDuration(imageName);
            durations.Add(duration);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error during execution: " + ex.Message);
            ProcessorWrapper.KillContainer(); // Double check container is killed
            Thread.Sleep(2000); // Wait a bit before retrying to see if maybe the container and port mapping took longer to clean up.
            i--; // retry this iteration
        }
    }

    var p80 = Percentile(durations, .8);
    return p80;
}

static double Percentile(List<double> sequence, double percentile)
{
    if (sequence == null || sequence.Count == 0)
        throw new InvalidOperationException("Sequence contains no elements.");

    var ordered = sequence.OrderBy(x => x).ToList();
    double position = (ordered.Count + 1) * percentile;
    int index = (int)position;

    if (index < 1)
        return ordered[0];
    if (index >= ordered.Count)
        return ordered[ordered.Count - 1];

    double fraction = position - index;
    return ordered[index - 1] + fraction * (ordered[index] - ordered[index - 1]);
}
