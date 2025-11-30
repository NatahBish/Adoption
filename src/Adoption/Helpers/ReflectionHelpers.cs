using System;
using System.Linq;
using System.Reflection;
using HarmonyLib.BUTR.Extensions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

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
                return false;
            }

            try
            {
                _setter(hero, value);
                return true;
            }
            catch
            {
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
            }
            catch
            {
                // ignore
            }

            return false;
        }

        // Set hero numeric money property/field to given value (robust across versions) — no logging
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
                        return true;
                    }

                    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null && IsNumericType(field.FieldType))
                    {
                        var converted = Convert.ChangeType(amount, field.FieldType);
                        field.SetValue(hero, converted);
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
                    return true;
                }

                // fallback: any numeric field
                var fallbackField = type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(f => IsNumericType(f.FieldType));
                if (fallbackField != null)
                {
                    var converted = Convert.ChangeType(amount, fallbackField.FieldType);
                    fallbackField.SetValue(hero, converted);
                    return true;
                }
            }
            catch
            {
                // ignore
            }
            return false;
        }

        private static bool IsNumericType(Type t)
        {
            return t == typeof(int) || t == typeof(long) || t == typeof(float) || t == typeof(double) || t == typeof(decimal) || t == typeof(short) || t == typeof(uint) || t == typeof(ulong);
        }
    }
}