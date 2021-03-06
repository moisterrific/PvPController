﻿using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;
using PvPController.Utilities;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using TShockAPI;

namespace PvPController.Variables {
    public class PvPPlayer : TSPlayer {

        DateTime _lastHit;
        public ProjectileTracker ProjTracker = new ProjectileTracker();
        public PvPPlayer LastHitBy = null;
        public PvPItem LastHitWeapon = null;
        public PvPProjectile LastHitProjectile = null;
        public int PreviousSelectedItem = 0;

        private int _medusaHitCount;

        public bool SeeTooltip = true;

        public PvPPlayer(int index) : base(index) {
            _lastHit = DateTime.Now;
            User = TShock.Players[index].User;
        }

        public bool TryGetUser() {
            User = TShock.Players[Index].User;
            return User != null;
        }

        /// <summary>
        /// Gets the item the player is currently holding.
        /// </summary>
        public PvPItem GetPlayerItem => PvPUtils.ConvertToPvPItem(SelectedItem);

        /// <summary>
        /// Finds the player's item from its inventory.
        /// </summary>
        /// <param Name="type">Type of item being found</param>
        /// <returns>The item in the player's inventory</returns>
        public PvPItem FindPlayerItem(int type) => PvPUtils.ConvertToPvPItem(TPlayer.inventory[TPlayer.FindItem(type)]);

        /// <summary>
        /// Gets the damage received from an attack.
        /// </summary>
        public int GetDamageReceived(int damage) => (int)TerrariaUtils.GetHurtDamage(this, damage);

        /// <summary>
        /// Gets the angle that a target is from the player in radians.
        /// </summary>
        public double GetAngleFrom(Vector2 target) => Math.Atan2(target.Y - this.Y, target.X - this.X);
        
        /// <summary>
        /// Checks whether a target is left from a player
        /// </summary>
        /// <returns>Returns true if the target is left of the player</returns>
        public bool IsLeftFrom(Vector2 target) => target.X > this.X;

        /// <summary>
        /// Gets the damage dealt to a person with server side calculations.
        /// </summary>
        public int GetDamageDealt(PvPPlayer attacker, PvPItem weapon, PvPProjectile projectile = null) {
            int damage = (projectile == null || projectile.GetConfigDamage() < 1) 
                ? weapon.GetPvPDamage(attacker) 
                : projectile.GetConfigDamage();

            damage += PvPUtils.GetAmmoDamage(attacker, weapon);
            damage += PvPUtils.GenerateDamageVariance();
            damage += PvPUtils.GetVortexDamage(attacker, weapon, damage);

            damage -= (int)(GetDefenseDifferenceFromModded() * 0.5);

            return damage;
        }

        /// <summary>
        /// Gets the defense of a player. Includes both vanilla and modded defense values.
        /// </summary>
        public int GetPlayerDefense() {
            int vanillaArmorDefense = 0;
            int moddedArmorDefense = 0;

            for (int x = 0; x < 9; x++) {
                vanillaArmorDefense += this.TPlayer.armor[x].defense;
                moddedArmorDefense += Database.GetData<int>(DbConsts.ItemTable, this.TPlayer.armor[x].netID, DbConsts.Defense);
            }
            
            return this.TPlayer.statDefense - vanillaArmorDefense + moddedArmorDefense;
        }

        /// <summary>
        /// Gets the critical percentage of an item.
        /// Note: The ranged crit value is the final crit value, whereas the other crits have to
        /// be added to the existing weapon crit value.
        /// </summary>
        public int GetCrit(PvPItem weapon) {
            int crit = weapon.crit;
            if (weapon.melee) crit += TPlayer.meleeCrit;
            else if (weapon.ranged) crit = TPlayer.rangedCrit;
            else if (weapon.magic) crit += TPlayer.magicCrit;
            else if (weapon.thrown) crit += TPlayer.thrownCrit;

            return crit;
        }

        /// <summary>
        /// Returns the difference from normal defense values from the modded defense values.
        /// </summary>
        public int GetDefenseDifferenceFromModded() {
            int vanillaArmorDefense = 0;
            int moddedArmorDefense = 0;

            for (int x = 0; x < 9; x++) {
                vanillaArmorDefense += this.TPlayer.armor[x].defense;
                moddedArmorDefense += Database.GetData<int>(DbConsts.ItemTable, this.TPlayer.armor[x].netID, DbConsts.Defense);
            }

            return moddedArmorDefense - vanillaArmorDefense;
        }

        /// <summary>
        /// Damages players. Criticals and custom knockback will apply if enabled.
        /// </summary>
        public void DamagePlayer(PvPPlayer attacker, PvPItem weapon, int damage, int hitDirection, bool isCrit) {
            damage *= isCrit ? 2 : 1;
            string star = isCrit ? "!!" : "*";
            var color = isCrit ? Color.SlateBlue : Color.DarkTurquoise;

            NetMessage.SendPlayerHurt(this.Index, PlayerDeathReason.ByCustomReason(PvPUtils.GetPvPDeathMessage(attacker, this, weapon)),
                damage, hitDirection, false, true, 5);
            Interface.PlayerTextPopup(attacker, this, star + TerrariaUtils.GetHurtDamage(this, damage) + star, color);
        }

