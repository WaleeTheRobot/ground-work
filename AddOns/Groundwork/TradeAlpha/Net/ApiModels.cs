using Groundwork.FeaturesEngineering;
using System.Text.Json.Serialization;

namespace NinjaTrader.Custom.AddOns.TradeAlpha.Net
{
    public class PredictRequest
    {
        // Single features engineering bar
        [JsonPropertyName("features_bar")]
        public FeaturesEngineeringBar FeaturesBar { get; set; }

        // Multiple features engineering bars (oldest -> newest)
        [JsonPropertyName("features_bars")]
        public FeaturesEngineeringBar[] FeaturesBars { get; set; }
    }

    public class SignalPrediction
    {
        [JsonPropertyName("prediction")]
        public double Prediction { get; set; }

        [JsonPropertyName("threshold_used")]
        public double ThresholdUsed { get; set; }

        [JsonPropertyName("signal")]
        public string Signal { get; set; }          // "TAKE" or "SKIP"

        [JsonPropertyName("confidence")]
        public string Confidence { get; set; }      // "HIGH", "MEDIUM", "LOW", "VERY_LOW", "ERROR"

        [JsonPropertyName("filtered")]
        public bool Filtered { get; set; }          // True if signal was filtered out

        [JsonPropertyName("filter_reason")]
        public string FilterReason { get; set; }    // Reason for filtering (null if not filtered)
    }

    public class PredictResponse
    {
        [JsonPropertyName("buy")]
        public SignalPrediction Buy { get; set; }

        [JsonPropertyName("sell")]
        public SignalPrediction Sell { get; set; }

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }
}
