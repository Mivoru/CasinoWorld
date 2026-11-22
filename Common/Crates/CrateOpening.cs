using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;

namespace CasinoWorld.Common.Crates
{
	/// <summary>
	/// One crate opening: a strip of random items scrolls past a pointer, slows down and stops
	/// on the prize. The prize is rolled when the crate is opened; the strip only shows it.
	/// </summary>
	public class CrateOpening
	{
		public const int StripLength = 44;
		public const int WinnerIndex = 38;
		public const int SpinTicks = 330;
		public const float SlotWidth = 84f;

		public readonly List<(CrateDrop drop, CrateRarity rarity)> Strip = new();
		public CrateKind Kind { get; private set; }
		public CrateDrop Prize { get; private set; }
		public CrateRarity PrizeRarity { get; private set; }
		public int PrizeStack { get; private set; }

		public bool Active { get; private set; }
		public bool Done { get; private set; }
		public int Timer { get; private set; }

		/// <summary>Strip scroll position in pixels: the slot under the pointer is Offset / SlotWidth.</summary>
		public float Offset { get; private set; }

		private float finalOffset;
		private int lastTickSlot;

		public void Start(CrateKind kind)
		{
			Kind = kind;
			(CrateDrop prize, CrateRarity rarity) = CrateLoot.Roll(kind, Main.rand);
			Prize = prize;
			PrizeRarity = rarity;
			PrizeStack = Main.rand.Next(prize.MinStack, prize.MaxStack + 1);

			Strip.Clear();
			for (int k = 0; k < StripLength; k++)
				Strip.Add(k == WinnerIndex ? (prize, rarity) : CrateLoot.Roll(kind, Main.rand));

			// Stop somewhere inside the winning slot, not always dead centre, for suspense.
			finalOffset = WinnerIndex * SlotWidth + Main.rand.NextFloat(-0.38f, 0.38f) * SlotWidth;
			Offset = 0f;
			Timer = 0;
			lastTickSlot = 0;
			Active = true;
			Done = false;
			SoundEngine.PlaySound(SoundID.Unlock);
		}

		public void Update(Player player)
		{
			if (!Active || Done)
				return;

			Timer++;
			float p = Math.Min(1f, Timer / (float)SpinTicks);
			float ease = 1f - (float)Math.Pow(1f - p, 4);   // fast start, long slow finish
			Offset = finalOffset * ease;

			int slot = (int)((Offset + SlotWidth / 2f) / SlotWidth);
			if (slot != lastTickSlot) {
				lastTickSlot = slot;
				SoundEngine.PlaySound(SoundID.MenuTick with { Volume = 0.5f, Pitch = 0.2f + p * 0.3f });
			}

			if (Timer >= SpinTicks)
				Finish(player);
		}

		/// <summary>Gives the prize at once (window closed early).</summary>
		public void FinishImmediately(Player player)
		{
			if (Active && !Done)
				Finish(player);
		}

		private void Finish(Player player)
		{
			Offset = finalOffset;
			Done = true;
			player.QuickSpawnItem(player.GetSource_Misc("CasinoWorld"), Prize.ItemType, PrizeStack);

			SoundEngine.PlaySound(PrizeRarity >= CrateRarity.Red ? SoundID.Item4 : SoundID.Grab);
			if (PrizeRarity == CrateRarity.Gold) {
				SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.6f, Volume = 0.5f });
				string name = Lang.GetItemNameValue(Prize.ItemType);
				Main.NewText(Language.GetTextValue("Mods.CasinoWorld.UI.Crate.GoldMessage", name), CrateLoot.RarityColors[(int)CrateRarity.Gold]);
			}
		}

		public void Close() => Active = false;
	}
}
