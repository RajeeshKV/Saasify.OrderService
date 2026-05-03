using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Text;
using Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Infrastructure.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT authentication
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "OrderService API",
        Version = "v1",
        Description = "OrderService Microservice API for Multi-tenant SaaS Platform",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "OrderService Support",
            Email = "support@orderservice.com"
        }
    });

    // Add JWT Authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

// Add Infrastructure and Application services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("https://saasify.rajeesh.online", 
                          "http://saasify.rajeesh.online",
                          "https://saasifyapi-client.rajeesh.online",
                          "http://saasifyapi-client.rajeesh.online",
                          "http://localhost:3000",
                          "https://localhost:3000",
                          "http://localhost:5000",
                          "https://localhost:5000")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Add health checks with proper configuration
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "ready" })
    .AddCheck<RabbitMQHealthCheck>("rabbitmq", tags: new[] { "ready" });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderService API V1");
        c.RoutePrefix = "swagger"; // Set route prefix for both dev and prod
        c.DocumentTitle = "OrderService API Documentation";
        c.DefaultModelsExpandDepth(-1); // Hide models by default
        c.DefaultModelExpandDepth(-1);
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.ShowExtensions();
        c.EnableFilter();
        
        if (app.Environment.IsProduction())
        {
            c.SupportedSubmitMethods(new[] { 
                Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Get, 
                Swashbuckle.AspNetCore.SwaggerUI.SubmitMethod.Post 
            });
        }
    });
}

// Skip HTTPS redirection for health endpoints to support both HTTP and HTTPS
app.UseWhen(context => !context.Request.Path.StartsWithSegments("/health"), 
    app => app.UseHttpsRedirection());

app.UseCors("AllowAll");

// Health check endpoints (placed before authentication)
app.MapGet("/health", async (IServiceProvider serviceProvider) =>
{
    var healthCheckService = serviceProvider.GetRequiredService<HealthCheckService>();
    var healthCheckReport = await healthCheckService.CheckHealthAsync();
    
    var databaseStatus = healthCheckReport.Entries.ContainsKey("database") 
        ? healthCheckReport.Entries["database"].Status.ToString()
        : "Unknown";
    var rabbitmqStatus = healthCheckReport.Entries.ContainsKey("rabbitmq") 
        ? healthCheckReport.Entries["rabbitmq"].Status.ToString()
        : "Unknown";
    
    return new
    {
        Status = healthCheckReport.Status.ToString(),
        Service = "OrderService",
        Timestamp = DateTime.UtcNow,
        Version = "1.0.0",
        Database = databaseStatus,
        RabbitMQ = rabbitmqStatus
    };
})
.RequireCors("AllowAll")
.WithName("HealthCheck")
.WithOpenApi();

// Detailed health check endpoint with proper health check response
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            Status = report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy",
            Checks = report.Entries.Select(entry => new
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                Description = entry.Value.Description,
                Duration = entry.Value.Duration.TotalMilliseconds,
                Data = entry.Value.Data
            }),
            TotalDuration = report.TotalDuration.TotalMilliseconds,
            Timestamp = DateTime.UtcNow
        };
        
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
})
.RequireCors("AllowAll")
.WithName("DetailedHealthCheck")
.WithOpenApi();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
