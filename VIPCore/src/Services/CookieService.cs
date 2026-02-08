using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Reflection;
using SwiftlyS2.Shared;
using VIPCore.Services;

using Microsoft.Extensions.Logging;

namespace VIPCore.Services;

public class CookieService(ISwiftlyCore core)
{
    private object? _playerCookiesApi;

    // Cached reflection handles — resolved once in SetPlayerCookiesApi
    private MethodInfo? _loadMethod;
    private MethodInfo? _setMethodDef;   // open generic Set<T>
    private MethodInfo? _getMethodDef;   // open generic Get<T>
    private MethodInfo? _saveMethod;

    // Cache closed generic MakeGenericMethod results per type
    private readonly ConcurrentDictionary<Type, MethodInfo> _setMethodCache = new();
    private readonly ConcurrentDictionary<Type, MethodInfo> _getMethodCache = new();

    public void SetPlayerCookiesApi(object? playerCookiesApi)
    {
        _playerCookiesApi = playerCookiesApi;

        // Clear caches when API instance changes
        _loadMethod = null;
        _setMethodDef = null;
        _getMethodDef = null;
        _saveMethod = null;
        _setMethodCache.Clear();
        _getMethodCache.Clear();

        if (playerCookiesApi == null) return;

        try
        {
            var apiType = playerCookiesApi.GetType();

            _loadMethod = apiType.GetMethod("Load", [typeof(SwiftlyS2.Shared.Players.IPlayer)])
                ?? apiType.GetMethods().FirstOrDefault(m => m.Name == "Load" && m.GetParameters().Length == 1);

            _setMethodDef = apiType.GetMethods().FirstOrDefault(m =>
                m.Name == "Set" && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 3
                && m.GetParameters()[0].ParameterType == typeof(long));

            _getMethodDef = apiType.GetMethods().FirstOrDefault(m =>
                m.Name == "Get" && m.IsGenericMethodDefinition
                && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType == typeof(long));

            _saveMethod = apiType.GetMethod("Save", [typeof(long)])
                ?? apiType.GetMethods().FirstOrDefault(m => m.Name == "Save" && m.GetParameters().Length == 1);
        }
        catch (Exception ex)
        {
            core.Logger.LogWarning(ex, "[VIPCore] Failed to cache Cookies API reflection methods.");
        }
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
        if (_playerCookiesApi == null || _loadMethod == null) return;

        try
        {
            _loadMethod.Invoke(_playerCookiesApi, [player]);
        }
        catch (Exception ex)
        {
            core.Logger.LogWarning(ex, "[VIPCore] Cookies API Load failed.");
        }
    }

    public void SetCookie<T>(ulong steamId, string key, T value)
    {
        if (steamId <= 0) return;
        if (_playerCookiesApi == null || _setMethodDef == null) return;

        try
        {
            var closedSet = _setMethodCache.GetOrAdd(typeof(T), t => _setMethodDef.MakeGenericMethod(t));
            closedSet.Invoke(_playerCookiesApi, [(long)steamId, key, value!]);

            _saveMethod?.Invoke(_playerCookiesApi, [(long)steamId]);
        }
        catch (Exception ex)
        {
            core.Logger.LogWarning(ex, "[VIPCore] Cookies API Set/Save failed.");
        }
    }

    public T GetCookie<T>(ulong steamId, string key)
    {
        if (_playerCookiesApi == null || _getMethodDef == null) return default!;

        object? value = null;
        try
        {
            var closedGet = _getMethodCache.GetOrAdd(typeof(T), t => _getMethodDef.MakeGenericMethod(t));
            value = closedGet.Invoke(_playerCookiesApi, [(long)steamId, key]);
        }
        catch (Exception ex)
        {
            core.Logger.LogWarning(ex, "[VIPCore] Cookies API Get failed.");
            return default!;
        }
        if (value is null) return default!;

        if (value is T typedValue) return typedValue;

        try
        {
            var targetType = typeof(T);
            if (targetType.IsEnum)
            {
                var underlying = Enum.GetUnderlyingType(targetType);
                var convertedUnderlying = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
                return (T)Enum.ToObject(targetType, convertedUnderlying!);
            }

            var converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return converted is null ? default! : (T)converted;
        }
        catch
        {
            return default!;
        }
    }
}
