using boots.Class;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
namespace boots
{
    class Program
    {
        public static Dictionary<long, string> userStatuses = new Dictionary<long, string>();
        public static Dictionary<long, ClientCL> userClients = new Dictionary<long, ClientCL>();
        public static Dictionary<long, ApplicationCl> applica = new Dictionary<long, ApplicationCl>();
        static async Task Main(string[] args)
        {
            Console.WriteLine("🚀 Запуск бота + Webhook сервер...");

            // 2. Инициализируем ГЛОБАЛЬНУЮ переменную
            var BotInstance = new TelegramBotClient("7607335451:AAETpK5iPliKKvJG8-ZSqik6rwUSfuEcfaM");

            using var cts = new CancellationTokenSource();

            // 3. Запускаем бота
            _ = Task.Run(() =>
            {
                BotInstance.StartReceiving(Update, Error, cancellationToken: cts.Token);
                Console.WriteLine("✅ Бот запущен (Polling)...");
            });

            // 4. Создаем Веб-сервер (теперь WebApplication должен быть виден)
            var builder = WebApplication.CreateBuilder(args);

            builder.WebHost.UseUrls("http://0.0.0.0:5000");

            var app = builder.Build();

            app.MapPost("/api/yookassa-webhook", async (HttpContext context) =>
            {
                await HandleYookassaWebhook(context);
            });

            app.MapGet("/", async () => "Bot is running! Webhook active.");

            Console.WriteLine("🌐 Веб-сервер запущен на http://85.239.34.112:5000");

            await app.RunAsync();
        }


        private static async Task HandleYookassaWebhook(HttpContext context)
        {
            try
            {
                // Читаем тело запроса (JSON от ЮKassa)
                using var reader = new System.IO.StreamReader(context.Request.Body);
                var json = await reader.ReadToEndAsync();

                Console.WriteLine($"🔔 Получен Webhook: {json}");

                // Парсим JSON (простой способ без лишних библиотек)
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Проверяем тип события
                var eventType = root.GetProperty("event").GetString();

                if (eventType == "payment.succeeded")
                {
                    // Платеж успешен!
                    var metadata = root.GetProperty("object").GetProperty("metadata");

                    if (metadata.TryGetProperty("order_id", out var orderIdProp))
                    {
                        int orderId = orderIdProp.GetInt32();
                        Console.WriteLine($"💰 Оплата прошла для заявки #{orderId}");

                        // МЕНЯЕМ СТАТУС В БАЗЕ
                        using (var db = new ManickEntities3())
                        {
                            var app = db.Application.FirstOrDefault(a => a.id_Application == orderId);
                            if (app != null)
                            {
                                app.Status = "Подтверждено"; // Или "Оплачено"
                                db.SaveChanges();

                                // Уведомляем клиента
                                // Нам нужно знать chatId клиента. 
                                // Вариант А: Сохранить chatId в базе при создании заявки (рекомендую!)
                                // Вариант Б: Найти клиента по ID и взять его chatId (если есть связь)

                                // Пример (предполагаем, что у тебя в Application есть поле Client или id_Client, а в Client нет chatId)
                                // ЛУЧШЕ: При создании заявки сохрани chatId прямо в Application в поле TelegramChatId (добавь его в БД)

                                // Если поля chatId в заявке нет, придется искать через клиента, если там есть telegram_id
                                // Для примера предположим, что мы можем найти chatId:
                                // long chatId = ...; 
                                // await bot.SendMessage(chatId, "✅ Оплата прошла! Вы записаны.");

                                Console.WriteLine($"Статус заявки #{orderId} изменен на 'Подтверждено'");
                            }
                        }
                    }
                }

                context.Response.StatusCode = 200; // Ответ ЮKassa: "Всё ок"
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка обработки вебхука: {ex.Message}");
                context.Response.StatusCode = 500; // Ошибка
            }
        }





