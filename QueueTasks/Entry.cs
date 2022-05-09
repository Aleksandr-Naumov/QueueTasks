namespace QueueTasks
{
    using Abstractions;

    using Microsoft.Extensions.DependencyInjection;

    using Services;

    public static class Entry
    {
        public static IServiceCollection AddQueueTasks<TExtensionService, TProvider>(this IServiceCollection serviceCollection)
            where TExtensionService : class, IExtensionService
            where TProvider : class, ICurrentOperatorProvider
        {
            serviceCollection.AddSingleton<IQueueOperatorManager, QueueOperatorManager>();
            serviceCollection.AddScoped<ITasksManager, TasksManager>();
            serviceCollection.AddScoped<ICurrentOperatorProvider, TProvider>();
            serviceCollection.AddScoped<IExtensionService, TExtensionService>();

            return serviceCollection;
        }
    }
}
