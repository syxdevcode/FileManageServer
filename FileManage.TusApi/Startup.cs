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

            #region �ļ��ϴ���С��������

            // IIS ����������
            services.Configure<IISServerOptions>(options =>
            {
                options.MaxRequestBodySize = long.MaxValue; 
            });

            // Kestrel������
            services.Configure<KestrelServerOptions>(options =>
            {
                options.Limits.MaxRequestBodySize = long.MaxValue; ;
            });

            // ͨ�����ϴ��ļ�
            services.Configure<FormOptions>(x =>
            {
                // 2147483647 2GB
                x.ValueLengthLimit = int.MaxValue;

                x.MultipartBodyLengthLimit = long.MaxValue;
                x.MultipartHeadersLengthLimit = int.MaxValue;
            });
            #endregion

            // ����cors
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

            // ����cors
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

            //�ļ��ϴ�·��
            var tusFiles = Path.Combine(env.WebRootPath, "tusfiles");

            if (!Directory.Exists(tusFiles))
                Directory.CreateDirectory(tusFiles);

            return new DefaultTusConfiguration
            {
                UrlPath = "/files",
                //�ļ��洢·��
                Store = new TusDiskStore(tusFiles),
                //Ԫ�����Ƿ������ֵ
                MetadataParsingStrategy = MetadataParsingStrategy.AllowEmptyValues,
                //�ļ����ں��ٸ���
                Expiration = new AbsoluteExpiration(TimeSpan.FromMinutes(5)),
                //�¼����������¼������������裩
                Events = new Events
                {
                    //�ϴ�����¼��ص�
                    OnFileCompleteAsync = async ctx =>
                    {
                        //��ȡ�ϴ��ļ�
                        var file = await ctx.GetFileAsync();

                        //��ȡ�ϴ��ļ�Ԫ����
                        var metadatas = await file.GetMetadataAsync(ctx.CancellationToken);

                        //��ȡ�����ļ�Ԫ�����е�Ŀ���ļ�����
                        var fileNameMetadata = metadatas["name"];

                        //Ŀ���ļ�����base64���룬����������Ҫ����
                        var fileName = fileNameMetadata.GetString(Encoding.UTF8);

                        var extensionName = Path.GetExtension(fileName);

                        //���ϴ��ļ�ת��Ϊʵ��Ŀ���ļ�
                        File.Move(Path.Combine(tusFiles, ctx.FileId), Path.Combine(tusFiles, $"{ctx.FileId}{extensionName}"));
                    }
                }
            };
        }

    }
}
