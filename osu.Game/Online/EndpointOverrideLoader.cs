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

            public string? APIClientSecret { get; set; }
            public string? APIClientID { get; set; }

            public string? WebsiteUrl { get; set; }
            public string? APIUrl { get; set; }
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

                // Apply only provided values. Validate + normalise the ones that must not have trailing slashes.
                applyString(ref endpoints.APIClientSecret, cfg.APIClientSecret);
                applyString(ref endpoints.APIClientID, cfg.APIClientID);

                applyUrlNoTrailingSlash(ref endpoints.WebsiteUrl, cfg.WebsiteUrl, nameof(cfg.WebsiteUrl));
                applyUrlNoTrailingSlash(ref endpoints.APIUrl, cfg.APIUrl, nameof(cfg.APIUrl));

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
