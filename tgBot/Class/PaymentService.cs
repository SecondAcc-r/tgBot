using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yandex.Checkout.V3;
namespace tgBot.Class
{
    internal class PaymentService
    {
        private readonly Client _client;

        public PaymentService()
        {
            // Вставь сюда свои данные из личного кабинета ЮKassa
            string shopId = "1288657";
            string secretKey = "test_d69ejIctE4IJJRHpawDFuKsFjU8jdR8MK0c43xY-9Rs";

            _client = new Client(shopId, secretKey);
        }

        /// <summary>
        /// Создает платеж и возвращает ссылку на оплату
        /// </summary>
        /// <param name="amount">Сумма в рублях</param>
        /// <param name="description">Описание платежа (например, номер заявки)</param>
        /// <param name="orderId">ID заявки в нашей БД (сохраняем в metadata)</param>
        public async Task<string> CreatePaymentLinkAsync(decimal amount, string description, int orderId)
        {
            var newPayment = new NewPayment
            {
                Amount = new Amount
                {
                    Value = amount,
                    Currency = "RUB"
                },
                Confirmation = new Confirmation
                {
                    Type = ConfirmationType.Redirect,
                    ReturnUrl = "https://web.telegram.org/k/#@FitOriginalBoots_bot" // Ссылка возврата (можно на бота)
                },
                Capture = true, // Сразу проводить оплату
                Description = description,
                Metadata = new Dictionary<string, string>
                {
                    { "order_id", orderId.ToString() } // Важно: привязываем ID заявки
                }
            };

            try
            {
                var payment = _client.CreatePayment(newPayment);
                return payment.Confirmation.ConfirmationUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка создания платежа: {ex.Message}");
                throw; // Пробрасываем ошибку дальше, чтобы обработать в боте
            }
        }
    }
}
