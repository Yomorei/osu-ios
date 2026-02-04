using System;
using System.IO;
using System.Text.Json;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;

namespace osu.Game.Overlays.Settings.Sections.Online
{
    public partial class PrivateServersSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Private servers";

        private const string config_file = "endpoints.json";

        private FormTextBox baseUrlTextBox = null!;
        private FormTextBox apiUrlTextBox = null!;
        private FormTextBox clientIdTextBox = null!;
        private FormTextBox clientSecretTextBox = null!;

        private SettingsButtonV2 saveButton = null!;
        private SettingsButtonV2 resetButton = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            baseUrlTextBox = new FormTextBox
            {
                PlaceholderText = "https://yourserver.tld",
            };

            apiUrlTextBox = new FormTextBox
            {
                PlaceholderText = "(optional) defaults to base url",
            };

            clientIdTextBox = new FormTextBox
            {
                PlaceholderText = "(optional)",
            };

            clientSecretTextBox = new FormTextBox
            {
                PlaceholderText = "(optional)",
            };

            saveButton = new SettingsButtonV2
            {
                Text = "Save",
                Action = save
            };

            resetButton = new SettingsButtonV2
            {
                Text = "Reset",
                Action = reset
            };

            Children = new Drawable[]
            {
                new SettingsNote
                {
                    Text = "Configure a custom server for online features. Restart the game after saving."
                },

                new SettingsItemV2(baseUrlTextBox)
                {
                    Caption = "Server base url",
                    TooltipText = "Website + API root. Must be an absolute HTTPS URL."
                },

                new SettingsItemV2(apiUrlTextBox)
                {
                    Caption = "API url (advanced)",
                    TooltipText = "If your API root differs from the base url."
                },

                new SettingsItemV2(clientIdTextBox)
                {
                    Caption = "OAuth client id (advanced)",
                    TooltipText = "Only required if your server expects OAuth like official."
                },

                new SettingsItemV2(clientSecretTextBox)
                {
                    Caption = "OAuth client secret (advanced)",
                    TooltipText = "Only required if your server expects OAuth like official."
                },

                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Direction = FillDirection.Horizontal,
                    Spacing = new osuTK.Vector2(10, 0),
                    Children = new Drawable[]
                    {
                        saveButton,
                        resetButton
                    }
                }
            };

            loadFromDiskIntoUI();
        }

        private sealed class EndpointConfigForDisk
        {
            public bool Enabled { get; set; } = true;

            public string? BaseUrl { get; set; }

            public string? APIUrl { get; set; }
            public string? APIClientID { get; set; }
            public string? APIClientSecret { get; set; }
        }

        private void loadFromDiskIntoUI()
        {
            try
            {
                if (!storage.Exists(config_file))
                    return;

                string fullPath = storage.GetFullPath(config_file);
                if (!File.Exists(fullPath))
                    return;

                string json = File.ReadAllText(fullPath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var cfg = JsonSerializer.Deserialize<EndpointConfigForDisk>(json, options);
                if (cfg == null)
                    return;

                baseUrlTextBox.Text = cfg.BaseUrl ?? string.Empty;
                apiUrlTextBox.Text = cfg.APIUrl ?? string.Empty;
                clientIdTextBox.Text = cfg.APIClientID ?? string.Empty;
                clientSecretTextBox.Text = cfg.APIClientSecret ?? string.Empty;
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed reading {config_file}: {ex}", LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        private void save()
        {
            try
            {
                string baseUrl = (baseUrlTextBox.Text ?? string.Empty).Trim();
                string apiUrl = (apiUrlTextBox.Text ?? string.Empty).Trim();
                string clientId = (clientIdTextBox.Text ?? string.Empty).Trim();
                string clientSecret = (clientSecretTextBox.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(baseUrl))
                    throw new InvalidDataException("Base url is required.");

                baseUrl = normaliseAbsoluteUrlNoTrailingSlash(baseUrl, "base url");

                if (!string.IsNullOrWhiteSpace(apiUrl))
                    apiUrl = normaliseAbsoluteUrlNoTrailingSlash(apiUrl, "api url");

                var cfg = new EndpointConfigForDisk
                {
                    Enabled = true,
                    BaseUrl = baseUrl,
                    APIUrl = string.IsNullOrWhiteSpace(apiUrl) ? null : apiUrl,
                    APIClientID = string.IsNullOrWhiteSpace(clientId) ? null : clientId,
                    APIClientSecret = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(cfg, options);

                string fullPath = storage.GetFullPath(config_file);
                File.WriteAllText(fullPath, json);

                Logger.Log($"Wrote {config_file} to: {fullPath}", LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed writing {config_file}: {ex}", LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        private void reset()
        {
            try
            {
                string fullPath = storage.GetFullPath(config_file);

                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                baseUrlTextBox.Text = string.Empty;
                apiUrlTextBox.Text = string.Empty;
                clientIdTextBox.Text = string.Empty;
                clientSecretTextBox.Text = string.Empty;

                Logger.Log($"{config_file} removed: {fullPath}", LoggingTarget.Runtime, LogLevel.Important);
            }
            catch (Exception ex)
            {
                Logger.Log($"Failed removing {config_file}: {ex}", LoggingTarget.Runtime, LogLevel.Error);
            }
        }

        private static string normaliseAbsoluteUrlNoTrailingSlash(string value, string fieldName)
        {
            string v = value.Trim().TrimEnd('/');

            if (!Uri.TryCreate(v, UriKind.Absolute, out var uri))
                throw new InvalidDataException($"{fieldName} must be an absolute URL.");

            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"{fieldName} must be HTTPS for iOS.");

            return v;
        }
    }
}
