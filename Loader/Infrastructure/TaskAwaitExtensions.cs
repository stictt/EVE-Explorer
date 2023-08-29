using Domain.Infrastructure.Interface;

namespace PrimaryAggregatorService.Infrastructure
{
    public static class TaskAwaitExtensions
    {
        public static void NoAwait(this Task task, ILoggerBase logger)
        {
            task.ContinueWith(t =>
            {
                if (t.IsFaulted) logger.LogInformation(t?.Exception?.Message);
            }, TaskContinuationOptions.ExecuteSynchronously);
        }
    }
}