        /// <summary>
        /// Sets a velocity to a player, emulating directional knockback.
        /// 
        /// This method requires SSC to be enabled. To allow knockback to work
        /// on non-SSC servers, the method will temporarily enable SSC to set player
        /// velocity.
        /// </summary>
        public void KnockBack(double knockback, double angle, double hitDirection = 1) {
            if (this.TPlayer.noKnockback) return;

            bool isSSC = Main.ServerSideCharacter;
            
            if (!isSSC) {
                Main.ServerSideCharacter = true;
                NetMessage.SendData(7, this.Index);
                this.IgnoreSSCPackets = true;
            }

            if (this.TPlayer.velocity.Length() <= Math.Abs(knockback)) {
                if (Math.Abs(this.TPlayer.velocity.Length() + knockback) < knockback) {
                    this.TPlayer.velocity.X += (float)(knockback * Math.Cos(angle) * hitDirection);
                    this.TPlayer.velocity.Y += (float)(knockback * Math.Sin(angle));
                } else {
                    this.TPlayer.velocity.X = (float)(knockback * Math.Cos(angle) * hitDirection);
                    this.TPlayer.velocity.Y = (float)(knockback * Math.Sin(angle));
                }
            }

            NetMessage.SendData(13, -1, -1, null, this.Index);
                
            if (!isSSC) {
                Main.ServerSideCharacter = false;
                NetMessage.SendData(7, this.Index);
                this.IgnoreSSCPackets = false;
            }
        }

        /// <summary>
        /// Applies effects that normally won't work in vanilla pvp.
        /// Effects include nebula/frost armor, yoyo-bag projectiles, and thorns/turtle damage.
        /// </summary>
        public void ApplyPvPEffects(PvPPlayer attacker, PvPItem weapon, PvPProjectile projectile, int damage) {
            this.ApplyReflectDamage(attacker, damage, weapon);
            this.ApplyArmorEffects(attacker, weapon);
            TerrariaUtils.ActivateYoyo(attacker, this, damage, weapon.knockBack);
        }

        /// <summary>
        /// Applies turtle and thorns damage to the attacker.
        /// </summary>
        public void ApplyReflectDamage(PvPPlayer attacker, int damage, PvPItem weapon) {
            PvPItem reflectType = new PvPItem();

            if (PvPController.Config.EnableTurtle && this.TPlayer.setBonus == Language.GetTextValue("ArmorSetBonus.Turtle") && weapon.melee) {
                reflectType.SpecialName = "Turtle Armor";
                int turtleDamage = (int)(damage * PvPController.Config.TurtleMultiplier);

                NetMessage.SendPlayerHurt(attacker.Index, PlayerDeathReason.ByCustomReason(PvPUtils.GetPvPDeathMessage(this, attacker, reflectType, 2)),
                    turtleDamage, 0, false, true, 5);
            }

            if (PvPController.Config.EnableThorns && this.TPlayer.FindBuffIndex(14) != -1) {
                reflectType.SpecialName = "Thorns";
                int thornDamage = (int)(damage * PvPController.Config.ThornMultiplier);

                NetMessage.SendPlayerHurt(attacker.Index, PlayerDeathReason.ByCustomReason(PvPUtils.GetPvPDeathMessage(this, attacker, reflectType, 2)),
                    thornDamage, 0, false, true, 5);
            } 
        }

        /// <summary>
        /// Applies nebula and frost armor effects.
        /// </summary>
        public void ApplyArmorEffects(PvPPlayer attacker, PvPItem weapon) {
            if (weapon.magic
                && attacker.TPlayer.armor[0].netID == 2760 && attacker.TPlayer.armor[1].netID == 2761 && attacker.TPlayer.armor[2].netID == 2762
                && PvPController.Config.EnableNebula) {

                if (attacker.TPlayer.FindBuffIndex(181) != -1 && PvPController.Config.NebulaTier3Duration != 0) {
                    attacker.SetBuff(175, (int)(PvPController.Config.NebulaTier3Duration * 60));
                    attacker.SetBuff(178, (int)(PvPController.Config.NebulaTier3Duration * 60));
                    attacker.SetBuff(181, (int)(PvPController.Config.NebulaTier3Duration * 60));
                } else if (attacker.TPlayer.FindBuffIndex(180) != -1 && PvPController.Config.NebulaTier3Duration != 0) {
                    attacker.SetBuff(175, 1);
                    attacker.SetBuff(178, 1);
                    attacker.SetBuff(181, 1);
                    Task.Delay(100).ContinueWith(t => {
                        attacker.SetBuff(175, (int)(PvPController.Config.NebulaTier3Duration * 60));
                        attacker.SetBuff(178, (int)(PvPController.Config.NebulaTier3Duration * 60));
                        attacker.SetBuff(181, (int)(PvPController.Config.NebulaTier3Duration * 60));
                    });
                } else if (attacker.TPlayer.FindBuffIndex(179) != -1 && PvPController.Config.NebulaTier2Duration != 0) {
                    attacker.SetBuff(174, 1);
                    attacker.SetBuff(177, 1);
                    attacker.SetBuff(180, 1);
                    Task.Delay(100).ContinueWith(t => {
                        attacker.SetBuff(174, (int)(PvPController.Config.NebulaTier2Duration * 60));
                        attacker.SetBuff(177, (int)(PvPController.Config.NebulaTier2Duration * 60));
                        attacker.SetBuff(180, (int)(PvPController.Config.NebulaTier2Duration * 60));
                    });
                } else {
                    attacker.SetBuff(173, (int)(PvPController.Config.NebulaTier1Duration * 60));
                    attacker.SetBuff(176, (int)(PvPController.Config.NebulaTier1Duration * 60));
                    attacker.SetBuff(179, (int)(PvPController.Config.NebulaTier1Duration * 60));
                }
            }

            if ((weapon.ranged || weapon.melee) 
                && attacker.TPlayer.armor[0].netID == 684 && attacker.TPlayer.armor[1].netID == 685 && attacker.TPlayer.armor[2].netID == 686
                && PvPController.Config.EnableFrost) {

                this.SetBuff(44, (int)(PvPController.Config.FrostDuration * 30));
            }
        }

