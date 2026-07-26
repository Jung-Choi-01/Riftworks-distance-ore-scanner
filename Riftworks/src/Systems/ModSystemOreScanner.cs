using Riftworks.src.Items.Wearable;
using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Riftworks.src.Systems
{
    public class ModSystemOreScanner : ModSystemWearableTick<ItemOreScanner>
    {
        ICoreClientAPI? capi;

        public override bool ShouldLoad(EnumAppSide forSide) => true;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            base.StartServerSide(api);
        }

        // I wanted to detect ore visually but I suck
        protected override void HandleItem(IPlayer player, ItemOreScanner oreScanner, ItemSlot slot, double hoursPassed, float dt)
        {
            double fuelBefore = FuelWearable.GetFuelHours(slot.Itemstack);
            double fuelAfter = fuelBefore;

            if (hoursPassed > 0)
            {
                FuelWearable.AddFuelHours(slot.Itemstack, -hoursPassed);

                fuelAfter = FuelWearable.GetFuelHours(slot.Itemstack);

                if (System.Math.Abs(fuelAfter - fuelBefore) >= 0.02)
                {
                    slot.MarkDirty();
                }
            }

            if (fuelAfter <= 0)
            {
                return;
            }

            BlockPos centerPos = player.Entity.Pos.AsBlockPos;
            int scanRadius = 10;

            Dictionary<string, int> detectedOres = new Dictionary<string, int>();

            for (int offsetX = -scanRadius; offsetX <= scanRadius; offsetX++)
            {
                for (int offsetY = -scanRadius; offsetY <= scanRadius; offsetY++)
                {
                    for (int offsetZ = -scanRadius; offsetZ <= scanRadius; offsetZ++)
                    {
                        BlockPos scanPos = new(centerPos.X + offsetX, centerPos.Y + offsetY, centerPos.Z + offsetZ);
                        Block? scannedBlock = sapi?.World.BlockAccessor.GetBlock(scanPos);

                        if (scannedBlock == null)
                        {
                            continue;
                        }

                        string path = scannedBlock.Code.Path;

                        // time to get the ore name
                        if (path.StartsWith("ore-"))
                        {
                            string trimmed = path.Substring("ore-".Length);
                            string[] parts = trimmed.Split('-');

                            string[] grades = new string[] { "poor", "medium", "rich", "bountiful" };

                            // Remove grade if present
                            int index = 0;
                            if (grades.Contains(parts[0]))
                            {
                                index = 1;
                            }

                            string oreName;
                            // If only 1 element left, it's the ore name
                            if (parts.Length - index == 1)
                            {
                                oreName = parts[index];
                            }
                            else
                            {
                                // Take everything except the last part
                                string[] oreParts = parts.Skip(index).Take(parts.Length - index - 1).ToArray();
                                oreName = string.Join("-", oreParts);
                            }

                            int distance = new int[] {offsetX, offsetY, offsetZ}.Sum(Math.Abs);
                            if(detectedOres.ContainsKey(oreName))
                            {
                                if(distance >= detectedOres.Get(oreName)) continue;
                                detectedOres[oreName] = distance;
                            } 
                            else
                            {
                                detectedOres[oreName] = distance;
                            }
                        }

                    }
                }
            }

            if (player is IServerPlayer serverPlayer && detectedOres.Count > 0)
            {
                string[] oresFormatted = detectedOres.OrderBy(pair => pair.Value).Select(pair => $"{pair.Key} ({pair.Value}m)").ToArray();
                string oreList = string.Join(", ", oresFormatted);
                serverPlayer.SendMessage(GlobalConstants.InfoLogChatGroup, $"Detected nearby ores - {oreList}.", EnumChatType.Notification);
            }
        }
    }

}
