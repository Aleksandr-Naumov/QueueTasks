namespace QueueTasks
{
    using Abstractions;

    using Contracts;
    using Microsoft.Extensions.DependencyInjection;

    using Services;
    using System;
    using System.IO;
    using System.Reflection;

    using Api.V1;

    public static class Entry
    {
        /// <summary>
        ///     Зарегистрировать зависимости очереди
        /// </summary>
        /// <typeparam name="TExtensionService">Сервис для расширения</typeparam>
        /// <typeparam name="TProvider">Провайдер для получения Id оператора текщуего пользователя</typeparam>
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

        /// <summary>
        ///     Добавить API очереди
        /// </summary>
        public static IMvcBuilder AddQueueTasksApi(this IMvcBuilder builder) =>
            builder
                .AddApplicationPart(Assembly.GetAssembly(typeof(QueueTasksController)))
                .AddXmlSerializerFormatters();

        /// <summary>
        ///     Добавить отображение документации API очереди в Swagger
        /// </summary>
        public static IServiceCollection AddQueueTasksSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                var xmlFile = $"{Assembly.GetAssembly(typeof(QueueTasksController))?.GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            }).AddSwaggerGenNewtonsoftSupport();

            return services;
        }
    }
}
