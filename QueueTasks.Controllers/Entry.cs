namespace QueueTasks.Controllers
{
    using System;
    using System.IO;
    using System.Reflection;

    using Api.V1;

    using Microsoft.Extensions.DependencyInjection;

    public static class Entry
    {
        /// <summary>
        ///     Добавление API очереди
        /// </summary>
        /// <param name="builder">IMvcBuilder</param>
        /// <returns>IMvcBuilder</returns>
        public static IMvcBuilder AddQueueTasksApi(this IMvcBuilder builder) =>
            builder
                .AddApplicationPart(Assembly.GetAssembly(typeof(QueueTasksController)))
                .AddXmlSerializerFormatters();


        public static IServiceCollection AddQueueTasksSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                var xmlFile = $"{Assembly.GetAssembly(typeof(QueueTasksController))?.GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            }).AddSwaggerGenNewtonsoftSupport();

            return services;
        }
    }
}
