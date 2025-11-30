using System;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib.BUTR.Extensions;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;
using System.Linq;

namespace Helpers
{
    internal static class ReflectionHelpers
    {
        private delegate void SetHeroStaticBodyPropertiesDelegate(Hero instance, StaticBodyProperties value);
        private static readonly SetHeroStaticBodyPropertiesDelegate? _setter =
            AccessTools2.GetPropertySetterDelegate<SetHeroStaticBodyPropertiesDelegate>(typeof(Hero), "StaticBodyProperties");

        public static bool TrySetHeroStaticBodyProperties(Hero hero, StaticBodyProperties value)
        {
            if (_setter is null)
            {
                InformationManager.DisplayMessage(new InformationMessage("[Adoption] Reflection: Hero.StaticBodyProperties setter not found for this game version."));
                return false;
            }

            try
            {
                _setter(hero, value);
                return true;
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[Adoption] Reflection failed: {ex.Message}"));
                return false;
            }
        }

        public static bool TrySetCharacterHiddenInEncyclopedia(CharacterObject characterObject, bool value)
        {
            if (characterObject == null)
                return false;

            try
            {
                var type = characterObject.GetType();

                // Try common property name variants
                string[] propNames = new[]
                {
                    "HiddenInEncylopedia",
                    "HiddenInEncyclopedia",
                    "IsHiddenInEncyclopedia",
                    "HiddenInEncylopaedia",
                    "HiddenInEncyclopaedia"
                };

                foreach (var name in propNames)
                {
                    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
                    {
                        prop.SetValue(characterObject, value);
                        return true;
                    }
                }

                // Try fields
                string[] fieldNames = new[] { "hiddenInEncylopedia", "hiddenInEncyclopedia" };
                foreach (var fName in fieldNames)
                {
                    var field = type.GetField(fName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null && field.FieldType == typeof(bool))
                    {
                        field.SetValue(characterObject, value);
                        return true;
                    }
                }

                // Try setter methods
                string[] methodNames = new[]
                {
                    "SetHiddenInEncyclopedia",
                    "SetHiddenInEncylopedia",
                    "SetHidden",
                    "SetHiddenInEncyclopaedia"
                };
                foreach (var mName in methodNames)
                {
                    var m = type.GetMethod(mName, BindingFlags.Public | BindingFlags.Instance);
                    if (m != null)
                    {
                        var pars = m.GetParameters();
                        if (pars.Length == 1 && pars[0].ParameterType == typeof(bool))
                        {
                            m.Invoke(characterObject, new object[] { value });
                            return true;
                        }
                    }
                }

                // Try AccessTools2 fallback
                try
                {
                    var setter = AccessTools2.GetPropertySetterDelegate<Action<CharacterObject, bool>>(type, "HiddenInEncylopedia");
                    if (setter != null)
                    {
                        setter(characterObject, value);
                        return true;
                    }
                }
                catch
                {
                    // ignore
                }

                InformationManager.DisplayMessage(new InformationMessage("[Adoption] Could not find HiddenInEncyclopedia setter on CharacterObject for this game version."));
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[Adoption] Reflection error setting HiddenInEncyclopedia: {ex.Message}"));
            }

            return false;
        }

        public static void LogToModule(string message)
        {
            try
            {
                // Determine module folder from assembly location (robust for Modules\<Module>\bin\<...>)
                string moduleDir = null;
                try
                {
                    var asmDir = Path.GetDirectoryName(typeof(Adoption.SubModule).Assembly.Location) ?? string.Empty;
                    var dirInfo = new DirectoryInfo(asmDir);
                    for (var anc = dirInfo; anc != null; anc = anc.Parent)
                    {
                        if (anc.Parent != null && anc.Parent.Name.Equals("Modules", StringComparison.OrdinalIgnoreCase))
                        {
                            moduleDir = anc.FullName;
                            break;
                        }
                    }
                }
                catch
                {
                    // ignore and fallback below
                }

                moduleDir ??= Path.Combine(Directory.GetCurrentDirectory(), "Modules", "Adoption");
                if (!Directory.Exists(moduleDir))
                    Directory.CreateDirectory(moduleDir);

                var logPath = Path.Combine(moduleDir, "adoption_actions.log");
                var entry = $"{DateTime.UtcNow:o} {message}{Environment.NewLine}";
                File.AppendAllText(logPath, entry);
            }
            catch
            {
                // never throw from logging
            }
        }

