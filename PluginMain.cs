using System;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Newtonsoft.Json;
using TShockAPI.Hooks;

namespace ReverseManaSicknessPlugin
{
    [ApiVersion(2, 1)]
    public class ReverseManaPlugin : TerrariaPlugin
    {
        public override string Name => "反向魔力病+法术吸血";
        public override string Author => "你的名字";
        public override string Version => "1.0.1";
        private static Config Settings;
        public ReverseManaPlugin(Main game) : base(game) { }

        public override void Initialize()
        {
            string path = Path.Combine(TShock.SavePath, "ReverseManaConfig.json");
            Settings = Config.Load(path);
            GetDataHandlers.PlayerStats += OnStats;
            ServerApi.Hooks.NpcStrike.Register(this, OnNpcHit);
            GeneralHooks.ReloadEvent += OnReload;
            Commands.ChatCommands.Add(new Command("reversemana.reload", ReloadCmd, "rmreload"));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GetDataHandlers.PlayerStats -= OnStats;
                ServerApi.Hooks.NpcStrike.Deregister(this, OnNpcHit);
                GeneralHooks.ReloadEvent -= OnReload;
            }
            base.Dispose(disposing);
        }

        private void OnStats(object sender, GetDataHandlers.PlayerStatsEventArgs e)
        {
            var p = e.Player;
            if (p == null || !p.active) return;
            float maxMana = p.statManaMax2;
            if (maxMana <= 0) return;
            float manaRate = p.statMana / maxMana;
            float dr = (1 - manaRate) * Settings.MaxDR;
            e.Stats.Endurance += dr;
            float sick = p.manaSick;
            if (sick > 0)
            {
                float bonus = sick * Settings.SickMultiplier;
                e.Stats.MagicDamage *= (1 + bonus);
            }
        }

        private void OnNpcHit(NpcStrikeEventArgs e)
        {
            if (e.Player == null || !e.Player.active) return;
            if (e.Damage <= 0) return;
            var held = e.Player.HeldItem;
            if (held == null || !held.magic) return;
            int heal = (int)(e.Damage * Settings.LeechRate);
            if (heal <= 0) return;
            e.Player.statLife = Math.Min(e.Player.statLife + heal, e.Player.statLifeMax2);
            var ts = TShock.Players[e.Player.whoAmI];
            if (ts != null)
                ts.SendData(PacketTypes.PlayerUpdate, "", e.Player.whoAmI);
        }

        private void OnReload(ReloadEventArgs args) => LoadConfig(args.Player);
        private void ReloadCmd(CommandArgs args) => LoadConfig(args.Player);

        private void LoadConfig(TSPlayer player = null)
        {
            try
            {
                string path = Path.Combine(TShock.SavePath, "ReverseManaConfig.json");
                Settings = Config.Load(path);
                string msg = "反向魔力病配置已重载！";
                if (player != null) player.SendSuccessMessage(msg);
                else TShock.Log.ConsoleInfo(msg);
            }
            catch (Exception ex)
            {
                string err = $"重载失败: {ex.Message}";
                if (player != null) player.SendErrorMessage(err);
                else TShock.Log.ConsoleError(err);
            }
        }
    }

    public class Config
    {
        public float MaxDR { get; set; } = 0.5f;
        public float LeechRate { get; set; } = 0.15f;
        public float SickMultiplier { get; set; } = 2.0f;

        public static Config Load(string path)
        {
            if (File.Exists(path))
                return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
            var cfg = new Config();
            File.WriteAllText(path, JsonConvert.SerializeObject(cfg, Formatting.Indented));
            return cfg;
        }
    }
}
