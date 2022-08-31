using LuckyRabbitsFoot.Interfaces;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using Object = StardewValley.Object;

namespace LuckyRabbitsFoot
{
    public class ModEntry : Mod
    {
        private const int RABBIT_FOOT_INDEX = 446;

        private bool _hasRabbitFootLuck { get; set; }

        private int _givenRabbitLuckBonus { get; set; }

        private Config Config { get; set; }

        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<Config>();

            _hasRabbitFootLuck = false;
            _givenRabbitLuckBonus = 0;

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.UpdateTicked += UpdateRabbitFootLuckBuff;
            helper.Events.Player.InventoryChanged += InventoryChangedHandler;

            // Adds and Remove buffs on day changing events, to prevent problems with buffs in saved file (yes, it can be).
            helper.Events.GameLoop.DayStarted += (a, temp) => ModifyLuckFromRabbitsFoot();
            helper.Events.GameLoop.DayEnding += (a, temp) => ClearAllBuffsOnDayEvent();
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");

            if (configMenu == null)
                return;

            else
                InitializeModConfigMenu(configMenu);
        }

        private void InitializeModConfigMenu(IGenericModConfigMenuApi configMenu)
        {
            // Register mod in config menu.
            configMenu.Register(ModManifest,
                                () => Config = new Config(),
                                () => Helper.WriteConfig(Config),
                                false);

            // Add section title.
            configMenu.AddSectionTitle(ModManifest, () => Helper.Translation.Get("general-settings"));

            // Add base luck value option.
            configMenu.AddNumberOption(ModManifest,
                                       name: () => Helper.Translation.Get("base-value"),
                                       tooltip: () => Helper.Translation.Get("base-value-des"),
                                       getValue: () => Config.BaseLuckValue,
                                       setValue: value => Config.BaseLuckValue = value);

            // Add multiplier value option.
            configMenu.AddNumberOption(ModManifest,
                                       name: () => Helper.Translation.Get("multiplier-value"),
                                       tooltip: () => Helper.Translation.Get("multiplier-value-des"),
                                       getValue: () => (float)Config.QualityMultiplier,
                                       setValue: value => Config.QualityMultiplier = value,
                                       min: 0.0F,
                                       max: 5.0F);

            // Add count multiplier option.
            configMenu.AddBoolOption(ModManifest,
                                     name: () => Helper.Translation.Get("count-multiplier"),
                                     tooltip: () => Helper.Translation.Get("count-multiplier-des"),
                                     getValue: () => Config.ApplyCountMultiplier,
                                     setValue: value => Config.ApplyCountMultiplier = value);
        }

        private void UpdateRabbitFootLuckBuff(object sender, UpdateTickedEventArgs e)
        {
            if (_hasRabbitFootLuck)
                UpdateLuckBuff(_givenRabbitLuckBonus);
        }

        private void InventoryChangedHandler(object sender, InventoryChangedEventArgs inv)
        {
            if (!inv.Added.Any<Item>(x => x.ParentSheetIndex == RABBIT_FOOT_INDEX) && !inv.Removed.Any<Item>(x => x.ParentSheetIndex == RABBIT_FOOT_INDEX))
                LuckReport("INVENTORYCHANGE-UNINVOLVED");
            else
                ModifyLuckFromRabbitsFoot();
        }

        private void SaveLoadedHandler(object sender, SaveLoadedEventArgs load) => ModifyLuckFromRabbitsFoot();

        private void DayStartedHandler(object sender, DayStartedEventArgs day) => ModifyLuckFromRabbitsFoot();

        private void ModifyLuckFromRabbitsFoot()
        {
            ResetLuckBonus();

            IEnumerable<Item> source1 = Game1.player.Items.Where(x => x != null && x.ParentSheetIndex == RABBIT_FOOT_INDEX);
            List<Object> objectList;

            if (source1 == null)
            {
                objectList = null;
            }
            else
            {
                IEnumerable<Object> source2 = source1.Select(x => (Object)x);
                objectList = source2?.ToList();
            }

            List<Object> source3 = objectList;

            if (source3 == null || source3.Count == 0)
                return;

            source3.Sort((x, y) =>
            {
                if (x.Quality >= y.Quality)
                    return 1;
                return x.Quality != y.Quality ? -1 : 0;
            });

            _givenRabbitLuckBonus = CalculateLuckBonus(source3.Last(), source3.Count);
            _hasRabbitFootLuck = true;

            UpdateLuckBuff(_givenRabbitLuckBonus);
            LuckReport("ADD");
        }

        private int CalculateLuckBonus(Object foot, int count)
        {
            // Using simple cast, to remove float part.
            int luckValue = (int)(foot.Quality * Config.QualityMultiplier) + Config.BaseLuckValue;

            if (Config.ApplyCountMultiplier)
                luckValue *= count;

            return luckValue;
        }

        private void ResetLuckBonus()
        {
            RemoveLuckBuff();
            _givenRabbitLuckBonus = 0;
            _hasRabbitFootLuck = false;
            LuckReport("RESET");
        }

        // Legacy Code.
        /*
        private int GetLuck() => Helper.Reflection.GetField<NetInt>(Game1.player, "luckLevel", true).GetValue().Value;

        private void SetLuck(int luck)
        {
            Monitor.Log($"Current Luck Level: {luck}.", LogLevel.Error);
        }
        */

        private void UpdateLuckBuff(int luck)
        {
            Buff luckBuff = Game1.buffsDisplay.otherBuffs.FirstOrDefault(b => b.which == 777);

            if (luckBuff != null)
            {
                luckBuff.millisecondsDuration = 250;
            }

            else
            {
                luckBuff = new Buff(0, 0, 0, 0, luck, 0, 0, 0, 0, 0, 0, 0, 0, "RabbitFoot", Helper.Translation.Get("rabbit-foot"))
                {
                    which = 777,
                    totalMillisecondsDuration = 250,
                    millisecondsDuration = 250,
                    description = Helper.Translation.Get("rabbit-foot-buff-des")
                };

                Game1.buffsDisplay.addOtherBuff(luckBuff);
            }
        }

        private void RemoveLuckBuff()
        {
            Game1.buffsDisplay.removeOtherBuff(777);
        }

        /// <summary>
        /// Удаляет все баффы игрока. <br />
        /// Вызывается при событиях смены дня, чтобы избежать возможных проблем с добавлением нескольких бонусов к удаче. <br />
        /// Вызов при начале нужен для исправления при загрузке с 'багованного' сохранения, при завершении — чтобы сохранение было корректным.
        /// </summary>
        private void ClearAllBuffsOnDayEvent() =>
                     Game1.buffsDisplay.clearAllBuffs();

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || (int)e.Button != 76)
                return;
            LuckReport("BUTTON");
        }

        private void LuckReport(string eventStr, bool forceShow = false, LogLevel logLevel = (LogLevel)1)
        {
            if (Game1.player.LuckLevel > 20)
            {
                forceShow = true;
                logLevel = (LogLevel)3;
                eventStr = "WARN-ABSURDLUCK:" + eventStr;
            }
            string str = $"[{eventStr}] {"addedLuckLevel"}: {Game1.player.addedLuckLevel}, {"luckLevel"}: {Game1.player.luckLevel}, {"LuckLevel"}: {Game1.player.LuckLevel}, {"DailyLuck"}: {Game1.player.DailyLuck}, {"_hasRabbitFootLuck"}: {_hasRabbitFootLuck}, {"_givenRabbitLuckBonus"} {_givenRabbitLuckBonus}";
            if (!(Monitor.IsVerbose | forceShow))
                return;
            Monitor.Log(str, logLevel);
        }
    }
}
