using System;
using Cookies.Contract;
using SwiftlyS2.Shared;
using VIPCore.Services;

using Microsoft.Extensions.Logging;

namespace VIPCore.Services;

public class CookieService(ISwiftlyCore core)
{
    private IPlayerCookiesAPIv1? _playerCookiesApi;

    public void SetPlayerCookiesApi(IPlayerCookiesAPIv1? playerCookiesApi)
    {
        _playerCookiesApi = playerCookiesApi;
    }

    public void LoadCookies()
    {
        // Cookies plugin handles persistence.
    }

    public void SaveCookies()
    {
        // Cookies plugin handles persistence.
    }

    public void LoadForPlayer(SwiftlyS2.Shared.Players.IPlayer player)
    {
        if (_playerCookiesApi == null) return;

        try
        {
            _playerCookiesApi.Load(player);
        }
        catch (Exception ex)
        {
            core.Logger.LogWarning(ex, "[VIPCore] Cookies API Load failed.");
        }
    }

    public void SetCookie<T>(long accountId, string key, T value)
    {
        if (accountId <= 0) return;
        if (_playerCookiesApi == null) return;

        try
        {
            _playerCookiesApi.Set(accountId, key, value!);
            _playerCookiesApi.Save(accountId);
        }
        catch (Exception ex)
        {
            core.Logger.LogWarning(ex, "[VIPCore] Cookies API Set/Save failed.");
        }
    }

    public T GetCookie<T>(long accountId, string key)
    {
        if (_playerCookiesApi == null) return default!;

        T? value = default;
        try
        {
            value = _playerCookiesApi.Get<T>(accountId, key);
        }
        catch (Exception ex)
        {
            core.Logger.LogWarning(ex, "[VIPCore] Cookies API Get failed.");
            return default!;
        }

        return value is null ? default! : value;
    }
}