        public static void LogWealthSnapshot(string context)
        {
            try
            {
                string moduleDir = null;
                try
                {
                    var asmDir = Path.GetDirectoryName(typeof(Adoption.SubModule).Assembly.Location) ?? string.Empty;
                    var dirInfo = new DirectoryInfo(asmDir);
                    for (var anc = dirInfo; anc != null; anc = anc.Parent)
                    {
                        if (anc.Parent != null && anc.Parent.Name.Equals("Modules", StringComparison.OrdinalIgnoreCase))
                        {
                            moduleDir = anc.FullName;
                            break;
                        }
                    }
                }
                catch
                {
                }

                moduleDir ??= Path.Combine(Directory.GetCurrentDirectory(), "Modules", "Adoption");
                if (!Directory.Exists(moduleDir))
                    Directory.CreateDirectory(moduleDir);

                var logPath = Path.Combine(moduleDir, "adoption_actions.log");

                var sb = new StringBuilder();
                sb.Append($"{DateTime.UtcNow:o} [{context}] ");

                try
                {
                    // Player clan snapshot
                    var clan = Clan.PlayerClan;
                    if (clan != null)
                    {
                        sb.Append("PlayerClan: ");
                        sb.Append(GetNumericPropsString(clan));
                        sb.Append("; ");
                    }
                }
                catch { }

                try
                {
                    // Main party snapshot (MobileParty.MainParty)
                    try
                    {
                        var campaignAssembly = typeof(Hero).Assembly;
                        var mobilePartyType = campaignAssembly.GetType("TaleWorlds.CampaignSystem.MobileParty");
                        if (mobilePartyType != null)
                        {
                            var mainPartyProp = mobilePartyType.GetProperty("MainParty", BindingFlags.Public | BindingFlags.Static);
                            if (mainPartyProp != null)
                            {
                                var mainParty = mainPartyProp.GetValue(null);
                                if (mainParty != null)
                                {
                                    sb.Append("MainParty: ");
                                    sb.Append(GetNumericPropsString(mainParty));
                                    sb.Append("; ");
                                }
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        // Also check PlayerCharacter (Hero.MainHero) fields that might contain money
                        var hero = Hero.MainHero;
                        if (hero != null)
                        {
                            sb.Append("MainHero: ");
                            sb.Append(GetNumericPropsString(hero));
                            sb.Append("; ");
                        }
                    }
                    catch { }

                    File.AppendAllText(logPath, sb.ToString() + Environment.NewLine);
                }
                catch
                {
                    // ignore logging errors
                }
            }
            catch
            {
                // ignore logging errors
            }
        }

        public static string GetNumericPropsString(object obj)
        {
            try
            {
                var type = obj.GetType();
                var names = new[] { "Gold", "Money", "Denars", "Treasury", "ClanGold", "ClanTreasury", "Wealth", "Balance", "GoldAmount" };
                var sb = new StringBuilder();
                foreach (var name in names)
                {
                    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (prop != null && IsNumericType(prop.PropertyType))
                    {
                        try
                        {
                            var val = prop.GetValue(obj);
                            sb.Append($"{name}={val};");
                        }
                        catch { }
                    }

                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null && IsNumericType(field.FieldType))
                    {
                        try
                        {
                            var val = field.GetValue(obj);
                            sb.Append($"{name}={val};");
                        }
                        catch { }
                    }
                }

                // if nothing found, attempt to find any numeric property and return the first few
                if (sb.Length == 0)
                {
                    var numericProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                        .Where(p => IsNumericType(p.PropertyType)).Take(5);
                    foreach (var p in numericProps)
                    {
                        try
                        {
                            var val = p.GetValue(obj);
                            sb.Append($"{p.Name}={val};");
                        }
                        catch { }
                    }
                }

                return sb.Length > 0 ? sb.ToString() : "(no-numeric-props-found)";
            }
            catch
            {
                return "(error-reading-props)";
            }
        }

        // New: set hero numeric money property/field to given value (robust across versions)
        public static bool TrySetHeroGold(Hero hero, long amount)
        {
            if (hero == null) return false;
            try
            {
                var type = hero.GetType();
                var names = new[] { "Gold", "Money", "Denars", "Wealth", "GoldAmount" };

                foreach (var name in names)
                {
                    var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (prop != null && IsNumericType(prop.PropertyType) && prop.CanWrite)
                    {
                        var converted = Convert.ChangeType(amount, prop.PropertyType);
                        prop.SetValue(hero, converted);
                        LogToModule($"TrySetHeroGold: set property {name} on {hero} to {amount}");
                        return true;
                    }

                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null && IsNumericType(field.FieldType))
                    {
                        var converted = Convert.ChangeType(amount, field.FieldType);
                        field.SetValue(hero, converted);
                        LogToModule($"TrySetHeroGold: set field {name} on {hero} to {amount}");
                        return true;
                    }
                }

                // fallback: any numeric writable property
                var fallbackProp = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(p => IsNumericType(p.PropertyType) && p.CanWrite);
                if (fallbackProp != null)
                {
                    var converted = Convert.ChangeType(amount, fallbackProp.PropertyType);
                    fallbackProp.SetValue(hero, converted);
                    LogToModule($"TrySetHeroGold: set fallback property {fallbackProp.Name} on {hero} to {amount}");
                    return true;
                }

                // fallback: any numeric field
                var fallbackField = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(f => IsNumericType(f.FieldType));
                if (fallbackField != null)
                {
                    var converted = Convert.ChangeType(amount, fallbackField.FieldType);
                    fallbackField.SetValue(hero, converted);
                    LogToModule($"TrySetHeroGold: set fallback field {fallbackField.Name} on {hero} to {amount}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogToModule($"TrySetHeroGold failed: {ex.Message}");
            }
            return false;
        }

        private static bool IsNumericType(Type t)
        {
            return t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double) || t == typeof(decimal) || t == typeof(short) || t == typeof(uint) || t == typeof(ulong);
        }
    }
}