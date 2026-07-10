using System;
using System.Collections.Generic;

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

        public override string ToString() => Title;
    }
}