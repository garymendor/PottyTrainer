using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using CheapLoc;
using Dalamud;
using Newtonsoft.Json;

namespace PottyTrainer.Services;

public class LocWrapper
{
    private static readonly Dictionary<string, Dictionary<string, LocEntry>> FallbackLocData = [];

    public const string FallbackLangCode = "en";

    private readonly Localization localization;
    private readonly string locResourceDirectory;
    private readonly string locResourcePrefix;
    private readonly bool useEmbedded;
    private readonly Assembly assembly;

    public LocWrapper(string locResourceDirectory, string locResourcePrefix = "", bool useEmbedded = false)
    {
        localization = new Localization(locResourceDirectory, locResourcePrefix, useEmbedded);
        this.locResourceDirectory = locResourceDirectory;
        this.locResourcePrefix = locResourcePrefix;
        this.useEmbedded = useEmbedded;
        assembly = Assembly.GetCallingAssembly();
    }

    public void SetupWithLangCode(string langCode)
    {
        if (langCode == FallbackLangCode)
            SetupWithFallbacks();
        else
        {
            localization.SetupWithLangCode(langCode);
            ClearFallbacks();
        }
    }

    public void SetupWithUiCulture() 
    {
        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == FallbackLangCode)
            SetupWithFallbacks();
        else
        {
            localization.SetupWithUiCulture();
            ClearFallbacks();
        }
    }

    public void SetupWithFallbacks()
    {
        localization.SetupWithFallbacks();
        try
        {
            var locData = ReadFallbackLocData();
            if (locData == null)
            {
                Plugin.Log.Error("Could not load loc {0}. Using blank fallbacks.", FallbackLangCode);
                return;
            }

            var assemblyName = GetAssemblyName(assembly);
            if (assemblyName == null)
            {
                Plugin.Log.Error("Could not get calling assembly name while loading loc {0}. Using blank fallbacks.", FallbackLangCode);
                return;
            }

            FallbackLocData.Remove(assemblyName);
            FallbackLocData.Add(assemblyName, JsonConvert.DeserializeObject<Dictionary<string, LocEntry>>(locData) ?? []);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Could not load loc {0}. Using blank fallbacks.", FallbackLangCode);
        }
    }

    internal void ClearFallbacks()
    {
        var assemblyName = GetAssemblyName(assembly);
        if (assemblyName == null)
        {
            Plugin.Log.Error("Could not get calling assembly name while unloading loc {0}.", FallbackLangCode);
            return;
        }

        FallbackLocData.Remove(assemblyName);
    }

    public static string Localize(string key, string? fallBack = null) => LocalizeInternal(key) ?? Localization.Localize(key, fallBack ?? string.Empty);

    private static string? LocalizeInternal(string key)
    {
        var assemblyName = GetAssemblyName(Assembly.GetCallingAssembly());
        if (assemblyName == null)
            return null;

        if (!FallbackLocData.TryGetValue(assemblyName, out var fallbackLocData))
            return null;
        
        if (!fallbackLocData.TryGetValue(key, out var localizedString))
            return null;

        if (string.IsNullOrEmpty(localizedString.Message))
            return null;

        return localizedString.Message;
    }

    private string? ReadFallbackLocData()
    {
        if (!useEmbedded)
        {
            return File.ReadAllText(Path.Combine(locResourceDirectory, $"{locResourcePrefix}{FallbackLangCode}.json"));
        }

        var resourceStream = assembly.GetManifestResourceStream($"{locResourceDirectory}{locResourcePrefix}{FallbackLangCode}.json");
        if (resourceStream == null)
            return null;

        using var reader = new StreamReader(resourceStream);
        return reader.ReadToEnd();
    }

     private static string? GetAssemblyName(Assembly assembly) => assembly.GetName().Name;
}