        /// <summary>
        /// Applies buffs to the attacker based off own buffs, if any.
        /// </summary>
        public void ApplyBuffDebuffs(PvPPlayer attacker, PvPItem weapon) {
            for(int x = 0; x < Terraria.Player.maxBuffs; x++) {
                int buffType = attacker.TPlayer.buffType[x];
                if (PresetData.FlaskDebuffs.ContainsKey(buffType)) {
                    if (weapon.melee) {
                        this.SetBuff(Database.GetBuffInfo(DbConsts.BuffTable, buffType, true));
                    }
                    continue;
                }
                this.SetBuff(Database.GetBuffInfo(DbConsts.BuffTable, buffType, true));
            }
        }

        /// <summary>
        /// Gets the first available ammo for a weapon.
        /// </summary>
        /// <param Name="weapon">The weapon to find ammo for.</param>
        /// <returns></returns>
        public PvPItem GetFirstAvailableAmmo(PvPItem weapon) {
            int useAmmo = weapon.useAmmo;

            if (useAmmo == AmmoID.None) return new PvPItem();

            for (int x = 54; x < NetItem.InventorySlots; x++) {
                if (this.TPlayer.inventory[x].ammo == useAmmo)
                    return PvPUtils.ConvertToPvPItem(this.TPlayer.inventory[x]);
            }

            for (int x = 0; x < NetItem.InventorySlots - 4; x++) {
                if (this.TPlayer.inventory[x].ammo == useAmmo)
                    return PvPUtils.ConvertToPvPItem(this.TPlayer.inventory[x]);
            }

            return new PvPItem();
        }

        /// <summary>
        /// Applies buffs to self based off a buff, if any.
        /// </summary>
        public void ApplyBuffSelfBuff() {
            for (int x = 0; x < Terraria.Player.maxBuffs; x++) {
                int buffType = this.TPlayer.buffType[x];
                this.SetBuff(Database.GetBuffInfo(DbConsts.BuffTable, buffType, false));
            }
        }

        /// <summary>
        /// Determines whether a person can be hit based off the config iframes.
        /// </summary>
        /// <returns></returns>
        public bool CanBeHit() {
            if ((DateTime.Now - _lastHit).TotalMilliseconds >= PvPController.Config.IframeTime) {
                _lastHit = DateTime.Now;
                return true;
            }

            return false;
        }

        public void SetBuff(BuffInfo buffInfo) => SetBuff(buffInfo.BuffId, buffInfo.BuffDuration);
        
        /// <summary>
        /// Determines whether a person can be hit with Medusa Head.
        /// A normal Medusa attack hits six times at once, so this method
        /// limits it down to one hit per attack.
        /// </summary>
        /// <returns></returns>
        public bool CheckMedusa() {
            _medusaHitCount++;
            if (_medusaHitCount != 1) {
                if (_medusaHitCount == 6) _medusaHitCount = 0;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Stores the weapon used to the projectile that was shot.
    /// </summary>
    public class ProjectileTracker {
        public PvPProjectile[] Projectiles = new PvPProjectile[Main.maxProjectileTypes];

        public ProjectileTracker() {
            for (int x = 0; x < Projectiles.Length; x++) {
                Projectiles[x] = new PvPProjectile(0);
            }
        }

        public void InsertProjectile(int index, int projectileType, int ownerIndex, PvPItem item) {
            Projectiles[projectileType] = new PvPProjectile(projectileType) {
                identity = index,
                ItemOriginated = item,
                owner = ownerIndex,
                OwnerProjectile = PvPController.PvPers[ownerIndex]
            };
        }
    }
}
