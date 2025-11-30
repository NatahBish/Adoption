using HarmonyLib.BUTR.Extensions;

using Helpers;

using SandBox;
using SandBox.Conversation;
using SandBox.Missions.AgentBehaviors;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Adoption.CampaignBehaviors
{
    public class AdoptionCampaignBehavior : CampaignBehaviorBase
    {
        private readonly Dictionary<Agent, AdoptionState> _previousAdoptionAttempts = new();

        private enum AdoptionState
        {
            Ended = -1,
            Untested,
            CanAdopt,
            Adopted,
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        public override void SyncData(IDataStore dataStore) { }

        public void OnSessionLaunched(CampaignGameStarter campaignGameStarter)
        {
            StringHelpers.SetCharacterProperties("PLAYER", Hero.MainHero.CharacterObject);
            AddDialogs(campaignGameStarter);
        }

        public void AddDialogs(CampaignGameStarter starter)
        {
            AddChildrenDialogs(starter);
            AddTeenagerDialogs(starter);
        }

        protected void AddChildrenDialogs(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "adoption_discussion",
                "town_or_village_children_player_no_rhyme", "adoption_child",
                "{=Sm4JdIxx}I can tell you have no parents to go back to child. I can be your {?PLAYER.GENDER}mother{?}father{\\?} if that is the case.",
                conversation_adopt_child_on_condition, null, 120);
            starter.AddDialogLine(
                "character_adoption_response",
                "adoption_child", "close_window",
                "{=P2m6bJg6}You want to be my {?PLAYER.GENDER}mom{?}dad{\\?}? Okay then![rf:happy][rb:very_positive]",
                null, conversation_adopt_child_on_consequence, 100);
        }

        protected void AddTeenagerDialogs(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "adoption_discussion",
                "town_or_village_player", "adoption_teen",
                "{=Na4j2oGk}Do you not have any parents to take care of you young {?CONVERSATION_CHARACTER.GENDER}woman{?}man{\\?}? You are welcome to be a part of my family.",
                conversation_adopt_child_on_condition, null, 120);
            starter.AddDialogLine(
                "character_adoption_response",
                "adoption_teen", "close_window",
                "{=NoHJAxWx}Thanks for allowing me to be a part of your family {?PLAYER.GENDER}madam{?}sir{\\?}. I gratefully accept![rf:happy][rb:very_positive]",
                null, conversation_adopt_child_on_consequence, 100);
        }

        private bool conversation_adopt_child_on_condition()
        {
            try
            {
                Agent agent = (Agent)Campaign.Current.ConversationManager.OneToOneConversationAgent;

                if (agent.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge)
                {
                    return false;
                }

                if (!_previousAdoptionAttempts.TryGetValue(agent, out AdoptionState adoptionState))
                {
                    // Clear out any old attempts
                    RemoveUnneededAdoptionAttempts();

                    _previousAdoptionAttempts.Add(agent, AdoptionState.Untested);
                }

                if (adoptionState == AdoptionState.CanAdopt)
                {
                    return true;
                }
                if (adoptionState == AdoptionState.Ended || adoptionState == AdoptionState.Adopted)
                {
                    return false;
                }

                float adoptionChance = Settings.Instance!.AdoptionChance;
                Debug.Print($"Adoption chance: {adoptionChance}");

                float random = MBRandom.RandomFloat;
                Debug.Print($"Random number: {random}");

                if (random < adoptionChance)
                {
                    Debug.Print($"Can adopt {agent}");
                    _previousAdoptionAttempts[agent] = AdoptionState.CanAdopt;
                    return true;
                }
                else
                {
                    Debug.Print($"Cannot adopt {agent}");
                    _previousAdoptionAttempts[agent] = AdoptionState.Ended;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogException(ex, "conversation_adopt_child_on_condition");
                return false;
            }
        }

        private void conversation_adopt_child_on_consequence()
        {
            try
            {
                Agent agent = (Agent)Campaign.Current.ConversationManager.OneToOneConversationAgent;
                CharacterObject character = Campaign.Current.ConversationManager.OneToOneConversationCharacter;

                _previousAdoptionAttempts[agent] = AdoptionState.Adopted;

                // Create hero object from character
                Settlement settlement = Hero.MainHero.CurrentSettlement;
                int age = MBMath.ClampInt((int)agent.Age, Campaign.Current.Models.AgeModel.BecomeChildAge, Campaign.Current.Models.AgeModel.HeroComesOfAge);
                Hero hero = HeroCreator.CreateSpecialHero(character, settlement, Clan.PlayerClan, null, age);
                AdoptedHeroCreator.CreateAdoptedHero(hero, settlement);

                // Copy appearance from agent
                ReflectionHelpers.TrySetHeroStaticBodyProperties(hero, agent.BodyPropertiesValue.StaticProperties);
                hero.Weight = agent.BodyPropertiesValue.Weight;
                hero.Build = agent.BodyPropertiesValue.Build;

                // Agent follows player character
                Campaign.Current.ConversationManager.ConversationEndOneShot += FollowMainAgent;
            }
            catch (Exception ex)
            {
                LogException(ex, "conversation_adopt_child_on_consequence");
            }
        }

        public void RemoveUnneededAdoptionAttempts()
        {
            try
            {
                var mission = Mission.Current;
                if (mission is null)
                    return;

                // Build a set of currently present agents by reflecting the "Agents" member
                var presentAgents = new HashSet<Agent>();

                object? agentsObj = null;
                var missionType = mission.GetType();

                // Try property, then field, then method
                var prop = missionType.GetProperty("Agents");
                if (prop != null)
                    agentsObj = prop.GetValue(mission);
                else
                {
                    var field = missionType.GetField("Agents");
                    if (field != null)
                        agentsObj = field.GetValue(mission);
                    else
                    {
                        var method = missionType.GetMethod("GetAgents") ?? missionType.GetMethod("get_Agents");
                        if (method != null)
                            agentsObj = method.Invoke(mission, null);
                    }
                }

                if (agentsObj is System.Collections.IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is Agent a)
                            presentAgents.Add(a);
                    }
                }

                // If we couldn't obtain agents, be conservative and skip removal
                if (presentAgents.Count == 0)
                    return;

                foreach (var pair in _previousAdoptionAttempts.ToList())
                {
                    if (!presentAgents.Contains(pair.Key))
                    {
                        _previousAdoptionAttempts.Remove(pair.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                LogException(ex, "RemoveUnneededAdoptionAttempts");
            }
        }

        public void FollowMainAgent()
        {
            DailyBehaviorGroup behaviorGroup = ConversationMission.OneToOneConversationAgent.GetComponent<CampaignAgentComponent>().AgentNavigator.GetBehaviorGroup<DailyBehaviorGroup>();
            FollowAgentBehavior followAgentBehavior = behaviorGroup.AddBehavior<FollowAgentBehavior>();
            behaviorGroup.SetScriptedBehavior<FollowAgentBehavior>();
            followAgentBehavior.SetTargetAgent(Agent.Main);
        }

        public void ResetAdoptionAttempts()
        {
            foreach (var pair in _previousAdoptionAttempts.ToList())
            {
                if (_previousAdoptionAttempts[pair.Key] == AdoptionState.Ended)
                {
                    _previousAdoptionAttempts[pair.Key] = AdoptionState.Untested;
                }
            }
        }

        private static void LogException(Exception ex, string context)
        {
            string moduleDir = null;
            string logPath = null;

            try
            {
                // Attempt to determine the module folder using the module assembly location
                try
                {
                    var asmDir = Path.GetDirectoryName(typeof(SubModule).Assembly.Location) ?? string.Empty;
                    var dirInfo = new DirectoryInfo(asmDir);
                    string found = null;
                    for (var anc = dirInfo; anc != null; anc = anc.Parent)
                    {
                        if (anc.Parent != null && anc.Parent.Name.Equals("Modules", StringComparison.OrdinalIgnoreCase))
                        {
                            // anc is the module folder (e.g. ...\Modules\Adoption)
                            found = anc.FullName;
                            break;
                        }
                    }

                    moduleDir = found ?? Path.Combine(Directory.GetCurrentDirectory(), "Modules", "Adoption");
                }
                catch
                {
                    moduleDir = Path.Combine(Directory.GetCurrentDirectory(), "Modules", "Adoption");
                }

                if (!Directory.Exists(moduleDir))
                {
                    Directory.CreateDirectory(moduleDir);
                }

                logPath = Path.Combine(moduleDir, "adoption_error.log");
                string entry = $"{DateTime.UtcNow:o} [{context}] {ex}\n\n";
                File.AppendAllText(logPath, entry);
            }
            catch
            {
                // swallow — logging must not crash the game
            }

            try
            {
                // show the actual path we attempted to write to (fallback to a reasonable default if null)
                var displayPath = logPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Modules", "Adoption", "adoption_error.log");
                InformationManager.DisplayMessage(new InformationMessage($"[Adoption] Error occurred ({context}). Log: {displayPath}"));
            }
            catch
            {
                // ignore UI errors
            }
        }
    }
}