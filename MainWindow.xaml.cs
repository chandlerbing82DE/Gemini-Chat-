using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Markdig;
using Microsoft.Web.WebView2.Core;
using Mscc.GenerativeAI;

namespace Gemini_WPF
{
    public partial class MainWindow : Window
    {
        private GoogleAI _googleAI = null!;
        private AppSettings _appSettings = new();

        private const string AppSettingsFile = "appsettings.json";
        private const string ChatsHistoryFile = "saved_chats.json";
        private static readonly HttpClient _httpClient = new();

        private List<ChatSessionData> _savedChats = new();
        private ChatSessionData? _activeSession = null;
        private readonly List<string> _selectedFilePaths = new();
        private bool _isLoadingChat = false;
        private bool _isWebViewInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                WebChatView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30); // #1E1E1E

                await WebChatView.EnsureCoreWebView2Async(null);
                _isWebViewInitialized = true;
                
                // ZROBIONE: Obsługa wiadomości zwracanych przez przyciski z HTML
                WebChatView.WebMessageReceived += WebChatView_WebMessageReceived;
                
                InitializeWebViewHtml();

                LoadSettings();

                if (string.IsNullOrWhiteSpace(_appSettings.ApiKey) || _appSettings.ApiKey.Contains("TUTAJ_WKLEJ"))
                {
                    MessageBox.Show("Nie skonfigurowano klucza API Gemini w pliku appsettings.json!\nWejdź do pliku i wklej swój klucz API.", "Brak Klucza API", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                _googleAI = new GoogleAI(_appSettings.ApiKey);

                await LoadModelsAsync();
                LoadSavedChats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania aplikacji: {ex.Message}", "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbModels_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CmbModels.SelectedItem != null && _appSettings.DefaultModel != CmbModels.SelectedItem.ToString())
            {
                _appSettings.DefaultModel = CmbModels.SelectedItem.ToString()!;
                SaveSettings();
            }
        }

