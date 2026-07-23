using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace Gemini_WPF
{
    public class AppSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string DefaultModel { get; set; } = "gemini-2.5-flash";
    }

    public class FileAttachmentData
    {
        public string FileName { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public string Base64Data { get; set; } = string.Empty;
    }

    public class ChatMessageData
    {
        public string Role { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public List<FileAttachmentData> Attachments { get; set; } = new();
    }

    public class ChatSessionData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public List<ChatMessageData> Messages { get; set; } = new();
        public string HtmlBody { get; set; } = string.Empty;
        public string SelectedModel { get; set; } = string.Empty;

        // NOWOŚĆ: Przechowuje identyfikator typu "cachedContents/ab12cd34ef56"
        public string? CachedContentName { get; set; }

        public override string ToString() => Title;
    }

    public class ApiRateLimiter
    {
        private readonly List<DateTime> _requestTimestamps = new();
        private readonly List<(DateTime Timestamp, int Tokens)> _tokenEntries = new();
        private readonly object _lock = new();

        // Rejestruje nowe zapytanie i liczbę przetworzonych tokenów (Input + Output)
        public void RegisterRequest(int totalTokens)
        {
            lock (_lock)
            {
                var now = DateTime.Now;
                _requestTimestamps.Add(now);
                _tokenEntries.Add((now, totalTokens));
                Cleanup();
            }
        }

        // Pobiera bieżące RPM oraz TPM z ostatnich 60 sekund
        public (int rpm, int tpm) GetCurrentStats()
        {
            lock (_lock)
            {
                Cleanup();
                int rpm = _requestTimestamps.Count;
                int tpm = _tokenEntries.Sum(e => e.Tokens);
                return (rpm, tpm);
            }
        }

        // Usuwa wpisy starsze niż 60 sekund
        private void Cleanup()
        {
            var cutoff = DateTime.Now.AddSeconds(-60);
            _requestTimestamps.RemoveAll(ts => ts < cutoff);
            _tokenEntries.RemoveAll(entry => entry.Timestamp < cutoff);
        }
                public class PastedImageData
        {
            public BitmapSource Bitmap { get; set; } = null!;
            public string Base64 { get; set; } = string.Empty;
            public string MimeType { get; set; } = string.Empty;
        }
    }

}