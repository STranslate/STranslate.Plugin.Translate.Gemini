namespace STranslate.Plugin.Translate.Gemini;

internal static class GeminiRequest
{
    public static (string Url, Options? Options) Create(Settings settings)
    {
        var model = settings.Model.Trim();
        if (model.Length == 0)
            model = "gemini-flash-latest";

        var endpoint = new UriBuilder(settings.Url);
        if (endpoint.Path == "/")
            endpoint.Path = $"/v1beta/models/{model}:streamGenerateContent";

        const string query = "alt=sse";
        if (endpoint.Scheme == Uri.UriSchemeHttps &&
            endpoint.Host.Equals("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
        {
            endpoint.Query = query;
            return (endpoint.Uri.AbsoluteUri, new Options
            {
                Headers = new Dictionary<string, string> { ["x-goog-api-key"] = settings.ApiKey }
            });
        }

        endpoint.Query = $"{query}&key={settings.ApiKey}";
        return (endpoint.Uri.ToString(), null);
    }
}
