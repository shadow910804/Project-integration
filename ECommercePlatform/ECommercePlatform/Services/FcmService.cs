using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ECommercePlatform.Services
{
    public class FcmService
    {
        private readonly string _serverKey = "YOUR_FIREBASE_SERVER_KEY";
        private readonly string _senderId = "YOUR_SENDER_ID";
        private readonly HttpClient _httpClient;
        public FcmService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("key", "=" + _serverKey);
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Sender", "id=" + _senderId);
        }
        public async Task<bool> SendNotificationAsync(string deviceToken, string title, string body)
        {
            var payload = new
            {
                to = deviceToken,
                notification = new
                {
                    title,
                    body
                },
                priority = "high"
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://fcm.googleapis.com/fcm/send", content);
            return response.IsSuccessStatusCode;
        }
    }
}
