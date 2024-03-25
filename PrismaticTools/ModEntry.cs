using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using PrismaticTools.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Tools;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace PrismaticTools {
    public class ModEntry : Mod {
        public static IModHelper ModHelper;
        public static ModConfig Config;
        public static Texture2D ToolsTexture;

        private int colorCycleIndex;
        private readonly List<Color> colors = new List<Color>();
        private AssetEditor AssetEditor;

        public override void Entry(IModHelper helper) {
            ModHelper = helper;
            this.AssetEditor = new AssetEditor();

            Config = this.Helper.ReadConfig<ModConfig>();

            ToolsTexture = ModHelper.ModContent.Load<Texture2D>("assets/tools.png");

            helper.ConsoleCommands.Add("ptools", "Upgrade all tools to prismatic", this.UpgradeTools);

            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Player.InventoryChanged += this.OnInventoryChanged;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.GameLoop.GameLaunched += this.OnGameLauched;

            this.InitColors();

            var harmony = new Harmony("stokastic.PrismaticTools");
            this.ApplyPatches(harmony);
        }

        private void ApplyPatches(Harmony harmony) {
            // furnaces
            harmony.Patch(
                original: AccessTools.Method(typeof(Object), nameof(Object.performObjectDropInAction)),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.Object_PerformObjectDropInAction))
            );

            // sprinklers
            harmony.Patch(
                original: AccessTools.Method(typeof(Farm), nameof(Farm.addCrows)),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.Farm_AddCrows))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Object), nameof(Object.GetBaseRadiusForSprinkler)),
                postfix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.After_Object_GetBaseRadiusForSprinkler))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Object), nameof(Object.updateWhenCurrentLocation)),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.Object_UpdatingWhenCurrentLocation))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Object), nameof(Object.placementAction)),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.Object_OnPlacing))
            );

            // tools
            harmony.Patch(
                original: AccessTools.Method(typeof(Tree), nameof(Tree.performToolAction)),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.Tree_PerformToolAction))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(FruitTree), nameof(FruitTree.performToolAction)),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.FruitTree_PerformToolAction))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Pickaxe), nameof(Pickaxe.DoFunction)),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.Pickaxe_DoFunction))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(ResourceClump), nameof(ResourceClump.performToolAction)),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.ResourceClump_PerformToolAction))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), "tilesAffected"),
                postfix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.Tool_TilesAffected_Postfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Axe), "MigrateLegacyItemId"),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.Axe_MigrateLegacyItemId))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(WateringCan), "MigrateLegacyItemId"),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.WateringCan_MigrateLegacyItemId))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Pickaxe), "MigrateLegacyItemId"),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.Pickaxe_MigrateLegacyItemId))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Hoe), "MigrateLegacyItemId"),
                prefix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.Hoe_MigrateLegacyItemId))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(Tool), nameof(Tool.Update)),
                postfix: new HarmonyMethod(typeof(PrismaticPatches), nameof(PrismaticPatches.Tool_Update))
            );
        }

        /// <summary>Raised after the game state is updated (≈60 times per second).</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e) {
            if (!e.IsMultipleOf(8))
                return;

            Farmer farmer = Game1.player;
            Item item;
            try {
                item = farmer.Items[farmer.CurrentToolIndex];
            } catch (System.ArgumentOutOfRangeException) {
                return;
            }

            if (!(item is Object obj) || obj.ParentSheetIndex != PrismaticBarItem.INDEX) {
                return;
            }

            foreach (var light in farmer.currentLocation.sharedLights.Values) {
                if (light.Identifier == (int)farmer.UniqueMultiplayerID) {
                    light.color.Value = this.colors[this.colorCycleIndex];
                }
            }
            this.colorCycleIndex = (this.colorCycleIndex + 1) % this.colors.Count;
        }

        public override object GetApi() {
            return new PrismaticAPI();
        }

        private void UpgradeTools(string command, string[] args) {
            foreach (Item item in Game1.player.Items) {
                if ((item is Axe || item is WateringCan || item is Pickaxe || item is Hoe) && (item as Tool).UpgradeLevel != 5) {
                    Tool t = (item as Tool);
                    int upgrades = 5 - t.UpgradeLevel;
                    // Offset Tile sprite by how many upgrades we have to make
                    // If upgrading over 3, skip 21 slots to align with the next row.
                    t.InitialParentTileIndex += upgrades >= 3 ? 7 * upgrades + 21 : 7 * (upgrades);
                    t.UpgradeLevel = 5;
                    if (item is Axe) { t.ItemId = "PrismaticAxe"; }
                    else if (item is WateringCan) { t.ItemId = "PrismaticWateringCan"; }
                    else if (item is Pickaxe) { t.ItemId = "PrismaticPickaxe"; }
                    else if (item is Hoe) { t.ItemId = "PrismaticHoe"; }
                }
            }
        }

        /// <summary>Adds light sources to prismatic bar and sprinkler items in inventory.</summary>
        private void AddLightsToInventoryItems() {
            if (!Config.UseSprinklersAsLamps) {
                return;
            }
            foreach (Item item in Game1.player.Items) {
                if (item is Object obj) {
                    if (obj.ParentSheetIndex == PrismaticSprinklerItem.INDEX) {
                        obj.lightSource = new LightSource(LightSource.cauldronLight, Vector2.Zero, 2.0f, new Color(0.0f, 0.0f, 0.0f));
                    } else if (obj.ParentSheetIndex == PrismaticBarItem.INDEX) {
                        obj.lightSource = new LightSource(LightSource.cauldronLight, Vector2.Zero, 1.0f, this.colors[this.colorCycleIndex]);
                    }
                }
            }
        }

        /// <summary>Set scarecrow mode for sprinkler items.</summary>
        private void SetScarecrowModeForAllSprinklers() {
            foreach (GameLocation location in Game1.locations) {
                foreach (Object obj in location.Objects.Values) {
                    if (obj.ParentSheetIndex == PrismaticSprinklerItem.INDEX) {
                        obj.Name = Config.UseSprinklersAsScarecrows
                            ? "Prismatic Scarecrow Sprinkler"
                            : "Prismatic Sprinkler";
                    }
                }
            }
        }

        /// <summary>Raised after items are added or removed to a player's inventory.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnInventoryChanged(object sender, InventoryChangedEventArgs e) {
            if (e.IsLocalPlayer)
                this.AddLightsToInventoryItems();
        }

        /// <summary>Raised after the player loads a save slot and the world is initialised.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e) {
            // force add sprinkler recipe for people who were level 10 before installing mod
            if (Game1.player.FarmingLevel >= PrismaticSprinklerItem.CRAFTING_LEVEL) {
                try {
                    Game1.player.craftingRecipes.Add("Prismatic Sprinkler", 0);
                } catch { }
            }

            this.AddLightsToInventoryItems();
            this.SetScarecrowModeForAllSprinklers();
        }

        /// <summary>Raised when an asset is being requested from the content pipeline.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnAssetRequested(object sender, AssetRequestedEventArgs e) {
            this.AssetEditor.OnAssetRequested(e);
        }

        /// <summary>Raised when the game is launched for the first time.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments</param>
        private void OnGameLauched(object sender, GameLaunchedEventArgs e) {
            Game1.toolData.Add("PrismaticAxe", this.DuplicateAndInitData(Game1.toolData["IridiumAxe"], "prismaticAxe", this.CreateUpgradeData("IridiumAxe")));
            Game1.toolData.Add("PrismaticWateringCan", this.DuplicateAndInitData(Game1.toolData["IridiumWateringCan"], "prismaticWatercan", this.CreateUpgradeData("IridiumWateringCan")));
            Game1.toolData.Add("PrismaticPickaxe", this.DuplicateAndInitData(Game1.toolData["IridiumPickaxe"], "prismaticPickaxe", this.CreateUpgradeData("IridiumPickaxe")));
            Game1.toolData.Add("PrismaticHoe", this.DuplicateAndInitData(Game1.toolData["IridiumHoe"], "prismaticHoe", this.CreateUpgradeData("IridiumHoe")));
        }

        /// <summary>Duplicates data from the passed in Tool Data and sets and fields that need to be updated.</summary>
        /// <param name="data">The ToolData class to copy</param>
        /// <param name="name">The name of the new tool being added.</param>
        /// <param name="upgradeData">Tool upgrade information that describes who this tool upgrades from.</param>
        /// <param name="offsetSprite">The offset from the parent sprite.</param>
        /// <param name="offsetMenuSprite">The offset from the parent menu sprite.</param>
        /// <returns>A fillde out ToolData class.</returns>
        private ToolData DuplicateAndInitData(ToolData data, string name, ToolUpgradeData upgradeData, int offsetSprite = 7, int offsetMenuSprite = 7) {
            ToolData newData = new ToolData();
            newData.ClassName = data.ClassName;
            newData.Name = name;
            newData.AttachmentSlots = data.AttachmentSlots;
            newData.DisplayName = ModEntry.ModHelper.Translation.Get(name);
            newData.Description = data.Description;
            newData.Texture = data.Texture;
            newData.SpriteIndex = data.SpriteIndex + offsetSprite;
            newData.MenuSpriteIndex = data.MenuSpriteIndex + offsetMenuSprite;
            newData.UpgradeLevel = data.UpgradeLevel + 1;
            newData.CanBeLostOnDeath = data.CanBeLostOnDeath;
            newData.UpgradeFrom = new List<ToolUpgradeData> { upgradeData };
            return newData;
        }

        /// <summary>Creates a new ToolUpgradeData setting the passed in id as the tool to upgrade from.</summary>
        /// <param name="requiredToolId">Name of the parent tool to upgrade from.</param>
        /// <returns>A filled out ToolUpgradeData class.</returns>
        private ToolUpgradeData CreateUpgradeData(string requiredToolId) {
            ToolUpgradeData newData = new ToolUpgradeData();
            newData.Price = ModEntry.Config.PrismaticToolCost;
            newData.TradeItemId = PrismaticBarItem.INDEX.ToString();
            newData.RequireToolId = requiredToolId;
            newData.TradeItemAmount = 5;
            return newData;
        }

        private void InitColors() {
            int n = 24;
            for (int i = 0; i < n; i++) {
                this.colors.Add(this.ColorFromHSV(360.0 * i / n, 1.0, 1.0));
            }
        }

        private Color ColorFromHSV(double hue, double saturation, double value) {
            int hi = System.Convert.ToInt32(System.Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - System.Math.Floor(hue / 60);

            value = value * 255;
            int v = System.Convert.ToInt32(value);
            int p = System.Convert.ToInt32(value * (1 - saturation));
            int q = System.Convert.ToInt32(value * (1 - f * saturation));
            int t = System.Convert.ToInt32(value * (1 - (1 - f) * saturation));

            v = 255 - v;
            p = 255 - v;
            q = 255 - q;
            t = 255 - t;

            switch (hi) {
                case 0:
                    return new Color(v, t, p);
                case 1:
                    return new Color(q, v, p);
                case 2:
                    return new Color(p, v, t);
                case 3:
                    return new Color(p, q, v);
                case 4:
                    return new Color(t, p, v);
                default:
                    return new Color(v, p, q);
            }
        }
    }
}
