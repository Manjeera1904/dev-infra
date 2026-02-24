using System.Diagnostics;

namespace EI.API.ServiceDefaults;

public static class TracingExtensions
{
    public static Activity? StartActivity(
        this ActivitySource source,
        string name,
        params (string Key, object? Value)[] tags)
    {
        var activity = source.StartActivity(name);

        if (activity != null)
        {
            foreach (var (key, value) in tags)
                activity.SetTag(key, value);
        }

        return activity;
    }
}
