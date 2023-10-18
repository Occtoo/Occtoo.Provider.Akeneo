namespace Occtoo.Akeneo.DataSync.Model;
public record AuthorizedToken(string AccessToken,
    int ExpiresIn = 3600,
    string TokenType = "",
    string RefreshToken = "",
    string Scope = "");
