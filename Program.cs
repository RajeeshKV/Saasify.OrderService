using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics;
using System.Text;
using Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

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

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoints
app.MapGet("/health", () => new
{
    Status = "Healthy",
    Service = "OrderService",
    Timestamp = DateTime.UtcNow,
    Version = "1.0.0"
})
.WithName("HealthCheck")
.WithOpenApi();

// Simple health check endpoint
app.MapHealthChecks("/healthz")
.WithName("DetailedHealthCheck")
.WithOpenApi();

app.Run();
