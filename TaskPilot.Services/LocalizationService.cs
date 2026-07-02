using Microsoft.AspNetCore.Http;
using System.Text.Json;
using TaskPilot.Models.Common;

namespace TaskPilot.Services
{
    public class LocalizationService : ILocalizationService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Dictionary<string, string> _enResources = new();
        private readonly Dictionary<string, string> _arResources = new();

        public LocalizationService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;

            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var enPath = Path.Combine(baseDir, "Resources", "en.json");
            var arPath = Path.Combine(baseDir, "Resources", "ar.json");
            var jsonOptions = new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip
            };
            if (File.Exists(enPath))
            {
                var enJson = File.ReadAllText(enPath);
                _enResources = JsonSerializer.Deserialize<Dictionary<string, string>>(enJson,jsonOptions) ?? new Dictionary<string, string>();
            }

            if (File.Exists(arPath))
            {
                var arJson = File.ReadAllText(arPath);
                _arResources = JsonSerializer.Deserialize<Dictionary<string, string>>(arJson, jsonOptions) ?? new Dictionary<string, string>();
            }
        }

        public string CurrentLanguage
        {
            get
            {
                var lang = _httpContextAccessor.HttpContext?.Request.Headers["lang"].ToString();
                return string.IsNullOrEmpty(lang) ? "en" : lang.ToLower();
            }
        }

        public string GetString(string key)
        {
            if (CurrentLanguage == "ar" && _arResources.TryGetValue(key, out var arVal))
                return arVal;

            if (_enResources.TryGetValue(key, out var enVal))
                return enVal;

            return key; // Fallback to key if not found
        }

        //public string GetLocalizedProperty(string enValue, string arValue)
        //{
        //    if (CurrentLanguage == "ar")
        //        return !string.IsNullOrEmpty(arValue) ? arValue : enValue;

        //    return !string.IsNullOrEmpty(enValue) ? enValue : arValue;
        //}
    }
}
