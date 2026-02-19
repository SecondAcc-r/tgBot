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
    internal class MyApplicationCL
    {

        public async static Task ShowMyApplication(ITelegramBotClient bot, Update update, Message msg, long chatId,bool isDelete)
        {
            


                try
                {
                    var user = msg.From;
                    long userId = user.Id;

                    // Преобразуем ID в строку ПЕРЕД запросом к БД
                    string telegramId = userId.ToString();

                    Console.WriteLine($"Ищем клиента с Telegram ID: '{telegramId}'");

                    // 1. Получаем ВСЕХ клиентов (для отладки)
                    var allClients = ManickEntities3.Context().Client.ToList();
                    Console.WriteLine($"Всего клиентов в БД: {allClients.Count}");

                    // 2. Находим нашего клиента
                    var tekuUser = ManickEntities3.Context().Client
                        .AsNoTracking()
                        .FirstOrDefault(u => u.idTelegram == telegramId);

                    // Альтернативный способ если не нашли:
                    if (tekuUser == null)
                    {
                        // Ищем без учета пробелов
                        tekuUser = ManickEntities3.Context().Client
                            .AsNoTracking()
                            .ToList() // Выгружаем в память
                            .FirstOrDefault(u =>
                                u.idTelegram != null &&
                                u.idTelegram.Trim() == telegramId.Trim());
                    }

                    if (tekuUser == null)
                    {
                        Console.WriteLine($"Клиент с Telegram ID '{telegramId}' не найден в БД");

                        // Покажем какие ID есть в БД для отладки
                        var existingIds = allClients
                            .Where(c => !string.IsNullOrEmpty(c.idTelegram))
                            .Select(c => c.idTelegram)
                            .Take(5)
                            .ToList();

                        if (existingIds.Any())
                        {
                            Console.WriteLine($"Примеры ID в БД: {string.Join(", ", existingIds)}");
                        }

                        await bot.SendMessage(msg.Chat.Id,
                            $"❌ Вы не зарегистрированы в системе.\n" +
                            $"Ваш Telegram ID: `{telegramId}`\n\n" +
                            $"Чтобы создать запись, нажмите 'Запись' в меню.");
                        Program.SetUserStatus(chatId, "None");
                        return;
                    }

                    Console.WriteLine($"Найден клиент: ID={tekuUser.id_Client}, Имя={tekuUser.Name}");

                    // 3. Получаем ВСЕ заявки клиента
                    var tekuApplications = ManickEntities3.Context().Application
                        .AsNoTracking()
                        .Where(a => a.id_Client == tekuUser.id_Client)
                        .OrderByDescending(a => a.id_Application) // Сначала новые
                        .ToList();

                if (isDelete == false)
                {


                    var completed = tekuApplications.Where(ch => ch.Status == "Выполнена").ToList();
                    foreach (var item in completed)
                    {
                        tekuApplications.Remove(item);
                    }
                    ManickEntities3.Context().SaveChanges();
                    Console.WriteLine($"Найдено заявок: {tekuApplications.Count}");

                    if (!tekuApplications.Any())
                    {
                        await bot.SendMessage(msg.Chat.Id,
                            $"📭 У вас нет активных записей, {tekuUser.Name}.");
                        Program.SetUserStatus(chatId, "None");
                        return;
                    }

                    // 4. Формируем красивое сообщение
                    var message = new StringBuilder();
                    message.AppendLine($"📋 **Ваши записи ({tekuApplications.Count}):**\n");

                    foreach (var application in tekuApplications)
                    {
                        // Получаем информацию об услуге
                        var typeWork = ManickEntities3.Context().TypeWork
                            .AsNoTracking()
                            .FirstOrDefault(t => t.id_TypeWork == application.id_TypeWork);

                        // Получаем информацию о времени
                        var window = ManickEntities3.Context().Window
                            .AsNoTracking()
                            .FirstOrDefault(w => w.id_Window == application.id_Window);

                        if (window != null && typeWork != null)
                        {
                            message.AppendLine($"📍 **Запись #{application.id_Application}**");
                            message.AppendLine($"   🗓️ {window.Date:dd.MM.yyyy} ⏰ {window.time:hh\\:mm}");
                            message.AppendLine($"   💼 {typeWork.Title}");
                            message.AppendLine($"   💰 {application.FactPrice} руб.");
                            message.AppendLine($"   📊 Статус: {application.Status}");
                            message.AppendLine();
                        }
                        else
                        {
                            message.AppendLine($"📍 Запись #{application.id_Application}");
                            message.AppendLine($"   ⚠️ Неполные данные");
                            message.AppendLine($"   📊 Статус: {application.Status}");
                            message.AppendLine();
                        }
                    }

                    message.AppendLine($"💡 Всего записей: {tekuApplications.Count}");

                    // 5. Отправляем сообщение
                    await bot.SendMessage(msg.Chat.Id, message.ToString());

                    // 6. Меняем статус
                    Program.SetUserStatus(chatId, "None");
                }
                else
                {
                    var keyboardButtons = new List<KeyboardButton[]>();

                    foreach (var app in tekuApplications)
                    {
                        string buttonText = $"{app.id_Application}";
                        keyboardButtons.Add(new[] { new KeyboardButton(buttonText) });
                    }
                    await bot.SendMessage(chatId, "Выберите запись для удаления", replyMarkup: new ReplyKeyboardMarkup(keyboardButtons){ResizeKeyboard = true,OneTimeKeyboard = true});
                   Program.SetUserStatus(chatId , "DELETE");
                }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка в ShowMyApplications:");
                    Console.WriteLine($"Сообщение: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                    await bot.SendMessage(msg.Chat.Id,
                        $"❌ Произошла ошибка при загрузке записей.\n" +
                        $"Попробуйте позже или обратитесь к администратору.");

                    Program.SetUserStatus(chatId, "None");
                }
        }
        public async static Task Delete(ITelegramBotClient bot, long chatId, int msg)
        {

            try
            {
                using (var context = new ManickEntities3())
                {
                    var appForDelete = context.Application.FirstOrDefault(a => a.id_Application == msg);
                    if (appForDelete == null)
                    {
                        Console.WriteLine("Ошибка: заявка не найдена");
                        return;
                    }

                    var window = context.Window.FirstOrDefault(w => w.id_Window == appForDelete.id_Window);
                    if (window == null)
                    {
                        Console.WriteLine("Ошибка: окно не найдено");
                        return;
                    }
                    window.Status = "Open";
                    context.Application.Remove(appForDelete);
                    context.SaveChanges();
                    Program.SetUserStatus(chatId, "None");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в Delete: {ex.Message}");
                await bot.SendMessage(chatId, "❌ Ошибка при отмене записи");
            }
        } 

               
    }
            
}
