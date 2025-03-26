using System;

namespace AnilistRPC
{
    public class AuthenticationData
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = string.Empty;
        public DateTime Expiry { get; set; } = DateTime.MinValue;
    }
}
