using Markdig;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using Mscc.GenerativeAI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Imaging;

// Rozwiązanie konfliktu CS0104: jednoznaczne wskazanie kontrolki WPF Image
using Image = System.Windows.Controls.Image;


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

        // Liczniki tokenów i kosztów dla bieżącej sesji
        private int _totalSessionInputTokens = 0;
        private int _totalSessionOutputTokens = 0;
        private decimal _totalSessionCostUsd = 0m;

        // Słownik przechowywujący stawki pobrane dynamicznie z LiteLLM
        private readonly Dictionary<string, ModelRate> _dynamicRates = new(StringComparer.OrdinalIgnoreCase);
        private const string LiteLlmPricingUrl = "https://raw.githubusercontent.com/BerriAI/litellm/main/model_prices_and_context_window.json";

        // Lista przechowująca WSZYSTKIE wklejone obrazy ze schowka
        private readonly List<PastedImageData> _pastedImages = new();
        private readonly ApiRateLimiter _rateLimiter = new();

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadDynamicPricingAsync();
                WebChatView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30);
                await WebChatView.EnsureCoreWebView2Async(null);
                _isWebViewInitialized = true;
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

        private void CmbModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbModels.SelectedItem != null && _appSettings.DefaultModel != CmbModels.SelectedItem.ToString())
            {
                _appSettings.DefaultModel = CmbModels.SelectedItem.ToString()!;
                SaveSettings();
            }
        }

        // 1. Przechwycenie Ctrl+V w polu tekstowym
        private void TxtInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (Clipboard.ContainsImage())
                {
                    e.Handled = true;
                    HandleImagePaste();
                }
            }
        }

        // 2. Obsługa wklejania wielu obrazów ze schowka
        private void HandleImagePaste()
        {
            try
            {
                BitmapSource? bitmap = null;
                IDataObject dataObject = Clipboard.GetDataObject();
                if (dataObject != null)
                {
                    if (dataObject.GetDataPresent(DataFormats.Bitmap))
                    {
                        bitmap = dataObject.GetData(DataFormats.Bitmap) as BitmapSource;
                    }
                    else if (Clipboard.ContainsImage())
                    {
                        bitmap = Clipboard.GetImage();
                    }
                }

                if (bitmap != null)
                {
                    // Kompresujemy i skalujemy obraz do max 1280px (oszczędność tokenów o 80-90%)
                    var (base64, mimeType) = CompressAndResizeImage(bitmap, maxDimension: 1280, quality: 75);

                    _pastedImages.Add(new PastedImageData
                    {
                        Bitmap = bitmap,
                        Base64 = base64,
                        MimeType = mimeType
                    });

                    RefreshPastedImagesUI();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się wkleić obrazu: {ex.Message}", "Błąd schowka", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Rysowanie miniaturek wklejonych obrazków
        private void RefreshPastedImagesUI()
        {
            PastedImagesStackPanel.Children.Clear();

            if (_pastedImages.Count == 0)
            {
                GridImagePreview.Visibility = Visibility.Collapsed;
                return;
            }

            GridImagePreview.Visibility = Visibility.Visible;

            for (int i = 0; i < _pastedImages.Count; i++)
            {
                var imgData = _pastedImages[i];
                int index = i;

                var container = new Grid { Margin = new Thickness(0, 0, 10, 0) };

                var border = new Border
                {
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCC")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(2)
                };

                var img = new Image
                {
                    Source = imgData.Bitmap,
                    Height = 70,
                    Width = 100,
                    Stretch = Stretch.Uniform
                };
                border.Child = img;
                container.Children.Add(border);

                // Przycisk "✕" do usuwania wybranej grafiki
                var btnRemove = new Button
                {
                    Content = "✕",
                    Width = 20,
                    Height = 20,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -5, -5, 0),
                    Padding = new Thickness(0),
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4D4D")),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };

                btnRemove.Click += (s, e) =>
                {
                    _pastedImages.RemoveAt(index);
                    RefreshPastedImagesUI();
                };

                container.Children.Add(btnRemove);
                PastedImagesStackPanel.Children.Add(container);
            }
        }

        // Zmniejszanie rozdzielczości i kompresja JPEG dla redukcji tokenów wizyjnych
        private (string base64, string mimeType) CompressAndResizeImage(BitmapSource bitmap, int maxDimension = 1280, int quality = 75)
        {
            double scale = 1.0;
            if (bitmap.PixelWidth > maxDimension || bitmap.PixelHeight > maxDimension)
            {
                scale = Math.Min((double)maxDimension / bitmap.PixelWidth, (double)maxDimension / bitmap.PixelHeight);
            }

            BitmapSource resized = bitmap;
            if (scale < 1.0)
            {
                resized = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
            }

            using var ms = new MemoryStream();
            var encoder = new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(resized));
            encoder.Save(ms);

            byte[] bytes = ms.ToArray();
            return (Convert.ToBase64String(bytes), "image/jpeg");
        }

        private void ClearAttachedImage()
        {
            _pastedImages.Clear();
            RefreshPastedImagesUI();
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
                    
                    pre { position: relative; background-color: #282c34; color: #abb2bf; padding: 12px; padding-top: 34px; border-radius: 8px; overflow-x: auto; font-family: 'Consolas', monospace; font-size: 13px; margin: 10px 0; }
                    code { background-color: #3e3e42; color: #f48fb1; padding: 2px 6px; border-radius: 4px; font-family: monospace; font-size: 13px; }
                    pre code { background-color: transparent; padding: 0; color: inherit; }
                    
                    .copy-code-btn { 
                        position: absolute; top: 5px; right: 5px; 
                        background: #3e3e42; color: #dcdcdc; border: 1px solid #555; 
                        padding: 3px 8px; border-radius: 4px; cursor: pointer; 
                        font-size: 11px; font-weight: bold; font-family: 'Segoe UI', sans-serif;
                        z-index: 10; transition: background 0.2s;
                    }
                    .copy-code-btn:hover { background: #0078d4; color: #fff; border-color: #0078d4; }
                    
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
                    
                    function addCodeCopyButtons() {
                        var pres = document.querySelectorAll('pre');
                        pres.forEach(function(pre) {
                            if (pre.querySelector('.copy-code-btn')) return;
                            
                            var btn = document.createElement('button');
                            btn.className = 'copy-code-btn';
                            btn.innerText = '📋 Kopiuj kod';
                            
                            btn.onclick = function(e) {
                                e.stopPropagation();
                                var codeEl = pre.querySelector('code');
                                var codeText = codeEl ? codeEl.innerText : pre.innerText;
                                
                                var base64Code = btoa(unescape(encodeURIComponent(codeText)));
                                handleAction('copyCode', base64Code);
                                
                                btn.innerText = '✅ Skopiowano!';
                                setTimeout(function() { btn.innerText = '📋 Kopiuj kod'; }, 2000);
                            };
                            pre.appendChild(btn);
                        });
                    }
                    function appendMessage(className, htmlContent) {
                        var div = document.createElement('div');
                        div.className = className;
                        div.innerHTML = htmlContent;
                        document.getElementById('chat-container').appendChild(div);
                        addCodeCopyButtons();
                        window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
                    }
                    function showThinking() {
                        if(document.getElementById('thinking')) return;
                        var div = document.createElement('div');
                        div.className = 'system waiting';
                        div.id = 'thinking';
                        div.innerHTML = '⏳ Gemini analizuje i pisze odpowiedź...';
                        document.getElementById('chat-container').appendChild(div);
                        window.scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
                    }
                    function removeThinking() {
                        var el = document.getElementById('thinking');
                        if(el) el.remove();
                    }
                    function restoreChat(fullHtml) {
                        document.getElementById('chat-container').innerHTML = fullHtml;
                        addCodeCopyButtons();
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
            if (string.IsNullOrEmpty(userMessage) && _selectedFilePaths.Count == 0 && _pastedImages.Count == 0) return;

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

            // 1. Przetwarzanie plików z dysku z optymalizacją obrazów
            foreach (var filePath in _selectedFilePaths)
            {
                if (!File.Exists(filePath)) continue;
                try
                {
                    string ext = Path.GetExtension(filePath).ToLower();
                    string mimeType = GetMimeType(ext);

                    if (mimeType.StartsWith("text/") || ext == ".cs" || ext == ".ps1" || ext == ".py" || ext == ".json" || ext == ".xml" || ext == ".html" || ext == ".css" || ext == ".bat" || ext == ".sh" || ext == ".sql")
                    {
                        string content = await File.ReadAllTextAsync(filePath);
                        string fileText = $"\n--- Plik: {Path.GetFileName(filePath)} ---\n{content}\n";
                        parts.Add(new TextData { Text = fileText });
                        savedAttachmentsForHistory.Add(new FileAttachmentData { FileName = Path.GetFileName(filePath), MimeType = "text/plain", Base64Data = fileText });
                        userChatVisualInfo += $"<br/><div class='attachment-badge'>📁 {Path.GetFileName(filePath)}</div>";
                    }
                    else if (mimeType.StartsWith("image/"))
                    {
                        using var stream = File.OpenRead(filePath);
                        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                        if (decoder.Frames.Count > 0)
                        {
                            var (base64Data, compressedMime) = CompressAndResizeImage(decoder.Frames[0], maxDimension: 1280, quality: 75);
                            parts.Add(new InlineData { MimeType = compressedMime, Data = base64Data });
                            savedAttachmentsForHistory.Add(new FileAttachmentData { FileName = Path.GetFileName(filePath), MimeType = compressedMime, Base64Data = base64Data });
                            userChatVisualInfo += $"<br/><img src='data:{compressedMime};base64,{base64Data}' />";
                        }
                    }
                    else
                    {
                        byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
                        string base64Data = Convert.ToBase64String(fileBytes);
                        parts.Add(new InlineData { MimeType = mimeType, Data = base64Data });
                        savedAttachmentsForHistory.Add(new FileAttachmentData { FileName = Path.GetFileName(filePath), MimeType = mimeType, Base64Data = base64Data });
                        userChatVisualInfo += $"<br/><div class='attachment-badge'>📁 {Path.GetFileName(filePath)}</div>";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd odczytu pliku {Path.GetFileName(filePath)}: {ex.Message}", "Błąd pliku", MessageBoxButton.OK, MessageBoxImage.Error);
                    BlockUI(false);
                    return;
                }
            }

            // 2. Wklejone obrazy ze schowka (Wysyłanie wszystkich obrazów na raz)
            for (int i = 0; i < _pastedImages.Count; i++)
            {
                var img = _pastedImages[i];
                parts.Add(new InlineData { MimeType = img.MimeType, Data = img.Base64 });

                savedAttachmentsForHistory.Add(new FileAttachmentData
                {
                    FileName = $"Obraz_ze_schowka_{i + 1}.jpg",
                    MimeType = img.MimeType,
                    Base64Data = img.Base64
                });

                userChatVisualInfo += $"<br/><img src='data:{img.MimeType};base64,{img.Base64}' />";
            }

            if (!string.IsNullOrEmpty(userMessage))
            {
                parts.Add(new TextData { Text = userMessage });
            }

            string base64UserMsg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userMessage));
            userChatVisualInfo += $"<div class='actions'><button class='action-btn' onclick=\"handleAction('edit', '{base64UserMsg}')\">✏️ Edytuj</button><button class='action-btn' onclick=\"handleAction('retry', '{base64UserMsg}')\">🔄 Ponów</button></div>";
            await AppendMessageToWebViewAsync("user", userChatVisualInfo);

            ClearAttachmentList();
            ClearAttachedImage();
            await ShowThinkingIndicatorAsync(true);

            try
            {
                bool isNewSession = (_activeSession == null);

                // ==========================================
                // OPCJA A: GENEROWANIE OBRAZÓW (IMAGEN)
                // ==========================================
                if (selectedModel.Contains("imagen"))
                {
                    var imageModel = _googleAI.ImageGenerationModel(selectedModel);
                    var imgRequest = new GenerateImagesRequest(userMessage, 1);

                    ImageGenerationResponse? imgResponse = null;
                    int maxRetries = 3;
                    int delayMs = 2000;

                    for (int retry = 0; retry < maxRetries; retry++)
                    {
                        try
                        {
                            imgResponse = await imageModel.GenerateImages(imgRequest);
                            break;
                        }
                        catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("Quota") || ex.Message.Contains("RESOURCE_EXHAUSTED"))
                        {
                            if (retry == maxRetries - 1) throw;
                            await Task.Delay(delayMs);
                            delayMs *= 2;
                        }
                    }

                    await ShowThinkingIndicatorAsync(false);
                    string base64Image = string.Empty;
                    if (imgResponse != null)
                    {
                        base64Image = ExtractBase64FromImageResponse(imgResponse);
                    }

                    if (!string.IsNullOrEmpty(base64Image))
                    {
                        if (base64Image.StartsWith("data:image"))
                        {
                            base64Image = base64Image.Substring(base64Image.IndexOf(",") + 1);
                        }

                        string aiChatVisualInfo = $"<strong>[{selectedModel}]:</strong><br/>" +
                                                 $"<p>Oto wygenerowany obraz dla zapytania: <em>\"{userMessage}\"</em></p>" +
                                                 $"<img src='data:image/jpeg;base64,{base64Image}' />";
                        await AppendMessageToWebViewAsync("ai", aiChatVisualInfo);

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
                        await AppendMessageToWebViewAsync("error", "<strong>Błąd:</strong> Model nie wygenerował obrazu (lub konto API ma ograniczenia regionalne / filtrowanie NSFW).");
                    }
                }
                // ==========================================
                // OPCJA B: STRUMIENIOWANIE TEKSTU (GEMINI)
                // ==========================================
                else
                {
                    if (isNewSession)
                    {
                        _activeSession = new ChatSessionData { Title = $"Czat: {DateTime.Now:dd-MM-yyyy HH:mm:ss}", SelectedModel = selectedModel };
                        _savedChats.Add(_activeSession);
                    }

                    var model = _googleAI.GenerativeModel(selectedModel);
                    var recentMessages = _activeSession?.Messages.TakeLast(4).ToList() ?? new List<ChatMessageData>();

                    var request = new GenerateContentRequest
                    {
                        Contents = BuildSdkHistory(recentMessages),
                        GenerationConfig = new GenerationConfig
                        {
                            MaxOutputTokens = 4096,
                            Temperature = 0.2f
                        }
                    };

                    request.Contents.Add(new Content("user") { Parts = parts });

                    IAsyncEnumerable<GenerateContentResponse>? responseStream = null;
                    int maxRetries = 4;
                    int delayMs = 2000;

                    for (int retry = 0; retry < maxRetries; retry++)
                    {
                        try
                        {
                            responseStream = model.GenerateContentStream(request);
                            break;
                        }
                        catch (Exception ex) when (ex.Message.Contains("429") || ex.Message.Contains("Quota") || ex.Message.Contains("RESOURCE_EXHAUSTED"))
                        {
                            if (retry == maxRetries - 1) throw;
                            await Task.Delay(delayMs);
                            delayMs *= 2;
                        }
                    }

                    string streamingDivId = $"ai_stream_{Guid.NewGuid().ToString("N")}";
                    string initialAiContainer = $"<strong>[{selectedModel}]:</strong><br/><div id='{streamingDivId}'><em>Generowanie odpowiedzi...</em></div>";
                    await AppendMessageToWebViewAsync("ai", initialAiContainer);
                    await ShowThinkingIndicatorAsync(false);

                    string responseText = string.Empty;
                    var markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                    var aiAttachmentsForHistory = new List<FileAttachmentData>();

                    int lastInputTokens = 0;
                    int lastOutputTokens = 0;

                    if (responseStream != null)
                    {
                        await foreach (var chunk in responseStream)
                        {
                            if (chunk.UsageMetadata != null)
                            {
                                lastInputTokens = (int)chunk.UsageMetadata.PromptTokenCount;
                                lastOutputTokens = (int)chunk.UsageMetadata.CandidatesTokenCount;
                            }

                            if (!string.IsNullOrEmpty(chunk.Text))
                            {
                                responseText += chunk.Text;
                                string safeText = responseText.Replace("\r\n", "\n");
                                string renderedAiHtml = Markdown.ToHtml(safeText, markdownPipeline);

                                if (chunk.Candidates != null)
                                {
                                    foreach (var candidate in chunk.Candidates)
                                    {
                                        if (candidate.Content?.Parts != null)
                                        {
                                            foreach (var p in candidate.Content.Parts)
                                            {
                                                if (p is Part part && part.InlineData != null && !string.IsNullOrEmpty(part.InlineData.Data))
                                                {
                                                    string mime = part.InlineData.MimeType ?? "image/jpeg";
                                                    string base64 = part.InlineData.Data;
                                                    renderedAiHtml += $"<br/><img src='data:{mime};base64,{base64}' />";
                                                    if (!aiAttachmentsForHistory.Any(a => a.Base64Data == base64))
                                                    {
                                                        aiAttachmentsForHistory.Add(new FileAttachmentData { FileName = "AI_Generated_Image", MimeType = mime, Base64Data = base64 });
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                string safeHtmlJson = JsonSerializer.Serialize(renderedAiHtml);
                                string updateScript = $"var el = document.getElementById('{streamingDivId}'); if(el) {{ el.innerHTML = {safeHtmlJson}; }} addCodeCopyButtons(); window.scrollTo({{ top: document.body.scrollHeight, behavior: 'auto' }});";
                                await WebChatView.CoreWebView2.ExecuteScriptAsync(updateScript);
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(responseText))
                    {
                        string emptyFallback = "<em>(Brak tekstu ani obrazka z API)</em>";
                        await WebChatView.CoreWebView2.ExecuteScriptAsync($"var el = document.getElementById('{streamingDivId}'); if(el) {{ el.innerHTML = \"{emptyFallback}\"; }}");
                    }

                    UpdateTokenStatsUI(lastInputTokens, lastOutputTokens, selectedModel);

                    string base64AiMsg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(responseText));
                    string actionsHtml = $"<div class='actions'><button class='action-btn' onclick=\"handleAction('copy', '{base64AiMsg}')\">📋 Kopiuj całość</button></div>";
                    string appendActionsScript = $"var el = document.getElementById('{streamingDivId}'); if(el) {{ el.insertAdjacentHTML('afterend', {JsonSerializer.Serialize(actionsHtml)}); }}";
                    await WebChatView.CoreWebView2.ExecuteScriptAsync(appendActionsScript);

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

        private (decimal inputCost, decimal outputCost, decimal totalCost) CalculateGeminiCost(string modelName, int inputTokens, int outputTokens)
        {
            string model = modelName.ToLower().Replace("models/", "").Trim();
            ModelRate? rate = null;

            foreach (var kvp in _dynamicRates)
            {
                if (model.Contains(kvp.Key) || kvp.Key.Contains(model))
                {
                    rate = kvp.Value;
                    break;
                }
            }

            rate ??= GetFallbackRate(model);

            decimal inputCost = (inputTokens / 1_000_000m) * rate.InputPerM;
            decimal outputCost = (outputTokens / 1_000_000m) * rate.OutputPerM;

            return (inputCost, outputCost, inputCost + outputCost);
        }

        private async Task LoadDynamicPricingAsync()
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                string json = await _httpClient.GetStringAsync(LiteLlmPricingUrl, cts.Token);

                using var doc = JsonDocument.Parse(json);
                _dynamicRates.Clear();

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    string modelKey = prop.Name.ToLower();

                    if (modelKey.Contains("gemini") || modelKey.Contains("imagen"))
                    {
                        var element = prop.Value;
                        decimal inputCostPerToken = 0m;
                        decimal outputCostPerToken = 0m;

                        if (element.TryGetProperty("input_cost_per_token", out var inProp) && inProp.TryGetDecimal(out var inVal))
                            inputCostPerToken = inVal;

                        if (element.TryGetProperty("output_cost_per_token", out var outProp) && outProp.TryGetDecimal(out var outVal))
                            outputCostPerToken = outVal;

                        if (inputCostPerToken > 0 || outputCostPerToken > 0)
                        {
                            string cleanKey = modelKey.Replace("gemini/", "").Trim();

                            _dynamicRates[cleanKey] = new ModelRate
                            {
                                InputPerM = inputCostPerToken * 1_000_000m,
                                OutputPerM = outputCostPerToken * 1_000_000m
                            };
                        }
                    }
                }
            }
            catch
            {
                // W razie braku sieci aplikacja skorzysta z lokalnego cennika
            }
        }

        private ModelRate GetFallbackRate(string model)
        {
            if (model.Contains("3.6-flash")) return new ModelRate { InputPerM = 1.50m, OutputPerM = 7.50m };
            if (model.Contains("3.5-flash")) return new ModelRate { InputPerM = 1.50m, OutputPerM = 9.00m };
            if (model.Contains("2.5-flash")) return new ModelRate { InputPerM = 0.15m, OutputPerM = 0.60m };
            if (model.Contains("flash-lite")) return new ModelRate { InputPerM = 0.10m, OutputPerM = 0.40m };
            if (model.Contains("pro")) return new ModelRate { InputPerM = 2.00m, OutputPerM = 12.00m };

            return new ModelRate { InputPerM = 0.50m, OutputPerM = 3.00m };
        }

        private void UpdateTokenStatsUI(int lastInputTokens, int lastOutputTokens, string selectedModel)
        {
            int totalLastRequestTokens = lastInputTokens + lastOutputTokens;

            _rateLimiter.RegisterRequest(totalLastRequestTokens);
            var (currentRpm, currentTpm) = _rateLimiter.GetCurrentStats();

            var (lastInputCost, lastOutputCost, lastTotalCost) = CalculateGeminiCost(selectedModel, lastInputTokens, lastOutputTokens);

            _totalSessionInputTokens += lastInputTokens;
            _totalSessionOutputTokens += lastOutputTokens;
            _totalSessionCostUsd += lastTotalCost;

            if (FindName("LblLastCost") is TextBlock lblLast &&
                FindName("LblRateLimits") is TextBlock lblLimits &&
                FindName("LblTotalCost") is TextBlock lblTotal)
            {
                lblLast.Text = $"Ostatnie zapytanie: In {lastInputTokens:N0} / Out {lastOutputTokens:N0} (${lastTotalCost:F5})";
                lblLimits.Text = $"Ostatnie 60s: {currentRpm} RPM | {currentTpm:N0} TPM";

                lblLimits.Foreground = currentTpm > 500_000
                    ? new SolidColorBrush(Colors.OrangeRed)
                    : new SolidColorBrush(Colors.Gold);

                lblTotal.Text = $"Suma sesji: In {_totalSessionInputTokens:N0} / Out {_totalSessionOutputTokens:N0} (~${_totalSessionCostUsd:F4})";
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
            ".ps1" or ".psm1" or ".psd1" => "text/plain",
            ".bat" or ".cmd" => "text/plain",
            ".sh" or ".bash" => "text/plain",
            ".py" => "text/plain",
            ".cs" => "text/plain",
            ".sql" => "text/plain",
            ".json" or ".xml" or ".yml" => "text/plain",
            ".html" or ".css" or ".js" => "text/plain",
            _ => "text/plain"
        };

        private List<Content> BuildSdkHistory(List<ChatMessageData> savedMessages)
        {
            var history = new List<Content>();
            for (int i = 0; i < savedMessages.Count; i++)
            {
                var msg = savedMessages[i];
                var parts = new List<IPart>();

                if (!string.IsNullOrEmpty(msg.Text))
                {
                    parts.Add(new TextData { Text = msg.Text });
                }

                if (parts.Count > 0)
                {
                    history.Add(new Content(string.IsNullOrEmpty(msg.Role) ? "user" : msg.Role) { Parts = parts });
                }
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
                    throw new Exception("Brak klucza API lub klucz jest domyślny.");

                string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={_appSettings.ApiKey}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Serwer zwrócił kod {response.StatusCode}: {errorContent}");
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(jsonResponse);
                if (doc.RootElement.TryGetProperty("models", out JsonElement modelsElement))
                {
                    int addedCount = 0;
                    foreach (JsonElement model in modelsElement.EnumerateArray())
                    {
                        if (model.TryGetProperty("name", out JsonElement nameElement))
                        {
                            string? fullModelName = nameElement.GetString();
                            if (string.IsNullOrEmpty(fullModelName)) continue;

                            string modelName = fullModelName.Replace("models/", "");
                            bool supportsGeneration = false;

                            if (model.TryGetProperty("supportedGenerationMethods", out JsonElement methods))
                            {
                                foreach (JsonElement method in methods.EnumerateArray())
                                {
                                    string? methodStr = method.GetString();
                                    if (methodStr == "generateContent" || methodStr == "generateImages")
                                    {
                                        supportsGeneration = true;
                                        break;
                                    }
                                }
                            }

                            if (supportsGeneration)
                            {
                                CmbModels.Items.Add(modelName);
                                addedCount++;
                            }
                        }
                    }

                    if (addedCount == 0)
                    {
                        throw new Exception("Nie znaleziono żadnych modeli wspierających generowanie treści.");
                    }
                }
                else
                {
                    throw new Exception("Niepoprawny format odpowiedzi API (brak pola 'models').");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się pobrać aktualnej listy modeli z API Gemini.\nPowód: {ex.Message}\n\nAplikacja załaduje domyślną listę modeli.",
                                "Błąd pobierania modeli", MessageBoxButton.OK, MessageBoxImage.Warning);

                string[] defaultModels = { "gemini-2.5-flash", "gemini-2.5-pro", "gemini-1.5-flash", "gemini-1.5-pro", "imagen-3.0-generate-002" };
                CmbModels.Items.Clear();
                foreach (var m in defaultModels)
                {
                    CmbModels.Items.Add(m);
                }
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

        private async void LstHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
            var ofd = new OpenFileDialog
            {
                Filter = "Wszystkie obsługiwane|*.png;*.jpg;*.jpeg;*.webp;*.pdf;*.txt;*.cs;*.ps1;*.bat;*.sh;*.py;*.json;*.xml;*.html;*.css;*.sql|Skrypty i Kod|*.ps1;*.bat;*.sh;*.py;*.cs;*.sql;*.json;*.xml|Wszystkie pliki|*.*",
                Multiselect = true
            };
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
                LblAttachment.Text = $"Załączono ({_selectedFilePaths.Count}) plików. Kliknij tu, aby wyczyścić";
                BtnAttach.Content = "\uE723";
            }
            else
            {
                LblAttachment.Visibility = Visibility.Collapsed;
                BtnAttach.Content = "\uE710";
            }
        }

        private void ClearAttachmentList()
        {
            _selectedFilePaths.Clear();
            UpdateAttachmentsUI();
        }

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
                    case "copyCode":
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

    public class PastedImageData
    {
        public BitmapSource Bitmap { get; set; } = null!;
        public string Base64 { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
    }

    public class ModelRate
    {
        public decimal InputPerM { get; set; }
        public decimal OutputPerM { get; set; }
    }
}