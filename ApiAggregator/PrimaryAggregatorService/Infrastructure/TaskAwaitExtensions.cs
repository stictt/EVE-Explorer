namespace PrimaryAggregatorService.Infrastructure
{
    public static class TaskAwaitExtensions
    {
        public static void NoAwait(this Task task, ILogger logger)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted) logger.LogInformation(t?.Exception?.Message);
            }, TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
