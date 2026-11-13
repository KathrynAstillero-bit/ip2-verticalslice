using Api.Features.Students;
using Api.Features.Courses;
using Api.Features.Admissions;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Features.Students.CreateStudent;
using Application.Features.Students.GetAllStudents;
using Application.Features.Students.GetStudentById;
using Application.Features.Courses.CreateCourse;
using Application.Features.Courses.GetAllCourses;
using Application.Features.Courses.GetCourseById;
using Application.Features.Courses.UpdateCourse;
using Application.Features.Courses.DeleteCourse;
using Application.Features.Admissions.CreateAdmission;
using Application.Features.Admissions.GetAdmissionById;
using Application.Features.Admissions.GetAllAdmissions;
using Domain.Common;
using FluentValidation;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Application.Abstractions;
using Infrastructure.Authentication;
using Api.Features.Auth;
using Application.Features.Auth.Register;
using Application.Features.Auth.Login;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);


var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["SigningKey"]!)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var dbContext = context.HttpContext.RequestServices
                    .GetRequiredService<IAppDbContext>();

                var jti = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
                var sub = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                    ?? context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var iatRaw = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Iat);

                if (jti is not null && await dbContext.RevokedTokens.AnyAsync(r => r.Jti == jti))
                {
                    context.Fail("Token has been revoked.");
                    return;
                }

                if (Guid.TryParse(sub, out var userId) && long.TryParse(iatRaw, out var iatUnix))
                {
                    var issuedAtUtc = DateTimeOffset.FromUnixTimeSeconds(iatUnix).UtcDateTime;
                    var user = await dbContext.Users.FindAsync([userId]);

                    if (user?.TokensValidFromUtc is { } validFrom && issuedAtUtc < validFrom)
                    {
                        context.Fail("Token was issued before the user's last sign-out-everywhere.");
                    }
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<
    ICommandHandler<LoginCommand, Result<LoginResponse>>,
    LoginCommandHandler>();

builder.Services.AddScoped<
    ICommandHandler<RegisterCommand, Result<RegisterResponse>>,
    RegisterCommandHandler>();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.SigningKey),
        "Jwt:SigningKey is required. Set it using .NET User Secrets.")
    .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.SigningKey ?? "") >= 32,
        "Jwt:SigningKey must be at least 32 bytes.")
    .ValidateOnStart();

builder.Services.AddScoped<ITokenService, JwtTokenService>();

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

// Database
builder.Services.AddDbContext<IAppDbContext, AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("Database")));

// Validators
builder.Services.AddValidatorsFromAssemblyContaining<CreateStudentValidator>();

// Handlers
builder.Services.AddScoped<
    ICommandHandler<CreateStudentCommand, Result<CreateStudentResponse>>,
    CreateStudentCommandHandler>();

builder.Services.AddScoped<
    IQueryHandler<GetAllStudentsQuery, Result<GetAllStudentsResponse>>,
    GetAllStudentsQueryHandler>();

builder.Services.AddScoped<
    IQueryHandler<GetStudentByIdQuery, Result<GetStudentByIdResponse>>,
    GetStudentByIdQueryHandler>();

builder.Services.AddScoped<
    ICommandHandler<CreateCourseCommand, Result<CreateCourseResponse>>,
    CreateCourseCommandHandler>();

builder.Services.AddScoped<
    IQueryHandler<GetAllCoursesQuery, Result<GetAllCoursesResponse>>,
    GetAllCoursesQueryHandler>();

builder.Services.AddScoped<
    IQueryHandler<GetCourseByIdQuery, Result<GetCourseByIdResponse>>,
    GetCourseByIdQueryHandler>();

builder.Services.AddScoped<
    ICommandHandler<CreateAdmissionCommand, Result<CreateAdmissionResponse>>,
    CreateAdmissionCommandHandler>();

builder.Services.AddScoped<
    IQueryHandler<GetAdmissionByIdQuery, Result<GetAdmissionByIdResponse>>,
    GetAdmissionByIdQueryHandler>();

builder.Services.AddScoped<
    IQueryHandler<GetAllAdmissionsQuery, Result<GetAllAdmissionsResponse>>,
    GetAllAdmissionsQueryHandler>();

builder.Services.AddScoped<
    ICommandHandler<UpdateCourseCommand, Result<UpdateCourseResponse>>,
    UpdateCourseCommandHandler>();

builder.Services.AddScoped<
    ICommandHandler<DeleteCourseCommand, Result>,
    DeleteCourseCommandHandler>();

builder.Services.AddScoped<IPasswordHasher, Infrastructure.Authentication.PasswordHasher>();

// API documentation
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Vertical Slice API";
        document.Info.Description =
            "A simple API demonstrating Vertical Slice Architecture";

        return Task.CompletedTask;
    });
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the access token from /api/auth/login"
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

        return Task.CompletedTask;
    });
});

builder.Services.AddHostedService<
    Infrastructure.BackgroundJobs.RevokedTokenCleanupService>();

var app = builder.Build();

await app.Services.MigrateAndSeedAsync();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Root endpoint
app.MapGet("/", () =>
    "Welcome to the Vertical Slice API! Go to /scalar/v1 for API documentation.");

// Map endpoints
app.MapStudentEndpoints();
app.MapCourseEndpoints();
app.MapAdmissionEndpoints();
app.MapAuthEndpoints();

app.UseAuthentication();
app.UseAuthorization();

app.Run();