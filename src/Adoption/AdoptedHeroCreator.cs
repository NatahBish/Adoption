using Helpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace Adoption
{
    public static class AdoptedHeroCreator
    {
        public static void CreateAdoptedHero(Hero hero, Settlement settlement)
        {
            if (Hero.MainHero.IsFemale)
            {
                hero.Mother = Hero.MainHero;
                if (null != Hero.MainHero.Spouse)
                {
                    hero.Father = Hero.MainHero.Spouse;
                }
                else
                {
                    CreateRandomLostParent(hero, settlement);
                }
            }
            else
            {
                hero.Father = Hero.MainHero;
                if (null != Hero.MainHero.Spouse)
                {
                    hero.Mother = Hero.MainHero.Spouse;
                }
                else
                {
                    CreateRandomLostParent(hero, settlement);
                }
            }

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

        public static void CreateRandomLostParent(Hero hero, Settlement settlement)
        {
            {
                int heroComesOfAge = Campaign.Current.Models.AgeModel.HeroComesOfAge;
                int age = MBRandom.RandomInt(heroComesOfAge + (int)hero.Age, heroComesOfAge * 2 + (int)hero.Age);
                CharacterObject randomElementWithPredicate;
                CharacterObject template = settlement.Culture.NotableTemplates.GetRandomElementWithPredicate((CharacterObject x) => x.Occupation == Occupation.Guard && !x.IsFemale);
                if (Hero.MainHero.IsFemale)
                {
                    randomElementWithPredicate = settlement.Culture.NotableTemplates.GetRandomElementWithPredicate((CharacterObject x) => x.Occupation == Occupation.Wanderer && !x.IsFemale);
                    if (null == randomElementWithPredicate)
                    {
                        randomElementWithPredicate = settlement.Culture.NotableTemplates.GetRandomElementWithPredicate((CharacterObject x) => x.Occupation == Occupation.Merchant && x.IsFemale);
                    }
                    if(null == randomElementWithPredicate)
                    {
                        randomElementWithPredicate = settlement.Culture.NotableTemplates.GetRandomElementWithPredicate((CharacterObject x) => x.Occupation == Occupation.Artisan && x.IsFemale);
                    }
                    hero.Father = HeroCreator.CreateSpecialHero(randomElementWithPredicate, hero.CurrentSettlement, Clan.PlayerClan, null, age);
                    if(0 < hero.Father.Gold)
                    {
                        hero.Father.ChangeHeroGold(-hero.Father.Gold);
                    }
                    hero.Father.CharacterObject.HiddenInEncyclopedia = true;
                    KillCharacterAction.ApplyByRemove(hero.Father);
                }
                else
                {
                    randomElementWithPredicate = settlement.Culture.NotableTemplates.GetRandomElementWithPredicate((CharacterObject x) => x.Occupation == Occupation.Wanderer && x.IsFemale);
                    if(null == randomElementWithPredicate)
                    {
                        randomElementWithPredicate = settlement.Culture.NotableTemplates.GetRandomElementWithPredicate((CharacterObject x) => x.Occupation == Occupation.Merchant && x.IsFemale);
                    }
                    if (null == randomElementWithPredicate)
                    {
                        randomElementWithPredicate = settlement.Culture.NotableTemplates.GetRandomElementWithPredicate((CharacterObject x) => x.Occupation == Occupation.Artisan && x.IsFemale);
                    }
                    hero.Mother = HeroCreator.CreateSpecialHero(randomElementWithPredicate, hero.CurrentSettlement, Clan.PlayerClan, null, age);
                    if (0 < hero.Mother.Gold)
                    {
                        hero.Mother.ChangeHeroGold(-hero.Mother.Gold);
                    }
                    hero.Mother.CharacterObject.HiddenInEncyclopedia = true;
                    KillCharacterAction.ApplyByRemove(hero.Mother);
                }
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
            // GetEquipmentRostersForInitialChildrenGeneration uses different equipment templates depending on if they are a child or teenager
            MBEquipmentRoster randomElementInefficiently = Campaign.Current.Models.EquipmentSelectionModel.GetEquipmentRostersForInitialChildrenGeneration(hero).GetRandomElementInefficiently();
            if (randomElementInefficiently is not null)
            {
                Equipment randomCivilianEquipment = randomElementInefficiently.GetRandomCivilianEquipment();
                EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, randomCivilianEquipment);
                Equipment equipment = new Equipment(Equipment.EquipmentType.Civilian);
                equipment.FillFrom(randomCivilianEquipment, false);
                EquipmentHelper.AssignHeroEquipmentFromEquipment(hero, equipment);
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
                    }
                }
                else
                {
                    adoptedHero.Father = Hero.MainHero;
                    if (null != adopter.Spouse)
                    {
                        adoptedHero.Mother = adopter.Spouse;
                    }
                }
                var rnd = new Random();
                int value = MBRandom.RandomInt(30, 70);
                adoptedHero.SetPersonalRelation(Hero.MainHero, value);
                Hero.MainHero.SetPersonalRelation(adoptedHero, value);
                if (null != Hero.MainHero.Spouse)
                {
                    value = MBRandom.RandomInt(30, 50);
                    adoptedHero.SetPersonalRelation(Hero.MainHero.Spouse, value);
                    Hero.MainHero.Spouse.SetPersonalRelation(adoptedHero, value);
                }
            }
            catch
            {
                // fall through to next attempt
                InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
            }

            InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
        }
    }
}