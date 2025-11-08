using Groundwork.FeaturesEngineering;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NinjaTrader.Custom.AddOns.TradeAlpha.Net
{

    public class ApiClient : IDisposable
    {
        private static readonly HttpClient SharedHttp;
        private readonly string _predictUrl;
        private readonly string _healthUrl;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly int _timeoutMs;
        private readonly int _retries;

        static ApiClient()
        {
            var handler = new HttpClientHandler { UseCookies = false };
            SharedHttp = new HttpClient(handler);
            // Don’t set a global timeout; we’ll cancel per-request with CTS
        }

        public ApiClient(string baseUrl, int timeoutMs = 5000, int retries = 1)
        {
            string safeBase = (baseUrl ?? "").TrimEnd('/');
            _predictUrl = $"{safeBase}/predict";
            _healthUrl = $"{safeBase}/health";
            _timeoutMs = Math.Max(500, timeoutMs);
            _retries = Math.Max(0, retries);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        public void Dispose() { /* no-op: we reuse SharedHttp */ }

        public bool HealthCheck()
        {
            try
            {
                using (var cts = new CancellationTokenSource(_timeoutMs))
                {
                    var resp = SharedHttp.GetAsync(_healthUrl, cts.Token).GetAwaiter().GetResult();
                    return resp.IsSuccessStatusCode;
                }
            }
            catch { return false; }
        }

        // Main prediction method - single-bar
        public PredictResponse Predict(FeaturesEngineeringBar featuresBar)
        {
            var req = new PredictRequest
            {
                FeaturesBar = featuresBar,
                FeaturesBars = new[] { featuresBar }
            };
            return PredictInternal(req);
        }

        // Multi-bar prediction (send last N features engineering bars)
        public PredictResponse Predict(FeaturesEngineeringBar[] featuresBars)
        {
            var bars = featuresBars ?? Array.Empty<FeaturesEngineeringBar>();
            var req = new PredictRequest
            {
                FeaturesBar = bars.Length > 0 ? bars[bars.Length - 1] : default,
                FeaturesBars = bars
            };
            return PredictInternal(req);
        }

        private PredictResponse PredictInternal(PredictRequest req)
        {
            string json = JsonSerializer.Serialize(req, _jsonOptions);

            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                    using (var cts = new CancellationTokenSource(_timeoutMs))
                    {
                        var httpTask = SharedHttp.PostAsync(_predictUrl, content, cts.Token);
                        var resp = httpTask.GetAwaiter().GetResult();
                        string body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        if (!resp.IsSuccessStatusCode)
                            return new PredictResponse { Error = $"HTTP {(int)resp.StatusCode}: {body}" };

                        var parsed = JsonSerializer.Deserialize<PredictResponse>(body, _jsonOptions);
                        return parsed ?? new PredictResponse { Error = "Empty/invalid JSON" };
                    }
                }
                catch (TaskCanceledException)
                {
                    if (attempt > _retries)
                        return new PredictResponse { Error = $"Timeout after {_timeoutMs}ms (attempt {attempt}) to {_predictUrl}" };
                    // brief backoff
                    System.Threading.Thread.Sleep(100);
                    continue;
                }
                catch (Exception ex)
                {
                    return new PredictResponse { Error = ex.Message };
                }
            }
        }
    }
}
