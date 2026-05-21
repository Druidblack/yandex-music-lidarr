using System.Collections.Generic;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.YandexMusic
{
    public class YandexMusicIndexerSettingsValidator : AbstractValidator<YandexMusicIndexerSettings>
    {
        public YandexMusicIndexerSettingsValidator()
        {
            RuleFor(x => x.OAuthToken)
                .NotEmpty()
                .WithMessage("OAuth token is required. See settings help for how to obtain one.");
        }
    }

    public class YandexMusicIndexerSettings : IIndexerSettings
    {
        private static readonly YandexMusicIndexerSettingsValidator Validator = new();

        public YandexMusicIndexerSettings()
        {
            BaseUrl = "https://api.music.yandex.net";
            PreferredQuality = (int)YandexMusicQualityOption.Lossless;
        }

        [FieldDefinition(0, Label = "OAuth Token", Type = FieldType.Password,
            Privacy = PrivacyLevel.Password,
            HelpText = "Long-lived Yandex OAuth token. Obtain it from https://oauth.yandex.ru/authorize" +
                       "?response_type=token&client_id=23cabbbdc6cd418abb4b39c32c41195d - the access_token" +
                       " fragment from the redirected URL is what goes here.")]
        public string OAuthToken { get; set; } = string.Empty;

        [FieldDefinition(1, Label = "Preferred Quality", Type = FieldType.Select,
            SelectOptions = typeof(YandexMusicQualityOption),
            HelpText = "Quality tier the indexer advertises in search results. Lossless requires" +
                       " an active Yandex Plus subscription on the authenticated account.")]
        public int PreferredQuality { get; set; }

        [FieldDefinition(2, Label = "Early Download Limit", Type = FieldType.Number,
            Unit = "days", Advanced = true,
            HelpText = "How many days before the release date Lidarr is allowed to download from this" +
                       " indexer. Leave empty for no limit.")]
        public int? EarlyReleaseLimit { get; set; }

        [FieldDefinition(3, Label = "Base URL", Type = FieldType.Textbox,
            Advanced = true, Hidden = HiddenType.HiddenIfNotSet,
            HelpText = "API base URL. Leave at the default unless you are routing through a mirror.")]
        public string BaseUrl { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }

    public enum YandexMusicQualityOption
    {
        [FieldOption(Label = "Low (AAC 64 kbps)")]
        Low = 0,

        [FieldOption(Label = "Normal (AAC 192 kbps)")]
        Normal = 1,

        [FieldOption(Label = "Lossless (FLAC)")]
        Lossless = 2,
    }
}
