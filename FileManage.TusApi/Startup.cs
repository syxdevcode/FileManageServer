using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileManage.TusApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using tusdotnet;
using tusdotnet.Models;
using tusdotnet.Models.Configuration;
using tusdotnet.Models.Expiration;
using tusdotnet.Stores;

namespace FileManage.TusApi
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(CreateTusConfiguration);

            #region 文件上传大小限制配置

            // IIS 服务器配置
            services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = long.MaxValue; 
            });

            // Kestrel服务器
            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = long.MaxValue; ;
            });

            // 通过表单上传文件
            services.Configure<FormOptions>(x =>
            {
                // 2147483647 2GB
                x.ValueLengthLimit = int.MaxValue;

                x.MultipartBodyLengthLimit = long.MaxValue;
                x.MultipartHeadersLengthLimit = int.MaxValue;
            });
            #endregion

            // 配置cors
            services.AddCors();

            services.AddControllersWithViews();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            // 配置cors
            app.UseCors(builder => builder
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowAnyOrigin()
                .WithExposedHeaders(tusdotnet.Helpers.CorsHelper.GetExposedHeaders())
            );

            app.UseStaticFiles();
            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseTus(httpContext => Task.FromResult(httpContext.RequestServices.GetService<DefaultTusConfiguration>()));

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");

                endpoints.MapGet("/files/{fileId}", DownloadFileEndpoint.HandleRoute);
            });
        }

        private DefaultTusConfiguration CreateTusConfiguration(IServiceProvider serviceProvider)
        {
            var env = (IWebHostEnvironment)serviceProvider.GetRequiredService(typeof(IWebHostEnvironment));

            //文件上传路径
            var tusFiles = Path.Combine(env.WebRootPath, "tusfiles");

            if (!Directory.Exists(tusFiles))
                Directory.CreateDirectory(tusFiles);

            return new DefaultTusConfiguration
            {
                UrlPath = "/files",
                //文件存储路径
                Store = new TusDiskStore(tusFiles),
                //元数据是否允许空值
                MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
                //文件过期后不再更新
                Expiration = new AbsoluteExpiration(TimeSpan.FromMinutes(5)),
                //事件处理（各种事件，满足你所需）
                Events = new Events
                {
                    //上传完成事件回调
                    OnFileCompleteAsync = async ctx =>
                    {
                        //获取上传文件
                        var file = await ctx.GetFileAsync();

                        //获取上传文件元数据
                        var metadatas = await file.GetMetadataAsync(ctx.CancellationToken);

                        //获取上述文件元数据中的目标文件名称
                        var fileNameMetadata = metadatas["name"];

                        //目标文件名以base64编码，所以这里需要解码
                        var fileName = fileNameMetadata.GetString(Encoding.UTF8);

                        var extensionName = Path.GetExtension(fileName);

                        //将上传文件转换为实际目标文件
                        File.Move(Path.Combine(tusFiles, ctx.FileId), Path.Combine(tusFiles, $"{ctx.FileId}{extensionName}"));
                    }
                }
            };
        }

    }
}
