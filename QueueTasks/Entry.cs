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
            serviceCollection.AddSingleton<QueueOperatorManager>();
            serviceCollection.AddTransient<IQueueOperatorManager>(x => x.GetService<QueueOperatorManager>());
            serviceCollection.AddTransient<ITasksManager>(x => x.GetService<QueueOperatorManager>());

            serviceCollection.AddScoped<ICurrentOperatorProvider, TProvider>();
            serviceCollection.AddScoped<IExtensionService, TExtensionService>();

            return serviceCollection;
        }
    }
}
