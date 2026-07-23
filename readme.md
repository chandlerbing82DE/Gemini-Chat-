# Gemini WPF Client

[PL] Nowoczesna aplikacja desktopowa dla systemu Windows (WPF), będąca zaawansowanym klientem dla modeli sztucznej inteligencji Google Gemini oraz generatora grafiki Imagen. Program oferuje płynne konwersacje tekstowe, zaawansowaną analizę załączników, wklejanie obrazów ze schowka ze smart-kompresją, monitoring kosztów zapytań w czasie rzeczywistym oraz lokalne zarządzanie historią czatów.

[EN] A modern Windows desktop application (WPF) acting as an advanced client for Google Gemini AI models and the Imagen image generator. The program offers seamless text conversations, multi-file attachment analysis, clipboard image pasting with smart token-saving compression, real-time cost tracking, and local chat history management.

---

## 🌐 JĘZYK INTERFEJSU / USER INTERFACE LANGUAGE

> ⚠️ **WAŻNA INFORMACJA / IMPORTANT NOTE:**  
> **PL:** Interfejs użytkownika (UI) oraz wszystkie komunikaty aplikacji są dostępne wyłącznie w języku polskim.  
> **EN:** The user interface (UI) and all application messages are available exclusively in Polish.

---

## 🚀 Funkcje / Features

### 🇵🇱 Wersja Polska
1. **Interaktywny Czat (Dark Mode):** Estetyczne okno rozmowy oparte na silniku Chromium (WebView2), automatycznie przewijane do najnowszych wiadomości.
2. **Renderowanie Markdown & Kod:** Odpowiedzi modeli AI są formatowane w locie za pomocą biblioteki Markdig. Bloki kodu zawierają dedykowany przycisk `📋 Kopiuj kod`.
3. **Zaawansowana Multimodalność & Wklejanie ze Schowka (`Ctrl + V`):**
   * **Wklejanie grafik (`Ctrl + V`):** Bezpośrednie wklejanie wielu obrazów ze schowka z dynamicznym podglądem w poziomym pasku nad polem tekstowym oraz możliwością usuwania poszczególnych miniatur (`✕`).
   * **Smart Kompresja Tokenów:** Automatyczne skalowanie grafik do max 1280px oraz kompresja JPEG (75%), ograniczająca zużycie tokenów wizyjnych o 80–90%.
   * **Dokumenty tekstowe i kod** (`.txt`, `.cs`, `.py`, `.json`, `.xml`, `.html`, `.css`, `.ps1`, `.bat`, `.sql`): Automatycznie wstrzykiwane jako kontekst.
   * **Pliki PDF i obrazy z pliku** (`.pdf`, `.png`, `.jpg`, `.webp`): Przesyłane bezpośrednio do analizy przez model.
4. **Monitoring Kosztów i Limitów (Dynamic Pricing):**
   * Pasek stanu (StatusBar) wyświetla dokładną liczbę tokenów oraz koszt każdego zapytania w USD.
   * Dynamiczne pobieranie aktualnych stawek cenowych z interfejsu LiteLLM (z lokalnym cennikiem zapasowym).
   * Licznik RPM (Requests Per Minute) i TPM (Tokens Per Minute) monitorujący limity API w oknie 60 sekund.
5. **Generowanie Obrazów (Imagen 3):** Pełna integracja z modelami z serii `imagen-3.0`. Wygenerowane obrazy wyświetlają się w czacie i zapisują w historii.
6. **Dynamiczna Lista Modeli:** Automatyczne pobieranie dostępnych modeli z Google API przy starcie (z opcją trybu offline).
7. **Interakcje z Wiadomościami:** Możliwość szybkiej edycji wysłanej wiadomości lub jej ponowienia bezpośrednio w oknie czatu.
8. **Lokalna Historia:** Automatyczny zapis rozmów do pliku JSON (`saved_chats.json`).

### 🇬🇧 English Version
1. **Interactive Chat (Dark Mode):** Dark-themed chat UI powered by Chromium (WebView2) with smooth auto-scrolling to the latest messages.
2. **Markdown & Code Rendering:** AI responses are formatted on the fly via Markdig. Code blocks feature a dedicated `📋 Copy code` button.
3. **Advanced Multimodality & Clipboard Support (`Ctrl + V`):**
   * **Multi-Image Paste (`Ctrl + V`):** Paste multiple images directly from the clipboard with a horizontal preview tray above the input box and individual delete buttons (`✕`).
   * **Smart Token Compression:** Auto-resizes images up to 1280px and applies JPEG compression (75%), reducing vision token usage by 80–90%.
   * **Text & Code Files** (`.txt`, `.cs`, `.py`, `.json`, `.xml`, `.html`, `.css`, `.ps1`, `.bat`, `.sql`): Injected directly as plain-text context.
   * **PDFs & Image Files** (`.pdf`, `.png`, `.jpg`, `.webp`): Sent directly for model analysis.
