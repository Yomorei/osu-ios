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

        private sealed class EndpointOverrideFile
        {
            public bool Enabled { get; set; } = true;

            // well.. should be user friendly atleast ? | allow one root URL to drive everything.
            public string? BaseUrl { get; set; }      // preferredddd
            public string? DomainUrl { get; set; }    // aliasss
            public string? ServerUrl { get; set; }    // alias??

            // Optional OAuth
            public string? APIClientSecret { get; set; }
            public string? APIClientID { get; set; }

            // Roots
            public string? WebsiteUrl { get; set; }
            public string? APIUrl { get; set; }

            // Optional direct overrides (advanced)
            public string? BeatmapSubmissionServiceUrl { get; set; }
            public string? SpectatorUrl { get; set; }
            public string? MultiplayerUrl { get; set; }
            public string? MetadataUrl { get; set; }
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

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var cfg = JsonSerializer.Deserialize<EndpointOverrideFile>(json, options);

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

                // If the user provided a single root URL, then it maps it to WebsiteUrl/APIUrl unless explicitly provided :_)
                string? baseUrl = firstNonEmpty(cfg.BaseUrl, cfg.DomainUrl, cfg.ServerUrl);

                bool baseProvided = !string.IsNullOrWhiteSpace(baseUrl);
                bool websiteProvided = !string.IsNullOrWhiteSpace(cfg.WebsiteUrl);
                bool apiProvided = !string.IsNullOrWhiteSpace(cfg.APIUrl);

                if (baseProvided)
                {
                    if (!websiteProvided) cfg.WebsiteUrl = baseUrl;
                    if (!apiProvided) cfg.APIUrl = baseUrl;

                    websiteProvided = !string.IsNullOrWhiteSpace(cfg.WebsiteUrl);
                    apiProvided = !string.IsNullOrWhiteSpace(cfg.APIUrl);
                }

                applyString(ref endpoints.APIClientSecret, cfg.APIClientSecret);
                applyString(ref endpoints.APIClientID, cfg.APIClientID);

                // Apply root URLs (must be absolute.. |  Website/API should not have "/" )
                applyUrlNoTrailingSlash(ref endpoints.WebsiteUrl, cfg.WebsiteUrl, nameof(cfg.WebsiteUrl));
                applyUrlNoTrailingSlash(ref endpoints.APIUrl, cfg.APIUrl, nameof(cfg.APIUrl));

                // If the user touched the root (base/api/website), derive the common sub services
                // unless they explicitly override those fields, but why would they {'_'}
                bool shouldDeriveFromRoot = websiteProvided || apiProvided || baseProvided;

                if (shouldDeriveFromRoot)
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

                // Apply advanced overrides last (so they win over derived defaults :> )
                applyOptionalUrlNoTrailingSlash(ref endpoints.BeatmapSubmissionServiceUrl, cfg.BeatmapSubmissionServiceUrl, nameof(cfg.BeatmapSubmissionServiceUrl));
                applyUrl(ref endpoints.SpectatorUrl, cfg.SpectatorUrl, nameof(cfg.SpectatorUrl));
                applyUrl(ref endpoints.MultiplayerUrl, cfg.MultiplayerUrl, nameof(cfg.MultiplayerUrl));
                applyUrl(ref endpoints.MetadataUrl, cfg.MetadataUrl, nameof(cfg.MetadataUrl));

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

        private static string? firstNonEmpty(params string?[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i]!.Trim();
            }

            return null;
        }

        private static void applyString(ref string target, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                target = value.Trim();
        }

        private static void applyUrlNoTrailingSlash(ref string target, string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            string v = value.Trim().TrimEnd('/');

            if (!Uri.TryCreate(v, UriKind.Absolute, out _))
                throw new InvalidDataException($"{fieldName} must be an absolute URL");

            target = v;
        }

        private static void applyOptionalUrlNoTrailingSlash(ref string? target, string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            string v = value.Trim().TrimEnd('/');

            if (!Uri.TryCreate(v, UriKind.Absolute, out _))
                throw new InvalidDataException($"{fieldName} must be an absolute URL");

            target = v;
        }

        private static void applyUrl(ref string target, string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            string v = value.Trim();

            if (!Uri.TryCreate(v, UriKind.Absolute, out _))
                throw new InvalidDataException($"{fieldName} must be an absolute URL");

            target = v;
        }
    }
}
