using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LambdaWithEmulatorTest;

public class ConsoleListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // Attach to interesting sources
        if (eventSource.Name == "Microsoft-Windows-DotNETRuntime")
        {
            EnableEvents(eventSource, EventLevel.Verbose,
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
