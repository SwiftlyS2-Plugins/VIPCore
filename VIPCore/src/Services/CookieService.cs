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

    public void SetCookie<T>(ulong steamId, string key, T value)
    {
        if (steamId <= 0) return;
        if (_playerCookiesApi == null) return;

        try
        {
            _playerCookiesApi.Set((long)steamId, key, value!);
            _playerCookiesApi.Save((long)steamId);
        }
        catch (Exception ex)
        {
            core.Logger.LogWarning(ex, "[VIPCore] Cookies API Set/Save failed.");
        }
    }

    public T GetCookie<T>(ulong steamId, string key)
    {
        if (_playerCookiesApi == null) return default!;

        T? value = default;
        try
        {
            value = _playerCookiesApi.Get<T>((long)steamId, key);
        }
        catch (System.Collections.Generic.KeyNotFoundException)
        {
            // Cookie doesn't exist yet, return default without warning
            return default!;
        }
        catch (Exception ex)
        {
            core.Logger.LogWarning(ex, "[VIPCore] Cookies API Get failed.");
            return default!;
        }

        return value is null ? default! : value;
    }
}