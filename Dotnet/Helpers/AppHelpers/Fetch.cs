using System.Diagnostics;
using Jint;

namespace Fenrus.Helpers.AppHelpers;

/// <summary>
/// Fetch helper
/// </summary>
public class Fetch
{
    public class FetchArgs
    {
        public Engine Engine { get; set; }
        public string AppUrl { get; set; }
        public object Parameters { get; set; }
        public Action<string> Log { get; set; }
    }
    
    /// <summary>
    /// Gets an instance of the fetch helper
    /// </summary>
    public static async Task<object> Execute(FetchArgs args)
    {
        try
        {
            var engine = args.Engine;
            var appUrl = args.AppUrl;
            var parameters = args.Parameters;

            using HttpClient client = new HttpClient();
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            string url;
            if (parameters is string str)
            {
                url = str;
                request.Headers.Add("Accept", "application/json");
            }
            else
            {
                var fp = JsonSerializer.Deserialize<FetchParameters>(
                    JsonSerializer.Serialize(parameters)
                    , new JsonSerializerOptions()
                    {
                        PropertyNameCaseInsensitive = true
                    });
                url = fp.Url;
                fp.Headers ??= new();
                if (fp.Headers.ContainsKey("Accept") == false)
                    fp.Headers.Add("Accept", "application/json");
                foreach (var header in fp.Headers)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                request.Method = fp.Method?.ToLower() switch
                {
                    "post" => HttpMethod.Post,
                    "put" => HttpMethod.Put,
                    "delete" => HttpMethod.Delete,
                    "patch" => HttpMethod.Patch,
                    _ => HttpMethod.Get
                };
            }

            if (url.StartsWith("http") == false)
            {
                if (appUrl.EndsWith('/') == false)
                    url = appUrl + '/' + url;
                else
                    url = appUrl + url;
            }

            args.Log("URL: " + url);
            request.RequestUri = new Uri(url);
            var result = await client.SendAsync(request);
            string content = await result.Content.ReadAsStringAsync();
            //return content;

            var trimmed = content.Trim();
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
            {
                try
                {
                    engine.SetValue("temp_json", content);
                    var parsed = engine.Evaluate("JSON.parse(temp_json)").ToObject();
                    return parsed;
                }
                catch (Exception ex)
                {
                    return content;
                }
            }

            if (trimmed == "true") return true;
            if (trimmed == "false") return false;
            if (double.TryParse(trimmed, out double dbl))
                return dbl;
            return content;
        }
        catch (Exception ex)
        {
            // just want to see the exception
            throw;
        }
    }

    /// <summary>
    /// Fetch parameters
    /// </summary>
    class FetchParameters
    {
        /// <summary>
        /// Gets or sets the URL to get
        /// </summary>
        public string Url { get; set;}
        /// <summary>
        /// Gets or sets timeout in seconds
        /// </summary>
        public int Timeout { get; set; }
        
        /// <summary>
        /// Gets or sets the request Method
        /// </summary>
        public string Method { get; set; }
        
        /// <summary>
        /// Gets or sets request headers
        /// </summary>
        public Dictionary<string, string> Headers { get; set; }
    }
}