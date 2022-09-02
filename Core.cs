using System;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;

namespace MineDetonator
{
    public class Core : BaseSettingsPlugin<Settings>
    {
        private static readonly string[] IgnoreMonsters = new[]
        {
            "Metadata/Monsters/LeagueBetrayal/BetrayalTaserNet",
            "Metadata/Monsters/LeagueBetrayal/BetrayalUpgrades/UnholyRelic",
            "Metadata/Monsters/LeagueBetrayal/BetrayalUpgrades/BetrayalDaemonSummonUnholyRelic"
        };

        private static readonly string[] IgnoreBuffs = new[]
            { "hidden_monster", "avarius_statue_buff", "hidden_monster_disable_minions" };

        private DateTime LastDetonationTime;

        public override void Render()
        {
            if (!GameController.InGame)
                return;
            var actor = GameController.Player.GetComponent<Actor>();
            var deployedObjects = actor.DeployedObjects.Where(x =>
                x.Entity != null && x.Entity.Path.Contains("Metadata/MiscellaneousObjects/RemoteMine")).ToList();

            if (deployedObjects.Count == 0)
                return;

            var realRange = Settings.DetonateDist.Value;
            var mineSkill = actor.ActorSkills.Find(x => x.Name.ToLower().Contains("mine"));
            if (mineSkill != null)
            {
                if (mineSkill.Stats.TryGetValue(GameStat.TotalSkillAreaOfEffectPctIncludingFinal, out var areaPct))
                {
                    realRange += realRange * areaPct / 100f;
                    Settings.CurrentAreaPct.Value = realRange;
                }
                else
                {
                    Settings.CurrentAreaPct.Value = 100;
                }
            }
            else
            {
                Settings.CurrentAreaPct.Value = 0;
            }

            bool IsValidMonster(Entity entity)
            {
                if (entity == null) return false;
                if (Vector2.Distance(GameController.Player.GridPos, entity.GridPos) >= Settings.DetonateDist.Value)
                    return false;
                if (!entity.HasComponent<Monster>()) return false;
                if (!entity.IsHostile || !entity.IsAlive) return false;
                if (IgnoreMonsters.Any(m => entity.Path.StartsWith(m))) return false;
                if (entity.HasComponent<Buffs>())
                {
                    var buffs = entity.GetComponent<Buffs>();
                    if (buffs != null)
                    {
                        var buffsList = buffs.BuffsList;
                        if (buffsList != null && buffsList.Count > 0 && IgnoreBuffs.Any(b1 =>
                            buffsList.Any(b2 => string.Equals(b1, b2.Name, StringComparison.OrdinalIgnoreCase))))
                            return false;
                    }
                }

                if (!entity.HasComponent<Actor>()) return false;
                var actor = entity.GetComponent<Actor>();
                if (!FilterNullAction(actor)) return false;
                var currentActorSkill = actor.CurrentAction?.Skill;
                if (currentActorSkill != null)
                {
                    if (currentActorSkill.Name.Equals("AtziriSummonDemons", StringComparison.OrdinalIgnoreCase))
                        return false;
                    if (currentActorSkill.Id == 728) return false;
                }

                return true;
            }

            var nearMonsters = GameController.Entities.Where(IsValidMonster).ToList();

            if (nearMonsters.Count == 0)
                return;

            Settings.TriggerReason = "Path: " + nearMonsters[0].Path;

            if (Settings.Debug.Value)
                LogMessage($"Ents: {nearMonsters.Count}. Last: {nearMonsters[0].Path}", 2);

            if ((DateTime.Now - LastDetonationTime).TotalMilliseconds > Settings.DetonateDelay.Value)
            {
                if (deployedObjects.Any(x =>
                    x.Entity != null && x.Entity.IsValid &&
                    x.Entity.GetComponent<Stats>().StatDictionary[GameStat.CannotDie] == 0))
                {
                    LastDetonationTime = DateTime.Now;
                    Keyboard.KeyPress(Settings.DetonateKey.Value);
                }
            }
        }

        #region Overrides of BasePlugin

        private bool FilterNullAction(Actor actor)
        {
            if (Settings.FilterNullAction.Value)
                return actor.CurrentAction != null;

            return true;
        }

        #endregion
    }
}