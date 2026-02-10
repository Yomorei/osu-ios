#nullable disable

using System;
using System.IO;
using System.Text.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;

namespace osu.Game.Online
{
    public static class EndpointOverrideLoader
    {
        private const string config_file = "endpoints.json";

        private static readonly JsonSerializerOptions read_options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private sealed class EndpointOverrideFile
        {
            public bool Enabled { get; set; } = true;

            public string BaseUrl { get; set; }
            public string DomainUrl { get; set; }
            public string ServerUrl { get; set; }

            public string APIClientSecret { get; set; }
            public string APIClientID { get; set; }

            public string WebsiteUrl { get; set; }
            public string APIUrl { get; set; }

            public string BeatmapSubmissionServiceUrl { get; set; }
            public string SpectatorUrl { get; set; }
            public string MultiplayerUrl { get; set; }
            public string MetadataUrl { get; set; }
        }

        public static bool TryApply(Storage storage, EndpointConfiguration endpoints, out string reason)
        {
            reason = string.Empty;

            try
            {
                if (!storage.Exists(config_file))
                {
                    reason = $"{config_file} not found";
                    return false;
                }

                string fullPath = storage.GetFullPath(config_file);

                if (!File.Exists(fullPath))
                {
                    reason = $"{config_file} missing at resolved path";
                    return false;
                }

                string json = File.ReadAllText(fullPath);
                var cfg = JsonSerializer.Deserialize<EndpointOverrideFile>(json, read_options);

                if (cfg == null)
                {
                    reason = $"{config_file} failed to parse";
                    return false;
                }

                if (!cfg.Enabled)
                {
                    reason = $"{config_file} disabled";
                    return false;
                }

                string baseUrl = firstNonEmpty(cfg.BaseUrl, cfg.DomainUrl, cfg.ServerUrl);

                bool baseProvided = !string.IsNullOrWhiteSpace(baseUrl);
                bool websiteProvided = !string.IsNullOrWhiteSpace(cfg.WebsiteUrl);
                bool apiProvided = !string.IsNullOrWhiteSpace(cfg.APIUrl);

                string websiteUrl = websiteProvided ? cfg.WebsiteUrl : (baseProvided ? baseUrl : null);
                string apiUrl = apiProvided ? cfg.APIUrl : (baseProvided ? baseUrl : null);

                if (!string.IsNullOrWhiteSpace(cfg.APIClientSecret))
                    endpoints.APIClientSecret = cfg.APIClientSecret.Trim();

                if (!string.IsNullOrWhiteSpace(cfg.APIClientID))
                    endpoints.APIClientID = cfg.APIClientID.Trim();

                if (!string.IsNullOrWhiteSpace(websiteUrl))
                    endpoints.WebsiteUrl = normaliseAbsoluteUrlNoTrailingSlash(websiteUrl, nameof(cfg.WebsiteUrl));

                if (!string.IsNullOrWhiteSpace(apiUrl))
                    endpoints.APIUrl = normaliseAbsoluteUrlNoTrailingSlash(apiUrl, nameof(cfg.APIUrl));

                bool shouldDeriveFromRoot = baseProvided || websiteProvided || apiProvided;

                if (shouldDeriveFromRoot && !string.IsNullOrWhiteSpace(endpoints.APIUrl))
                {
                    if (string.IsNullOrWhiteSpace(cfg.SpectatorUrl))
                        endpoints.SpectatorUrl = $"{endpoints.APIUrl}/signalr/spectator";

                    if (string.IsNullOrWhiteSpace(cfg.MultiplayerUrl))
                        endpoints.MultiplayerUrl = $"{endpoints.APIUrl}/signalr/multiplayer";

                    if (string.IsNullOrWhiteSpace(cfg.MetadataUrl))
                        endpoints.MetadataUrl = $"{endpoints.APIUrl}/signalr/metadata";

                    if (string.IsNullOrWhiteSpace(cfg.BeatmapSubmissionServiceUrl))
                        endpoints.BeatmapSubmissionServiceUrl = $"{endpoints.APIUrl}/beatmap-submission";
                }

                if (!string.IsNullOrWhiteSpace(cfg.BeatmapSubmissionServiceUrl))
                    endpoints.BeatmapSubmissionServiceUrl = normaliseAbsoluteUrlNoTrailingSlash(cfg.BeatmapSubmissionServiceUrl, nameof(cfg.BeatmapSubmissionServiceUrl));

                if (!string.IsNullOrWhiteSpace(cfg.SpectatorUrl))
                    endpoints.SpectatorUrl = normaliseAbsoluteUrl(cfg.SpectatorUrl, nameof(cfg.SpectatorUrl));

                if (!string.IsNullOrWhiteSpace(cfg.MultiplayerUrl))
                    endpoints.MultiplayerUrl = normaliseAbsoluteUrl(cfg.MultiplayerUrl, nameof(cfg.MultiplayerUrl));

                if (!string.IsNullOrWhiteSpace(cfg.MetadataUrl))
                    endpoints.MetadataUrl = normaliseAbsoluteUrl(cfg.MetadataUrl, nameof(cfg.MetadataUrl));

                reason = $"{config_file} applied from storage";
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log($"Endpoint override failed: {ex}", LoggingTarget.Runtime, LogLevel.Error);
                reason = "exception while applying override";
                return false;
            }
        }

        private static string firstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i].Trim();
            }

            return null;
        }

        private static string normaliseAbsoluteUrlNoTrailingSlash(string value, string fieldName)
        {
            string v = value.Trim().TrimEnd('/');

            if (!Uri.TryCreate(v, UriKind.Absolute, out _))
                throw new InvalidDataException($"{fieldName} must be an absolute URL");

            return v;
        }

        private static string normaliseAbsoluteUrl(string value, string fieldName)
        {
            string v = value.Trim();

            if (!Uri.TryCreate(v, UriKind.Absolute, out _))
                throw new InvalidDataException($"{fieldName} must be an absolute URL");

            return v;
        }
    }
}
