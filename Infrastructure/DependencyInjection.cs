using Application;
using Application.Handlers;
using Domain;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Infrastructure.HealthChecks;

namespace Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            // Database
            services.AddDbContext<OrderDbContext>((serviceProvider, options) =>
            {
                var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL") 
                    ?? configuration.GetConnectionString("DefaultConnection");
                
                options.UseNpgsql(connectionString, npgsqlOptions => 
                {
                    npgsqlOptions.MigrationsAssembly("Infrastructure");
                })
                .ConfigureWarnings(warnings => 
                    warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.ManyServiceProvidersCreatedWarning));
            });

            // RabbitMQ
            services.AddSingleton<IRabbitMQService, RabbitMQService>();

            // Services
            services.AddScoped<IOrderService, Application.OrderService>();
            services.AddScoped<IOrderCommandHandler, Application.Handlers.OrderCommandHandler>();

            // Background Services
            services.AddHostedService<MessageProcessorService>();

            // Health Checks are registered in Program.cs

            return services;
        }

        public static IServiceCollection AddApplication(
            this IServiceCollection services)
        {
            // Application services are registered in AddInfrastructure
            return services;
        }
    }
}
