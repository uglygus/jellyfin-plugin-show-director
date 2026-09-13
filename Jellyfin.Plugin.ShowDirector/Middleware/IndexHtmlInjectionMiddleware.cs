using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ShowDirector.Middleware
{
    /// <summary>
    /// Rewrites the served index.html in memory to add our injected
    /// &lt;script&gt; tag, instead of writing to the file on disk. This avoids
    /// requiring write permission on the jellyfin-web folder, which several
    /// packaging setups (native Linux packages, hardened containers) don't
    /// grant to the Jellyfin process.
    ///
    /// Only requests that look like the SPA shell (path is "/", "/web",
    /// "/web/", or ends in "index.html") are buffered - everything else
    /// (API calls, images, video/audio streams, JS/CSS bundles) passes
    /// straight through untouched so we never buffer a large response.
    /// </summary>
    public class IndexHtmlInjectionMiddleware : IMiddleware
    {
        private const string ScriptTagMarker = "data-plugin=\"ShowDirector\"";

        private readonly ILogger<IndexHtmlInjectionMiddleware> _logger;

        public IndexHtmlInjectionMiddleware(ILogger<IndexHtmlInjectionMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (!ShouldIntercept(context))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            var config = Plugin.Instance?.Configuration;
            if (config is { Enabled: false })
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            var originalBody = context.Response.Body;
            await using var buffer = new MemoryStream();
            context.Response.Body = buffer;

            try
            {
                await next(context).ConfigureAwait(false);

                var contentType = context.Response.ContentType ?? string.Empty;
                var isHtml = contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase);

                if (context.Response.StatusCode != StatusCodes.Status200OK || !isHtml)
                {
                    // Not what we expected (304 Not Modified, non-HTML, etc.)
                    // - pass the buffered bytes through unchanged.
                    context.Response.Body = originalBody;
                    buffer.Seek(0, SeekOrigin.Begin);
                    await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
                    return;
                }

                buffer.Seek(0, SeekOrigin.Begin);
                string html;
                using (var reader = new StreamReader(buffer, Encoding.UTF8, false, 4096, leaveOpen: true))
                {
                    html = await reader.ReadToEndAsync().ConfigureAwait(false);
                }

                if (!html.Contains(ScriptTagMarker, StringComparison.Ordinal)
                    && html.Contains("</body>", StringComparison.OrdinalIgnoreCase))
                {
                    html = InjectScriptTag(html, config);
                }

                var bytes = Encoding.UTF8.GetBytes(html);
                context.Response.Body = originalBody;
                context.Response.ContentLength = bytes.Length;
                await originalBody.WriteAsync(bytes).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ShowDirector] Failed to rewrite response, passing through unmodified");
                if (!context.Response.HasStarted)
                {
                    context.Response.Body = originalBody;
                    buffer.Seek(0, SeekOrigin.Begin);
                    await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
                }
            }
            finally
            {
                context.Response.Body = originalBody;
            }
        }

        private static bool ShouldIntercept(HttpContext context)
        {
            if (!HttpMethods.IsGet(context.Request.Method))
            {
                return false;
            }

            var path = context.Request.Path.Value ?? string.Empty;

            // Anything with a file extension other than .html is a static
            // asset (js/css/png/etc.) - never buffer those.
            var lastSegment = path.Length == 0 ? string.Empty : path[(path.LastIndexOf('/') + 1)..];
            if (lastSegment.Contains('.', StringComparison.Ordinal)
                && !lastSegment.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return path == "/"
                || path.Equals("/web", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/web/", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase);
        }

        private static string InjectScriptTag(string html, PluginConfiguration? config)
        {
            var separator = System.Net.WebUtility.HtmlEncode(config?.MultiDirectorSeparator ?? ", ");
            var maxDirectors = config?.MaxDirectors ?? 2;
            var applyMovies = (config?.ApplyToMovies ?? true) ? "true" : "false";
            var applySeries = (config?.ApplyToSeries ?? true) ? "true" : "false";
            var applyEpisodes = (config?.ApplyToEpisodes ?? false) ? "true" : "false";

            var scriptTag =
                "<script plugin=\"ShowDirector\" " + ScriptTagMarker + " " +
                $"src=\"/web/ConfigurationPage?name={Plugin.JsPageName}\" " +
                $"data-separator=\"{separator}\" " +
                $"data-max-directors=\"{maxDirectors}\" " +
                $"data-apply-movies=\"{applyMovies}\" " +
                $"data-apply-series=\"{applySeries}\" " +
                $"data-apply-episodes=\"{applyEpisodes}\" " +
                "defer></script>\n</body>";

            var place = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
            return html.Remove(place, "</body>".Length).Insert(place, scriptTag);
        }
    }
}