        private void InitializeWebViewHtml()
        {
            if (!_isWebViewInitialized) return;

            string baseHtml = @"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8' />
                <style>
                    body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 14px; line-height: 1.6; color: #f5f5f5; margin: 15px; background-color: #1e1e1e; }
                    .user { background-color: #2b3d52; padding: 12px 16px; border-radius: 12px; margin-bottom: 15px; border-left: 5px solid #1e88e5; max-width: 85%; margin-left: auto; box-shadow: 0 1px 2px rgba(0,0,0,0.2); }
                    .ai { background-color: #252526; padding: 12px 16px; border-radius: 12px; margin-bottom: 15px; border-left: 5px solid #43a047; max-width: 85%; margin-right: auto; box-shadow: 0 1px 3px rgba(0,0,0,0.3); }
                    .system { color: #aaaaaa; font-style: italic; text-align: center; margin: 15px 0; font-size: 12px; }
                    .error { background-color: #421f1f; border-left: 5px solid #e53935; color: #ff9494; padding: 10px; border-radius: 8px; margin-bottom: 15px; }
                    .waiting { color: #ffb74d; font-style: italic; font-weight: bold; }
                    pre { background-color: #282c34; color: #abb2bf; padding: 12px; border-radius: 8px; overflow-x: auto; font-family: 'Consolas', monospace; font-size: 13px; }
                    code { background-color: #3e3e42; color: #f48fb1; padding: 2px 6px; border-radius: 4px; font-family: monospace; font-size: 13px; }
                    img { max-width: 100%; border-radius: 6px; margin-top: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.4); }
                    .attachment-badge { display: inline-block; background-color: #333337; border: 1px solid #555555; border-radius: 4px; padding: 4px 8px; margin-top: 5px; font-size: 12px; color: #e0e0e0; }
                    .actions { text-align: right; margin-top: 10px; padding-top: 5px; border-top: 1px solid rgba(255,255,255,0.1); }
                    .action-btn { background: transparent; color: #9cdcfe; border: none; padding: 4px 10px; border-radius: 4px; cursor: pointer; font-size: 11px; margin-left: 5px; font-weight: bold; }
                    .action-btn:hover { background: rgba(255,255,255,0.1); color: #fff; }
                </style>
                <script>
                    function handleAction(actionType, textBytes) {
                        window.chrome.webview.postMessage(JSON.stringify({ action: actionType, textBytes: textBytes }));
                    }
                    function appendMessage(className, htmlContent) {
                        var div = document.createElement('div');
                        div.className = className;
                        div.innerHTML = htmlContent;
                        document.getElementById('chat-container').appendChild(div);
                        window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
                    }
                    function showThinking() {
                        if(document.getElementById('thinking')) return;
                        var div = document.createElement('div');
                        div.className = 'system waiting';
                        div.id = 'thinking';
                        div.innerHTML = '🤖 Gemini analizuje i pisze odpowiedź...';
                        document.getElementById('chat-container').appendChild(div);
                        window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
                    }
                    function removeThinking() {
                        var el = document.getElementById('thinking');
                        if(el) el.remove();
                    }
                    function restoreChat(fullHtml) {
                        document.getElementById('chat-container').innerHTML = fullHtml;
                        window.scrollTo(0, document.body.scrollHeight);
                    }
                    function getChatHtml() {
                        return document.getElementById('chat-container').innerHTML;
                    }
                </script>
            </head>
            <body>
                <div id='chat-container'>
                    <div class='system'>Rozpoczęto nową sesję czatu. Napisz wiadomość poniżej!</div>
                </div>
            </body>
            </html>";
            WebChatView.NavigateToString(baseHtml);
        }

        private async Task AppendMessageToWebViewAsync(string className, string htmlContent)
        {
            if (!_isWebViewInitialized) return;
            string safeClass = JsonSerializer.Serialize(className);
            string safeHtml = JsonSerializer.Serialize(htmlContent);
            await WebChatView.CoreWebView2.ExecuteScriptAsync($"appendMessage({safeClass}, {safeHtml});");
        }

        private async Task ShowThinkingIndicatorAsync(bool show)
        {
            if (!_isWebViewInitialized) return;
            string command = show ? "showThinking();" : "removeThinking();";
            await WebChatView.CoreWebView2.ExecuteScriptAsync(command);
        }

        private async Task<string> GetCurrentChatHtmlAsync()
        {
            if (!_isWebViewInitialized) return string.Empty;
            string rawHtml = await WebChatView.CoreWebView2.ExecuteScriptAsync("getChatHtml();");
            return JsonSerializer.Deserialize<string>(rawHtml) ?? string.Empty;
        }

        private async void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            string userMessage = TxtInput.Text.Trim();
            if (string.IsNullOrEmpty(userMessage) && _selectedFilePaths.Count == 0) return;

            string? selectedModel = CmbModels.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(selectedModel))
            {
                MessageBox.Show("Proszę wybrać model z listy.", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TxtInput.Clear();
            BlockUI(true);

            string safeUserMsgText = System.Net.WebUtility.HtmlEncode(userMessage).Replace("\n", "<br/>");
            string userChatVisualInfo = $"<strong>Ty:</strong><br/>{safeUserMsgText}";

            var parts = new List<IPart>();
            var savedAttachmentsForHistory = new List<FileAttachmentData>();

            foreach (var filePath in _selectedFilePaths)
            {
                if (!File.Exists(filePath)) continue;
                try
                {
                    string ext = Path.GetExtension(filePath).ToLower();
                    string mimeType = GetMimeType(ext);

                    // Textbasierte Dateien
                    if (mimeType.StartsWith("text/") || ext == ".cs" || ext == ".py" || ext == ".json" || ext == ".xml" || ext == ".html" || ext == ".css")
                    {
                        string content = await File.ReadAllTextAsync(filePath);
                        string fileText = $"\n--- Plik: {Path.GetFileName(filePath)} ---\n{content}\n";
                        
                        // ZWINGEND TextData verwenden!
                        parts.Add(new TextData { Text = fileText });
                        
                        savedAttachmentsForHistory.Add(new FileAttachmentData { FileName = Path.GetFileName(filePath), MimeType = "text/plain", Base64Data = fileText });
                        userChatVisualInfo += $"<br/><div class='attachment-badge'>📄 {Path.GetFileName(filePath)}</div>";
                    }
                    else
                    {
                        byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                        string base64Data = Convert.ToBase64String(fileBytes);

                        // ZWINGEND InlineData verwenden!
                        parts.Add(new InlineData { MimeType = mimeType, Data = base64Data });
                        savedAttachmentsForHistory.Add(new FileAttachmentData { FileName = Path.GetFileName(filePath), MimeType = mimeType, Base64Data = base64Data });

                        if (mimeType.StartsWith("image/"))
                            userChatVisualInfo += $"<br/><img src='data:{mimeType};base64,{base64Data}' />";
                        else
                            userChatVisualInfo += $"<br/><div class='attachment-badge'>📎 {Path.GetFileName(filePath)}</div>";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd odczytu pliku {Path.GetFileName(filePath)}: {ex.Message}", "Błąd pliku", MessageBoxButton.OK, MessageBoxImage.Error);
                    BlockUI(false);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(userMessage))
            {
                // ZWINGEND TextData verwenden!
                parts.Add(new TextData { Text = userMessage });
            }

            string base64UserMsg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userMessage));
            userChatVisualInfo += $"<div class='actions'><button class='action-btn' onclick=\"handleAction('edit', '{base64UserMsg}')\">✏️ Edytuj</button><button class='action-btn' onclick=\"handleAction('retry', '{base64UserMsg}')\">🔄 Ponów</button></div>";

            await AppendMessageToWebViewAsync("user", userChatVisualInfo);
            ClearAttachmentList();
            await ShowThinkingIndicatorAsync(true);

            try
            {
                if (selectedModel.Contains("imagen"))
                {
                    var imageModel = _googleAI.ImageGenerationModel(selectedModel);
                    var imgRequest = new GenerateImagesRequest(userMessage, 1);
                    
                    var imgResponse = await imageModel.GenerateImages(imgRequest);
                    await ShowThinkingIndicatorAsync(false);

                    string base64Image = ExtractBase64FromImageResponse(imgResponse);

                    if (!string.IsNullOrEmpty(base64Image))
                    {
                        string aiChatVisualInfo = $"<strong>[{selectedModel}]:</strong><br/>" +
                                                 $"<p>Oto wygenerowany obraz dla zapytania: <em>\"{userMessage}\"</em></p>" +
                                                 $"<img src='data:image/jpeg;base64,{base64Image}' />";

                        await AppendMessageToWebViewAsync("ai", aiChatVisualInfo);

                        bool isNewSession = (_activeSession == null);
                        if (isNewSession)
                        {
                            _activeSession = new ChatSessionData { Title = $"Obraz: {(userMessage.Length > 20 ? userMessage.Substring(0, 20) + "..." : userMessage)}", SelectedModel = selectedModel };
                            _savedChats.Add(_activeSession);
                        }

                        var savedImg = new List<FileAttachmentData>
                        {
                            new FileAttachmentData { FileName = "generated_image.jpg", MimeType = "image/jpeg", Base64Data = base64Image }
                        };

                        _activeSession!.Messages.Add(new ChatMessageData { Role = "user", Text = userMessage });
                        _activeSession.Messages.Add(new ChatMessageData { Role = "model", Text = "[Wygenerowano obraz]", Attachments = savedImg });
                        _activeSession.HtmlBody = await GetCurrentChatHtmlAsync();

                        SaveAllChatsToFile();

                        if (isNewSession)
                        {
                            RefreshHistoryListBox();
                            _isLoadingChat = true;
                            LstHistory.SelectedItem = _activeSession;
                            _isLoadingChat = false;
                        }
                    }
                    else
                    {
                        await AppendMessageToWebViewAsync("error", "<strong>Błąd:</strong> Model nie wygenerował obrazu (lub biblioteka zmieniła format odpowiedzi).");
                    }
                }
                else
                {
                    var model = _googleAI.GenerativeModel(selectedModel);
                    var request = new GenerateContentRequest { Contents = BuildSdkHistory(_activeSession?.Messages ?? new List<ChatMessageData>()) };
                    
                    request.Contents.Add(new Content("user") { Parts = parts });

                    var response = await model.GenerateContent(request);
                    await ShowThinkingIndicatorAsync(false);

                    string responseText = response.Text ?? string.Empty;
                    string renderedAiHtml = "";

                    if (!string.IsNullOrWhiteSpace(responseText))
                    {
                        string safeText = responseText;

                        // KORREKTUR: Finde das absolut erste Vorkommen von ```markdown und das absolut letzte Vorkommen von ```
                        int tagIndex = safeText.IndexOf("```markdown");
                        int tagLength = 11;
                        if (tagIndex == -1)
                        {
                            tagIndex = safeText.IndexOf("```md");
                            tagLength = 5;
                        }

                        if (tagIndex != -1)
                        {
                            int lastTickIndex = safeText.LastIndexOf("```");
                            // Stellen Sie sicher, dass der gefundene Schlusstag nach dem Starttag liegt
                            if (lastTickIndex > tagIndex + tagLength)
                            {
                                // Ersetze die äußere Umhüllung durch 4 Backticks (````). 
                                // Markdig erkennt dadurch, dass jegliche inneren 3 Backticks (wie ```bash) als reiner Text zu behandeln sind.
                                safeText = safeText.Substring(0, tagIndex) 
                                         + "````markdown\n" 
                                         + safeText.Substring(tagIndex + tagLength, lastTickIndex - (tagIndex + tagLength)).Trim() 
                                         + "\n````\n" 
                                         + safeText.Substring(lastTickIndex + 3).Trim();
                            }
                        }

                        var markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                        renderedAiHtml = Markdown.ToHtml(safeText, markdownPipeline);
                    }

                    var aiAttachmentsForHistory = new List<FileAttachmentData>();
                    if (response.Candidates != null)
                    {
                        foreach (var candidate in response.Candidates)
                        {
                            if (candidate.Content?.Parts != null)
                            {
                                foreach (var part in candidate.Content.Parts)
                                {
                                    if (part is Part p && p.InlineData != null && !string.IsNullOrEmpty(p.InlineData.Data))
                                    {
                                        string mime = p.InlineData.MimeType ?? "image/jpeg";
                                        string base64 = p.InlineData.Data;
                                        renderedAiHtml += $"<br/><img src='data:{mime};base64,{base64}' />";
                                        aiAttachmentsForHistory.Add(new FileAttachmentData { FileName = "AI_Generated_Image", MimeType = mime, Base64Data = base64 });
                                    }
                                }
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(renderedAiHtml)) renderedAiHtml = "<em>(Brak tekstu ani obrazka z API)</em>";

                    string base64AiMsg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(responseText));
                    
                    string aiChatVisualInfo = $"<strong>[{selectedModel}]:</strong><br/>{renderedAiHtml}";
                    aiChatVisualInfo += $"<div class='actions'><button class='action-btn' onclick=\"handleAction('copy', '{base64AiMsg}')\">📋 Kopiuj</button></div>";
                    
                    await AppendMessageToWebViewAsync("ai", aiChatVisualInfo);

                    bool isNewSession = (_activeSession == null);
                    if (isNewSession)
                    {
                        _activeSession = new ChatSessionData { Title = $"Czat: {DateTime.Now:dd-MM-yyyy HH:mm:ss}", SelectedModel = selectedModel };
                        _savedChats.Add(_activeSession);
                    }

                    _activeSession!.Messages.Add(new ChatMessageData { Role = "user", Text = userMessage, Attachments = savedAttachmentsForHistory });
                    _activeSession.Messages.Add(new ChatMessageData { Role = "model", Text = responseText, Attachments = aiAttachmentsForHistory });
                    _activeSession.HtmlBody = await GetCurrentChatHtmlAsync();

                    SaveAllChatsToFile();

                    if (isNewSession)
                    {
                        RefreshHistoryListBox();
                        _isLoadingChat = true;
                        LstHistory.SelectedItem = _activeSession;
                        _isLoadingChat = false;
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowThinkingIndicatorAsync(false);
                await AppendMessageToWebViewAsync("error", $"<strong>Wystąpił błąd komunikacji z API:</strong><br/>{ex.Message}");
            }
            finally
            {
                BlockUI(false);
                TxtInput.Focus();
            }
        }

        private string ExtractBase64FromImageResponse(object response)
        {
            if (response == null) return string.Empty;
            var type = response.GetType();

            foreach (var propName in new[] { "GeneratedImages", "Images", "Predictions", "Results", "Candidates" })
            {
                var prop = type.GetProperty(propName);
                if (prop != null)
                {
                    var list = prop.GetValue(response) as System.Collections.IEnumerable;
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            if (item == null) continue;
                            var itemType = item.GetType();

                            foreach (var byteProp in new[] { "ImageBytes", "BytesBase64Encoded", "Base64", "Data", "Image" })
                            {
                                var bp = itemType.GetProperty(byteProp);
                                if (bp != null)
                                {
                                    var val = bp.GetValue(item);
                                    if (val is string s && !string.IsNullOrEmpty(s)) return s;
                                    if (val is byte[] b && b.Length > 0) return Convert.ToBase64String(b);

                                    if (val != null && byteProp == "Image")
                                    {
                                        var nestedType = val.GetType();
                                        foreach (var nestedProp in new[] { "ImageBytes", "BytesBase64Encoded", "Base64", "Data" })
                                        {
                                            var np = nestedType.GetProperty(nestedProp);
                                            if (np != null)
                                            {
                                                var nVal = np.GetValue(val);
                                                if (nVal is string ns && !string.IsNullOrEmpty(ns)) return ns;
                                                if (nVal is byte[] nb && nb.Length > 0) return Convert.ToBase64String(nb);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return string.Empty;
        }

        private void TxtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                BtnSend_Click(this, null!);
                e.Handled = true;
            }
        }

        private string GetMimeType(string ext) => ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "text/plain"
        };

        private List<Content> BuildSdkHistory(List<ChatMessageData> savedMessages)
        {
            var history = new List<Content>();
            foreach (var msg in savedMessages)
            {
                var parts = new List<IPart>();

                foreach (var att in msg.Attachments)
                {
                    bool isText = att.MimeType == "text/plain" || (att.Base64Data != null && att.Base64Data.Contains("--- Plik:"));

                    if (isText && !string.IsNullOrEmpty(att.Base64Data))
                    {
                        parts.Add(new TextData { Text = att.Base64Data });
                    }
                    else if (att.MimeType != null && att.Base64Data != null)
                    {
                        // KORREKTUR: Usuwamy błędny prefiks "data:...;base64," jeśli istnieje w historii!
                        string cleanBase64 = att.Base64Data;
                        int prefixIndex = cleanBase64.IndexOf("base64,");
                        if (prefixIndex >= 0)
                        {
                            cleanBase64 = cleanBase64.Substring(prefixIndex + 7);
                        }

                        parts.Add(new InlineData { MimeType = att.MimeType, Data = cleanBase64 });
                    }
                }
                if (!string.IsNullOrEmpty(msg.Text))
                {
                    parts.Add(new TextData { Text = msg.Text });
                }

                history.Add(new Content(string.IsNullOrEmpty(msg.Role) ? "user" : msg.Role) { Parts = parts });
            }
            return history;
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(AppSettingsFile))
                {
                    string json = File.ReadAllText(AppSettingsFile);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, AppSettings>>(json);
                    if (dict != null && dict.TryGetValue("AppSettings", out var settings))
                    {
                        _appSettings = settings;
                        return;
                    }
                }
            }
            catch { }
            _appSettings = new AppSettings { ApiKey = "TUTAJ_WKLEJ_SWOJ_KLUCZ_API_GEMINI" };
            SaveSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var wrapper = new { AppSettings = _appSettings };
                File.WriteAllText(AppSettingsFile, JsonSerializer.Serialize(wrapper, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void LoadSavedChats()
        {
            try
            {
                if (File.Exists(ChatsHistoryFile))
                {
                    string json = File.ReadAllText(ChatsHistoryFile);
                    _savedChats = JsonSerializer.Deserialize<List<ChatSessionData>>(json) ?? new List<ChatSessionData>();
                    RefreshHistoryListBox();
                }
            }
            catch { }
        }

        private void SaveAllChatsToFile()
        {
            try
            {
                File.WriteAllText(ChatsHistoryFile, JsonSerializer.Serialize(_savedChats, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private async Task LoadModelsAsync()
        {
            CmbModels.Items.Clear();
            try
            {
                if (string.IsNullOrWhiteSpace(_appSettings.ApiKey) || _appSettings.ApiKey.Contains("TUTAJ_WKLEJ"))
                    throw new Exception("Brak klucza API.");

                string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={_appSettings.ApiKey}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                string jsonResponse = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);

                if (doc.RootElement.TryGetProperty("models", out JsonElement modelsElement))
                {
                    foreach (JsonElement model in modelsElement.EnumerateArray())
                    {
                        if (model.TryGetProperty("name", out JsonElement nameElement))
                        {
                            string? modelName = nameElement.GetString()?.Replace("models/", "");
                            if (!string.IsNullOrEmpty(modelName)) CmbModels.Items.Add(modelName);
                        }
                    }
                }
            }
            catch
            {
                string[] defaultModels = { "gemini-2.5-flash", "gemini-1.5-flash", "gemini-1.5-pro", "imagen-3.0-generate-002" };
                foreach (var m in defaultModels) CmbModels.Items.Add(m);
            }

            int defaultIdx = CmbModels.Items.IndexOf(_appSettings.DefaultModel);
            CmbModels.SelectedIndex = defaultIdx >= 0 ? defaultIdx : 0;
        }

        private void BlockUI(bool block)
        {
            BtnSend.IsEnabled = !block;
            TxtInput.IsReadOnly = block;
            BtnAttach.IsEnabled = !block;
            CmbModels.IsEnabled = !block;
            LstHistory.IsEnabled = !block;
            BtnClear.IsEnabled = !block;
            this.Cursor = block ? Cursors.Wait : Cursors.Arrow;
        }

        private void RefreshHistoryListBox()
        {
            LstHistory.ItemsSource = null;
            LstHistory.ItemsSource = _savedChats;
        }

        private async void LstHistory_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isLoadingChat) return;
            if (LstHistory.SelectedItem is ChatSessionData selectedSession)
            {
                _isLoadingChat = true;
                _activeSession = selectedSession;

                int modelIdx = CmbModels.Items.IndexOf(_activeSession.SelectedModel);
                if (modelIdx >= 0) CmbModels.SelectedIndex = modelIdx;

                if (_isWebViewInitialized)
                {
                    string safeHtml = JsonSerializer.Serialize(_activeSession.HtmlBody);
                    string command = $"restoreChat({safeHtml});";
                    await WebChatView.CoreWebView2.ExecuteScriptAsync(command);
                }
                _isLoadingChat = false;
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _activeSession = null;
            LstHistory.SelectedIndex = -1;
            InitializeWebViewHtml();
            ClearAttachmentList();
            TxtInput.Clear();
            TxtInput.Focus();
        }

        private void BtnDeleteChat_Click(object sender, RoutedEventArgs e)
        {
            if (LstHistory.SelectedItem is ChatSessionData selectedChat)
            {
                _savedChats.Remove(selectedChat);
                SaveAllChatsToFile();
                if (_activeSession == selectedChat) BtnClear_Click(null!, null!);
                else RefreshHistoryListBox();
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadModelsAsync();

        private void LblAttachment_Click(object sender, MouseButtonEventArgs e) => ClearAttachmentList();

        private void BtnAttach_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { Filter = "Pliki multimedialne i dokumenty|*.png;*.jpg;*.jpeg;*.webp;*.pdf;*.txt;*.cs;*.json;*.xml;*.py;*.html;*.css|Wszystkie pliki|*.*", Multiselect = true };
            if (ofd.ShowDialog() == true)
            {
                foreach (var path in ofd.FileNames)
                {
                    if (!_selectedFilePaths.Contains(path)) _selectedFilePaths.Add(path);
                }
                UpdateAttachmentsUI();
            }
        }

        private void UpdateAttachmentsUI()
        {
            if (_selectedFilePaths.Count > 0)
            {
                LblAttachment.Visibility = Visibility.Visible;
                LblAttachment.Text = $"Załączono ({_selectedFilePaths.Count}) plików. Kliknij tu, aby wyczyścić.";
                
                // \uE723 ist der Unicode für das Büroklammer-Icon (Attach)
                BtnAttach.Content = "\uE723"; 
            }
            else
            {
                LblAttachment.Visibility = Visibility.Collapsed;
                
                // \uE710 ist der Unicode für das Plus-Icon (Add)
                BtnAttach.Content = "\uE710"; 
            }
        }

        private void ClearAttachmentList()
        {
            _selectedFilePaths.Clear();
            UpdateAttachmentsUI();
        }

        // NOWA METODA: Czeka na kliknięcia użytkownika w podglądzie HTML
        private async void WebChatView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string json = e.TryGetWebMessageAsString();
                using JsonDocument doc = JsonDocument.Parse(json);
                string action = doc.RootElement.GetProperty("action").GetString() ?? "";
                string base64Text = doc.RootElement.GetProperty("textBytes").GetString() ?? "";
                
                string decodedText = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Text));

                switch (action)
                {
                    case "copy":
                        Clipboard.SetText(decodedText);
                        break;
                    case "edit":
                        TxtInput.Text = decodedText;
                        TxtInput.Focus();
                        break;
                    case "retry":
                        TxtInput.Text = decodedText;
                        BtnSend_Click(this, null!);
                        break;
                }
            }
            catch { }
        }
    }
}