using boots.Class;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using static System.Net.Mime.MediaTypeNames;

namespace boots
{
    class Program
    {
        public static Dictionary<long, string> userStatuses = new Dictionary<long, string>();
        public static Dictionary<long, ClientCL> userClients = new Dictionary<long, ClientCL>();
        public static Dictionary<long, ApplicationCl> applica = new Dictionary<long, ApplicationCl>();
        static void Main(string[] args)
        {

            var client = new TelegramBotClient("7607335451:AAETpK5iPliKKvJG8-ZSqik6rwUSfuEcfaM");
            client.StartReceiving(Update, Error);
            Console.WriteLine("Бот запущен!");
            Console.ReadLine();

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

            try
            {
                // 1. Проверяем что есть сообщение
                var msg = update?.Message;
                if (msg == null) return;

                // 2. Проверяем что есть отправитель
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


