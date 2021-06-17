using Api.Scheduler;
using Domain.Authenticate;
using Domain.Token;
using Domain.User;
using Infrastructure;
using Infrastructure.AppSettings;
using Infrastructure.Code;
using Infrastructure.Contexts;
using Infrastructure.Crypto;
using Infrastructure.Email;
using Infrastructure.FileStorage;
using Infrastructure.Host;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Services.Implementations;
using System;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using Autofac;
using AutoMapper.Contrib.Autofac.DependencyInjection;
using Domain.Code;
using Infrastructure.Repositories;
using Infrastructure.Repositories.Token;
using Infrastructure.Repositories.User;
using Middleware;

namespace Api
{
    public class Startup
    {
        private static readonly string Env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        private static readonly string AppSettings = "appsettings.json";

        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();

            var key = Encoding.ASCII.GetBytes(Configuration["AppSettings:Secret"]);
            services.AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(x =>
                {
                    x.RequireHttpsMetadata = false;
                    x.SaveToken = true;
                    x.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        RequireExpirationTime = false,
                        ValidateLifetime = false
                    };
                });

            var securityScheme = new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = "Authorization",
                BearerFormat = "Bearer {authToken}",
                Description = "JWT Token",
                Type = SecuritySchemeType.ApiKey
            };
            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1",
                    new OpenApiInfo
                    {
                        Title = "Admin Portal API", Version = "v1",
                        Description = Configuration["AppSettings:Environment"]
                    });

                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Api.xml"));
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Domain.xml"));

                c.AddSecurityDefinition(
                    "Bearer", securityScheme
                );
                c.AddSecurityRequirement(
                    new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference
                                {
                                    Type = ReferenceType.SecurityScheme, Id = "Bearer"
                                }
                            },
                            new string[] { }
                        }
                    });
            });

            services
                .AddControllers()
                .AddNewtonsoftJson()
                .AddNewtonsoftJson(
                    options => options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
                );
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            var dbContextOptionsBuilder =
                new DbContextOptionsBuilder<Context>().UseNpgsql(
                    Configuration.GetConnectionString("DefaultConnection"),
                    x => x.MigrationsAssembly("DBMigrations")
                );
            builder
                .RegisterType<Context>()
                .WithParameter("options", dbContextOptionsBuilder.Options)
                .InstancePerLifetimeScope();

            #region DI Service

            builder
                .RegisterInstance(new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile(AppSettings, optional: true, reloadOnChange: false)
                    .Build()
                )
                .As<IConfigurationRoot>()
                .SingleInstance();

            builder.RegisterType<AuthenticationService>().As<IAuthenticationService>();
            builder.RegisterType<TokenService>().As<ITokenService>();
            builder.RegisterType<UserService>().As<IUserService>();
            builder.RegisterType<CodeService>().As<ICodeService>();

            builder.RegisterType<CryptoHelper>().SingleInstance();
            builder.RegisterType<ScheduleTask>().As<IHostedService>().SingleInstance();

            builder
                .Register(p => Configuration
                    .GetSection(nameof(FileStorageConfiguration))
                    .Get<FileStorageConfiguration>()
                )
                .As<FileStorageConfiguration>()
                .SingleInstance();

            builder
                .RegisterInstance(Configuration
                    .GetSection(nameof(AppSettingsConfiguration))
                    .Get<AppSettingsConfiguration>()
                )
                .As<AppSettingsConfiguration>()
                .SingleInstance();

            #endregion

            #region DI Repository

            builder.RegisterType<UserRepository>().As<IUserRepository>();
            builder.RegisterType<TokenRepository>().As<ITokenRepository>();
            builder.RegisterType<SqlRepository>();

            #endregion

            #region DI Infrastructure

            builder
                .RegisterInstance(Configuration
                    .GetSection(nameof(EmailConfiguration))
                    .Get<EmailConfiguration>()
                )
                .As<EmailConfiguration>()
                .SingleInstance();

            builder.RegisterType<EmailSender>().As<IEmailSender>();

            builder
                .RegisterInstance(Configuration
                    .GetSection(nameof(HostConfiguration))
                    .Get<HostConfiguration>())
                .As<HostConfiguration>()
                .SingleInstance();
            builder
                .RegisterInstance(Configuration
                    .GetSection(nameof(CodeConfiguration))
                    .Get<CodeConfiguration>())
                .As<CodeConfiguration>()
                .SingleInstance();

            builder.RegisterType<InitializeInfrastructure>();

            #endregion
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            ILoggerFactory loggerFactory,
            InitializeInfrastructure infrastructure
        )
        {
            
            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();
            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Api v1"); });

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();

            // app.UseVersionMiddleware();
            // app.UseCorrelationId();
            // app.UseContextContainer();
            
            app.UseCors(builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod().Build());

            app.UseAuthentication();

            // app.UseLocalization();
            // app.UseRequestResponseLogging();
            // app.UseTraceRequestLogging();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(opt =>
            {
                opt.MapControllers();
            });
            
            UpdateDatabase(app);
            infrastructure.FileStorage();
        }

        private static void UpdateDatabase(IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>().CreateScope())
            {
                using (var context = serviceScope.ServiceProvider.GetService<Context>())
                {
                    context?.Database.Migrate();
                }
            }
        }
    }
}