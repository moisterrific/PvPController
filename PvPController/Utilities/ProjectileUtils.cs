﻿using System.Linq;
using PvPController.Variables;
using Terraria;

namespace PvPController.Utilities {
    public class ProjectileUtils {
        /// <summary>
        /// Gets the weapon of a projectile.
        /// For certain projectiles, it will pull from a list of
        /// projectile-to-weapon-mapping Dictionaries and returns
        /// the weapon based off the dictionary mapping.
        /// </summary>
        /// <param name="owner">Owner of projectile.</param>
        /// <param name="type">Type of projectile.</param>
        /// <returns>Returns the item the projectile came from.</returns>
        public static PvPItem GetProjectileWeapon(PvPPlayer owner, int type) {
            PvPItem weapon;
            if (PresetData.PresetProjDamage.ContainsKey(type)) {
                weapon = new PvPItem {
                    Damage = PresetData.PresetProjDamage[type],
                    SpecialName = Lang.GetProjectileName(type).ToString()
                };
            } else if (PresetData.ProjHooks.ContainsKey(type)) {
                weapon = new PvPItem(type);
            } else if (PresetData.FromWhatItem.ContainsKey(type)) {
                weapon = owner.FindPlayerItem(PresetData.FromWhatItem[type]);
            } else if (PresetData.MinionItem.ContainsKey(type)) {
                weapon = owner.FindPlayerItem(PresetData.MinionItem[type]);
            } else {
                weapon = owner.GetPlayerItem;
            }
            return weapon;
        }

        /// <summary>
        /// Finds a free projectile index from Main's projectile array.
        /// </summary>
        public static int FindFreeIndex() {
            for (int x = 0; x < Main.projectile.Length; x++) {
                if (!Main.projectile[x].active)
                    return x;
            }

            return -1;
        }

        /// <summary>
        /// Gets the projectile from the Main's projectile array
        /// </summary>
        /// <param name="identity">Identity of projectile</param>
        /// <param name="type">Type of projectile</param>
        /// <param name="owner">Owner Id</param>
        /// <returns>A projectile from Main.projectile</returns>
        public static Projectile GetMainProjectile(int identity, int type, int owner) {
            return Main.projectile.Where(c => c != null)
                .Where(c => c.active)
                .SingleOrDefault(c => c.identity == identity && c.type == type && c.owner == owner);
        }
    }
}
