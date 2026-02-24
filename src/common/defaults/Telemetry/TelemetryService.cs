using System.Diagnostics;

namespace EI.API.ServiceDefaults.Telemetry
{
    public static class TelemetryService
    {
        private static ActivitySource? _activitySource;
        private static readonly object _lock = new();

        public static ActivitySource ActivitySource =>
            _activitySource ?? throw new InvalidOperationException(
                "TelemetryService has not been initialized.");

        /// <summary>
        /// Initializes the ActivitySource name for OpenTelemetry.
        /// </summary>
        public static void Initialize(string applicationName)
        {
            if (string.IsNullOrWhiteSpace(applicationName))
                throw new ArgumentException("Application name cannot be null or empty.", nameof(applicationName));

            lock (_lock)
            {
                // If already initialized with the same name, do nothing.
                if (_activitySource != null && _activitySource.Name == applicationName)
                    return;

                // Dispose previous instance if tests reinitialize.
                _activitySource?.Dispose();

                _activitySource = new ActivitySource(applicationName);
            }
        }
    }
}