        private static async Task Error(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
        {
            Console.WriteLine($"Error: {exception.Message}");
            await Task.CompletedTask;
        }

        private static string GetUserStatus(long chatId)
        {
            return userStatuses.TryGetValue(chatId, out string status) ? status : "None";
        }

        public static void SetUserStatus(long chatId, string status)
        {
            userStatuses[chatId] = status;
            Console.WriteLine($"User {chatId} status: {status}");
        }

        private static ClientCL GetUserClient(long chatId)
        {
            if (!userClients.ContainsKey(chatId))
            {
                userClients[chatId] = new ClientCL();
            }
            return userClients[chatId];
        }

        private static ApplicationCl GetUserApplication(long chatId)
        {
            if (!applica.ContainsKey(chatId))
            {
                applica[chatId] = new ApplicationCl();
            }
            return applica[chatId];
        }

        async static Task Update(ITelegramBotClient bot, Update update, CancellationToken token)
        {

            if (update.CallbackQuery != null)
            {
                var query = update.CallbackQuery;
                var data = query.Data;
                var chatId = query.Message.Chat.Id;
                var messageId = query.Message.MessageId;

                // --- НОВАЯ ЛОГИКА: ОБРАБОТКА ОПЛАТЫ ---
                if (data.StartsWith("pay_"))
                {
                    // Сначала убираем кружок загрузки у пользователя
                    await bot.AnswerCallbackQuery(query.Id, "Генерирую ссылку на оплату...");

                    // Извлекаем ID заявки
                    if (int.TryParse(data.Replace("pay_", ""), out int appId))
                    {
                        try
                        {
                            // 1. Находим заявку в базе, чтобы узнать сумму и проверить статус
                            using (var db = new ManickEntities3())
                            {
                                var app = db.Application.FirstOrDefault(a => a.id_Application == appId);

                                if (app == null)
                                {
                                    await bot.SendMessage(chatId, "❌ Заявка не найдена.");
                                    return;
                                }

                                if (app.Status == "Подтверждено" || app.Status == "Оплачено")
                                {
                                    await bot.SendMessage(chatId, "ℹ️ Эта запись уже оплачена!");
                                    return;
                                }

                                // 2. Вызываем наш класс оплаты
                                var paymentService = new tgBot.Class.PaymentService();

                                string link = await paymentService.CreatePaymentLinkAsync(
                                    amount: (decimal)app.FactPrice,
                                    description: $"Оплата записи #{app.id_Application}",
                                    orderId: app.id_Application
                                );

                                // 3. Редактируем сообщение: меняем текст и добавляем ссылку-кнопку
                                var urlKeyboard = new InlineKeyboardMarkup(new[]
                                {
                        new[] { InlineKeyboardButton.WithUrl("🔗 Перейти к оплате", link) }
                    });

                                await bot.EditMessageText(
                                    chatId,
                                    messageId,
                                    $"💳 **Оплата заказа #{app.id_Application}**\n\n" +
                                    $"Сумма: **{app.FactPrice} руб.**\n" +
                                    $"Нажмите кнопку ниже, чтобы перейти на безопасную страницу оплаты ЮKassa.",
                                    parseMode: ParseMode.Markdown,
                                    replyMarkup: urlKeyboard
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка оплаты: {ex.Message}");
                            await bot.SendMessage(chatId, $"❌ Не удалось создать платеж: {ex.Message}. Попробуйте позже.");
                        }
                    }
                    return; // Важно выйти, чтобы код ниже не выполнялся
                }

                // ... тут могут быть другие проверки (complete_, enroll_ и т.д.) ...

                // Если кнопка не наша, просто снимаем нагрузку
                await bot.AnswerCallbackQuery(query.Id);
            }





            try
            {
                var msg = update?.Message;
                if (msg == null) return;
                var user = msg.From;
                if (user == null) return;

                long userId = user.Id;
                long chatId = msg.Chat.Id;
                string text = msg.Text;

                Console.WriteLine($"Message from {user.FirstName} ({chatId}): {text}");

                // 3. Инициализируем статус если нужно
                if (!userStatuses.ContainsKey(chatId))
                {
                    userStatuses[chatId] = "None";
                }

                string currentStatus = GetUserStatus(chatId);

                // ОБЪЯВЛЯЕМ переменные здесь, чтобы они были доступны во всей функции
                ClientCL clientCL = null;
                ApplicationCl userApp = null;

                // Обработка команды /start
                if (text?.ToLower().Contains("/start") == true)
                {
                    // Получаем данные пользователя
                    clientCL = GetUserClient(chatId);
                    userApp = GetUserApplication(chatId);

                    SetUserStatus(chatId, "None");
                    string userIds = userId.ToString();

                    using (var context = new ManickEntities3())
                    {
                        var profile = context.Client.FirstOrDefault(c => c.idTelegram == userIds);
                        clientCL.idTelegram = userId.ToString();

                        if (profile != null)
                        {
                            // ВАЖНО: Сохраняем IdClient в userApp!
                            userApp.IdClient = profile.id_Client;
                            clientCL.Name = profile.Name;
                            clientCL.Surname = profile.Surname;

                            await bot.SendMessage(
                               chatId,
                               $"Привет {profile.Name}!",
                               replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                            new[] { new KeyboardButton("📅 Запись") },
                            new[] { new KeyboardButton("📋 Мои записи"), new KeyboardButton("Отменить запись") },
                            new[] { new KeyboardButton("💅 Услуги")},
                            new[] { new KeyboardButton("⭐ Отзывы"), new KeyboardButton("👩‍💼 Контакты") }
                                })
                               {
                                   ResizeKeyboard = true
                               }
                            );
                        }
                        else
                        {
                            await bot.SendMessage(chatId, "Давайте познакомимся 😊");
                            await bot.SendMessage(chatId, "Введите фамилию");
                            SetUserStatus(chatId, "Familia");
                        }
                    }
                    return;
                }

                // Для всех остальных случаев получаем данные здесь
                clientCL = GetUserClient(chatId);
                userApp = GetUserApplication(chatId);

                // Обработка по статусам
                switch (currentStatus)
                {
                    case "Familia":
                        if (text != null)
                        {
                            clientCL.Surname = text;
                            await bot.SendMessage(chatId, "Введите имя");
                            SetUserStatus(chatId, "Name");
                        }
                        break;

                    case "Name":
                        if (text != null)
                        {
                            clientCL.Name = text;
                            clientCL.idTelegram = userId.ToString();

                            // Используем один контекст для всей операции
                            using (var context = new ManickEntities3())
                            {
                                // Проверяем, не зарегистрирован ли уже пользователь
                                var existingClient = context.Client
                                    .FirstOrDefault(c => c.idTelegram == clientCL.idTelegram);

                                Client dbClient;

                                if (existingClient == null)
                                {
                                    // Создаем нового клиента
                                    dbClient = new Client
                                    {
                                        Surname = clientCL.Surname,
                                        Name = clientCL.Name,
                                        idTelegram = clientCL.idTelegram,
                                    };

                                    context.Client.Add(dbClient);
                                }
                                else
                                {
                                    // Обновляем существующего клиента
                                    existingClient.Surname = clientCL.Surname;
                                    existingClient.Name = clientCL.Name;
                                    dbClient = existingClient;
                                }

                                context.SaveChanges();
                                userApp.IdClient = dbClient.id_Client;
                            }

                            SetUserStatus(chatId, "None");

                            // Отправляем приветственное сообщение
                            await bot.SendMessage(
                                chatId,
                                $"Привет {clientCL.Name}! Регистрация завершена!",
                                replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                            new[] { new KeyboardButton("📅 Запись") },
                            new[] { new KeyboardButton("📋 Мои записи"), new KeyboardButton("Отменить запись") },
                            new[] { new KeyboardButton("💅 Услуги")},
                            new[] { new KeyboardButton("⭐ Отзывы"), new KeyboardButton("👩‍💼 Контакты") }
                                })
                                {
                                    ResizeKeyboard = true
                                }
                            );
                        }
                        break;

                    case "None":
                        if (text == "❌ Отмена")
                        {
                            SetUserStatus(chatId, "None");
                            await bot.SendMessage(
                                chatId,
                                "Отменено",
                                replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                            new[] { new KeyboardButton("📅 Запись") },
                            new[] { new KeyboardButton("📋 Мои записи"), new KeyboardButton("Отменить запись") },
                            new[] { new KeyboardButton("💅 Услуги")},
                            new[] { new KeyboardButton("⭐ Отзывы"), new KeyboardButton("👩‍💼 Контакты") }
                                })
                                {
                                    ResizeKeyboard = true
                                }
                            );
                        }
                        else if (text == "📅 Запись" || text == "Запись")
                        {
                            await ShowWindowClass.ShowWindow(bot, chatId);
                            SetUserStatus(chatId, "Window");
                        }
                        else if (text == "📋 Мои записи" || text == "Мои записи")
                        {
                            await MyApplicationCL.ShowMyApplication(bot, update, msg, chatId,false);
                        }
                        else if(text =="⭐ Отзывы" || text == "Отзывы")
                        {
                            var inlineKeyboard = new InlineKeyboardMarkup(new[]
                             {
                                new[]
                                {
                                    InlineKeyboardButton.WithUrl("👉 Нажмите чтобы перейти","https://t.me/+5uyVOzwoui5kYTYy")
                                }
                            });
                            await bot.SendMessage(chatId, "Присоединяйтесь к чату!", replyMarkup: inlineKeyboard);
                        }
                        else if (text == "💅 Услуги" || text == "Услуги")
                        {
                            await GetTypeOfWorkCL.ShowTypeOfWork(bot, chatId);
                        }
                        else if (text == "Отменить запись")
                        {
                            await MyApplicationCL.ShowMyApplication(bot, update, msg, chatId, false);
                            await MyApplicationCL.ShowMyApplication(bot, update, msg, chatId, true);
                        }
                        break;
                    case "DELETE":
                        await MyApplicationCL.Delete(bot,chatId,Convert.ToInt32(msg.Text));
                        await bot.SendMessage(
                                chatId,
                                "Запись удалена",
                                replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                            new[] { new KeyboardButton("📅 Запись") },
                            new[] { new KeyboardButton("📋 Мои записи"), new KeyboardButton("Отменить запись") },
                            new[] { new KeyboardButton("💅 Услуги")},
                            new[] { new KeyboardButton("⭐ Отзывы"), new KeyboardButton("👩‍💼 Контакты") }
                                })
                                {
                                    ResizeKeyboard = true
                                }
                            );
                        break;
                    case "Window":
                        if (text == "❌ Отмена")
                        {
                            SetUserStatus(chatId, "None");
                            await bot.SendMessage(
                                chatId,
                                "Отменено",
                                replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                            new[] { new KeyboardButton("📅 Запись") },
                            new[] { new KeyboardButton("📋 Мои записи"), new KeyboardButton("Отменить запись") },
                            new[] { new KeyboardButton("💅 Услуги")},
                            new[] { new KeyboardButton("⭐ Отзывы"), new KeyboardButton("👩‍💼 Контакты") }
                                })
                                {
                                    ResizeKeyboard = true
                                }
                            );
                        }
                        else
                        {
                            bool success = await ShowWindowClass.GetWindow(bot, chatId, text, userApp);
                            if (success)
                            {
                                Console.WriteLine($"IdClient перед выбором услуги: {userApp.IdClient}");
                                await GetTypeOfWorkCL.GetTypeOfWork(bot, chatId);
                                SetUserStatus(chatId, "SaveTypeOfWork");
                            }
                        }
                        break;
                    case "GetTypeOfWork":
                        if (text == "❌ Отмена")
                        {
                            SetUserStatus(chatId, "None");
                            await bot.SendMessage(
                                chatId,
                                "Отменено",
                                replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                            new[] { new KeyboardButton("📅 Запись") },
                            new[] { new KeyboardButton("📋 Мои записи"), new KeyboardButton("Отменить запись") },
                            new[] { new KeyboardButton("💅 Услуги")},
                            new[] { new KeyboardButton("⭐ Отзывы"), new KeyboardButton("👩‍💼 Контакты") }
                                })
                                {
                                    ResizeKeyboard = true
                                }
                            );
                        }
                        break;
                    case "SaveTypeOfWork":
                    
                        if (text == "❌ Отмена")
                        {
                            SetUserStatus(chatId, "None");
                            await bot.SendMessage(
                                chatId,
                                "Отменено",
                                replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                            new[] { new KeyboardButton("📅 Запись") },
                            new[] { new KeyboardButton("📋 Мои записи"), new KeyboardButton("Отменить запись") },
                            new[] { new KeyboardButton("💅 Услуги")},
                            new[] { new KeyboardButton("⭐ Отзывы"), new KeyboardButton("👩‍💼 Контакты") }
                                })
                                {
                                    ResizeKeyboard = true
                                }
                            );
                        }
                        else
                        {
                            bool success = await GetTypeOfWorkCL.SaveTypeOfWork(bot, chatId, text, userApp);
                            if (success)
                            {
                                // Очищаем временные данные
                                userClients.Remove(chatId);
                                applica.Remove(chatId);

                                SetUserStatus(chatId, "None");

                                await bot.SendMessage(
                                    chatId,
                                    "✅ Запись создана!",
                                    replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                            new[] { new KeyboardButton("📅 Запись") },
                            new[] { new KeyboardButton("📋 Мои записи"), new KeyboardButton("Отменить запись") },
                            new[] { new KeyboardButton("💅 Услуги")},
                            new[] { new KeyboardButton("⭐ Отзывы"), new KeyboardButton("👩‍💼 Контакты") }
                                })
                                    {
                                        ResizeKeyboard = true
                                    }
                            );
                            }
                            else
                            {
                                await bot.SendMessage(chatId, "Извините время уже занято(");
                                SetUserStatus(chatId, "None");
                                await bot.SendMessage(
                                    chatId,
                                    "Выберите другое доступное время",
                                   replyMarkup: new ReplyKeyboardMarkup(new[]
                                {
                            new[] { new KeyboardButton("📅 Запись") },
                            new[] { new KeyboardButton("📋 Мои записи"), new KeyboardButton("Отменить запись") },
                            new[] { new KeyboardButton("💅 Услуги")},
                            new[] { new KeyboardButton("⭐ Отзывы"), new KeyboardButton("👩‍💼 Контакты") }
                                })
                                   {
                                       ResizeKeyboard = true
                                   }
                            );
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Update: {ex.Message}");
            }
        }
    }
}


