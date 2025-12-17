using SearchEngine.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SearchEngine.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ===================== SERVICES =====================

            builder.Services.AddControllers();

            // ---- CORS (مهم للفرونت إند) ----
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });

            // ===================== BUILD SEARCH ENGINE =====================

            // المسار الصح للـ NDJSON (داخل مشروع الـ API)
            string dataPath = Path.Combine(
                builder.Environment.ContentRootPath,
                "data",
                "index.ndjson"
            );

            if (!File.Exists(dataPath))
            {
                throw new FileNotFoundException(
                    $"index.ndjson not found at: {dataPath}"
                );
            }

            // 1) اقرأ الصفحات (للعرض في النتائج)
            var reader = new NdjsonPageReader();
            var pages = reader.Read(dataPath).ToList();

            // 2) ابني الـ Inverted Index
            var index = new InvertedIndex();
            var tokenizer = new Tokenizer();
            var indexer = new Indexer(tokenizer, index);

            indexer.BuildFromNdjson(dataPath);

            // 3) Query Engine (القلب)
            var queryEngine = new QueryEngine(index, pages);

            builder.Services.AddSingleton(queryEngine);

            // ===================== APP =====================

            var app = builder.Build();

            app.UseHttpsRedirection();

            app.UseCors("AllowAll");

            app.MapControllers();

            app.Run();
        }
    }
}
