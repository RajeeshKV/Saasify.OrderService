using Application;
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
                
                var dataSourceBuilder = new Npgsql.NpgsqlDataSourceBuilder(connectionString);
                dataSourceBuilder.EnableDynamicJson(); // Enable dynamic JSON serialization
                var dataSource = dataSourceBuilder.Build();
                
                options.UseNpgsql(dataSource, b => b.MigrationsAssembly("Infrastructure"));
            });

            // RabbitMQ
            services.AddSingleton<IRabbitMQService, RabbitMQService>();

            // Services
            services.AddScoped<IOrderService, Application.OrderService>();

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
