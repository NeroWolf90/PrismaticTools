using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Objects;

namespace PrismaticTools.Framework {
    public class AssetEditor {
        private string barName => ModEntry.ModHelper.Translation.Get("prismaticBar.name");
        private string barDesc => ModEntry.ModHelper.Translation.Get("prismaticBar.description");
        private string sprinklerName => ModEntry.ModHelper.Translation.Get("prismaticSprinkler.name");
        private string sprinklerDesc => ModEntry.ModHelper.Translation.Get("prismaticSprinkler.description");

        public void OnAssetRequested(AssetRequestedEventArgs e) {
            // new item sprites
            if (e.NameWithoutLocale.IsEquivalentTo("Maps/springobjects")) {
                e.Edit(asset => {
                    var editor = asset.AsImage();

                    Texture2D bar = ModEntry.ModHelper.ModContent.Load<Texture2D>("assets/prismaticBar.png");
                    Texture2D sprinkler = ModEntry.ModHelper.ModContent.Load<Texture2D>("assets/prismaticSprinkler.png");

                    editor.ExtendImage(minWidth: editor.Data.Width, minHeight: 1200 / 24 * 16);
                    editor.PatchImage(bar, targetArea: this.GetRectangle(PrismaticBarItem.INDEX));
                    editor.PatchImage(sprinkler, targetArea: this.GetRectangle(PrismaticSprinklerItem.INDEX));
                });
            }

            // new item data
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects")) {
                e.Edit(asset => {
                    var data = asset.AsDictionary<string, ObjectData>().Data;

                    data.Add(PrismaticBarItem.INDEX.ToString(), new ObjectData { Name = this.barName, DisplayName = this.barName, Description = this.barDesc, Price = PrismaticBarItem.PRICE, Type = PrismaticBarItem.TYPE, Category = PrismaticBarItem.CATEGORY, Edibility = PrismaticBarItem.EDIBILITY, SpriteIndex = PrismaticBarItem.INDEX });
                    data.Add(PrismaticSprinklerItem.INDEX.ToString(), new ObjectData { Name = this.sprinklerName, DisplayName = this.sprinklerName, Description = this.sprinklerDesc, Price = PrismaticSprinklerItem.PRICE, Type = PrismaticSprinklerItem.TYPE, Category = PrismaticSprinklerItem.CATEGORY, Edibility = PrismaticSprinklerItem.EDIBILITY, SpriteIndex = PrismaticSprinklerItem.INDEX });
                });
            }

            // new recipes
            else if (e.NameWithoutLocale.IsEquivalentTo("Data/CraftingRecipes")) {
                e.Edit(asset => {
                    var data = asset.AsDictionary<string, string>().Data;

                    // somehow the Dictionary maintains ordering, so reconstruct it with new sprinkler recipe immediately after prismatic
                    Dictionary<string, string> newDict = new();
                    foreach (string key in data.Keys) {
                        newDict.Add(key, data[key]);
                        if (key.Equals("Iridium Sprinkler")) {
                            if (asset.Locale != "en")
                                newDict.Add("Prismatic Sprinkler", $"{PrismaticBarItem.INDEX.ToString()} 2 787 2/Home/{PrismaticSprinklerItem.INDEX.ToString()}/false/Farming {PrismaticSprinklerItem.CRAFTING_LEVEL}/{this.sprinklerName}");
                            else
                                newDict.Add("Prismatic Sprinkler", $"{PrismaticBarItem.INDEX.ToString()} 2 787 2/Home/{PrismaticSprinklerItem.INDEX.ToString()}/false/Farming {PrismaticSprinklerItem.CRAFTING_LEVEL}");
                        }
                    }

                    data.Clear();
                    foreach (string key in newDict.Keys) {
                        data.Add(key, newDict[key]);
                    }
                });
            }

            // tool sprites
            else if (e.NameWithoutLocale.IsEquivalentTo("TileSheets/tools")) {
                e.Edit(asset => {
                    var editor = asset.AsImage();

                    editor.PatchImage(ModEntry.ToolsTexture, patchMode: PatchMode.Overlay);
                });
            }
        }

        public Rectangle GetRectangle(int id) {
            int x = (id % 24) * 16;
            int y = (id / 24) * 16;
            return new Rectangle(x, y, 16, 16);
        }
    }
}
