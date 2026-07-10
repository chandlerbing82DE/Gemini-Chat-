# Gemini WPF Client

[PL] Nowoczesna aplikacja desktopowa dla systemu Windows (WPF), będąca zaawansowanym klientem dla modeli sztucznej inteligencji **Google Gemini** oraz generatora grafiki **Imagen**. Program oferuje płynne konwersacje tekstowe, analizę załączników (multimodalność) oraz lokalne zarządzanie historią czatów.

[EN] A modern Windows desktop application (WPF) acting as an advanced client for **Google Gemini** AI models and the **Imagen** image generator. The program offers seamless text conversations, multi-file attachment analysis (multimodality), and local chat history management.

---

## 🌐 JĘZYK INTERFEJSU / USER INTERFACE LANGUAGE

> ⚠️ **WAŻNA INFORMACJA / IMPORTANT NOTE:**  
> **PL:** Interfejs użytkownika (UI) oraz wszystkie komunikaty aplikacji są dostępne **wyłącznie w języku polskim**.  
> **EN:** The user interface (UI) and all application messages are available **exclusively in Polish**.

---

## 🚀 Funkcje / Features

### 🇵🇱 Wersja Polska
1. **Interaktywny Czat (Dark Mode):** Estetyczne okno rozmowy oparte na silniku Chromium (`WebView2`), automatycznie przewijane do najnowszych wiadomości.
2. **Renderowanie Markdown:** Odpowiedzi modeli AI są formatowane w locie (nagłówki, listy, tabele, bloki kodu) za pomocą biblioteki `Markdig`.
3. **Załączniki i Multimodalność:**
   * **Dokumenty tekstowe i kod** (`.txt`, `.cs`, `.py`, `.json`, `.xml`, `.html`, `.css`): Automatycznie wstrzykiwane jako kontekst tekstowy.
   * **Grafika** (`.png`, `.jpg`, `.jpeg`, `.webp`): Przesyłana jako Base64, wyświetlana bezpośrednio w czacie i analizowana przez model.
   * **Dokumenty PDF** (`.pdf`): Wysyłane do analizy treści.
4. **Generowanie Obrazów (Imagen 3):** Pełna integracja z modelami `imagen-3.0`. Wygenerowane obrazy wyświetlają się w czacie i zapisują w historii.
5. **Dynamiczna lista modeli:** Automatyczne pobieranie dostępnych modeli z Google API przy starcie (z opcją trybu offline).
6. **Lokalna historia:** Automatyczny zapis rozmów do pliku JSON (`saved_chats.json`).

### 🇬🇧 English Version
1. **Interactive Chat (Dark Mode):** Beautiful chat window powered by Chromium (`WebView2`), with auto-scroll to the latest messages.
2. **Markdown Rendering:** AI responses are formatted on the fly (headers, lists, tables, code blocks) using the `Markdig` library.
3. **Attachments & Multimodality:**
   * **Text Documents & Code** (`.txt`, `.cs`, `.py`, `.json`, `.xml`, `.html`, `.css`): Automatically injected as text context.
   * **Images** (`.png`, `.jpg`, `.jpeg`, `.webp`): Sent as Base64, rendered inside the chat, and analyzed by vision models.
   * **PDF Documents** (`.pdf`): Sent for direct content analysis.
4. **Image Generation (Imagen 3):** Seamless integration with `imagen-3.0` models. Generated images are displayed in the chat and saved to history.
5. **Dynamic Model List:** Fetches available models from the Google API on startup (with an offline fallback list).
6. **Local History:** Automatically saves all conversations to a local JSON file (`saved_chats.json`).

---

## 🛠️ Wymagania systemowe / System Requirements

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

* **`MainWindow.xaml / MainWindow.xaml.cs`** — Główny interfejs i logika biznesowa (WPF, WebView2, API Gemini & Imagen).
* **`ChatModels.cs`** — Modele danych / Data models (`AppSettings`, `FileAttachmentData`, `ChatMessageData`, `ChatSessionData`).
* **`appsettings.json`** — Plik konfiguracyjny / Configuration file.
* **`saved_chats.json`** — Lokalna historia czatów / Local chat history database (auto-generated).

---

## 💡 Instrukcja użytkowania / How to Use

### 🇵🇱 Po polsku:
1. **Wysyłanie:** Wpisz tekst i kliknij **Wyślij** lub naciśnij **`Ctrl + Enter`**.
2. **Nowy czat:** Kliknij ikonę miotły na dolnym pasku, aby wyczyścić bieżący widok.
3. **Załączniki:** Kliknij ikonę **`+`**, wybierz pliki. Kliknięcie w napis nad polem tekstowym usuwa dodane pliki.
4. **Generowanie obrazu:** Wybierz model `imagen-*` z listy, wpisz swój prompt i kliknij **Wyślij**.
5. **Usuwanie historii:** Kliknij ikonę kosza przy wybranym czacie na liście bocznej.

### 🇬🇧 In English:
1. **Sending:** Type your message and click **Wyślij** (Send) or press **`Ctrl + Enter`**.
2. **New Chat:** Click the broom icon on the bottom bar to clear the current view.
3. **Attachments:** Click the **`+`** icon, select your files. Click the text label above the input box to clear selected files.
4. **Image Generation:** Select an `imagen-*` model from the dropdown, type your prompt, and click **Wyślij** (Send).
5. **Delete Chat:** Click the trash bin icon next to a chat item in the left sidebar.

---

## 📦 Biblioteki / Dependencies

* **`Mscc.GenerativeAI`** — Google Gemini API .NET wrapper.
* **`Markdig`** — Markdown to HTML converter.
* **`Microsoft.Web.WebView2`** — Chromium browser control for WPF.
* **`System.Text.Json`** — JSON serialization.

---

## 📄 Licencja i Warunki Użycia / License & Terms of Use

**PL:**  
Projekt jest udostępniany na **modyfikowanej licencji MIT**.  
Zezwala się na dowolne korzystanie z oprogramowania, jego modyfikację oraz dystrybucję, **pod warunkiem wyraźnego i widocznego oznaczenia autora (link do profilu GitHub autora) w widocznym miejscu interfejsu użytkownika aplikacji** (np. w oknie "O programie", stopce lub nagłówku głównego okna).

**EN:**  
This project is distributed under a **modified MIT License**.  
You are free to use, modify, and distribute this software, **provided that clear and visible attribution to the author (a hyperlink to the author's GitHub profile) is displayed in a prominent place within the application's user interface** (e.g., in an "About" window, footer, or header of the main window).

---
*Autor projektu / Project Author: chandlerbing82DE*
`
````