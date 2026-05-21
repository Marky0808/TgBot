using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using DeepL;

namespace TelegramDeepLBot
{
    public class HistoryRecord
    {
        public int Id { get; set; }
        public string OriginalText { get; set; } = string.Empty;
        public string TranslatedText { get; set; } = string.Empty;
        public string SourceLang { get; set; } = string.Empty;
        public string TargetLang { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public class UserSession
    {
        public string State { get; set; } = "Idle"; // Idle, WaitingForLangs, WaitingForText, WaitingForDelete
        public string CurrentSourceLang { get; set; } = string.Empty;
        public string CurrentTargetLang { get; set; } = string.Empty;
        public List<HistoryRecord> History { get; set; } = new();
        public int NextHistoryId { get; set; } = 1;
    }

    class Program
    {
        private static readonly string TelegramToken = "TG-BOTtoken";
        private static readonly string DeepLKey = "86fd7180-e978-4e70-bb1a-d2f6b7086dea:fx";

        private static Translator? _translator;

        private static readonly Dictionary<long, UserSession> UserSessions = new();

        static async Task Main(string[] args)
        {
            var botClient = new TelegramBotClient(TelegramToken);
            _translator = new Translator(DeepLKey);

            using var cts = new CancellationTokenSource();
            var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };

            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            var me = await botClient.GetMe();
            Console.WriteLine($"👋 Бот @{me.Username} успішно запущений!");
            Console.WriteLine("Натисни Enter, щоб зупинити...");
            Console.ReadLine();
            cts.Cancel();
        }
        
        private static UserSession GetSession(long chatId)
        {
            if (!UserSessions.ContainsKey(chatId))
            {
                UserSessions[chatId] = new UserSession();
            }
            return UserSessions[chatId];
        }

        static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
                {
                    await HandleCallbackQueryAsync(botClient, update.CallbackQuery, cancellationToken);
                    return;
                }
                
                if (update.Type == UpdateType.Message && update.Message?.Text != null)
                {
                    await HandleMessageAsync(botClient, update.Message, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка під час обробки: {ex.Message}");
            }
        }

        static async Task HandleMessageAsync(ITelegramBotClient botClient, Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var text = message.Text!.Trim();
            var session = GetSession(chatId);

            Console.WriteLine($"[{DateTime.Now}] {message.Chat.FirstName}: {text} (Стан: {session.State})");
            
            if (text.StartsWith("/start"))
            {
                session.State = "Idle";
                await SendMainMenuAsync(botClient, chatId, cancellationToken);
                return;
            }
            
            switch (session.State)
            {
                case "WaitingForLangs":
                    await ProcessLanguageSelectionAsync(botClient, chatId, text, session, cancellationToken);
                    break;
                case "WaitingForText":
                    await ProcessTranslationAsync(botClient, chatId, text, session, cancellationToken);
                    break;
                case "WaitingForDelete":
                    await ProcessHistoryDeletionAsync(botClient, chatId, text, session, cancellationToken);
                    break;
                default:
                    await botClient.SendMessage(chatId, "Будь ласка, скористайся меню нижче 👇", cancellationToken: cancellationToken);
                    await SendMainMenuAsync(botClient, chatId, cancellationToken);
                    break;
            }
        }

        static async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
        {
            var chatId = callbackQuery.Message!.Chat.Id;
            var session = GetSession(chatId);
            var action = callbackQuery.Data;
            
            await botClient.AnswerCallbackQuery(callbackQuery.Id, cancellationToken: cancellationToken);

            switch (action)
            {
                case "menu_languages":
                    session.State = "Idle";
                    await ShowAvailableLanguagesAsync(botClient, chatId, cancellationToken);
                    break;

                case "menu_translate":
                    session.State = "WaitingForLangs";
                    string msgLangs = "🔤 Введіть мови у форматі: **З ЯКОЇ НА ЯКУ** (їх абревіатури).\n\n" +
                                      "🔹 *Приклад 1:* `EN-UK` (з англійської на українську)\n" +
                                      "🔹 *Приклад 2:* `auto-EN-US` (автовизначення на американську англійську)\n\n" +
                                      "Чекаю на ваш ввід:";
                    await botClient.SendMessage(chatId, msgLangs, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                    break;

                case "menu_history":
                    session.State = "Idle";
                    await ShowHistoryAsync(botClient, chatId, session, cancellationToken);
                    break;

                case "menu_delete":
                    if (!session.History.Any())
                    {
                        await botClient.SendMessage(chatId, "📭 Ваша історія наразі порожня.", cancellationToken: cancellationToken);
                        await SendMainMenuAsync(botClient, chatId, cancellationToken);
                        break;
                    }
                    session.State = "WaitingForDelete";
                    await botClient.SendMessage(chatId, "🗑 Введіть **ID** записів, які хочете видалити, через кому (наприклад: `1, 3, 5`):", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                    break;
            }
        }
        

        private static async Task SendMainMenuAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            var welcomeText = "🤖 Привіт! Я розумний бот-перекладач на базі DeepL.\n\n" +
                              "Я вмію якісно перекладати тексти, розпізнавати мови та зберігати твою історію перекладів.\n" +
                              "Обери потрібну дію в меню нижче 👇";

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new [] { InlineKeyboardButton.WithCallbackData("🌍 Доступні мови", "menu_languages") },
                new [] { InlineKeyboardButton.WithCallbackData("🔄 Зробити переклад", "menu_translate") },
                new [] { InlineKeyboardButton.WithCallbackData("📖 Історія перекладів", "menu_history") },
                new [] { InlineKeyboardButton.WithCallbackData("🗑 Видалити з історії", "menu_delete") }
            });

            await botClient.SendMessage(chatId, welcomeText, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
        }

        private static async Task ShowAvailableLanguagesAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
        {
            try
            {
                await botClient.SendMessage(chatId, "⏳ Завантажую список мов з DeepL...", cancellationToken: cancellationToken);
                
                var targetLanguages = await _translator!.GetTargetLanguagesAsync();
                
                string responseText = "🌍 **Наявні мови для перекладу:**\n\n";
                foreach (var lang in targetLanguages)
                {
                    responseText += $"• {lang.Name} (`{lang.Code}`)\n";
                }

                await botClient.SendMessage(chatId, responseText, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                await SendMainMenuAsync(botClient, chatId, cancellationToken);
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(chatId, $"❌ Помилка завантаження мов: {ex.Message}", cancellationToken: cancellationToken);
            }
        }

        private static async Task ProcessLanguageSelectionAsync(ITelegramBotClient botClient, long chatId, string text, UserSession session, CancellationToken cancellationToken)
        {
            var parts = text.Split(new[] { '-', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length != 2)
            {
                await botClient.SendMessage(chatId, "⚠️ Неправильний формат. Спробуйте ще раз, наприклад: `EN-UK` або `auto-EN-US`", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
                return;
            }

            session.CurrentSourceLang = parts[0].ToUpper() == "AUTO" ? "auto" : parts[0].ToUpper();
            session.CurrentTargetLang = parts[1].ToUpper();
            session.State = "WaitingForText";

            await botClient.SendMessage(chatId, $"✅ Напрямок обрано: **{session.CurrentSourceLang}** ➡️ **{session.CurrentTargetLang}**\n\nТепер відправте текст для перекладу:", parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
        }

        private static async Task ProcessTranslationAsync(ITelegramBotClient botClient, long chatId, string text, UserSession session, CancellationToken cancellationToken)
        {
            try
            {
                string? sourceLang = session.CurrentSourceLang == "auto" ? null : session.CurrentSourceLang;
                
                var translatedText = await _translator!.TranslateTextAsync(
                    text,
                    sourceLanguageCode: sourceLang,
                    targetLanguageCode: session.CurrentTargetLang);
                
                string detectedSourceLang = sourceLang ?? translatedText.DetectedSourceLanguageCode;
                
                var record = new HistoryRecord
                {
                    Id = session.NextHistoryId++,
                    OriginalText = text,
                    TranslatedText = translatedText.Text,
                    SourceLang = detectedSourceLang,
                    TargetLang = session.CurrentTargetLang,
                    Date = DateTime.Now
                };
                session.History.Add(record);
                
                string resultMessage = $"✅ **Переклад ({detectedSourceLang} ➡️ {session.CurrentTargetLang}):**\n\n{translatedText.Text}";
                await botClient.SendMessage(chatId, resultMessage, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
            }
            catch (DeepLException ex)
            {
                await botClient.SendMessage(chatId, $"❌ Помилка DeepL (можливо, невірна абревіатура мови): {ex.Message}", cancellationToken: cancellationToken);
            }
            finally
            {
                session.State = "Idle";
                await SendMainMenuAsync(botClient, chatId, cancellationToken);
            }
        }

        private static async Task ShowHistoryAsync(ITelegramBotClient botClient, long chatId, UserSession session, CancellationToken cancellationToken)
        {
            if (!session.History.Any())
            {
                await botClient.SendMessage(chatId, "📭 Ваша історія перекладів порожня.", cancellationToken: cancellationToken);
                await SendMainMenuAsync(botClient, chatId, cancellationToken);
                return;
            }

            string response = "📖 **Ваша історія перекладів:**\n\n";
            foreach (var item in session.History)
            {
                response += $"🆔 **ID:** `{item.Id}`\n" +
                            $"📅 **Дата:** {item.Date:dd.MM.yyyy HH:mm}\n" +
                            $"🔄 **Напрямок:** {item.SourceLang} ➡️ {item.TargetLang}\n" +
                            $"📝 **Оригінал:** {item.OriginalText}\n" +
                            $"✅ **Переклад:** {item.TranslatedText}\n" +
                            $"➖\n";
            }

            await botClient.SendMessage(chatId, response, parseMode: ParseMode.Markdown, cancellationToken: cancellationToken);
            await SendMainMenuAsync(botClient, chatId, cancellationToken);
        }

        private static async Task ProcessHistoryDeletionAsync(ITelegramBotClient botClient, long chatId, string text, UserSession session, CancellationToken cancellationToken)
        {
            var stringIds = text.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int deletedCount = 0;

            foreach (var strId in stringIds)
            {
                if (int.TryParse(strId, out int idToRemove))
                {
                    var item = session.History.FirstOrDefault(h => h.Id == idToRemove);
                    if (item != null)
                    {
                        session.History.Remove(item);
                        deletedCount++;
                    }
                }
            }

            if (deletedCount > 0)
            {
                await botClient.SendMessage(chatId, $"🗑 Успішно видалено записів: {deletedCount}.", cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendMessage(chatId, "⚠️ Жодного запису з такими ID не знайдено.", cancellationToken: cancellationToken);
            }

            session.State = "Idle";
            await SendMainMenuAsync(botClient, chatId, cancellationToken);
        }

        static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);
            return Task.CompletedTask;
        }
    }
}