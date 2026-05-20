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
            string shopId = "1288657";
            string secretKey = "test_d69ejIctE4IJJRHpawDFuKsFjU8jdR8MK0c43xY-9Rs";
            _client = new Client(shopId, secretKey);
        }
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
                    ReturnUrl = "https://web.telegram.org/k/#@FitOriginalBoots_bot" 
                },
                Capture = true, 
                Description = description,
                Metadata = new Dictionary<string, string>
                {
                    { "order_id", orderId.ToString() } 
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
                throw; 
            }
        }
    }
}
