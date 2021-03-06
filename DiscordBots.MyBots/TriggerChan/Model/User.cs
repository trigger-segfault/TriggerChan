﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TriggersTools.DiscordBots.Database;
using TriggersTools.DiscordBots.Database.Model;
using TriggersTools.DiscordBots.SpoilerBot.Model;

namespace TriggersTools.DiscordBots.TriggerChan.Model {
	/// <summary>
	/// The base database model for a user.
	/// </summary>
	public class User : IDbUser, IDbSpoilerUser, IDbModelCreating {
		/// <summary>
		/// The snowflake Id key.
		/// </summary>
		[Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
		public ulong Id { get; set; }
		/// <summary>
		/// The End User Data snowflake Id key.
		/// </summary>
		[Required]
		public ulong EndUserDataId {
			get => Id;
			set => Id = value;
		}
		/// <summary>
		/// Gets if the user has been banned from the use of the bot.
		/// </summary>
		public bool Banned { get; set; }

		/// <summary>
		/// Checks if the user has asked this information to be deleted.
		/// </summary>
		/// <param name="euds">The info to that the user requested to be deleted.</param>
		/// <returns>True if the data should be deleted.</returns>
		public bool ShouldKeep(EndUserDataContents euds, EndUserDataType type) {
			return !euds.EraseAll;
		}


		/// <summary>
		/// Gets the profile associated with the user.
		/// </summary>
		//[ForeignKey(nameof(EndUserDataId))]
		//public UserProfile Profile { get; set; }
		/// <summary>
		/// Gets the spoilers that the user has created.
		/// </summary>
		//[InverseProperty(nameof(Spoiler.User))]
		public List<Spoiler> Spoilers { get; set; }
		/// <summary>
		/// Gets the spoilers the user has been spoiled to.
		/// </summary>
		//[InverseProperty(nameof(SpoiledUser.User))]
		public List<SpoiledUser> SpoiledUsers { get; set; }


		/// <summary>
		/// Gets the entity type of this Discord model.
		/// </summary>
		[NotMapped]
		public EntityType Type => EntityType.User;

		public void ModelCreating(ModelBuilder modelBuilder, DbContextEx db) {
			modelBuilder.Entity<User>()
				.HasMany(u => u.Spoilers)
				.WithOne()
				.HasForeignKey(nameof(Spoiler.AuthorId));
			modelBuilder.Entity<User>()
				.HasMany(u => u.SpoiledUsers)
				.WithOne()
				.HasForeignKey(nameof(SpoiledUser.UserId));
		}
	}
}
