using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Pathoschild.LookupAnything.Framework.Constants;
using Pathoschild.LookupAnything.Framework.Data;
using Pathoschild.LookupAnything.Framework.Fields;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Monsters;
using Object = StardewValley.Object;

namespace Pathoschild.LookupAnything.Framework.Subjects
{
    /// <summary>Describes an NPC (including villagers, monsters, and pets).</summary>
    internal class CharacterSubject : BaseSubject
    {
        /*********
        ** Properties
        *********/
        /// <summary>The NPC type.s</summary>
        private readonly TargetType TargetType;

        /// <summary>The lookup target.</summary>
        private readonly NPC Target;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="npc">The lookup target.</param>
        /// <param name="type">The NPC type.</param>
        /// <param name="metadata">Provides metadata that's not available from the game data directly.</param>
        /// <remarks>Reverse engineered from <see cref="NPC"/>.</remarks>
        public CharacterSubject(NPC npc, TargetType type, Metadata metadata)
        {
            // get display type
            string typeName;
            if (type == TargetType.Villager)
                typeName = "Villager";
            else if (type == TargetType.Monster)
                typeName = "Monster";
            else
                typeName = npc.GetType().Name;

            // initialise
            this.Target = npc;
            this.TargetType = type;
            CharacterData overrides = metadata.GetCharacter(npc, type);
            this.Initialise(overrides?.Name ?? npc.getName(), overrides?.Description, typeName);
        }

        /// <summary>Get the data to display for this subject.</summary>
        /// <param name="metadata">Provides metadata that's not available from the game data directly.</param>
        public override IEnumerable<ICustomField> GetData(Metadata metadata)
        {
            NPC npc = this.Target;

            switch (this.TargetType)
            {
                case TargetType.Villager:
                    if (!metadata.Constants.AsocialVillagers.Contains(npc.getName()))
                    {
                        var giftTastes = this.GetGiftTastes(npc);
                        yield return new GenericField("Birthday", $"{Utility.capitalizeFirstLetter(npc.birthday_Season)} {npc.birthday_Day}");
                        yield return new GenericField("Can romance", npc.datable);
                        yield return new CharacterFriendshipField("Friendship", Game1.player.friendships[npc.name][0], NPC.friendshipPointsPerHeartLevel, NPC.maxFriendshipPoints);
                        yield return new GenericField("Talked today", Game1.player.friendships[npc.name][2] == 1);
                        yield return new GenericField("Gifted today", Game1.player.friendships[npc.name][3] > 0);
                        yield return new GenericField("Gifted this week", $"{Game1.player.friendships[npc.name][1]} of {NPC.maxGiftsPerWeek}");
                        yield return new CharacterGiftTastesField("Loves gifts", giftTastes, GiftTaste.Love);
                        yield return new CharacterGiftTastesField("Likes gifts", giftTastes, GiftTaste.Like);
                    }
                    break;

                case TargetType.Pet:
                    Pet pet = (Pet)npc;
                    yield return new CharacterFriendshipField("Love", pet.friendshipTowardFarmer, Pet.maxFriendship / 10, Pet.maxFriendship);
                    yield return new GenericField("Petted today", GameHelper.GetPrivateField<bool>(pet, "wasPetToday"));
                    break;

                case TargetType.Monster:
                    // basic info
                    Monster monster = (Monster)npc;
                    Item[] drops = (from id in monster.objectsToDrop let item = GameHelper.GetObjectBySpriteIndex(id) orderby item.Name select (Item)item).ToArray();
                    yield return new GenericField("Invincible", $"For {GameHelper.GetPrivateField<int>(monster, "invincibleCountdown")} seconds", hasValue: monster.isInvincible());
                    yield return new PercentageBarField("Health", monster.health, monster.maxHealth, Color.Green, Color.Gray, $"{Math.Round((monster.health / (monster.maxHealth * 1f) * 100))}% ({monster.health} of {monster.maxHealth})");
                    yield return new ItemDropListField("Will drop", drops, defaultText: "nothing");
                    yield return new GenericField("XP", monster.experienceGained);
                    yield return new GenericField("Defence", monster.resilience);
                    yield return new GenericField("Attack", monster.damageToFarmer);

                    // Adventure Guild quest
                    AdventureGuildQuestData adventureGuildQuest = metadata.GetAdventurerGuildQuest(monster.name);
                    if (adventureGuildQuest != null)
                    {
                        int kills = adventureGuildQuest.Targets.Select(p => Game1.stats.getMonstersKilled(p)).Sum();
                        yield return new GenericField("Adventure Guild", $"{(kills >= adventureGuildQuest.RequiredKills ? "complete" : "in progress")} (killed {kills} of {adventureGuildQuest.RequiredKills})");
                    }
                    break;
            }
        }

        /// <summary>Draw the subject portrait (if available).</summary>
        /// <param name="spriteBatch">The sprite batch being drawn.</param>
        /// <param name="position">The position at which to draw.</param>
        /// <param name="size">The size of the portrait to draw.</param>
        /// <returns>Returns <c>true</c> if a portrait was drawn, else <c>false</c>.</returns>
        public override bool DrawPortrait(SpriteBatch spriteBatch, Vector2 position, Vector2 size)
        {
            NPC npc = this.Target;

            // use character portrait (most NPCs)
            if (npc.Portrait != null)
            {
                spriteBatch.DrawSprite(npc.Portrait, new Rectangle(0, 0, NPC.portrait_width, NPC.portrait_height), position.X, position.Y, Color.White, size.X / NPC.portrait_width);
                return true;
            }

            // else draw sprite (e.g. for pets)
            npc.Sprite.draw(spriteBatch, position, 1, 0, 0, Color.White, scale: size.X / npc.Sprite.getWidth());
            return true;
        }


        /*********
        ** Private methods
        *********/
        /// <summary>Get how much an NPC likes receiving each item as a gift.</summary>
        /// <param name="npc">The NPC.</param>
        private IDictionary<GiftTaste, Item[]> GetGiftTastes(NPC npc)
        {
            IDictionary<GiftTaste, List<Item>> tastes = new Dictionary<GiftTaste, List<Item>>();
            foreach (var objectInfo in Game1.objectInformation)
            {
                Object item = GameHelper.GetObjectBySpriteIndex(objectInfo.Key);
                if (!npc.canReceiveThisItemAsGift(item))
                    continue;
                try
                {
                    GiftTaste taste = (GiftTaste)npc.getGiftTasteForThisItem(item);
                    if (!tastes.ContainsKey(taste))
                        tastes[taste] = new List<Item>();
                    tastes[taste].Add(item);
                }
                catch (Exception)
                {
                    // some NPCs (e.g. dog) claim to allow gifts, but crash if you check their preference
                }
            }
            return tastes.ToDictionary(p => p.Key, p => p.Value.ToArray());
        }
    }
}