4. **Real-time Cost & Rate Limits Monitor:**
   * Status bar shows exact token counts and estimated USD costs per query and total session usage.
   * Dynamic pricing integration with LiteLLM API (includes offline fallback rates).
   * Real-time 60-second window tracker for RPM (Requests Per Minute) and TPM (Tokens Per Minute).
5. **Image Generation (Imagen 3):** Integration with `imagen-3.0` models. Generated images render directly in the chat stream and save to history.
6. **Dynamic Model Fetching:** Automatically retrieves available models from the Google API on launch (with offline default fallback).
7. **Message Actions:** Edit previous messages or retry queries directly within the chat window.
8. **Local History:** Automatically persists all conversations into a local JSON file (`saved_chats.json`).

---

## 🛠 Wymagania systemowe / System Requirements

* **OS:** Windows 10 / Windows 11 (64-bit)
* **Framework:** .NET 8.0 Runtime
* **Component:** Microsoft Edge WebView2 Runtime
* **API Key:** Google Gemini API Key ([Google AI Studio](https://aistudio.google.com/))

---

## ⚙️ Konfiguracja / Configuration

**PL:** Przed uruchomieniem wklej swój klucz API do pliku `appsettings.json` w folderze programu:  
**EN:** Before running, paste your API key into the `appsettings.json` file in the application directory:

```json
{
  "AppSettings": {
    "ApiKey": "TUTAJ_WKLEJ_SWOJ_KLUCZ_API_GEMINI",
    "DefaultModel": "gemini-2.5-flash"
  }
}
```

---

## 📂 Struktura Projektu / Project Structure

* `MainWindow.xaml` — Główny układ UI (WPF, WebView2, kontener podglądu grafik, pasek stanu).
* `MainWindow.xaml.cs` — Główna logika biznesowa (obsługa API Gemini/Imagen, strumieniowanie, kompresja grafik, kalkulator kosztów, limity RPM/TPM).
* `ChatModels.cs` — Definicje modeli danych (`AppSettings`, `FileAttachmentData`, `ChatMessageData`, `ChatSessionData`, `ApiRateLimiter`).
* `appsettings.json` — Plik konfiguracyjny (Klucz API, domyślny model).
* `saved_chats.json` — Baza lokalnej historii czatów (generowana automatycznie).

---

## 💡 Instrukcja użytkowania / How to Use

### 🇵🇱 Po polsku:
1. **Wysyłanie:** Wpisz tekst i kliknij **Wyślij** lub naciśnij `Ctrl + Enter`.
2. **Wklejanie obrazków:** Naciśnij `Ctrl + V` w polu tekstowym, aby wkleić obraz ze schowka. Możesz wkleić wiele obrazów – pojawią się w pasku podglądu.
3. **Załączniki z dysku:** Kliknij ikonę `+`, aby załączyć pliki tekstowe, kod lub PDF.
4. **Generowanie obrazu:** Wybierz model z prefiksem `imagen-*` z listy, wpisz prompt i kliknij **Wyślij**.
5. **Nowy czat:** Kliknij przycisk **Nowy Czat** w lewym panelu, aby rozpocząć czystą sesję.
6. **Usuwanie historii:** Wybierz czat z listy bocznej i kliknij przycisk **Usuń Czat**.

### 🇬🇧 In English:
1. **Sending:** Type your message and click **Wyślij** (Send) or press `Ctrl + Enter`.
2. **Pasting Images:** Press `Ctrl + V` inside the text input to paste images from your clipboard. Multiple images will appear in the preview tray above.
3. **Disk Attachments:** Click the `+` button to select code, text documents, or PDFs.
4. **Image Generation:** Select an `imagen-*` model from the dropdown, type your image prompt, and click **Wyślij**.
5. **New Chat:** Click the **Nowy Czat** button on the left sidebar to start a fresh conversation.
6. **Delete Chat:** Select a chat item from the sidebar and click **Usuń Czat**.

---

## 📦 Biblioteki / Dependencies

* `Mscc.GenerativeAI` — Oficjalny wrapper .NET dla Google Gemini API.
* `Markdig` — Konwerter Markdown do czytelnego formatu HTML.
* `Microsoft.Web.WebView2` — Silnik Chromium dla zaawansowanego renderowania wiadomości w WPF.
* `System.Text.Json` — Serializacja i deserializacja struktur JSON.

---

## 📄 Licencja i Warunki Użycia / License & Terms of Use

**PL:**  
Projekt jest udostępniany na modyfikowanej licencji MIT.  
Zezwala się na dowolne korzystanie z oprogramowania, jego modyfikację oraz dystrybucję, pod warunkiem wyraźnego i widocznego oznaczenia autora (link do profilu GitHub autora) w widocznym miejscu interfejsu użytkownika aplikacji (np. w oknie "O programie", stopce lub nagłówku głównego okna).

**EN:**  
This project is distributed under a modified MIT License.  
You are free to use, modify, and distribute this software, provided that clear and visible attribution to the author (a hyperlink to the author's GitHub profile) is displayed in a prominent place within the application's user interface (e.g., in an "About" window, footer, or header of the main window).

---
*Autor projektu / Project Author: **chandlerbing82DE***
