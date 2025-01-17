﻿using AATool.Configuration;
using AATool.Data.Categories;
using AATool.Data.Objectives;
using AATool.Data.Objectives.Pickups;
using AATool.Net;
using AATool.Saves;
using AATool.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AATool.Data.Progress
{
    [JsonObject]
    public class WorldState
    {
        public static readonly WorldState Empty = new ();

        public static WorldState FromJsonString(string jsonString)
        {
            try
            {
                //deserialize progress
                dynamic json = JsonConvert.DeserializeObject<WorldState>(jsonString);
                WorldState state = JsonConvert.DeserializeObject<WorldState>(jsonString);

                //clone complex data structures
                state.CompletedAdvancements = json.CompletedAdvancements;
                state.CompletedCriteria = json.CompletedCriteria;
                state.PickupTotals = json.PickupTotals;
                state.KillTotals = json.KillTotals;
                state.Players = json.Players;

                //make sure everyone's names and avatars are loaded
                foreach (KeyValuePair<Uuid, Contribution> contribution in state.Players)
                    Player.FetchIdentityAsync(contribution.Key);
                return state;
            }
            catch
            {
                //error deserializing world state
                return new WorldState();
            }
        }

        [JsonProperty] public Dictionary<Uuid, Contribution> Players { get; private set; }
        [JsonProperty] public HashSet<string> CompletedAdvancements { get; private set; }
        [JsonProperty] public HashSet<(string adv, string crit)> CompletedCriteria { get; private set; }
        [JsonProperty] public HashSet<string> BlocksPlaced { get; private set; }
        [JsonProperty] public HashSet<string> DeathMessages { get; private set; }
        [JsonProperty] public Dictionary<string, int> PickupTotals { get; private set; }
        [JsonProperty] public Dictionary<string, int> DropTotals { get; private set; }
        [JsonProperty] public Dictionary<string, int> KillTotals { get; private set; }

        [JsonProperty] public TimeSpan InGameTime { get; private set; }

        [JsonProperty] public string GameCategory { get; private set; }
        [JsonProperty] public string GameVersion  { get; private set; }

        [JsonProperty] public int Deaths        { get; private set; }
        [JsonProperty] public int DamageTaken   { get; private set; }
        [JsonProperty] public int DamageDealt   { get; private set; }
        [JsonProperty] public int Jumps         { get; private set; }
        [JsonProperty] public int Sleeps        { get; private set; }
        [JsonProperty] public int SaveAndQuits  { get; private set; }

        [JsonProperty] public double KilometersFlown    { get; private set; }
        [JsonProperty] public int BreadEaten            { get; private set; }
        [JsonProperty] public int ItemsEnchanted        { get; private set; } 
        [JsonProperty] public int EnderPearlsThrown     { get; private set; }
        [JsonProperty] public int TemplesRaided         { get; private set; }
        [JsonProperty] public int TNTPickedUp           { get; private set; }
        [JsonProperty] public int TNTPlaced             { get; private set; }

        [JsonProperty] public int CreepersKilled        { get; private set; }
        [JsonProperty] public int DrownedKilled         { get; private set; }
        [JsonProperty] public int WitherSkeletonsKilled { get; private set; }
        [JsonProperty] public int FishCollected         { get; private set; }
        [JsonProperty] public int PhantomsKilled        { get; private set; }

        [JsonProperty] public int SugarcaneCollected    { get; private set; }
        [JsonProperty] public int LecternsMined         { get; private set; }
        [JsonProperty] public int NetherrackMined       { get; private set; }
        [JsonProperty] public int GoldMined             { get; private set; }
        [JsonProperty] public int EnderChestsMined      { get; private set; }

        [JsonProperty] public int GoldIngotsPickedUp    { get; private set; }
        [JsonProperty] public int GoldIngotsDropped     { get; private set; }

        [JsonProperty] public bool AnyoneHasGodApple    { get; private set; }

        public WorldState()
        {
            this.CompletedAdvancements = new ();
            this.CompletedCriteria = new ();
            this.BlocksPlaced = new ();
            this.DeathMessages = new ();
            this.PickupTotals = new ();
            this.DropTotals = new ();
            this.KillTotals = new ();
            this.Players = new ();
        }

        public string ToJsonString()
        {
            //store current category and version data then serialize
            this.GameCategory = Tracker.CurrentCategory;
            this.GameVersion = Tracker.CurrentVersion;
            return JsonConvert.SerializeObject(this);
        }

        public Dictionary<Uuid, DateTime> CompletionsOf(IObjective objective)
        {
            //compile a list of all players who have completed this objective
            var completionists = new Dictionary<Uuid, DateTime>();
            if (objective is Advancement advancement)
            {
                foreach (KeyValuePair<Uuid, Contribution> player in this.Players)
                {
                    if (player.Value.Advancements.TryGetValue(advancement.Id, out DateTime completed))
                        completionists.Add(player.Key, completed);
                }
            }
            else if (objective is Criterion criterion)
            {
                foreach (KeyValuePair<Uuid, Contribution> player in this.Players)
                {
                    if (player.Value.Criteria.Contains((criterion.Owner.Id, criterion.Id)))
                        completionists.Add(player.Key, objective.WhenFirstCompleted());
                }
            }
            else if (objective is Block block)
            {
                foreach (KeyValuePair<Uuid, Contribution> player in this.Players)
                {
                    if (player.Value.IncludesBlock(block.Id))
                        completionists.Add(player.Key, objective.WhenFirstCompleted());
                }
            }
            else if (objective is Death death)
            {
                if (this.DeathMessages.Contains(death.Id))
                    completionists.Add(Tracker.GetMainPlayer(), DateTime.Now);
            }
            return completionists;
        }

        public bool IsAdvancementCompleted(string id) =>
            this.CompletedAdvancements.Contains(id);

        public bool IsBlockPlaced(string id) =>
            this.BlocksPlaced.Contains(id);

        public bool IsCriterionCompleted((string, string) criterion) =>
            this.CompletedCriteria.Contains(criterion);

        public int PickedUp(string item) =>
            this.PickupTotals.TryGetValue(item, out int count) ? count : 0;

        public int Dropped(string item) =>
            this.DropTotals.TryGetValue(item, out int count) ? count : 0;

        public int KillCount(string mob) =>
            this.KillTotals.TryGetValue(mob, out int count) ? count : 0;

        public void Sync(WorldFolder world)
        {
            this.Clear();
            if (!world.IsEmpty)
            {
                //sync progress from local world
                if (Tracker.Category is AllAchievements)
                    this.SyncAchievements(world);
                else
                    this.SyncAdvancements(world);

                //sync statistics from local world
                this.SyncStatistics(world);
            }
        }

        private void Clear()
        {
            this.Players.Clear();
            this.CompletedAdvancements.Clear();
            this.CompletedCriteria.Clear();
            this.BlocksPlaced.Clear();
            this.DeathMessages.Clear();
            this.PickupTotals.Clear();
            this.DropTotals.Clear();
            this.KillTotals.Clear();
        }

        private void SyncAdvancements(WorldFolder world)
        {
            //add each player uuid in current world
            foreach (Uuid id in world.GetAllUuids())
                this.Players[id] = new Contribution(id);

            foreach (Advancement advancement in Tracker.Advancements.AllAdvancements.Values)
            {
                //add advancement if completed
                if (world.Advancements.TryGetAdvancementCompletions(advancement.Id, out Dictionary<Uuid, DateTime> ids))
                {
                    this.CompletedAdvancements.Add(advancement.Id);
                    foreach (Uuid id in ids.Keys)
                        this.Players[id].AddAdvancement(advancement.Id, ids[id]);
                }

                if (advancement.HasCriteria)
                {
                    //add completed criteria if this advancement has any
                    Dictionary<Uuid, HashSet<(string adv, string crit)>> completions;
                    completions = world.Advancements.GetAllCriteriaCompletions(advancement);
                    foreach (KeyValuePair<Uuid, HashSet<(string adv, string crit)>> player in completions)
                    {
                        foreach ((string adv, string crit) criterion in player.Value)
                        {
                            this.Players[player.Key].AddCriterion(criterion.adv, criterion.crit);
                            this.CompletedCriteria.Add(criterion);
                        }
                    }
                }
            }

            //detect god apple from chest using mojang banner recipe
            this.AnyoneHasGodApple = false;
            if (world.Advancements.TryGetAdvancementCompletions(EGap.BannerRecipe, 
                out Dictionary<Uuid, DateTime> recipeHolders))
            {
                foreach (Uuid id in recipeHolders.Keys)
                    this.Players[id].HasGodApple = this.AnyoneHasGodApple = true;
            }
        }

        private void SyncAchievements(WorldFolder world)
        {
            //add each player uuid in current world
            foreach (Uuid id in world.GetAllUuids())
                this.Players[id] = new Contribution(id);

            foreach (Achievement achievement in Tracker.Achievements.AllAdvancements.Values)
            {
                //add advancement if completed
                if (world.Achievements.TryGetAchievementCompletionFor(achievement.Id, out List<Uuid> ids))
                {
                    this.CompletedAdvancements.Add(achievement.Id);
                    foreach (Uuid id in ids)
                        this.Players[id].AddAdvancement(achievement.Id, default);
                }

                if (achievement.HasCriteria)
                {
                    //add completed criteria if this advancement has any
                    Dictionary<Uuid, HashSet<(string adv, string crit)>> completions;
                    completions = world.Achievements.GetAllCriteriaCompletions(achievement);
                    foreach (KeyValuePair<Uuid, HashSet<(string adv, string crit)>> player in completions)
                    {
                        foreach ((string adv, string crit) criterion in player.Value)
                        {
                            this.Players[player.Key].AddCriterion(criterion.adv, criterion.crit);
                            this.CompletedCriteria.Add(criterion);
                        }
                    }
                }
            }
        }

        private void SyncStatistics(WorldFolder world)
        {
            //igt
            this.InGameTime = world.Statistics.GetInGameTime();

            //currently unused
            this.Deaths       = world.Statistics.GetTotalDeaths();
            this.Jumps        = world.Statistics.GetTotalJumps();
            this.Sleeps       = world.Statistics.GetTotalSleeps();
            this.DamageTaken  = world.Statistics.GetTotalDamageTaken();
            this.DamageDealt  = world.Statistics.GetTotalDamageDealt();
            this.SaveAndQuits = world.Statistics.GetTotalSaveAndQuits();

            //run completion screen stats page 1
            this.KilometersFlown    = world.Statistics.GetTotalKilometersFlown();
            this.BreadEaten         = world.Statistics.TimesUsed("bread");
            this.ItemsEnchanted     = world.Statistics.GetItemsEnchanted();
            this.EnderPearlsThrown  = world.Statistics.TimesUsed("ender_pearl");
            this.TNTPickedUp        = world.Statistics.TimesPickedUp("minecraft:tnt", out _);
            this.TNTPlaced          = world.Statistics.TimesUsed("tnt");
            this.TemplesRaided      = world.Statistics.TimesMined("tnt") / 9;

            //run completion screen stats page 2
            this.CreepersKilled         = world.Statistics.KillsOf("creeper");
            this.DrownedKilled          = world.Statistics.KillsOf("drowned");
            this.WitherSkeletonsKilled  = world.Statistics.KillsOf("wither_skeleton");
            this.FishCollected          = world.Statistics.KillsOf("cod") + world.Statistics.KillsOf("salmon");
            this.PhantomsKilled         = world.Statistics.KillsOf("phantom");

            //run completion screen stats page 3
            this.LecternsMined      = world.Statistics.TimesMined("lectern");
            this.SugarcaneCollected = world.Statistics.TimesPickedUp("minecraft:sugar_cane", out _);
            this.NetherrackMined    = world.Statistics.TimesMined("netherrack");
            this.GoldMined          = world.Statistics.TimesMined("gold_block");
            this.EnderChestsMined   = world.Statistics.TimesMined("ender_chest");

            this.GoldIngotsPickedUp = world.Statistics.TimesPickedUp("minecraft:gold_ingot", out _);
            this.GoldIngotsDropped = world.Statistics.TimesDropped("minecraft:gold_ingot", out _);

            //pickup counts
            IEnumerable<string> trackingItems = Tracker.Category is AllBlocks
                ? Tracker.Pickups.All.Keys.Union(Tracker.Blocks.All.Keys)
                : Tracker.Pickups.All.Keys;

            foreach (string item in trackingItems)
            {
                //update times picked up
                int pickups = world.Statistics.TimesPickedUp(item, out Dictionary<Uuid, int> pickupsByPlayer);
                if (pickups > 0)
                {
                    if (!this.PickupTotals.ContainsKey(item))
                        this.PickupTotals[item] = pickups;
                    else
                        this.PickupTotals[item] += pickups;

                    foreach (KeyValuePair<Uuid, int> count in pickupsByPlayer)
                    {
                        if (this.Players.TryGetValue(count.Key, out Contribution player))
                            player.AddItemCount(item, count.Value);
                    }
                }

                //update times dropped
                int drops = world.Statistics.TimesDropped(item, out Dictionary<Uuid, int> dropsByPlayer);
                if (drops > 0)
                {
                    if (!this.DropTotals.ContainsKey(item))
                        this.DropTotals[item] = drops;
                    else
                        this.DropTotals[item] += drops;

                    foreach (KeyValuePair<Uuid, int> count in dropsByPlayer)
                    {
                        if (this.Players.TryGetValue(count.Key, out Contribution player))
                            player.AddDropCount(item, count.Value);
                    }
                }
            }

            //block placements
            foreach (Block block in Tracker.Blocks.All.Values)
            {
                if (!world.Statistics.TryGetUseCount(block.Id, out List<Uuid> ids))
                    continue;

                this.BlocksPlaced.Add(block.Id);
                foreach (Uuid id in ids)
                {
                    if (this.Players.TryGetValue(id, out Contribution player))
                        player.AddBlock(block.Id);
                }
            }
        }

        public void SyncDeathMessages()
        {
            if (ActiveInstance.TryGetLog(out string log) && Player.TryGetName(Tracker.GetMainPlayer(), out string name))
            {
                log = log.ToLower();
                foreach (Death death in Tracker.Deaths.All.Values)
                {
                    foreach (string message in death.Messages)
                    {
                        if (log.Contains($"[server thread/info]: {name.ToLower()} {message}"))
                        {
                            this.DeathMessages.Add(death.Id);
                            break;
                        }
                    }
                }
            }
        }
    }
}
