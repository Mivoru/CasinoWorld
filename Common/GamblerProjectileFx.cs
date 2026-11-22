using System.Collections.Generic;
using CasinoWorld.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CasinoWorld.Common
{
	/// <summary>
	/// Shared visual polish for Gambler projectiles, by material: a fading afterimage trail while they
	/// fly, and material-specific debris on impact (paper flecks for cards, pips for dice, gold for chips).
	/// </summary>
	public class GamblerProjectileFx : GlobalProjectile
	{
		private enum Material { Card, Die, Chip }

		private const int TrailLength = 6;
		private static Dictionary<int, (Material material, Color trail)> styles;

		public override void SetStaticDefaults()
		{
			styles = new() {
				[ModContent.ProjectileType<ThrownCard>()] = (Material.Card, new Color(255, 240, 230)),
				[ModContent.ProjectileType<RoyalCard>()] = (Material.Card, new Color(255, 210, 90)),
				[ModContent.ProjectileType<SpectralCard>()] = (Material.Card, new Color(120, 240, 255)),
				[ModContent.ProjectileType<GalaxyCard>()] = (Material.Card, new Color(200, 140, 255)),
				[ModContent.ProjectileType<LoadedDie>()] = (Material.Die, new Color(255, 255, 255)),
				[ModContent.ProjectileType<ExplodingDie>()] = (Material.Die, new Color(255, 150, 80)),
				[ModContent.ProjectileType<DoublingDie>()] = (Material.Die, new Color(140, 255, 200)),
				[ModContent.ProjectileType<HighRollerChip>()] = (Material.Chip, new Color(255, 90, 90)),
				[ModContent.ProjectileType<JackpotChip>()] = (Material.Chip, new Color(255, 210, 80)),
				[ModContent.ProjectileType<RevolverChip>()] = (Material.Chip, new Color(255, 120, 120)),
			};
			foreach (int type in styles.Keys) {
				ProjectileID.Sets.TrailCacheLength[type] = TrailLength;
				ProjectileID.Sets.TrailingMode[type] = 2;
			}
		}

		public override void Unload() => styles = null;

		public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
			=> entity.ModProjectile?.Mod is CasinoWorld;

		public override bool PreDraw(Projectile projectile, ref Color lightColor)
		{
			if (styles == null || !styles.TryGetValue(projectile.type, out var style))
				return true;
			Texture2D tex = TextureAssets.Projectile[projectile.type].Value;
			int frames = Main.projFrames[projectile.type];
			Rectangle frame = tex.Frame(1, frames, 0, projectile.frame);
			Vector2 origin = frame.Size() / 2f;
			Vector2 half = projectile.Size / 2f;
			// Additive-looking afterimages: oldest faintest, smaller towards the tail.
			for (int k = projectile.oldPos.Length - 1; k >= 1; k--) {
				if (projectile.oldPos[k] == Vector2.Zero)
					continue;
				float t = 1f - k / (float)projectile.oldPos.Length;
				Color c = style.trail * (0.35f * t) * projectile.Opacity;
				c.A = 0;
				Main.EntitySpriteDraw(tex, projectile.oldPos[k] + half - Main.screenPosition, frame, c,
					projectile.oldRot[k], origin, projectile.scale * (0.7f + 0.3f * t), SpriteEffects.None);
			}
			return true;
		}

		public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (styles == null || !styles.TryGetValue(projectile.type, out var style) || Main.dedServ)
				return;
			switch (style.material) {
				case Material.Card:
					for (int k = 0; k < 5; k++) {
						Dust d = Dust.NewDustPerfect(projectile.Center, DustID.Confetti_Pink + Main.rand.Next(2), Main.rand.NextVector2Circular(2.5f, 2.5f), 0, style.trail, 0.8f);
						d.noGravity = false;
					}
					break;
				case Material.Die:
					for (int k = 0; k < 4; k++)
						Dust.NewDustPerfect(projectile.Center, DustID.Smoke, Main.rand.NextVector2Circular(1.5f, 1.5f), 120, default, 0.9f);
					for (int k = 0; k < 3; k++)
						Dust.NewDustPerfect(projectile.Center, DustID.WhiteTorch, Main.rand.NextVector2Circular(3f, 3f), 0, default, 0.7f).noGravity = true;
					break;
				case Material.Chip:
					for (int k = 0; k < 6; k++)
						Dust.NewDustPerfect(projectile.Center, DustID.GoldCoin, Main.rand.NextVector2Circular(3f, 3f), 0, default, 0.9f);
					break;
			}
		}
	}
}
