using Helpers;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Adoption
{
    public static class AdoptedHeroCreator
    {
        public static void CreateAdoptedHero(Hero hero, Settlement settlement)
        {
            // Parent assignments
            CreateRandomLostParent(hero, settlement);

            // Traits derived from DeliverOffSpring method
            try
            {
                LordTraits(hero, hero.Mother, hero.Father);
            }
            catch { }

            // Common updates after creating hero
            try
            {
                hero.SetNewOccupation(Occupation.Lord);
                hero.UpdateHomeSettlement();
            }
            catch { }

            // Initialize hero developer (version tolerant)
            try
            {
                TryInitializeHeroDeveloper(hero);
            }
            catch { }

            // Equipment derived from OnNewGameCreatedPartialFollowUp
            try
            {
                EquipmentForChild(hero);
            }
            catch { }

            // Custom notification for adoption
            try
            {
                OnHeroAdopted(Hero.MainHero, hero);
            }
            catch { }
        }

        private static string GetCultureName(Hero hero)
        {
            try
            {
                var charObj = hero.CharacterObject;
                if (charObj == null) return "unknown";
                var prop = charObj.GetType().GetProperty("Culture");
                if (prop != null)
                {
                    var cult = prop.GetValue(charObj);
                    if (cult != null)
                    {
                        var nameProp = cult.GetType().GetProperty("StringId") ?? cult.GetType().GetProperty("Name");
                        var val = nameProp?.GetValue(cult);
                        return val?.ToString() ?? cult.ToString();
                    }
                }

                var cultureNameProp = charObj.GetType().GetProperty("CultureName");
                if (cultureNameProp != null)
                {
                    var val = cultureNameProp.GetValue(charObj);
                    return val?.ToString() ?? "unknown";
                }

                return charObj.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        public static void CreateRandomLostParent(Hero hero, Settlement settlement)
        {
            try
            {
                int heroComesOfAge = Campaign.Current.Models.AgeModel.HeroComesOfAge;
                int age = MBRandom.RandomInt(heroComesOfAge + (int)hero.Age, heroComesOfAge * 2 + (int)hero.Age);

                var culture = settlement.Culture;
                var templates = GetNotableAndWandererTemplates(culture).ToList();

                IEnumerable<CharacterObject> candidates;
                if (Hero.MainHero.IsFemale)
                {
                    candidates = templates.Where((CharacterObject x) => x.Occupation == Occupation.Wanderer && !x.IsFemale);
                }
                else
                {
                    candidates = templates.Where((CharacterObject x) => x.Occupation == Occupation.Wanderer && x.IsFemale);
                }

                var candidateList = candidates.ToList();

                if (candidateList.Count == 0)
                {
                    var wanderers = GetPropertyAsEnumerable<CharacterObject>(culture, "WandererTemplates")?.ToList();
                    if (wanderers != null && wanderers.Count > 0)
                    {
                        candidateList = wanderers.Where(x => x.Occupation == Occupation.Wanderer && (Hero.MainHero.IsFemale ? x.IsFemale : !x.IsFemale)).ToList();
                    }
                }

                if (candidateList.Count == 0)
                {
                    candidateList = templates;
                }

                if (candidateList.Count == 0)
                {
                    InformationManager.DisplayMessage(new InformationMessage("[Adoption] Could not find suitable wanderer/notable templates for creating lost parent; skipping parent creation."));
                    return;
                }

                int idx = candidateList.Count == 1 ? 0 : MBRandom.RandomInt(0, candidateList.Count - 1);
                CharacterObject randomElementWithPredicate = candidateList[idx];

                if (Hero.MainHero.IsFemale)
                {
                    hero.Father = HeroCreator.CreateSpecialHero(randomElementWithPredicate, hero.CurrentSettlement, Clan.PlayerClan, null, age);

                    // Clear any gold the created hero might carry (prevents transfer into clan)
                    ReflectionHelpers.TrySetHeroGold(hero.Father, 0);

                    try
                    {
                        KillCharacterAction.ApplyByRemove(hero.Father);
                    }
                    catch { }

                    ReflectionHelpers.TrySetCharacterHiddenInEncyclopedia(hero.Father.CharacterObject, true);
                }
                else
                {
                    hero.Mother = HeroCreator.CreateSpecialHero(randomElementWithPredicate, hero.CurrentSettlement, Clan.PlayerClan, null, age);

                    // Clear any gold the created hero might carry (prevents transfer into clan)
                    ReflectionHelpers.TrySetHeroGold(hero.Mother, 0);

                    try
                    {
                        KillCharacterAction.ApplyByRemove(hero.Mother);
                    }
                    catch { }

                    ReflectionHelpers.TrySetCharacterHiddenInEncyclopedia(hero.Mother.CharacterObject, true);
                }
            }
            catch (Exception ex)
            {
                InformationManager.DisplayMessage(new InformationMessage($"[Adoption] CreateRandomLostParent failed: {ex.Message}"));
            }
        }

        public static void LordTraits(Hero hero, Hero mother, Hero father)
        {
            hero.ClearTraits();
            float randomFloat = MBRandom.RandomFloat;
            int num;
            if ((double)randomFloat < 0.1)
            {
                num = 0;
            }
            else if ((double)randomFloat < 0.5)
            {
                num = 1;
            }
            else if ((double)randomFloat < 0.9)
            {
                num = 2;
            }
            else
            {
                num = 3;
            }
            List<TraitObject> list = DefaultTraits.Personality.ToList();
            list.Shuffle();
            for (int i = 0; i < Math.Min(list.Count, num); i++)
            {
                int num2 = ((double)MBRandom.RandomFloat < 0.5) ? MBRandom.RandomInt(list[i].MinValue, 0) : MBRandom.RandomInt(1, list[i].MaxValue + 1);
                hero.SetTraitLevel(list[i], num2);
            }
            foreach (TraitObject traitObject in TraitObject.All.Except(DefaultTraits.Personality))
            {
                hero.SetTraitLevel(traitObject, ((double)MBRandom.RandomFloat < 0.5) ? mother.GetTraitLevel(traitObject) : father.GetTraitLevel(traitObject));
            }
        }

        public static void EquipmentForChild(Hero hero)
        {
            try
            {
                MBEquipmentRoster randomElementInefficiently = Campaign.Current.Models.EquipmentSelectionModel.GetEquipmentRostersForInitialChildrenGeneration(hero).GetRandomElementInefficiently();
                if (randomElementInefficiently is not null)
                {
                    Equipment randomCivilianEquipment = randomElementInefficiently.GetRandomCivilianEquipment();
                    EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, randomCivilianEquipment);

                    // Attempt to construct a secondary Equipment instance in a version tolerant way,
                    // but if the constructor or FillFrom isn't present simply skip the secondary assignment.
                    try
                    {
                        var eqType = typeof(Equipment);
                        object? newEquipment = null;
                        var ctorBool = eqType.GetConstructor(new Type[] { typeof(bool) });
                        if (ctorBool != null)
                            newEquipment = ctorBool.Invoke(new object[] { false });
                        else
                        {
                            var ctorParamless = eqType.GetConstructor(Type.EmptyTypes);
                            if (ctorParamless != null)
                                newEquipment = ctorParamless.Invoke(null);
                            else
                                newEquipment = Activator.CreateInstance(eqType);
                        }

                        if (newEquipment != null)
                        {
                            var fillFromMethod = eqType.GetMethod("FillFrom", new Type[] { typeof(Equipment), typeof(bool) }) ??
                                                 eqType.GetMethod("FillFrom", new Type[] { typeof(Equipment) }) ??
                                                 eqType.GetMethod("FillFrom", BindingFlags.Public | BindingFlags.Instance);

                            if (fillFromMethod != null)
                            {
                                var pars = fillFromMethod.GetParameters();
                                if (pars.Length == 2)
                                    fillFromMethod.Invoke(newEquipment, new object[] { randomCivilianEquipment, false });
                                else
                                    fillFromMethod.Invoke(newEquipment, new object[] { randomCivilianEquipment });
                                EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, (Equipment)newEquipment);
                            }
                        }
                    }
                    catch
                    {
                        // ignore equipment reflection failures
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        public static void OnHeroAdopted(Hero adopter, Hero adoptedHero)
        {
            // prepare localization tokens
            StringHelpers.SetCharacterProperties("ADOPTER", adopter.CharacterObject);
            StringHelpers.SetCharacterProperties("ADOPTED_HERO", adoptedHero.CharacterObject);

            var text = new TextObject("{=DjzDTNHw}{ADOPTER.LINK} adopted {ADOPTED_HERO.LINK}.");

            try
            {
                if (Hero.MainHero.IsFemale)
                {
                    adoptedHero.Mother = Hero.MainHero;
                    if (null != adopter.Spouse)
                    {
                        adoptedHero.Father = adopter.Spouse;
                    }else
                    {
                        adoptedHero.Father = null;
                    }
                }
                else
                {
                    adoptedHero.Father = Hero.MainHero;
                    if(null != adopter.Spouse)
                    {
                        adoptedHero.Mother = adopter.Spouse;
                    }else
                    {
                        adoptedHero.Mother = null;
                    }
                }
                adoptedHero.SetPersonalRelation(Hero.MainHero, 50);
                Hero.MainHero.SetPersonalRelation(adoptedHero, 50);
                if (null != Hero.MainHero.Spouse)
                {
                    adoptedHero.SetPersonalRelation(Hero.MainHero.Spouse, 50);
                    Hero.MainHero.Spouse.SetPersonalRelation(adoptedHero, 50);
                }
            }
            catch
            {
                // fall through to next attempt
                InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
            }

            // Try to call MBInformationManager.AddQuickInformation with one of its available overloads via reflection.
            try
            {
                var mbInfoType = typeof(MBInformationManager);
                var methods = mbInfoType.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == "AddQuickInformation");
                    foreach (var m in methods)
                    {
                        var pars = m.GetParameters();
                        if (pars.Length == 1 && pars[0].ParameterType == typeof(TextObject))
                        {
                            m.Invoke(null, new object[] { text });
                            return;
                        }

                        if (pars.Length == 4)
                        {
                            object basicChar = adopter.CharacterObject as BasicCharacterObject;
                            m.Invoke(null, new object[] { text, 0, basicChar, null });
                            return;
                        }

                        if (pars.Length == 2)
                        {
                            if (pars[1].ParameterType == typeof(string))
                            {
                                m.Invoke(null, new object[] { text, "" });
                                return;
                            }
                            if (pars[1].ParameterType == typeof(int))
                            {
                                m.Invoke(null, new object[] { text, 0 });
                                return;
                            }
                        }
                    }
            }
            catch
            {
                // fall through to fallback below
            }

            InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
        }

        private static bool TryInitializeHeroDeveloper(Hero hero)
        {
            try
            {
                var heroType = hero.GetType();

                object? heroDev = null;
                var prop = heroType.GetProperty("HeroDeveloper", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    heroDev = prop.GetValue(hero);
                }
                else
                {
                    var getter = heroType.GetMethod("get_HeroDeveloper", BindingFlags.Public | BindingFlags.Instance) ??
                                 heroType.GetMethod("GetHeroDeveloper", BindingFlags.Public | BindingFlags.Instance);
                    if (getter != null)
                        heroDev = getter.Invoke(hero, null);
                }

                if (heroDev != null)
                {
                    var devType = heroDev.GetType();
                    var initMethod = devType.GetMethod("InitializeHeroDeveloper", BindingFlags.Public | BindingFlags.Instance) ??
                                     devType.GetMethod("InitHeroDeveloper", BindingFlags.Public | BindingFlags.Instance);
                    if (initMethod != null)
                    {
                        var pars = initMethod.GetParameters();
                        object?[] args = pars.Length switch
                        {
                            2 => new object?[] { true, null },
                            1 => new object?[] { true },
                            0 => Array.Empty<object?>(),
                            _ => new object?[] { true, null }
                        };
                        initMethod.Invoke(heroDev, args);
                        return true;
                    }
                }

                var initOnHero = heroType.GetMethod("InitializeHeroDeveloper", BindingFlags.Public | BindingFlags.Instance) ??
                                 heroType.GetMethod("InitHeroDeveloper", BindingFlags.Public | BindingFlags.Instance);
                if (initOnHero != null)
                {
                    var pars = initOnHero.GetParameters();
                    object?[] args = pars.Length switch
                    {
                        2 => new object?[] { true, null },
                        1 => new object?[] { true },
                        0 => Array.Empty<object?>(),
                        _ => new object?[] { true, null }
                    };
                    initOnHero.Invoke(hero, args);
                    return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        private static IEnumerable<CharacterObject> GetNotableAndWandererTemplates(CultureObject culture)
        {
            List<CharacterObject> result = new List<CharacterObject>();
            try
            {
                var type = culture.GetType();
                object? value = null;

                var prop = type.GetProperty("NotableAndWandererTemplates", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    value = prop.GetValue(culture);
                }
                else
                {
                    var getter = type.GetMethod("get_NotableAndWandererTemplates", BindingFlags.Public | BindingFlags.Instance) ??
                                 type.GetMethod("GetNotableAndWandererTemplates", BindingFlags.Public | BindingFlags.Instance);
                    if (getter != null)
                        value = getter.Invoke(culture, null);
                }

                if (value is System.Collections.IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is CharacterObject co)
                            result.Add(co);
                    }
                }
                else
                {
                    var propNotable = type.GetProperty("NotableTemplates", BindingFlags.Public | BindingFlags.Instance);
                    if (propNotable != null)
                    {
                        var notableObj = propNotable.GetValue(culture);
                        if (notableObj is System.Collections.IEnumerable notableEnum)
                        {
                            foreach (var item in notableEnum)
                                if (item is CharacterObject co) result.Add(co);
                        }
                    }

                    var propWanderer = type.GetProperty("WandererTemplates", BindingFlags.Public | BindingFlags.Instance);
                    if (propWanderer != null)
                    {
                        var wandererObj = propWanderer.GetValue(culture);
                        if (wandererObj is System.Collections.IEnumerable wandererEnum)
                        {
                            foreach (var item in wandererEnum)
                                if (item is CharacterObject co) result.Add(co);
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            foreach (var co in result)
                yield return co;
        }

        private static IEnumerable<T>? GetPropertyAsEnumerable<T>(object obj, string propertyName) where T : class
        {
            try
            {
                var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    var val = prop.GetValue(obj);
                    if (val is System.Collections.IEnumerable en)
                    {
                        var list = new List<T>();
                        foreach (var item in en)
                        {
                            if (item is T t)
                                list.Add(t);
                        }
                        return list;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }
    }
}
