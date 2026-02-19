using boots.Class;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace boots
{
    internal class ShowWindowClass
    {
        public static async Task ShowWindow(ITelegramBotClient bot, long chatId)
        {
           

            try
            {
                var windows = ManickEntities3.Context().Window
                    .Where(w => w.Status == "Open")
                    .OrderBy(w => w.Date)
                    .ThenBy(w => w.time)
                    .ToList();

                if (!windows.Any())
                {
                    await bot.SendMessage(chatId, "Нет свободных окон");
                    Program.SetUserStatus(chatId, "None");
                    
                }

                var keyboardButtons = new List<KeyboardButton[]>();

                foreach (var window in windows)
                {
                    string buttonText = $"{window.Date:dd.MM.yyyy} {window.time:hh\\:mm}";
                    keyboardButtons.Add(new[] { new KeyboardButton(buttonText) });
                }

                keyboardButtons.Add(new[] { new KeyboardButton("❌ Отмена") });

                await bot.SendMessage(
                    chatId,
                    "Выберите время:",
                    replyMarkup: new ReplyKeyboardMarkup(keyboardButtons)
                    {
                        ResizeKeyboard = true,
                        OneTimeKeyboard = true
                    }
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in ShowWindow: {ex.Message}");
                await bot.SendMessage(chatId, "Ошибка при загрузке расписания");
                Program.SetUserStatus(chatId, "None");
            }
        }

        public static async Task<bool> GetWindow(ITelegramBotClient bot, long chatId, string selectedText, ApplicationCl userApp)
        {
            try
            {
                var allWindows = ManickEntities3.Context().Window.ToList();
                var window = allWindows.FirstOrDefault(w => $"{w.Date:dd.MM.yyyy} {w.time:hh\\:mm}" == selectedText);

                if (window != null)
                {
                    // Сохраняем в заявку ЭТОГО пользователя
                    userApp.IdWindow = window.id_Window;

                    // Обновляем статус окна

                    ManickEntities3.Context().SaveChanges();

                    await bot.SendMessage(chatId, $"✅ Время выбрано!\nДата: {window.Date:dd.MM.yyyy}\nВремя: {window.time:hh\\:mm}");
                    return true;
                }
                else
                {
                    await bot.SendMessage(chatId, "❌ Выбрано некорректное время");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetWindow: {ex.Message}");
                await bot.SendMessage(chatId, "❌ Ошибка при выборе времени");
                return false;
            }
        }
    }
}
