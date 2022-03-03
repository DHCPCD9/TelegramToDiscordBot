using DiscordToTelegramBot.Database.Tables;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DiscordToTelegramBot.Database
{
    public class ApplicationContext : DbContext
    {

        public DbSet<DatabaseMessages> Messages { get; set; }

        
        public ApplicationContext()
        {
            Database.EnsureCreated();
            Database.AutoTransactionsEnabled = true;
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=local.db");
        }
    }
}
