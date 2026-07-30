using System;

namespace Conecting
{
    /// <summary>
    /// Application Internationalization and Localization Manager.
    /// Provides dynamic translation helper functions (Spanish / English).
    /// </summary>
    public static class AppI18n
    {
        public static string CurrentLanguage
        {
            get { return PeerResolver.GetSavedLanguage(); }
        }

        public static bool IsEnglish
        {
            get { return CurrentLanguage == "en"; }
        }

        /// <summary>
        /// Returns localized text string based on active language setting.
        /// </summary>
        public static string T(string spanishText, string englishText)
        {
            return IsEnglish ? englishText : spanishText;
        }
    }
}
