using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace boots.Class
{
    internal class GetTypeOfWorkCL
    {

        public static async Task GetTypeOfWork(ITelegramBotClient bot, long chatId) // ← возвращаем Task, а не void
        {
            try
            {
                var typeWorks = ManickEntities3.Context().TypeWork.ToList();

                if (!typeWorks.Any())
                {
                    await bot.SendMessage(chatId, "Нет доступных услуг");
                    return;
                }

                var keyboardButtons = new List<KeyboardButton[]>();

                foreach (var type in typeWorks)
                {
                    string buttonText = $"{type.Title} - {type.BasePrice} руб.";
                    keyboardButtons.Add(new[] { new KeyboardButton(buttonText) });
                }

                keyboardButtons.Add(new[] { new KeyboardButton("❌ Отмена") });

                await bot.SendMessage(
                    chatId,
                    "Выберите услугу:",
                    replyMarkup: new ReplyKeyboardMarkup(keyboardButtons)
                    {
                        ResizeKeyboard = true,
                        OneTimeKeyboard = true
                    }
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTypeOfWork: {ex.Message}");
                await bot.SendMessage(chatId, "Ошибка при загрузке услуг");
            }
        }

        public static async Task<bool> SaveTypeOfWork(ITelegramBotClient bot, long chatId, string selectedText, ApplicationCl userApp)
        {         
            try
            {
                using (var context = new ManickEntities3())
                {
                    // Получаем текущего пользователя по telegramId для проверки
                    var telegramId = chatId.ToString();
                    var currentClient = context.Client.FirstOrDefault(c => c.idTelegram == telegramId);

                    Console.WriteLine($"Client from DB: Id={currentClient?.id_Client}, Telegram={currentClient?.idTelegram}");

                    // Если IdClient в userApp неверный, используем ID из БД
                    if (userApp.IdClient == 0 && currentClient != null)
                    {
                        Console.WriteLine($"Fixing IdClient: {userApp.IdClient} -> {currentClient.id_Client}");
                        userApp.IdClient = currentClient.id_Client;
                    }

                    var window = context.Window.FirstOrDefault(u => u.id_Window == userApp.IdWindow);

                    if (window == null)
                    {
                        Console.WriteLine($"ERROR: Window {userApp.IdWindow} not found");
                        await bot.SendMessage(chatId, "❌ Ошибка: выбранное время не найдено");
                        return false;
                    }

                    Console.WriteLine($"Window status: {window.Status}, Date: {window.Date}, Time: {window.time}");

                    if (window.Status != "Open")
                    {
                        Console.WriteLine($"ERROR: Window is not open. Status: {window.Status}");
                        await bot.SendMessage(chatId, "❌ Это время уже занято");
                        return false;
                    }

                    // ВАЖНО: НЕЛЬЗЯ использовать строковую интерполяцию в LINQ to Entities
                    // Решение 1: Разбираем строку selectedText
                    var parts = selectedText.Split(new[] { " - " }, StringSplitOptions.None);
                    if (parts.Length != 2)
                    {
                        Console.WriteLine($"ERROR: Invalid format: {selectedText}");
                        await bot.SendMessage(chatId, "❌ Некорректный формат услуги");
                        return false;
                    }

                    var title = parts[0];
                    var priceText = parts[1].Replace(" руб.", "").Trim();

                    if (!int.TryParse(priceText, out int price))
                    {
                        Console.WriteLine($"ERROR: Cannot parse price: {priceText}");
                        await bot.SendMessage(chatId, "❌ Некорректная цена услуги");
                        return false;
                    }

                    // Ищем тип работы по названию и цене
                    var type = context.TypeWork
                        .FirstOrDefault(w => w.Title == title && w.BasePrice == price);

                    if (type == null)
                    {
                        // Альтернативный поиск: получить все и отфильтровать в памяти
                        var allTypes = context.TypeWork.ToList();
                        type = allTypes.FirstOrDefault(w =>
                            $"{w.Title} - {w.BasePrice} руб." == selectedText);

                        if (type == null)
                        {
                            Console.WriteLine($"ERROR: Type work not found for: Title='{title}', Price={price}");
                            await bot.SendMessage(chatId, "❌ Услуга не найдена");
                            return false;
                        }
                    }

                    Console.WriteLine($"Type work found: {type.Title}, Price: {type.BasePrice}, ID: {type.id_TypeWork}");

                    // Проверяем, что клиент существует
                    var client = context.Client.FirstOrDefault(c => c.id_Client == userApp.IdClient);
                    if (client == null)
                    {
                        Console.WriteLine($"ERROR: Client {userApp.IdClient} not found in DB");
                        // Пытаемся найти клиента по telegramId
                        client = context.Client.FirstOrDefault(c => c.idTelegram == telegramId);
                        if (client != null)
                        {
                            Console.WriteLine($"Found client by Telegram ID: {client.id_Client}");
                            userApp.IdClient = client.id_Client;
                        }
                        else
                        {
                            await bot.SendMessage(chatId, "❌ Ошибка: клиент не найден. Пройдите регистрацию через /start");
                            return false;
                        }
                    }

                    // Создаем заявку
                    var application = new Application()
                    {
                        Status = "Ожидание",
                        id_Window = userApp.IdWindow,
                        FactPrice = type.BasePrice,
                        id_Client = userApp.IdClient,
                        id_TypeWork = type.id_TypeWork,
                    };

                    Console.WriteLine($"Creating application: ClientId={application.id_Client}, WindowId={application.id_Window}");

                    // Обновляем статус окна
                    window.Status = "Booked";

                    // Сохраняем изменения
                    context.Application.Add(application);
                    context.SaveChanges();

                    Console.WriteLine($"SUCCESS: Application created with ID: {application.id_Application}");

                    await bot.SendMessage(chatId, $"✅ Запись оформлена! Номер: #{application.id_Application}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in SaveTypeOfWork: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }

                await bot.SendMessage(chatId, "❌ Ошибка при сохранении записи");
                return false;
            }
        }
        public static async Task ShowTypeOfWork(ITelegramBotClient bot, long chatId) // ← возвращаем Task, а не void
        {
            try
            {
                var typeWorks = ManickEntities3.Context().TypeWork.ToList();

                if (!typeWorks.Any())
                {
                    await bot.SendMessage(chatId, "Нет доступных услуг");
                    return;
                }

                var messageType = new StringBuilder();
                foreach (var type in typeWorks)
                {
                    messageType.AppendLine($"{type.Title}-{type.BasePrice}руб-{type.Description}");
                    messageType.AppendLine($"-----------------------------");
                }
                await bot.SendMessage(chatId, $"{messageType.ToString()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetTypeOfWork: {ex.Message}");
                await bot.SendMessage(chatId, "Ошибка при загрузке услуг");
            }
        }
    }
}
