#nullable disable

using System;
using System.IO;
using System.Text.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Online
{
    public partial class PrivateServersSettings : SettingsSubsection
    {
        protected override LocalisableString Header => "Private servers";

        private const string config_file = "endpoints.json";

        private static readonly JsonSerializerOptions read_options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        private static readonly JsonSerializerOptions write_options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private readonly Bindable<string> baseUrl = new Bindable<string>();
        private readonly Bindable<string> apiUrl = new Bindable<string>();
        private readonly Bindable<string> clientId = new Bindable<string>();
        private readonly Bindable<string> clientSecret = new Bindable<string>();

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
                Caption = "Server base url",
                HintText = "Website + API root. Must be an absolute HTTPS URL.",
                PlaceholderText = "https://yourserver.tld",
                Current = baseUrl
            };

            apiUrlTextBox = new FormTextBox
            {
                Caption = "API url (advanced)",
                HintText = "If your API root differs from the base url.",
                PlaceholderText = "(optional) defaults to base url",
                Current = apiUrl
            };

            clientIdTextBox = new FormTextBox
            {
                Caption = "OAuth client id (advanced)",
                HintText = "Only required if your server expects OAuth like official.",
                PlaceholderText = "(optional)",
                Current = clientId
            };

            clientSecretTextBox = new FormTextBox
            {
                Caption = "OAuth client secret (advanced)",
                HintText = "Only required if your server expects OAuth like official.",
                PlaceholderText = "(optional)",
                Current = clientSecret
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
                    Current =
                    {
                        Value = new SettingsNote.Data(
                            "Configure a custom server for online features. Restart the game after saving.",
                            SettingsNote.Type.Informational)
                    }
                },

                new SettingsItemV2(baseUrlTextBox),
                new SettingsItemV2(apiUrlTextBox),
                new SettingsItemV2(clientIdTextBox),
                new SettingsItemV2(clientSecretTextBox),

                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Y,
                    RelativeSizeAxes = Axes.X,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10, 0),
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

            public string BaseUrl { get; set; }
            public string APIUrl { get; set; }
            public string APIClientID { get; set; }
            public string APIClientSecret { get; set; }
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
                var cfg = JsonSerializer.Deserialize<EndpointConfigForDisk>(json, read_options);

                if (cfg == null)
                    return;

                baseUrl.Value = cfg.BaseUrl ?? string.Empty;
                apiUrl.Value = cfg.APIUrl ?? string.Empty;
                clientId.Value = cfg.APIClientID ?? string.Empty;
                clientSecret.Value = cfg.APIClientSecret ?? string.Empty;
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
                string baseUrlValue = (baseUrl.Value ?? string.Empty).Trim();
                string apiUrlValue = (apiUrl.Value ?? string.Empty).Trim();
                string clientIdValue = (clientId.Value ?? string.Empty).Trim();
                string clientSecretValue = (clientSecret.Value ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(baseUrlValue))
                    throw new InvalidDataException("Base url is required.");

                baseUrlValue = normaliseAbsoluteUrlNoTrailingSlash(baseUrlValue, "base url");

                if (!string.IsNullOrWhiteSpace(apiUrlValue))
                    apiUrlValue = normaliseAbsoluteUrlNoTrailingSlash(apiUrlValue, "api url");

                var cfg = new EndpointConfigForDisk
                {
                    Enabled = true,
                    BaseUrl = baseUrlValue,
                    APIUrl = string.IsNullOrWhiteSpace(apiUrlValue) ? null : apiUrlValue,
                    APIClientID = string.IsNullOrWhiteSpace(clientIdValue) ? null : clientIdValue,
                    APIClientSecret = string.IsNullOrWhiteSpace(clientSecretValue) ? null : clientSecretValue
                };

                string json = JsonSerializer.Serialize(cfg, write_options);

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

                baseUrl.Value = string.Empty;
                apiUrl.Value = string.Empty;
                clientId.Value = string.Empty;
                clientSecret.Value = string.Empty;

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
