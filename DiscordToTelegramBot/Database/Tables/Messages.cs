using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordToTelegramBot.Database.Tables
{
    [Table("messages")]
    public class DatabaseMessages
    {

        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong Id { get; set; }

        [Column("telegram_id")]
        public int TelegramId { get; set; }


        [Column("telegram_chanel_id")]
        public long TelegramChannelId { get; set; }

        [Column("discord_id")]
        public ulong DiscordId { get; set; }

        [Column("discord_channel_id")]
        public ulong DiscordChannelId { get; set; }

        [Column("discord_thread_id")]
        public ulong ThreadId { get; set; }

        [Column("message_id_in_chat")]
        public int MessageIdInChat { get; set; }
    }
}
